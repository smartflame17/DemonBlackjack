using System;
using System.Collections.Generic;

[Serializable]

public sealed class GameData
{
    public const int CurrentSchemaVersion = 2;

    // Store game info (mostly RunState) here
    public int schemaVersion;
    public long timestamp;
    public RunStateData runState;
    public string dialogueSaveData;

    public GameData()
    {
        schemaVersion = CurrentSchemaVersion;
        timestamp = DateTime.Now.ToFileTime();
        dialogueSaveData = string.Empty;
    }
}

[Serializable]
public sealed class RunStateData
{
    public int seed;
    public RunPhase phase;
    public int money;
    public int encounterIndex;
    public int difficultyLevel;
    public int devilProgression;
    public float currentShopPriceInflationRate;
    public int maxActiveItemSlots;
    public int playerBurstThresholdBonus;
    public List<RunCardData> deck = new();
    public List<string> relicIds = new();
    public List<string> globalModifierIds = new();
    public List<string> activeItemIds = new();
    public List<BattleResultData> battleHistory = new();
    public List<DevilAffinityData> devilAffinities = new();
    public List<RankModifierData> rankModifierIds = new();
    public BattleStateData battleState;
}

[Serializable]
public sealed class RunCardData
{
    public string instanceId;
    public Suit suit;
    public Rank rank;
    public string modifierId;
}

[Serializable]
public sealed class BattleResultData
{
    public bool playerWon;
    public int roundCount;
    public int playerMoneyAfterBattle;
    public int opponentMoneyAfterBattle;
}

[Serializable]
public sealed class DevilAffinityData
{
    public string devilId;
    public int affinity;
}

[Serializable]
public sealed class RankModifierData
{
    public Rank rank;
    public string modifierId;
    public int paidPrice;
}

[Serializable]
public sealed class BattleStateData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public BattleConfigData config;
    public BattlePhase phase;
    public int opponentMoney;
    public int roundNumber;
    public int reshuffleCount;
    public int battleSeed;
    public RoundStateData currentRound;
    public DeckData playerDeck;
    public DeckData opponentDeck;
    public RandomStateData randomState;
    public List<ModifierData> activeModifiers = new();
    public List<RoundResolutionData> combatHistory = new();
    public List<CardData> playerHandCarryover = new();
    public List<CardData> opponentHandCarryover = new();
    public DevilStrategyStateData devilStrategyState;
    public List<RelicRuntimeStateData> relicRuntimeStates = new();
}

[Serializable]
public sealed class BattleConfigData
{
    public string encounterId;
    public string devilId;
    public int opponentStartingMoney;
    public int startingHandSize;
    public int burstThreshold;
    public int baseWager;
    public string strategyId;
    public int strategyDrawValue;
    public int strategyStartingMoney;
    public bool hasPlayerDeckOverride;
    public bool hasOpponentDeckOverride;
    public List<ModifierData> initialModifiers = new();
    public List<CardData> playerDeckOverride = new();
    public List<CardData> opponentDeckOverride = new();
}

[Serializable]
public sealed class RoundStateData
{
    public int roundNumber;
    public int playerBurstThreshold;
    public int opponentBurstThreshold;
    public int baseWager;
    public int wagerMultiplier;
    public int playerStake;
    public int opponentStake;
    public bool playerActsFirst;
    public bool wagerCommitted;
    public bool playerBurstPenaltyResolved;
    public bool opponentBurstPenaltyResolved;
    public int opponentMoneyLost;
    public int playerMoneyLost;
    public int playerPokerEarnings;
    public bool playerStood;
    public bool opponentStood;
    public bool playerPlayedThisTurn;
    public bool opponentPlayedThisTurn;
    public int maxPlayedPileCardCount;
    public ScoreResultData playerScore;
    public ScoreResultData opponentScore;
    public List<CardData> playerHand = new();
    public List<CardData> playerPlayedCards = new();
    public List<CardData> opponentHand = new();
    public List<CardData> opponentVisibleCards = new();
    public List<CardData> opponentOriginalVisibleCards = new();
    public List<Combatant> opponentVisibleCardOwners = new();
    public List<CardData> sharedVisibleCards = new();
    public List<CardData> lockedCards = new();
    public List<CardData> revealedFutureCards = new();
    public List<string> pendingEffectIds = new();
    public List<ModifierData> scoringModifiers = new();
    public List<PokerHandRank> achievedPlayerPokerHandRanks = new();
}

[Serializable]
public sealed class DeckData
{
    public List<CardData> drawPile = new();
    public List<CardData> discardPile = new();
    public RandomStateData randomState;
}

[Serializable]
public sealed class RandomStateData
{
    public int seed;
    public List<RandomCallData> calls = new();
}

[Serializable]
public sealed class RandomCallData
{
    public int minimumInclusive;
    public int maximumExclusive;
}

[Serializable]
public sealed class CardData
{
    public Suit suit;
    public Rank rank;
    public string modifierId;

    public Card ToCard() => new(suit, rank, modifierId);

    public static CardData FromCard(Card card)
    {
        return new CardData
        {
            suit = card.Suit,
            rank = card.Rank,
            modifierId = card.ModifierId
        };
    }
}

[Serializable]
public sealed class ModifierData
{
    public string id;
    public ModifierOperation operation;
    public int value;
    public int remainingRounds;

    public Modifier ToModifier() => new(id, operation, value, remainingRounds);

    public static ModifierData FromModifier(Modifier modifier)
    {
        return new ModifierData
        {
            id = modifier.Id,
            operation = modifier.Operation,
            value = modifier.Value,
            remainingRounds = modifier.RemainingRounds
        };
    }
}

[Serializable]
public sealed class ScoreResultData
{
    public int blackjackScore;
    public int finalScore;
    public PokerHandRank pokerRank;
    public float pokerMultiplier;
    public bool isBurst;
    public bool isBlackjack;

    public ScoreResult ToScoreResult() => new(blackjackScore, finalScore, pokerRank, pokerMultiplier, isBurst, isBlackjack);

    public static ScoreResultData FromScoreResult(ScoreResult score)
    {
        return new ScoreResultData
        {
            blackjackScore = score.BlackjackScore,
            finalScore = score.FinalScore,
            pokerRank = score.PokerRank,
            pokerMultiplier = score.PokerMultiplier,
            isBurst = score.IsBurst,
            isBlackjack = score.IsBlackjack
        };
    }
}

[Serializable]
public sealed class RoundResolutionData
{
    public bool hasWinner;
    public Combatant winner;
    public int opponentMoneyLost;
    public int playerMoneyLost;

    public RoundResolution ToRoundResolution()
    {
        return new RoundResolution(hasWinner ? winner : null, opponentMoneyLost, playerMoneyLost);
    }

    public static RoundResolutionData FromRoundResolution(RoundResolution resolution)
    {
        return new RoundResolutionData
        {
            hasWinner = resolution.Winner.HasValue,
            winner = resolution.Winner.GetValueOrDefault(),
            opponentMoneyLost = resolution.OpponentMoneyLost,
            playerMoneyLost = resolution.PlayerMoneyLost
        };
    }
}

[Serializable]
public sealed class DevilStrategyStateData
{
    public string strategyId;
    public bool isYandereMode;
    public int winStreak;
    public int lossStreak;
    public int lastResolvedRoundNumber;
    public int currentWager;
    public List<bool> winStreakDialogueShown = new();
    public List<bool> lossStreakDialogueShown = new();
}

[Serializable]
public sealed class RelicRuntimeStateData
{
    public string relicId;
    public int counter;
    public bool hasAnchor;
    public Rank anchorRank;
    public Suit anchorSuit;
}
