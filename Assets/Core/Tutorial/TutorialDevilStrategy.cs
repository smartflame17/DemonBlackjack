using System;
using System.Collections.Generic;

public sealed class TutorialDevilStrategy : IDevilStrategy
{
    private readonly int standScore;

    public TutorialDevilStrategy(int drawValue, int standScore)
    {
        DrawValue = drawValue <= 0 ? 3 : drawValue;
        this.standScore = standScore <= 0 ? 17 : standScore;
    }

    public int DrawValue { get; }

    public void UpdateDevilState(DevilStateUpdateContext context)
    {
    }

    public DevilTurnChoice ChooseTurnAction(BattleState battle, RoundState round)
    {
        if (round == null)
            return DevilTurnChoice.Stand;

        ScoreResult score = ScoreResolver.Resolve(round.OpponentVisibleCards, round.ScoringModifiers, round.TargetScore, round.OpponentBurstThreshold);
        if (score.FinalScore >= standScore || score.IsBurst)
            return DevilTurnChoice.Stand;

        if (round.OpponentHand.Count > 0)
            return DevilTurnChoice.Play;

        return battle != null && battle.OpponentDrawPile.Count > 0 ? DevilTurnChoice.Hit : DevilTurnChoice.Stand;
    }

    public int ChooseCardIndex(BattleState battle, RoundState round)
    {
        return round != null && round.OpponentHand.Count > 0 ? 0 : -1;
    }

    public IEnumerable<Card> CreateStartingDeck(RunState runState, BattleConfig config)
    {
        return Array.Empty<Card>();
    }

    public void RegisterAffinityHooks(BattleState battle)
    {
    }

    public void UnregisterAffinityHooks(BattleState battle)
    {
    }

    public string GetDialogueId()
    {
        return null;
    }

    public IEnumerable<Modifier> GetGlobalModifiers(RunState runState)
    {
        return Array.Empty<Modifier>();
    }
}
