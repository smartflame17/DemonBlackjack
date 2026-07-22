using System;
using System.Collections.Generic;
using System.Linq;

public static class ScoreResolver
{
    public static ScoreResult Resolve(IReadOnlyList<Card> cards, IReadOnlyList<Modifier> modifiers, int targetScore, int burstThreshold, int blackjackBonus = 0)
    {
        int blackjackScore = ResolveBlackjackScore(cards) + blackjackBonus;
        bool isBurst = blackjackScore > burstThreshold;

        PokerResult poker = ResolvePoker(cards);

        int modifiedScore = ApplyModifiers(blackjackScore, 1, modifiers);
        bool isBlackjack = modifiedScore == targetScore;    // fk you codex for adding arbitrary card.Count==2

        return new ScoreResult(blackjackScore, modifiedScore, poker.Rank, poker.Multiplier, isBurst, isBlackjack);
    }

    public static PokerResult ResolvePoker(IReadOnlyList<Card> cards)
    {
        PokerHandRank rank = ResolvePokerRank(cards, out List<int> cardIndices);
        return new PokerResult(rank, GetPokerMultiplier(rank), cardIndices);
    }

    private static int ResolveBlackjackScore(IReadOnlyList<Card> cards) // TODO: apply any attached modifiers before evaluating
    {
        int score = 0;
        int aces = 0;

        foreach (Card card in cards)
        {
            bool negativeRank = string.Equals(card.ModifierId, CardModifierResolver.NegativeRank, StringComparison.Ordinal);
            bool copyQueen = string.Equals(card.ModifierId, CardModifierResolver.CopyQueen, StringComparison.Ordinal);
            score += copyQueen ? 0 : negativeRank ? -card.BlackjackValue : card.BlackjackValue;

            if (card.Rank == Rank.Ace && !negativeRank)
                aces++;
        }

        while (score > 21 && aces > 0)
        {
            score -= 10;
            aces--;
        }

        return score;
    }

    private static int ApplyModifiers(int baseScore, int pokerMultiplier, IReadOnlyList<Modifier> modifiers)
    {
        int score = baseScore;

        if (modifiers != null)
        {
            foreach (Modifier modifier in modifiers.Where(modifier => modifier.Operation == ModifierOperation.Add))
                score += modifier.Value;

            foreach (Modifier modifier in modifiers.Where(modifier => modifier.Operation == ModifierOperation.Multiply))
                score *= modifier.Value;

            foreach (Modifier modifier in modifiers.Where(modifier => modifier.Operation == ModifierOperation.Override))
                score = modifier.Value;
        }

        return Math.Max(0, score * Math.Max(1, pokerMultiplier));
    }

    private static int GetPokerMultiplier(PokerHandRank rank)
    {
        return rank switch
        {
            PokerHandRank.Pair => 2,
            PokerHandRank.TwoPair => 3,
            PokerHandRank.ThreeOfAKind => 3,
            PokerHandRank.Straight => 4,
            PokerHandRank.Flush => 4,
            PokerHandRank.FullHouse => 5,
            PokerHandRank.FourOfAKind => 6,
            PokerHandRank.StraightFlush => 8,
            PokerHandRank.RoyalFlush => 10,
            _ => 0
        };
    }

    private static PokerHandRank ResolvePokerRank(IReadOnlyList<Card> cards, out List<int> cardIndices)
    {
        cardIndices = new List<int>();
        if (cards == null || cards.Count < 2)
            return PokerHandRank.HighCard;

        var indexedCards = cards.Select((card, index) => new IndexedCard(card, index)).ToList();
        var rankGroups = indexedCards
            .GroupBy(entry => entry.Card.Rank)
            .Select(group => group.OrderBy(entry => entry.Index).ToList())
            .OrderByDescending(group => group.Count)
            .ThenByDescending(group => (int)group[0].Card.Rank)
            .ToList();
        var suitGroups = indexedCards.GroupBy(entry => entry.Card.Suit).Select(group => group.ToList()).ToList();
        List<IndexedCard> flushGroup = suitGroups.FirstOrDefault(group => group.Count >= 5);
        bool flush = flushGroup != null;
        bool straight = HasStraight(indexedCards, out bool royal, out List<int> straightIndices);
        bool straightFlush = false;
        bool straightFlushRoyal = false;
        List<int> straightFlushIndices = null;
        foreach (List<IndexedCard> group in suitGroups.Where(group => group.Count >= 5))
        {
            if (!HasStraight(group, out bool groupRoyal, out List<int> groupIndices))
                continue;

            straightFlush = true;
            straightFlushRoyal = groupRoyal;
            straightFlushIndices = groupIndices;
            break;
        }

        if (straightFlush && straightFlushRoyal)
        {
            cardIndices = straightFlushIndices;
            return PokerHandRank.RoyalFlush;
        }

        if (straightFlush)
        {
            cardIndices = straightFlushIndices;
            return PokerHandRank.StraightFlush;
        }

        if (rankGroups[0].Count == 4)
        {
            cardIndices = rankGroups[0].Take(4).Select(entry => entry.Index).ToList();
            return PokerHandRank.FourOfAKind;
        }

        if (rankGroups[0].Count == 3 && rankGroups.Count > 1 && rankGroups[1].Count >= 2)
        {
            cardIndices = rankGroups[0].Take(3).Concat(rankGroups[1].Take(2)).Select(entry => entry.Index).ToList();
            return PokerHandRank.FullHouse;
        }

        if (flush)
        {
            cardIndices = flushGroup.Take(5).Select(entry => entry.Index).ToList();
            return PokerHandRank.Flush;
        }

        if (straight)
        {
            cardIndices = straightIndices;
            return PokerHandRank.Straight;
        }

        if (rankGroups[0].Count == 3)
        {
            cardIndices = rankGroups[0].Take(3).Select(entry => entry.Index).ToList();
            return PokerHandRank.ThreeOfAKind;
        }

        List<List<IndexedCard>> pairs = rankGroups.Where(group => group.Count == 2).ToList();

        if (pairs.Count >= 2)
        {
            cardIndices = pairs.Take(2).SelectMany(group => group.Take(2)).Select(entry => entry.Index).ToList();
            return PokerHandRank.TwoPair;
        }

        if (pairs.Count == 1)
        {
            cardIndices = pairs[0].Take(2).Select(entry => entry.Index).ToList();
            return PokerHandRank.Pair;
        }

        return PokerHandRank.HighCard;
    }

    private static bool HasStraight(IReadOnlyList<IndexedCard> cards, out bool royal, out List<int> cardIndices)
    {
        var rankEntries = new Dictionary<int, IndexedCard>();
        for (int i = 0; i < cards.Count; i++)
        {
            IndexedCard entry = cards[i];
            int value = entry.Card.Rank == Rank.Ace ? 14 : (int)entry.Card.Rank;
            if (!rankEntries.ContainsKey(value))
                rankEntries[value] = entry;
            if (value == 14 && !rankEntries.ContainsKey(1))
                rankEntries[1] = entry;
        }

        int run = 1;
        royal = false;
        cardIndices = new List<int>();
        List<int> values = rankEntries.Keys.OrderBy(value => value).ToList();

        for (int i = 1; i < values.Count; i++)
        {
            if (values[i] == values[i - 1] + 1)
            {
                run++;

                if (run >= 5)
                {
                    royal = values[i] == 14 && values.Contains(10);
                    cardIndices = values.Skip(i - 4).Take(5).Select(value => rankEntries[value].Index).ToList();
                    return true;
                }
            }
            else
            {
                run = 1;
            }
        }

        return false;
    }

    private readonly struct IndexedCard
    {
        public IndexedCard(Card card, int index)
        {
            Card = card;
            Index = index;
        }

        public Card Card { get; }
        public int Index { get; }
    }
}

public readonly struct ScoreResult
{
    public ScoreResult(int blackjackScore, int finalScore, PokerHandRank pokerRank, int pokerMultiplier, bool isBurst, bool isBlackjack)
    {
        BlackjackScore = blackjackScore;
        FinalScore = finalScore;
        PokerRank = pokerRank;
        PokerMultiplier = pokerMultiplier;
        IsBurst = isBurst;
        IsBlackjack = isBlackjack;
    }

    public int BlackjackScore { get; }
    public int FinalScore { get; }
    public PokerHandRank PokerRank { get; }
    public int PokerMultiplier { get; }
    public bool IsBurst { get; }
    public bool IsBlackjack { get; }
}

public readonly struct PokerResult
{
    public PokerResult(PokerHandRank rank, int multiplier, IReadOnlyList<int> cardIndices)
    {
        Rank = rank;
        Multiplier = multiplier;
        CardIndices = cardIndices != null ? new List<int>(cardIndices) : new List<int>();
    }

    public PokerHandRank Rank { get; }
    public int Multiplier { get; }
    public IReadOnlyList<int> CardIndices { get; }

    public override string ToString()
    {
        return $"PokerResult(Rank={Rank}, Multiplier={Multiplier}, CardIndices=[{string.Join(", ", CardIndices)}])";
    }
}
