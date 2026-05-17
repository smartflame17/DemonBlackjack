using System;
using System.Collections.Generic;

public sealed class RunState
{
    private readonly List<Card> _deck = new();
    private readonly List<string> _relicIds = new();
    private readonly List<string> _globalModifierIds = new();
    private readonly List<BattleResult> _battleHistory = new();

    public RunState(int seed, int maxPlayerHp = 50, int startingGold = 0)
    {
        Seed = seed;
        MaxPlayerHp = Math.Max(1, maxPlayerHp);
        PlayerHp = MaxPlayerHp;
        Gold = Math.Max(0, startingGold);
        _deck.AddRange(global::Deck.CreateStandardDeck());
    }

    public int Seed { get; }
    public RunPhase Phase { get; private set; } = RunPhase.Inactive;
    public int PlayerHp { get; private set; }
    public int MaxPlayerHp { get; private set; }
    public int Gold { get; private set; }
    public int EncounterIndex { get; private set; }
    public int DifficultyLevel { get; private set; } = 1;
    public int DevilProgression { get; private set; }
    public IReadOnlyList<Card> Deck => _deck;
    public IReadOnlyList<string> RelicIds => _relicIds;
    public IReadOnlyList<string> GlobalModifierIds => _globalModifierIds;
    public IReadOnlyList<BattleResult> BattleHistory => _battleHistory;

    public void SetPhase(RunPhase phase)
    {
        Phase = phase;
    }

    public void AddGold(int amount)
    {
        Gold = Math.Max(0, Gold + amount);
        EventBus.Publish(new GoldChangedEvent(Gold, amount));
    }

    public void SetPlayerHp(int hp)
    {
        PlayerHp = Math.Clamp(hp, 0, MaxPlayerHp);
    }

    public void AddRelic(string relicId)
    {
        if (!string.IsNullOrWhiteSpace(relicId))
            _relicIds.Add(relicId);
    }

    public void AddGlobalModifier(string modifierId)
    {
        if (!string.IsNullOrWhiteSpace(modifierId))
            _globalModifierIds.Add(modifierId);
    }

    public int CreateBattleSeed()
    {
        unchecked
        {
            return Seed + (EncounterIndex * 397) + (DifficultyLevel * 101) + DevilProgression;
        }
    }

    public void ApplyBattleResult(BattleResult result)
    {
        _battleHistory.Add(result);
        SetPlayerHp(result.PlayerHpAfterBattle);
        EncounterIndex++;

        if (result.PlayerWon)
            DevilProgression++;
    }
}

public readonly struct BattleResult
{
    public BattleResult(bool playerWon, int roundCount, int playerHpAfterBattle, int opponentHpAfterBattle)
    {
        PlayerWon = playerWon;
        RoundCount = roundCount;
        PlayerHpAfterBattle = playerHpAfterBattle;
        OpponentHpAfterBattle = opponentHpAfterBattle;
    }

    public bool PlayerWon { get; }
    public int RoundCount { get; }
    public int PlayerHpAfterBattle { get; }
    public int OpponentHpAfterBattle { get; }
}
