using System;
using System.Collections.Generic;

// Core strategy interface for devil AI behavior.
public interface IDevilStrategy
{
    int DrawValue { get; }
    void UpdateDevilState(BattleState battle = null, RoundState round = null);
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

    public virtual void UpdateDevilState(BattleState battle, RoundState round)
    {
        // Default implementation does nothing. Override in derived classes for more complex behavior.
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

    public virtual IEnumerable<Card> CreateStartingDeck(RunState runState, BattleConfig config)
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
    public Devil1Strategy(int startMoney = 1000) : base(drawValue: 3)
    {
        startingMoney = startMoney <= 0 ? 1000 : startMoney;
    }

    private readonly int startingMoney;
    private bool isYandereMode = false;
    private int winStreak = 0;
    private int lossStreak = 0;
    private bool[] winStreakDialogueShown = new bool[4]; // Track if win streak dialogue has been shown for 2, 3, and 4 wins
    private bool[] lossStreakDialogueShown = new bool[4]; // Track if loss streak dialogue has been shown for 2, 3, and 4 losses

    // When should this update be called within the gameplay loop? It should be handled before any event publishing to account for other event-based behavior to be triggered correctly.
    public override void UpdateDevilState(BattleState battle, RoundState round)
    {
        if (battle == null || round == null)
            return;
        //TODO: check battle.CombatHistory to update win/loss streaks

        if (battle.OpponentMoney < startingMoney * 0.2f || lossStreak >= 3)
            isYandereMode = true;
        else
            isYandereMode = false;
        
    }

    public override IEnumerable<Card> CreateStartingDeck(RunState runState, BattleConfig config)
    {
        return Deck.CreateAllHeartsDeck(4);
    }

    public override string GetDialogueId()
    {
        if (winStreak >= 2 && winStreak <= 4 && !winStreakDialogueShown[winStreak - 1])
        {
            winStreakDialogueShown[winStreak - 1] = true;
            return $"devil1_WinStreak{winStreak}_Dialogue";
        }
        else if (lossStreak >= 2 && lossStreak <= 4 && !lossStreakDialogueShown[lossStreak - 1])
        {
            lossStreakDialogueShown[lossStreak - 1] = true;
            return $"devil1_LossStreak{lossStreak}_Dialogue";
        }
        return null; // No dialogue to show
    }

    // Additional devil-specific logic can be added here if needed
}
