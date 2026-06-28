using System;
using System.Collections.Generic;

public interface IDevilStrategy
{
    int DrawValue { get; }
    int ChooseWager(BattleState battle, RoundState pendingRound, int defaultWager);
    WagerResponse ChoosePlayerWagerResponse(BattleState battle, RoundState pendingRound, int proposedWager);
    DevilTurnChoice ChooseTurnAction(BattleState battle, RoundState round);
    int ChooseCardIndex(BattleState battle, RoundState round);
    IEnumerable<Card> CreateStartingDeck(RunState runState, BattleConfig config);
    void RegisterAffinityHooks(BattleState battle);     // TODO: Attach to wager events to adjust affinity based on player choices. For example, accepting a wager could increase affinity, while declining could decrease it. This would allow the strategy to adapt over time based on the player's behavior.
    void UnregisterAffinityHooks(BattleState battle);
    IEnumerable<Modifier> GetGlobalModifiers(RunState runState);
}


// Default strategy that always plays first card in hand and has a fixed draw value (default 2). Can be used for testing or as a simple baseline strategy.
// Expand upon here to create complex AI strategies that consider the current battle and round state to make informed decisions on which card to play and how much to draw.
public sealed class BasicDevilStrategy : IDevilStrategy
{
    public BasicDevilStrategy(int drawValue = 3)
    {
        DrawValue = drawValue <= 0 ? 3 : drawValue;
    }

    public int DrawValue { get; }

    public int ChooseWager(BattleState battle, RoundState pendingRound, int defaultWager)
    {
        if (battle == null || pendingRound == null || defaultWager <= 0)
            return Math.Max(0, defaultWager);

        return defaultWager * (int)EvaluateHandLevel(pendingRound.OpponentHand);
    }

    public WagerResponse ChoosePlayerWagerResponse(BattleState battle, RoundState pendingRound, int proposedWager)
    {
        return RollPlayerBeneficialDecision(battle) ? WagerResponse.Accept : WagerResponse.Decline;
    }

    public DevilTurnChoice ChooseTurnAction(BattleState battle, RoundState round)
    {
        if (battle == null || round == null)
            return DevilTurnChoice.Stand;

        ScoreResult currentScore = ScoreResolver.Resolve(round.OpponentVisibleCards, round.ScoringModifiers, round.TargetScore, round.OpponentBurstThreshold);
        if (currentScore.FinalScore >= round.TargetScore)
            return DevilTurnChoice.Stand;

        int bestHandIndex = FindBestPlayableCardIndex(round.OpponentHand, round.OpponentVisibleCards, round);
        if (bestHandIndex >= 0)
            return DevilTurnChoice.Play;

        return ShouldHit(currentScore.FinalScore) ? DevilTurnChoice.Hit : DevilTurnChoice.Stand;
    }

    public int ChooseCardIndex(BattleState battle, RoundState round)
    {
        if (round == null)
            return -1;

        int bestIndex = FindBestPlayableCardIndex(round.OpponentHand, round.OpponentVisibleCards, round);
        return bestIndex >= 0 ? bestIndex : 0;
    }

    public IEnumerable<Card> CreateStartingDeck(RunState runState, BattleConfig config)
    {
        return Deck.CreateStandardDeck();
    }

    public void RegisterAffinityHooks(BattleState battle)
    {
    }

    public void UnregisterAffinityHooks(BattleState battle)
    {
    }

    public IEnumerable<Modifier> GetGlobalModifiers(RunState runState)
    {
        return Array.Empty<Modifier>();
    }

    private static int FindBestPlayableCardIndex(IReadOnlyList<Card> hand, IReadOnlyList<Card> visibleCards, RoundState round)
    {
        int bestIndex = -1;
        int bestScore = int.MinValue;

        for (int i = 0; i < hand.Count; i++)
        {
            var cards = new List<Card>(visibleCards) { hand[i] };
            ScoreResult score = ScoreResolver.Resolve(cards, round.ScoringModifiers, round.TargetScore, round.OpponentBurstThreshold);
            if (score.IsBurst)
                continue;

            if (score.FinalScore > bestScore)
            {
                bestScore = score.FinalScore;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static bool ShouldHit(int currentScore)
    {
        return currentScore < 17;
    }

    private static DevilHandLevel EvaluateHandLevel(IReadOnlyList<Card> hand)
    {
        int rankTotal = 0;
        if (hand != null)
        {
            for (int i = 0; i < hand.Count; i++)
                rankTotal += (int)hand[i].Rank;
        }

        if (rankTotal >= 20)
            return DevilHandLevel.VeryHigh;
        if (rankTotal >= 18)
            return DevilHandLevel.High;
        if (rankTotal >= 15)
            return DevilHandLevel.Medium;
        if (rankTotal >= 11)
            return DevilHandLevel.Low;

        return DevilHandLevel.VeryLow;
    }

    private static bool RollPlayerBeneficialDecision(BattleState battle)
    {
        if (battle == null)
            return true;

        int affinity = Math.Max(0, battle.RunState.GetDevilAffinity(battle.Config.DevilId));
        int acceptChance = Math.Clamp(50 + (affinity * 5), 50, 95);
        return battle.NextRandomInclusive(1, 100) <= acceptChance;
    }
}
