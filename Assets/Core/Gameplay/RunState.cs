using System;
using System.Collections.Generic;

public sealed class RunState
{
    public const int StandardDeckSize = 52;

    private readonly List<RunCard> _runDeck = new();
    private readonly List<Card> _deck = new();
    private readonly List<string> _relicIds = new();
    private readonly List<string> _globalModifierIds = new();
    private readonly List<string> _activeItemIds = new();
    private readonly List<BattleResult> _battleHistory = new();
    private readonly Dictionary<string, int> _devilAffinities = new();
    private readonly Dictionary<Rank, string> _rankModifierIds = new();

    public RunState(int seed, int maxPlayerHp = 100, int startingGold = 100)
    {
        Seed = seed;
        Money = Math.Max(0, startingGold);
        MaxPlayerHp = Math.Max(1, maxPlayerHp);
        _runDeck.AddRange(CreateStandardRunDeck());
        RefreshBattleDeck();
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
    public IReadOnlyList<RunCard> RunDeck => _runDeck;
    public IReadOnlyList<Card> Deck => _deck;
    public IReadOnlyList<string> RelicIds => _relicIds;
    public IReadOnlyList<string> GlobalModifierIds => _globalModifierIds;
    public IReadOnlyList<string> ActiveItemIds => _activeItemIds;
    public IReadOnlyList<BattleResult> BattleHistory => _battleHistory;
    public IReadOnlyDictionary<string, int> DevilAffinities => _devilAffinities;
    public IReadOnlyDictionary<Rank, string> RankModifierIds => _rankModifierIds;

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

        _devilAffinities[devilId] = GetDevilAffinity(devilId) + amount;
    }

    public void AddRelic(string relicId)
    {
        if (!string.IsNullOrWhiteSpace(relicId) && !_relicIds.Contains(relicId))
            _relicIds.Add(relicId);
    }

    public bool RemoveRelic(string relicId)
    {
        return !string.IsNullOrWhiteSpace(relicId) && _relicIds.Remove(relicId);
    }

    public bool HasRelic(string relicId)
    {
        return !string.IsNullOrWhiteSpace(relicId) && _relicIds.Contains(relicId);
    }

    public void AddGlobalModifier(string modifierId)
    {
        if (!string.IsNullOrWhiteSpace(modifierId))
            _globalModifierIds.Add(modifierId);
    }

    public void AddActiveItem(string itemId)
    {
        if (!string.IsNullOrWhiteSpace(itemId))
            _activeItemIds.Add(itemId);
    }

    public bool RemoveActiveItem(string itemId)
    {
        return !string.IsNullOrWhiteSpace(itemId) && _activeItemIds.Remove(itemId);
    }

    public bool HasActiveItem(string itemId)
    {
        return !string.IsNullOrWhiteSpace(itemId) && _activeItemIds.Contains(itemId);
    }

    public bool TryGetCard(string instanceId, out RunCard card)
    {
        if (!string.IsNullOrWhiteSpace(instanceId))
        {
            for (int i = 0; i < _runDeck.Count; i++)
            {
                if (_runDeck[i].InstanceId == instanceId)
                {
                    card = _runDeck[i];
                    return true;
                }
            }
        }

        card = default;
        return false;
    }

    public bool AttachRankModifier(Rank rank, string modifierId)
    {
        if (!Enum.IsDefined(typeof(Rank), rank) || string.IsNullOrWhiteSpace(modifierId))
            return false;

        _rankModifierIds[rank] = modifierId;
        RefreshBattleDeck();
        return true;
    }

    public bool DetachRankModifier(Rank rank)
    {
        if (!_rankModifierIds.Remove(rank))
            return false;

        RefreshBattleDeck();
        return true;
    }

    public bool TryGetRankModifier(Rank rank, out string modifierId)
    {
        return _rankModifierIds.TryGetValue(rank, out modifierId);
    }

    public IReadOnlyList<Card> CreateBattleDeck()
    {
        RefreshBattleDeck();
        return new List<Card>(_deck);
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

    private void RefreshBattleDeck()
    {
        _deck.Clear();

        for (int i = 0; i < _runDeck.Count; i++)
        {
            RunCard runCard = _runDeck[i];
            _rankModifierIds.TryGetValue(runCard.Rank, out string modifierId);
            _deck.Add(CardModifierResolver.Apply(runCard, modifierId));
        }
    }

    private static List<RunCard> CreateStandardRunDeck()
    {
        var cards = new List<RunCard>(StandardDeckSize);

        foreach (Suit suit in Enum.GetValues(typeof(Suit)))
        {
            foreach (Rank rank in Enum.GetValues(typeof(Rank)))
                cards.Add(new RunCard(RunCard.CreateInstanceId(suit, rank), suit, rank));
        }

        return cards;
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
