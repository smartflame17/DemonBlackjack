using System;
using System.Collections.Generic;

// Core strategy interface for devil AI behavior.
public interface IDevilStrategy
{
    int DrawValue { get; }
    DevilTurnChoice ChooseTurnAction(BattleState battle, RoundState round);
    int ChooseCardIndex(BattleState battle, RoundState round);
    IEnumerable<Card> CreateStartingDeck(RunState runState, BattleConfig config);
    void RegisterAffinityHooks(BattleState battle);
    void UnregisterAffinityHooks(BattleState battle);
    string GetDialogueId();
    IEnumerable<Modifier> GetGlobalModifiers(RunState runState);
}


// Default strategy that always plays best card in hand and has a fixed draw value (default 3). Can be used for testing or as a simple baseline strategy.
// Expand upon here to create complex AI strategies that consider the current battle and round state to make informed decisions on which card to play and how much to draw.
public class BasicDevilStrategy : IDevilStrategy
{
    public BasicDevilStrategy(int drawValue = 3)
    {
        DrawValue = drawValue <= 0 ? 3 : drawValue;
    }

    public int DrawValue { get; }

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

    public virtual string GetDialogueId()
    {
        return "Error";
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

}

public class Devil1Strategy : BasicDevilStrategy
{
    public Devil1Strategy() : base(drawValue: 3)
    {
    }

    public override string GetDialogueId()
    {
        // TODO: Check devil state, conditions, and other factors to determine which dialogue ID to return. For now, we return a fixed ID for Devil1.
        return "Devil1TestConversation";
    }

    // Additional devil-specific logic can be added here if needed
}
