using System;
using System.Collections.Generic;

[Serializable]

public sealed class GameData
{
    // Store game info (mostly RunState) here
    public long timestamp;
    public RunStateData runState;
    public string dialogueSaveData;

    public GameData()
    {
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
