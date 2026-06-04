using System;
using System.Collections.Generic;

public sealed class RunState
{
    private readonly List<Card> _deck = new();
    private readonly List<string> _relicIds = new();
    private readonly List<string> _globalModifierIds = new();
    private readonly List<BattleResult> _battleHistory = new();
    private readonly Dictionary<string, int> _devilAffinities = new();

    public RunState(int seed, int maxPlayerHp = 100, int startingGold = 100)
    {
        Seed = seed;
        Money = Math.Max(0, startingGold);
        MaxPlayerHp = Math.Max(1, maxPlayerHp);
        _deck.AddRange(global::Deck.CreateStandardDeck());
    }

    public int Seed { get; }
    public RunPhase Phase { get; private set; } = RunPhase.Inactive;
    public int PlayerHp => Money;
    public int MaxPlayerHp { get; private set; }
    public int Money { get; private set; }
    public int Gold => Money;
    public int EncounterIndex { get; private set; }
    public int DifficultyLevel { get; private set; } = 1;
    public int DevilProgression { get; private set; }
    public IReadOnlyList<Card> Deck => _deck;
    public IReadOnlyList<string> RelicIds => _relicIds;
    public IReadOnlyList<string> GlobalModifierIds => _globalModifierIds;
    public IReadOnlyList<BattleResult> BattleHistory => _battleHistory;
    public IReadOnlyDictionary<string, int> DevilAffinities => _devilAffinities;

    public void SetPhase(RunPhase phase)
    {
        Phase = phase;
    }

    public void AddGold(int amount)
    {
        AddMoney(amount);
    }

    public void AddMoney(int amount)
    {
        Money = Math.Max(0, Money + amount);
        EventBus.Publish(new MoneyChangedEvent(Combatant.Player, Money, amount));
        EventBus.Publish(new GoldChangedEvent(Money, amount));
    }

    public void SetPlayerHp(int hp)
    {
        SetMoney(hp);
    }

    public void SetMoney(int money)
    {
        int previous = Money;
        Money = Math.Max(0, money);
        int delta = Money - previous;
        EventBus.Publish(new MoneyChangedEvent(Combatant.Player, Money, delta));
        EventBus.Publish(new GoldChangedEvent(Money, delta));
    }

    public int GetDevilAffinity(string devilId)
    {
        if (string.IsNullOrWhiteSpace(devilId))
            return 0;

        return _devilAffinities.TryGetValue(devilId, out int affinity) ? affinity : 0;
    }

    public void AddDevilAffinity(string devilId, int amount)
    {
        if (string.IsNullOrWhiteSpace(devilId) || amount == 0)
            return;

        _devilAffinities[devilId] = Math.Clamp(GetDevilAffinity(devilId) + amount, 0, 100);
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
        SetMoney(result.PlayerMoneyAfterBattle);
        EncounterIndex++;

        if (result.PlayerWon)
            DevilProgression++;
    }
}

public readonly struct BattleResult
{
    public BattleResult(bool playerWon, int roundCount, int playerHpAfterBattle, int opponentHpAfterBattle)
        : this(playerWon, roundCount, playerHpAfterBattle, opponentHpAfterBattle, playerHpAfterBattle, opponentHpAfterBattle)
    {
    }

    public BattleResult(bool playerWon, int roundCount, int playerMoneyAfterBattle, int opponentMoneyAfterBattle, int playerHpAfterBattle, int opponentHpAfterBattle)
    {
        PlayerWon = playerWon;
        RoundCount = roundCount;
        PlayerMoneyAfterBattle = playerMoneyAfterBattle;
        OpponentMoneyAfterBattle = opponentMoneyAfterBattle;
        PlayerHpAfterBattle = playerHpAfterBattle;
        OpponentHpAfterBattle = opponentHpAfterBattle;
    }

    public bool PlayerWon { get; }
    public int RoundCount { get; }
    public int PlayerMoneyAfterBattle { get; }
    public int OpponentMoneyAfterBattle { get; }
    public int PlayerHpAfterBattle { get; }
    public int OpponentHpAfterBattle { get; }
}
