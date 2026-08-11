using System;
using System.Collections.Generic;

public enum DevilStateUpdatePoint
{
    RoundStarted,
    OpponentTurnStarted,
    RoundResolved
}

public readonly struct DevilStateUpdateContext
{
    public DevilStateUpdateContext(
        BattleState battle,
        RoundState round,
        DevilStateUpdatePoint updatePoint,
        RoundResolution? resolution = null)
    {
        Battle = battle ?? throw new ArgumentNullException(nameof(battle));
        Round = round ?? throw new ArgumentNullException(nameof(round));

        bool isRoundResolved = updatePoint == DevilStateUpdatePoint.RoundResolved;
        if (isRoundResolved != resolution.HasValue)
            throw new ArgumentException("A resolution must be supplied only for a round-resolved update.", nameof(resolution));

        UpdatePoint = updatePoint;
        Resolution = resolution;
    }

    public BattleState Battle { get; }
    public RoundState Round { get; }
    public DevilStateUpdatePoint UpdatePoint { get; }
    public RoundResolution? Resolution { get; }
}

public interface IDevilOpponentFieldModifier
{
    void RefreshOpponentField(BattleState battle, RoundState round);
}

public interface IDevilRoundPayoutModifier
{
    int GetOpponentWinBonus(BattleState battle, RoundState round);
}

public interface IDevilRoundWagerModifier
{
    int GetRoundWager(BattleState battle, int baseWager);
}

public interface IDevilPokerPayoutModifier
{
    int ModifyPlayerPokerPayout(BattleState battle, RoundState round, int proposedPayout);
}

// Core strategy interface for devil AI behavior.
public interface IDevilStrategy
{
    int DrawValue { get; }
    void UpdateDevilState(DevilStateUpdateContext context);
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

    public virtual void UpdateDevilState(DevilStateUpdateContext context)
    {
        // Default implementation does nothing. Override in derived classes for more complex behavior.
    }

    public DevilTurnChoice ChooseTurnAction(BattleState battle, RoundState round)
    {
        if (battle == null || round == null)
            return DevilTurnChoice.Stand;

        ScoreResult currentScore = ScoreResolver.Resolve(round.OpponentVisibleCards, round.ScoringModifiers, round.OpponentBurstThreshold);
        if (currentScore.FinalScore >= round.OpponentBurstThreshold)
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
            ScoreResult score = ScoreResolver.Resolve(cards, round.ScoringModifiers, round.OpponentBurstThreshold);
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

public class Devil1Strategy : BasicDevilStrategy, IDevilOpponentFieldModifier, IDevilRoundPayoutModifier
{
    public Devil1Strategy(int startMoney = 1000) : base(drawValue: 3)
    {
        startingMoney = startMoney <= 0 ? 1000 : startMoney;
    }

    private readonly int startingMoney;
    private bool isYandereMode = false;
    private int winStreak = 0;
    private int lossStreak = 0;
    private int lastResolvedRoundNumber = -1;
    private bool[] winStreakDialogueShown = new bool[4]; // Track if win streak dialogue has been shown for 2, 3, and 4 wins
    private bool[] lossStreakDialogueShown = new bool[4]; // Track if loss streak dialogue has been shown for 2, 3, and 4 losses

    public override void UpdateDevilState(DevilStateUpdateContext context)
    {
        if (context.UpdatePoint == DevilStateUpdatePoint.RoundResolved
            && context.Round.RoundNumber > lastResolvedRoundNumber)
        {
            UpdateStreaks(context.Resolution.Value);
            lastResolvedRoundNumber = context.Round.RoundNumber;
        }

        RefreshMoneyState(context.Battle);
    }

    public void RefreshOpponentField(BattleState battle, RoundState round)
    {
        if (battle == null || round == null)
            return;

        RefreshMoneyState(battle);
        int targetSpades = isYandereMode
            ? Math.Min(3, Math.Max(1, lossStreak - 2))
            : 0;
        int transformedCards = 0;

        round.RefreshOpponentVisibleCards((card, owner) =>
        {
            if (owner != Combatant.Opponent
                || card.Suit != Suit.Hearts
                || transformedCards >= targetSpades)
            {
                return card;
            }

            transformedCards++;
            return new Card(Suit.Spades, card.Rank, card.ModifierId);
        });
    }

    public int GetOpponentWinBonus(BattleState battle, RoundState round)
    {
        if (battle == null || round == null || round.BaseWager <= 0)
            return 0;

        int heartCount = 0;
        int spadeCount = 0;
        for (int i = 0; i < round.OpponentVisibleCards.Count; i++)
        {
            Suit suit = round.OpponentVisibleCards[i].Suit;
            if (suit == Suit.Hearts)
                heartCount++;
            else if (suit == Suit.Spades)
                spadeCount++;
        }

        float multiplier = (heartCount + 1L) * GameplayConstants.Devil1Config.Devil1HeartMultiplier;
        for (int i = 0; i < spadeCount; i++)
        {
            multiplier *= GameplayConstants.Devil1Config.Devil1SpadeMultiplier;
            if (multiplier >= int.MaxValue)
            {
                multiplier = int.MaxValue;
                break;
            }
        }

        long bonus = (long)multiplier * round.BaseWager;
        return bonus >= int.MaxValue ? int.MaxValue : (int)bonus;
    }

    private void RefreshMoneyState(BattleState battle)
    {
        if (battle.OpponentMoney <= startingMoney * 0.2f)
        {
            PixelCrushers.DialogueSystem.DialogueLua.SetVariable("OverrideToLowAffinity", true);
            PixelCrushers.DialogueSystem.DialogueLua.SetVariable("OverrideToMidAffinity", false);
        }
        else if (battle.OpponentMoney <= startingMoney * 0.4f)
        {
            PixelCrushers.DialogueSystem.DialogueLua.SetVariable("OverrideToLowAffinity", false);
            PixelCrushers.DialogueSystem.DialogueLua.SetVariable("OverrideToMidAffinity", true);
        }
        else
        {
            PixelCrushers.DialogueSystem.DialogueLua.SetVariable("OverrideToLowAffinity", false);
            PixelCrushers.DialogueSystem.DialogueLua.SetVariable("OverrideToMidAffinity", false);
        }

        isYandereMode = battle.OpponentMoney < startingMoney * 0.2f || lossStreak >= 3;
    }

    private void UpdateStreaks(RoundResolution resolution)
    {
        //if (resolution.Winner == Combatant.Opponent)
        if (resolution.OpponentMoneyLost < resolution.PlayerMoneyLost)
        {
            winStreak++;
            lossStreak = 0;
            int affinity = PixelCrushers.DialogueSystem.DialogueLua.GetVariable("Devil1Affinity").asInt;
            PixelCrushers.DialogueSystem.DialogueLua.SetVariable("Devil1Affinity", affinity + 5);
        }
        //else if (resolution.Winner == Combatant.Player)
        else if (resolution.PlayerMoneyLost < resolution.OpponentMoneyLost)
        {
            lossStreak++;
            winStreak = 0;
            int affinity = PixelCrushers.DialogueSystem.DialogueLua.GetVariable("Devil1Affinity").asInt;
            PixelCrushers.DialogueSystem.DialogueLua.SetVariable("Devil1Affinity", affinity - 5);
        }
        else
        {
            winStreak = 0;
            lossStreak = 0;
        }
    }

    public override IEnumerable<Card> CreateStartingDeck(RunState runState, BattleConfig config)
    {
        return Deck.CreateDeckOfSuits(Suit.Hearts, duplicateCount: 4);
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

public class Devil2Strategy : BasicDevilStrategy, IDevilRoundWagerModifier, IDevilPokerPayoutModifier
{
    private int currentWager;
    private int lastResolvedRoundNumber = -1;

    public Devil2Strategy(int drawValue = 3) : base(drawValue)
    {
    }

    public override void UpdateDevilState(DevilStateUpdateContext context)
    {
        if (context.UpdatePoint != DevilStateUpdatePoint.RoundResolved
            || context.Round.RoundNumber <= lastResolvedRoundNumber)
        {
            return;
        }

        lastResolvedRoundNumber = context.Round.RoundNumber;
        if (context.Resolution.Value.Winner == Combatant.Player)
            currentWager = InflateWager(context.Round.BaseWager);
    }

    public int GetRoundWager(BattleState battle, int baseWager)
    {
        return currentWager > 0 ? currentWager : Math.Max(1, baseWager);
    }

    public int ModifyPlayerPokerPayout(BattleState battle, RoundState round, int proposedPayout)
    {
        int payout = Math.Max(0, proposedPayout);
        if (battle == null
            || round == null
            || battle.OpponentMoney >= GameplayConstants.Devil2Config.Devil2CircuitBreakerThreshold)
        {
            return payout;
        }

        int remainingPayout = Math.Max(
            0,
            GameplayConstants.Devil2Config.Devil2PokerPayoutThreshold - round.PlayerPokerEarnings);
        return Math.Min(payout, remainingPayout);
    }

    private static int InflateWager(int wager)
    {
        float inflationRate = GameplayConstants.Devil2Config.Devil2WagerInflationRate;
        if (float.IsNaN(inflationRate) || inflationRate <= 0f)
            return 1;

        double inflatedWager = Math.Truncate(Math.Max(1, wager) * (double)inflationRate);
        if (double.IsInfinity(inflatedWager) || inflatedWager >= int.MaxValue)
            return int.MaxValue;

        return Math.Max(1, (int)inflatedWager);
    }

    public override string GetDialogueId()
    {
        return null;
    }
}

// TODO: Implement Devil3Strategy and Devil4Strategy with unique behaviors and AI logic as needed.
public class Devil3Strategy : BasicDevilStrategy
{
    public Devil3Strategy(int drawValue = 3) : base(drawValue)
    {
    }
}

public class Devil4Strategy : BasicDevilStrategy
{
    public Devil4Strategy(int drawValue = 3) : base(drawValue)
    {
    }
}
