using System;
using System.Collections.Generic;
using System.Linq;

public static class ScoreResolver
{
    private const int RequiredStraightOrFlushCardCount = 5;
    private static readonly Rank[] RoyalFlushRanks =
    {
        Rank.Ten,
        Rank.Jack,
        Rank.Queen,
        Rank.King,
        Rank.Ace
    };

    public static ScoreResult Resolve(IReadOnlyList<Card> cards, IReadOnlyList<Modifier> modifiers, int targetScore, int burstThreshold, int blackjackBonus = 0)
    {
        int blackjackScore = ResolveBlackjackScore(cards) + blackjackBonus;
        bool isBurst = blackjackScore > burstThreshold;

        PokerResult poker = ResolvePoker(cards);

        float modifiedScore = ApplyModifiers(blackjackScore, 1, modifiers);
        bool isBlackjack = modifiedScore == targetScore;

        return new ScoreResult(blackjackScore, (int)modifiedScore, poker.Rank, poker.Multiplier, isBurst, isBlackjack);
    }

    public static PokerResult ResolvePoker(IReadOnlyList<Card> cards)
    {
        PokerHandRank rank = ResolvePokerRank(cards, out List<int> cardIndices);
        return new PokerResult(rank, GetPokerMultiplier(rank), cardIndices);
    }

    private static int ResolveBlackjackScore(IReadOnlyList<Card> cards)
    {
        int score = 0;
        int aces = 0;

        foreach (Card card in cards)
        {
            int value = ResolveCardBlackjackValue(card, out bool usesSoftAce);
            score += value;

            if (usesSoftAce)
                aces++;
        }

        while (score > 21 && aces > 0)
        {
            score -= 10;
            aces--;
        }

        return score;
    }

    private static int ResolveCardBlackjackValue(Card card, out bool usesSoftAce)
    {
        usesSoftAce = false;

        if (CardModifierResolver.TryGetResolvedJokerValue(card.ModifierId, out int jokerValue))
            return jokerValue;

        if (string.Equals(card.ModifierId, CardModifierResolver.NegativeRank, StringComparison.Ordinal))
            return -card.BlackjackValue;

        if (IsZeroValueModifier(card.ModifierId))
            return 0;

        if (string.Equals(card.ModifierId, CardModifierResolver.DoubleCardValue, StringComparison.Ordinal))
            return card.BlackjackValue * 2;

        if (string.Equals(card.ModifierId, CardModifierResolver.HalfCardValue, StringComparison.Ordinal))
            return card.BlackjackValue / 2;

        usesSoftAce = card.Rank == Rank.Ace;
        return card.BlackjackValue;
    }

    private static bool IsZeroValueModifier(string modifierId)
    {
        return string.Equals(modifierId, CardModifierResolver.CopyQueen, StringComparison.Ordinal)
            || string.Equals(modifierId, CardModifierResolver.OutsourceOpponentCard, StringComparison.Ordinal)
            || string.Equals(modifierId, CardModifierResolver.DuplicateKing, StringComparison.Ordinal)
            || string.Equals(modifierId, CardModifierResolver.DuplicateOpponentCard, StringComparison.Ordinal)
            || string.Equals(modifierId, CardModifierResolver.HitTopDeckCard, StringComparison.Ordinal)
            || string.Equals(modifierId, CardModifierResolver.SplitPreviousCard, StringComparison.Ordinal)
            || string.Equals(modifierId, CardModifierResolver.ZeroThenForceHit, StringComparison.Ordinal);
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

    public static float GetPokerMultiplier(PokerHandRank rank)
    {
        return rank switch
        {
            PokerHandRank.Pair => 0.2f,
            PokerHandRank.LowStraight => 1.5f,
            PokerHandRank.TwoPair => 2f,
            PokerHandRank.LowFlush => 2.5f,
            PokerHandRank.ThreeOfAKind => 5f,
            PokerHandRank.Straight => 25f,
            PokerHandRank.Flush => 50f,
            PokerHandRank.FullHouse => 70f,
            PokerHandRank.FourOfAKind => 400f,
            PokerHandRank.StraightFlush => 777f,
            PokerHandRank.RoyalFlush => 777f,
            _ => 0
        };
    }

    // TODO: Calculate for LowStraight and LowFlush (4-card straight and flush)
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

        if (TryGetRoyalFlush(suitGroups, out List<int> royalFlushIndices))
        {
            cardIndices = royalFlushIndices;
            return PokerHandRank.RoyalFlush;
        }

        if (TryGetBestStraightFlush(suitGroups, out List<int> straightFlushIndices))
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

        if (TryGetBestFlush(suitGroups, out List<int> flushIndices))
        {
            cardIndices = flushIndices;
            return PokerHandRank.Flush;
        }

        if (TryGetBestStraight(indexedCards, RequiredStraightOrFlushCardCount, out List<int> straightIndices, out _, out _))
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

    private static bool TryGetRoyalFlush(IReadOnlyList<List<IndexedCard>> suitGroups, out List<int> cardIndices)
    {
        cardIndices = new List<int>();
        for (int groupIndex = 0; groupIndex < suitGroups.Count; groupIndex++)
        {
            List<IndexedCard> group = suitGroups[groupIndex];
            var entriesByRank = group
                .GroupBy(entry => entry.Card.Rank)
                .ToDictionary(rankGroup => rankGroup.Key, rankGroup => rankGroup.OrderBy(entry => entry.Index).First());
            if (RoyalFlushRanks.Any(rank => !entriesByRank.ContainsKey(rank)))
                continue;

            List<int> candidate = RoyalFlushRanks.Select(rank => entriesByRank[rank].Index).ToList();
            if (cardIndices.Count == 0 || HasEarlierOriginalIndices(candidate, cardIndices))
                cardIndices = candidate;
        }

        return cardIndices.Count > 0;
    }

    private static bool TryGetBestStraightFlush(IReadOnlyList<List<IndexedCard>> suitGroups, out List<int> cardIndices)
    {
        cardIndices = new List<int>();
        int bestHighestRank = 0;
        int bestSequenceHigh = 0;

        for (int groupIndex = 0; groupIndex < suitGroups.Count; groupIndex++)
        {
            List<IndexedCard> group = suitGroups[groupIndex];
            if (!TryGetBestStraight(
                    group,
                    RequiredStraightOrFlushCardCount,
                    out List<int> candidate,
                    out int candidateHighestRank,
                    out int candidateSequenceHigh))
            {
                continue;
            }

            if (cardIndices.Count == 0
                || IsBetterStraightCandidate(
                    candidate,
                    candidateHighestRank,
                    candidateSequenceHigh,
                    cardIndices,
                    bestHighestRank,
                    bestSequenceHigh))
            {
                cardIndices = candidate;
                bestHighestRank = candidateHighestRank;
                bestSequenceHigh = candidateSequenceHigh;
            }
        }

        return cardIndices.Count > 0;
    }

    private static bool TryGetBestFlush(IReadOnlyList<List<IndexedCard>> suitGroups, out List<int> cardIndices)
    {
        List<IndexedCard> bestCards = null;
        int bestRankSum = 0;

        for (int groupIndex = 0; groupIndex < suitGroups.Count; groupIndex++)
        {
            List<IndexedCard> group = suitGroups[groupIndex];
            if (group.Count < RequiredStraightOrFlushCardCount)
                continue;

            List<IndexedCard> candidate = group
                .OrderByDescending(entry => (int)entry.Card.Rank)
                .ThenBy(entry => entry.Index)
                .Take(RequiredStraightOrFlushCardCount)
                .ToList();
            int candidateRankSum = candidate.Sum(entry => (int)entry.Card.Rank);

            if (bestCards == null
                || candidateRankSum > bestRankSum
                || candidateRankSum == bestRankSum && HasHigherRanks(candidate, bestCards)
                || candidateRankSum == bestRankSum
                    && !HasHigherRanks(bestCards, candidate)
                    && HasEarlierOriginalIndices(
                        candidate.Select(entry => entry.Index).ToList(),
                        bestCards.Select(entry => entry.Index).ToList()))
            {
                bestCards = candidate;
                bestRankSum = candidateRankSum;
            }
        }

        cardIndices = bestCards == null
            ? new List<int>()
            : bestCards.OrderBy(entry => entry.Index).Select(entry => entry.Index).ToList();
        return cardIndices.Count > 0;
    }

    private static bool TryGetBestStraight(
        IReadOnlyList<IndexedCard> cards,
        int requiredCardCount,
        out List<int> cardIndices,
        out int highestRank,
        out int sequenceHigh)
    {
        cardIndices = new List<int>();
        highestRank = 0;
        sequenceHigh = 0;
        if (cards == null || requiredCardCount <= 0 || cards.Count < requiredCardCount)
            return false;

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

        List<int> values = rankEntries.Keys.OrderBy(value => value).ToList();

        for (int start = 0; start <= values.Count - requiredCardCount; start++)
        {
            bool consecutive = true;
            for (int offset = 1; offset < requiredCardCount; offset++)
            {
                if (values[start + offset] != values[start] + offset)
                {
                    consecutive = false;
                    break;
                }
            }

            if (!consecutive)
                continue;

            List<int> candidateValues = values.Skip(start).Take(requiredCardCount).ToList();
            List<int> candidate = candidateValues.Select(value => rankEntries[value].Index).ToList();
            int candidateHighestRank = candidateValues.Max(value => (int)rankEntries[value].Card.Rank);
            int candidateSequenceHigh = candidateValues[^1];

            if (cardIndices.Count == 0
                || IsBetterStraightCandidate(
                    candidate,
                    candidateHighestRank,
                    candidateSequenceHigh,
                    cardIndices,
                    highestRank,
                    sequenceHigh))
            {
                cardIndices = candidate;
                highestRank = candidateHighestRank;
                sequenceHigh = candidateSequenceHigh;
            }
        }

        return cardIndices.Count > 0;
    }

    private static bool IsBetterStraightCandidate(
        IReadOnlyList<int> candidate,
        int candidateHighestRank,
        int candidateSequenceHigh,
        IReadOnlyList<int> best,
        int bestHighestRank,
        int bestSequenceHigh)
    {
        if (candidateHighestRank != bestHighestRank)
            return candidateHighestRank > bestHighestRank;

        if (candidateSequenceHigh != bestSequenceHigh)
            return candidateSequenceHigh > bestSequenceHigh;

        return HasEarlierOriginalIndices(candidate, best);
    }

    private static bool HasHigherRanks(IReadOnlyList<IndexedCard> candidate, IReadOnlyList<IndexedCard> best)
    {
        for (int i = 0; i < Math.Min(candidate.Count, best.Count); i++)
        {
            int candidateRank = (int)candidate[i].Card.Rank;
            int bestRank = (int)best[i].Card.Rank;
            if (candidateRank != bestRank)
                return candidateRank > bestRank;
        }

        return candidate.Count > best.Count;
    }

    private static bool HasEarlierOriginalIndices(IReadOnlyList<int> candidate, IReadOnlyList<int> best)
    {
        List<int> candidateIndices = candidate.OrderBy(index => index).ToList();
        List<int> bestIndices = best.OrderBy(index => index).ToList();
        for (int i = 0; i < Math.Min(candidateIndices.Count, bestIndices.Count); i++)
        {
            if (candidateIndices[i] != bestIndices[i])
                return candidateIndices[i] < bestIndices[i];
        }

        return candidateIndices.Count < bestIndices.Count;
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
    public ScoreResult(int blackjackScore, int finalScore, PokerHandRank pokerRank, float pokerMultiplier, bool isBurst, bool isBlackjack)
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
    public float PokerMultiplier { get; }
    public bool IsBurst { get; }
    public bool IsBlackjack { get; }
}

public readonly struct PokerResult
{
    public PokerResult(PokerHandRank rank, float multiplier, IReadOnlyList<int> cardIndices)
    {
        Rank = rank;
        Multiplier = multiplier;
        CardIndices = cardIndices != null ? new List<int>(cardIndices) : new List<int>();
    }

    public PokerHandRank Rank { get; }
    public float Multiplier { get; }
    public IReadOnlyList<int> CardIndices { get; }

    public override string ToString()
    {
        return $"PokerResult(Rank={Rank}, Multiplier={Multiplier}, CardIndices=[{string.Join(", ", CardIndices)}])";
    }
}
