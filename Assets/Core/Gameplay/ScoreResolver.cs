using System;
using System.Collections.Generic;
using System.Linq;

public static class ScoreResolver
{
    public static ScoreResult Resolve(IReadOnlyList<Card> cards, IReadOnlyList<Modifier> modifiers, int targetScore, int burstThreshold)
    {
        int blackjackScore = ResolveBlackjackScore(cards);
        PokerHandRank pokerRank = ResolvePokerRank(cards);
        int multiplier = GetPokerMultiplier(pokerRank);

        int modifiedScore = ApplyModifiers(blackjackScore, multiplier, modifiers);
        bool isBurst = modifiedScore > burstThreshold;
        bool isBlackjack = modifiedScore == targetScore && cards.Count == 2;

        return new ScoreResult(blackjackScore, modifiedScore, pokerRank, multiplier, isBurst, isBlackjack);
    }

    private static int ResolveBlackjackScore(IReadOnlyList<Card> cards)
    {
        int score = 0;
        int aces = 0;

        foreach (Card card in cards)
        {
            score += card.BlackjackValue;

            if (card.Rank == Rank.Ace)
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
            _ => 1
        };
    }

    private static PokerHandRank ResolvePokerRank(IReadOnlyList<Card> cards)
    {
        if (cards == null || cards.Count < 2)
            return PokerHandRank.HighCard;

        var rankGroups = cards.GroupBy(card => card.Rank).Select(group => group.Count()).OrderByDescending(count => count).ToList();
        var suitGroups = cards.GroupBy(card => card.Suit).ToList();
        bool flush = cards.Count >= 5 && suitGroups.Any(group => group.Count() >= 5);
        bool straight = HasStraight(cards, out bool royal);
        bool straightFlush = suitGroups.Any(group => group.Count() >= 5 && HasStraight(group.ToList(), out royal));

        if (straightFlush && royal)
            return PokerHandRank.RoyalFlush;

        if (straightFlush)
            return PokerHandRank.StraightFlush;

        if (rankGroups[0] == 4)
            return PokerHandRank.FourOfAKind;

        if (rankGroups[0] == 3 && rankGroups.Count > 1 && rankGroups[1] >= 2)
            return PokerHandRank.FullHouse;

        if (flush)
            return PokerHandRank.Flush;

        if (straight)
            return PokerHandRank.Straight;

        if (rankGroups[0] == 3)
            return PokerHandRank.ThreeOfAKind;

        int pairs = rankGroups.Count(count => count == 2);

        if (pairs >= 2)
            return PokerHandRank.TwoPair;

        return pairs == 1 ? PokerHandRank.Pair : PokerHandRank.HighCard;
    }

    private static bool HasStraight(IReadOnlyList<Card> cards, out bool royal)
    {
        var values = cards.Select(card => card.Rank == Rank.Ace ? 14 : (int)card.Rank).Distinct().OrderBy(value => value).ToList();

        if (values.Contains(14))
            values.Insert(0, 1);

        int run = 1;
        royal = false;

        for (int i = 1; i < values.Count; i++)
        {
            if (values[i] == values[i - 1] + 1)
            {
                run++;

                if (run >= 5)
                {
                    royal = values[i] == 14 && values.Contains(10);
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
