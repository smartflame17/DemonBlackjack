using System;
using System.Collections.Generic;

public interface IDevilStrategy
{
    int DrawValue { get; }
    int ChooseWager(BattleState battle, int minimum, int maximum, int step);
    WagerResponse ChoosePlayerWagerResponse(BattleState battle, RoundState pendingRound, int proposedWager);
    WagerResponse ChoosePlayerReductionResponse(BattleState battle, RoundState pendingRound, int proposedWager);
    DevilTurnChoice ChooseTurnAction(BattleState battle, RoundState round);
    int ChooseCardIndex(BattleState battle, RoundState round);
    IEnumerable<Card> CreateStartingDeck(RunState runState, BattleConfig config);
    void RegisterAffinityHooks(BattleState battle);
    void UnregisterAffinityHooks(BattleState battle);
    IEnumerable<Modifier> GetGlobalModifiers(RunState runState);
}


// Default strategy that always plays first card in hand and has a fixed draw value (default 2). Can be used for testing or as a simple baseline strategy.
// Expand upon here to create complex AI strategies that consider the current battle and round state to make informed decisions on which card to play and how much to draw.
public sealed class BasicDevilStrategy : IDevilStrategy
{
    public BasicDevilStrategy(int drawValue = 2)
    {
        DrawValue = drawValue <= 0 ? 2 : drawValue;
    }

    public int DrawValue { get; }

    public int ChooseWager(BattleState battle, int minimum, int maximum, int step)
    {
        if (battle == null)
            return minimum;

        int affinity = battle.RunState.GetDevilAffinity(battle.Config.DevilId);
        int biasedMaximum = Math.Max(minimum, maximum - (Math.Max(0, affinity) * step));
        return ClampToStep(battle.NextRandomInclusive(minimum, biasedMaximum), minimum, biasedMaximum, step);
    }

    public WagerResponse ChoosePlayerWagerResponse(BattleState battle, RoundState pendingRound, int proposedWager)
    {
        return RollPlayerBeneficialDecision(battle) ? WagerResponse.Accept : WagerResponse.Reduce;
    }

    public WagerResponse ChoosePlayerReductionResponse(BattleState battle, RoundState pendingRound, int proposedWager)
    {
        return RollPlayerBeneficialDecision(battle) ? WagerResponse.Accept : WagerResponse.Reduce;
    }

    public DevilTurnChoice ChooseTurnAction(BattleState battle, RoundState round)
    {
        if (battle == null || round == null)
            return DevilTurnChoice.Stand;

        ScoreResult currentScore = ScoreResolver.Resolve(round.OpponentVisibleCards, round.ScoringModifiers, round.TargetScore, round.BurstThreshold);
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
            ScoreResult score = ScoreResolver.Resolve(cards, round.ScoringModifiers, round.TargetScore, round.BurstThreshold);
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

    private static int ClampToStep(int value, int minimum, int maximum, int step)
    {
        int clamped = Math.Clamp(value, minimum, maximum);
        int offset = clamped - minimum;
        return minimum + ((offset / step) * step);
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
