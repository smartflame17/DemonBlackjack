using System;
using System.Collections.Generic;

public sealed class RunState
{
    public const int StandardDeckSize = 52;
    public const int DefaultMaxActiveItemSlots = 2;

    private readonly List<RunCard> _runDeck = new();
    private readonly List<Card> _deck = new();
    private readonly List<string> _relicIds = new();
    private readonly List<string> _globalModifierIds = new();
    private readonly List<string> _activeItemIds = new();
    private readonly List<BattleResult> _battleHistory = new();
    private readonly Dictionary<string, int> _devilAffinities = new();
    private readonly Dictionary<Rank, OwnedRankUpgrade> _rankUpgrades = new();

    public RunState(int seed, int maxPlayerHp = 100, int startingGold = 100, int maxActiveItemSlots = DefaultMaxActiveItemSlots)
    {
        Seed = seed;
        Money = Math.Max(0, startingGold);
        MaxPlayerHp = Math.Max(1, maxPlayerHp);
        MaxActiveItemSlots = maxActiveItemSlots > 0 ? maxActiveItemSlots : DefaultMaxActiveItemSlots;
        AddEmptyActiveItemSlots(MaxActiveItemSlots);
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
    public int MaxActiveItemSlots { get; private set; }
    public bool AreActiveItemSlotsFull => FindEmptyActiveItemSlot() < 0;
    public IReadOnlyList<RunCard> RunDeck => _runDeck;
    public IReadOnlyList<Card> Deck => _deck;
    public IReadOnlyList<string> RelicIds => _relicIds;
    public IReadOnlyList<string> GlobalModifierIds => _globalModifierIds;
    public IReadOnlyList<string> ActiveItemIds => _activeItemIds;
    public IReadOnlyList<BattleResult> BattleHistory => _battleHistory;
    public IReadOnlyDictionary<string, int> DevilAffinities => _devilAffinities;
    public IReadOnlyDictionary<Rank, OwnedRankUpgrade> RankUpgrades => _rankUpgrades;

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
        if (!string.IsNullOrWhiteSpace(relicId) && !_relicIds.Contains(relicId))
            _relicIds.Add(relicId);
    }

    public ShopPurchaseResult TryPurchaseRelic(string relicId, int price)
    {
        price = Math.Max(0, price);
        if (string.IsNullOrWhiteSpace(relicId))
            return ShopPurchaseResult.Failed(ShopPurchaseFailure.InvalidOffer, price);
        if (HasRelic(relicId))
            return ShopPurchaseResult.Failed(ShopPurchaseFailure.AlreadyOwned, price);
        if (Money < price)
            return ShopPurchaseResult.Failed(ShopPurchaseFailure.InsufficientFunds, price);

        AddMoney(-price);
        _relicIds.Add(relicId);
        EventBus.Publish(new RelicAddedEvent(relicId));
        return ShopPurchaseResult.Success(price);
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

    public bool AddActiveItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || AreActiveItemSlotsFull)
            return false;

        _activeItemIds[FindEmptyActiveItemSlot()] = itemId;
        EventBus.Publish(new ActiveItemAddedEvent(itemId, GetActiveItemCount(itemId)));
        return true;
    }

    public bool TrySetMaxActiveItemSlots(int maxActiveItemSlots)
    {
        if (maxActiveItemSlots < 1)
            return false;

        for (int i = maxActiveItemSlots; i < _activeItemIds.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(_activeItemIds[i]))
                return false;
        }

        MaxActiveItemSlots = maxActiveItemSlots;
        if (_activeItemIds.Count > maxActiveItemSlots)
            _activeItemIds.RemoveRange(maxActiveItemSlots, _activeItemIds.Count - maxActiveItemSlots);
        else
            AddEmptyActiveItemSlots(maxActiveItemSlots - _activeItemIds.Count);
        EventBus.Publish(new ActiveItemCapacityChangedEvent(MaxActiveItemSlots));
        return true;
    }

    public int GetActiveItemCount(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return 0;

        int count = 0;
        for (int i = 0; i < _activeItemIds.Count; i++)
        {
            if (string.Equals(_activeItemIds[i], itemId, StringComparison.OrdinalIgnoreCase))
                count++;
        }
        return count;
    }

    public ShopPurchaseResult TryPurchaseActiveItem(string itemId, int price)
    {
        price = Math.Max(0, price);
        if (string.IsNullOrWhiteSpace(itemId))
            return ShopPurchaseResult.Failed(ShopPurchaseFailure.InvalidOffer, price);
        if (AreActiveItemSlotsFull)
            return ShopPurchaseResult.Failed(ShopPurchaseFailure.ActiveItemSlotsFull, price);
        if (Money < price)
            return ShopPurchaseResult.Failed(ShopPurchaseFailure.InsufficientFunds, price);

        AddMoney(-price);
        AddActiveItem(itemId);
        return ShopPurchaseResult.Success(price);
    }

    public bool RemoveActiveItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        int slotIndex = FindActiveItemSlot(itemId);
        if (slotIndex < 0)
            return false;

        _activeItemIds[slotIndex] = null;

        EventBus.Publish(new ActiveItemRemovedEvent(itemId, GetActiveItemCount(itemId)));
        return true;
    }

    public bool HasActiveItem(string itemId)
    {
        return !string.IsNullOrWhiteSpace(itemId) && FindActiveItemSlot(itemId) >= 0;
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

        _rankUpgrades[rank] = new OwnedRankUpgrade(rank, modifierId, 0);
        return true;
    }

    public bool DetachRankModifier(Rank rank)
    {
        if (!_rankUpgrades.Remove(rank))
            return false;
        return true;
    }

    public bool TryGetRankModifier(Rank rank, out string modifierId)
    {
        if (_rankUpgrades.TryGetValue(rank, out OwnedRankUpgrade upgrade))
        {
            modifierId = upgrade.UpgradeId;
            return true;
        }
        modifierId = null;
        return false;
    }

    public bool TryGetRankUpgrade(Rank rank, out OwnedRankUpgrade upgrade) => _rankUpgrades.TryGetValue(rank, out upgrade);

    public ShopPurchaseResult TryPurchaseRankUpgrade(Rank rank, string upgradeId, int price)
    {
        price = Math.Max(0, price);
        if (!Enum.IsDefined(typeof(Rank), rank) || string.IsNullOrWhiteSpace(upgradeId))
            return ShopPurchaseResult.Failed(ShopPurchaseFailure.InvalidOffer, price);

        _rankUpgrades.TryGetValue(rank, out OwnedRankUpgrade previous);
        int refund = string.IsNullOrWhiteSpace(previous.UpgradeId) ? 0 : previous.PaidPrice / 2;
        int netCost = Math.Max(0, price - refund);
        if (Money < netCost)
            return ShopPurchaseResult.Failed(ShopPurchaseFailure.InsufficientFunds, price, refund);

        AddMoney(-netCost);
        _rankUpgrades[rank] = new OwnedRankUpgrade(rank, upgradeId, price);
        EventBus.Publish(new RankUpgradeChangedEvent(this, rank, previous.UpgradeId, upgradeId));
        return ShopPurchaseResult.Success(price, refund);
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

    public RunStateData ToData()
    {
        var data = new RunStateData
        {
            seed = Seed,
            phase = Phase,
            gold = Gold,
            maxPlayerHp = MaxPlayerHp,
            encounterIndex = EncounterIndex,
            difficultyLevel = DifficultyLevel,
            devilProgression = DevilProgression,
            maxActiveItemSlots = MaxActiveItemSlots
        };

        for (int i = 0; i < _runDeck.Count; i++)
        {
            RunCard card = _runDeck[i];
            data.deck.Add(new RunCardData
            {
                instanceId = card.InstanceId,
                suit = card.Suit,
                rank = card.Rank,
                modifierId = card.ModifierId
            });
        }

        data.relicIds.AddRange(_relicIds);
        data.globalModifierIds.AddRange(_globalModifierIds);
        data.activeItemIds.AddRange(_activeItemIds);

        for (int i = 0; i < _battleHistory.Count; i++)
        {
            BattleResult result = _battleHistory[i];
            data.battleHistory.Add(new BattleResultData
            {
                playerWon = result.PlayerWon,
                roundCount = result.RoundCount,
                playerMoneyAfterBattle = result.PlayerMoneyAfterBattle,
                opponentMoneyAfterBattle = result.OpponentMoneyAfterBattle,
                playerHpAfterBattle = result.PlayerHpAfterBattle,
                opponentHpAfterBattle = result.OpponentHpAfterBattle
            });
        }

        foreach (KeyValuePair<string, int> affinity in _devilAffinities)
        {
            data.devilAffinities.Add(new DevilAffinityData
            {
                devilId = affinity.Key,
                affinity = affinity.Value
            });
        }

        foreach (KeyValuePair<Rank, OwnedRankUpgrade> modifier in _rankUpgrades)
        {
            data.rankModifierIds.Add(new RankModifierData
            {
                rank = modifier.Key,
                modifierId = modifier.Value.UpgradeId,
                paidPrice = modifier.Value.PaidPrice
            });
        }

        return data;
    }

    public static RunState FromData(RunStateData data, int fallbackMaxPlayerHp = 100, int fallbackStartingGold = 100)
    {
        if (data == null)
            return null;

        int maxPlayerHp = data.maxPlayerHp > 0 ? data.maxPlayerHp : fallbackMaxPlayerHp;
        int startingGold = data.gold >= 0 ? data.gold : fallbackStartingGold;
        int savedItemSlotCount = data.activeItemIds?.Count ?? 0;
        int maxActiveItemSlots = data.maxActiveItemSlots > 0
            ? Math.Max(data.maxActiveItemSlots, savedItemSlotCount)
            : Math.Max(DefaultMaxActiveItemSlots, savedItemSlotCount);
        var state = new RunState(data.seed, maxPlayerHp, startingGold, maxActiveItemSlots);
        state.Phase = data.phase;
        state.EncounterIndex = Math.Max(0, data.encounterIndex);
        state.DifficultyLevel = Math.Max(1, data.difficultyLevel);
        state.DevilProgression = Math.Max(0, data.devilProgression);

        state._runDeck.Clear();
        if (data.deck != null && data.deck.Count > 0)
        {
            for (int i = 0; i < data.deck.Count; i++)
            {
                RunCardData card = data.deck[i];
                if (card == null)
                    continue;

                state._runDeck.Add(new RunCard(card.instanceId, card.suit, card.rank, card.modifierId));
            }
        }
        else
        {
            state._runDeck.AddRange(CreateStandardRunDeck());
        }

        AddNonBlankRange(state._relicIds, data.relicIds);
        AddNonBlankRange(state._globalModifierIds, data.globalModifierIds);
        if (data.activeItemIds != null)
        {
            for (int i = 0; i < data.activeItemIds.Count; i++)
                state._activeItemIds[i] = string.IsNullOrWhiteSpace(data.activeItemIds[i]) ? null : data.activeItemIds[i];
        }

        if (data.battleHistory != null)
        {
            for (int i = 0; i < data.battleHistory.Count; i++)
            {
                BattleResultData result = data.battleHistory[i];
                if (result == null)
                    continue;

                state._battleHistory.Add(new BattleResult(
                    result.playerWon,
                    Math.Max(0, result.roundCount),
                    Math.Max(0, result.playerMoneyAfterBattle),
                    Math.Max(0, result.opponentMoneyAfterBattle),
                    Math.Max(0, result.playerHpAfterBattle),
                    Math.Max(0, result.opponentHpAfterBattle)));
            }
        }

        if (data.devilAffinities != null)
        {
            for (int i = 0; i < data.devilAffinities.Count; i++)
            {
                DevilAffinityData affinity = data.devilAffinities[i];
                if (affinity == null || string.IsNullOrWhiteSpace(affinity.devilId))
                    continue;

                state._devilAffinities[affinity.devilId] = Math.Clamp(affinity.affinity, 0, 100);
            }
        }

        if (data.rankModifierIds != null)
        {
            for (int i = 0; i < data.rankModifierIds.Count; i++)
            {
                RankModifierData modifier = data.rankModifierIds[i];
                if (modifier == null || string.IsNullOrWhiteSpace(modifier.modifierId) || !Enum.IsDefined(typeof(Rank), modifier.rank))
                    continue;

                state._rankUpgrades[modifier.rank] = new OwnedRankUpgrade(modifier.rank, modifier.modifierId, modifier.paidPrice);
            }
        }

        state.RefreshBattleDeck();
        return state;
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
            _deck.Add(runCard.ToBattleCard());
        }
    }

    private static void AddNonBlankRange(List<string> target, List<string> source)
    {
        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(source[i]))
                target.Add(source[i]);
        }
    }

    private int FindEmptyActiveItemSlot()
    {
        for (int i = 0; i < _activeItemIds.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(_activeItemIds[i]))
                return i;
        }
        return -1;
    }

    private int FindActiveItemSlot(string itemId)
    {
        for (int i = 0; i < _activeItemIds.Count; i++)
        {
            if (string.Equals(_activeItemIds[i], itemId, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private void AddEmptyActiveItemSlots(int count)
    {
        for (int i = 0; i < count; i++)
            _activeItemIds.Add(null);
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
