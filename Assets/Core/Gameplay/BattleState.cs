using System;
using System.Collections.Generic;

public sealed class BattleState
{
    private readonly Deck _playerDeck;
    private readonly Deck _opponentDeck;
    private readonly Random _random;
    private readonly List<Modifier> _activeModifiers = new();
    private readonly List<RoundResolution> _combatHistory = new();
    private readonly List<Card> _playerHandCarryover = new();
    private readonly List<Card> _opponentHandCarryover = new();
    private readonly List<BattleRelicRuntime> _relicRuntimes;
    private readonly BattleEffectRuntime _effectRuntime;
    private bool _disposed;

    public BattleState(RunState runState, BattleConfig config)
    {
        RunState = runState ?? throw new ArgumentNullException(nameof(runState));
        Config = config ?? throw new ArgumentNullException(nameof(config));
        EventBus = new ScopedEventBus();
        global::EventBus.Subscribe<RankUpgradeChangedEvent>(OnRankUpgradeChanged);
        global::EventBus.Subscribe<RelicAddedEvent>(OnRelicAdded);
        global::EventBus.Subscribe<RelicRemovedEvent>(OnRelicRemoved);
        _relicRuntimes = BattleRelicRuntimeFactory.CreateAll(runState.RelicIds, this);
        _effectRuntime = new BattleEffectRuntime(this);
        EventBus.Subscribe<CardPlayedEvent>(OnCardPlayedForRefill);
        EventBus.Subscribe<CardDiscardedEvent>(OnCardDiscardedForRefill);
        CommandQueue = new CommandQueue();
        OpponentMoney = config.OpponentStartingMoney;
        BattleSeed = runState.CreateBattleSeed();
        IReadOnlyList<Card> playerDeck = config.HasPlayerDeckOverride ? config.PlayerDeckOverride : runState.CreateBattleDeck();
        IEnumerable<Card> opponentDeck = config.HasOpponentDeckOverride ? config.OpponentDeckOverride : config.DevilStrategy.CreateStartingDeck(runState, config);
        _playerDeck = new Deck(playerDeck, BattleSeed, !config.HasPlayerDeckOverride, config.HasPlayerDeckOverride);
        _opponentDeck = new Deck(opponentDeck, BattleSeed + 17, !config.HasOpponentDeckOverride, config.HasOpponentDeckOverride);
        _random = new Random(BattleSeed);
        _activeModifiers.AddRange(config.InitialModifiers);
        _activeModifiers.AddRange(config.DevilStrategy.GetGlobalModifiers(runState));
        config.DevilStrategy.RegisterAffinityHooks(this);
        PlayerDrawValue = config.StartingHandSize;
    }

    public RunState RunState { get; }
    public BattleConfig Config { get; }
    public ScopedEventBus EventBus { get; }
    public CommandQueue CommandQueue { get; }
    public BattlePhase Phase { get; private set; } = BattlePhase.Inactive;
    public int PlayerMoney => RunState.Money;
    public int OpponentMoney { get; private set; }
    public int RoundNumber { get; private set; }
    public int PlayerDrawValue { get; }
    public int ReshuffleCount { get; private set; }
    public int BattleSeed { get; }
    public bool IsBattleOver => PlayerMoney <= 0 || OpponentMoney <= 0 || Phase == BattlePhase.BattleEnd;
    public RoundState CurrentRound { get; private set; }
    public IReadOnlyList<Modifier> ActiveModifiers => _activeModifiers;
    public IReadOnlyList<RoundResolution> CombatHistory => _combatHistory;
    public IReadOnlyList<BattleRelicRuntime> RelicRuntimes => _relicRuntimes;
    public IReadOnlyList<Card> PlayerDrawPile => _playerDeck.DrawPile;
    public IReadOnlyList<Card> PlayerDiscardPile => _playerDeck.DiscardPile;
    public IReadOnlyList<Card> OpponentDrawPile => _opponentDeck.DrawPile;
    public IReadOnlyList<Card> OpponentDiscardPile => _opponentDeck.DiscardPile;

    public void Initialize()
    {
        SetPhase(BattlePhase.Init);
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.BattleStarted, Config.EncounterId));
        SetPhase(BattlePhase.PreRound);
    }

    public bool StartRound(int wager)
    {
        if (IsBattleOver || CurrentRound != null)
            return false;

        int proposedWager = Math.Max(0, wager);
        if (proposedWager <= 0 || PlayerMoney <= 0 || OpponentMoney <= 0)
            return false;

        bool playerActsFirst = RoundNumber % 2 == 0;
        int nextRoundNumber = RoundNumber + 1;
        int playerBurstThreshold = RelicRuleResolver.ResolvePlayerBurstThreshold(RunState, Config.BurstThreshold);
        int opponentBurstThreshold = RelicRuleResolver.ResolveOpponentBurstThreshold(RunState, Config.BurstThreshold);
        int playerStake = Math.Min(PlayerMoney, proposedWager);
        int opponentStake = Math.Min(OpponentMoney, proposedWager);
        var nextRound = new RoundState(nextRoundNumber, playerBurstThreshold, opponentBurstThreshold, proposedWager, playerActsFirst);
        if (!nextRound.TryCommitWager(playerStake, opponentStake))
            return false;

        SetPhase(BattlePhase.PreRound);
        RoundNumber = nextRoundNumber;
        CurrentRound = nextRound;
        Config.DevilStrategy.UpdateDevilState(new DevilStateUpdateContext(
            this,
            CurrentRound,
            DevilStateUpdatePoint.RoundStarted));
        RefreshDevilOpponentField();
        EventBus.Publish(new RoundStartedEvent(RoundNumber));
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.RoundStarted, $"{RoundNumber}:{proposedWager}"));

        RestoreCarryoverHands();
        RefillHandsForRoundStart(playerActsFirst);

        RunState.AddMoney(-playerStake);
        AddOpponentMoney(-opponentStake);

        EventBus.Publish(new WagerCommittedEvent(proposedWager, CurrentRound.PlayerActsFirst, WagerResponse.Accept));
        global::EventBus.Publish(new MoneyTransferReasonEvent(Combatant.System, MoneyTransferReason.InitialWager, playerStake));

        BeginPlayerTurn();

        return true;
    }

    public bool TryPlayCard(int handIndex)
    {
        if (Phase != BattlePhase.PlayerPhase || CurrentRound == null)
            return false;

        if (!CurrentRound.TryPlayCard(handIndex, ApplyPlayerCardForPlay, out Card card))
            return false;

        PublishPlayerCardPlayed(card);
        RefillPlayerHandIfEmpty();
        ResolveScores();
        return true;
    }

    public bool TryHit()
    {
        if (!TryHitCard())
            return false;

        return CompletePlayerTurn();
    }

    public bool TryHitWithoutEndingTurn()
    {
        if (!TryHitCard())
            return false;

        ResolveScores();
        return true;
    }

    private bool TryHitCard()
    {
        if (Phase != BattlePhase.PlayerPhase
            || CurrentRound == null
            || !CurrentRound.CanPlacePlayedCard(Combatant.Player))
            return false;

        if (!TryDrawCard(Combatant.Player, out Card card))
            return false;
        EventBus.Publish(new CardDrawnEvent(Combatant.Player, card, _playerDeck.RemainingCards));

        card = ApplyPlayerCardForPlay(card);
        if (!CurrentRound.TryPlayHitCard(card))
        {
            DiscardOverflowCard(Combatant.Player, card);
            return false;
        }

        EventBus.Publish(new PlayerHitUsedEvent(RoundNumber, card));
        PublishPlayerCardPlayed(card);
        return true;
    }

    public bool TryStand()
    {
        if (Phase != BattlePhase.PlayerPhase || CurrentRound == null)
            return false;

        CurrentRound.MarkPlayerStood();
        return CompletePlayerTurn();
    }

    public RoundResolution EndPlayerPhase()
    {
        if (Phase != BattlePhase.PlayerPhase || CurrentRound == null)
            return default;

        CompletePlayerTurn();
        return _combatHistory.Count > 0 ? _combatHistory[^1] : default;
    }

    public void CleanupRound()
    {
        if (CurrentRound == null)
            return;

        CurrentRound.MoveHandsTo(_playerHandCarryover, _opponentHandCarryover);
        var playerCards = new List<Card>(CurrentRound.TakePlayerCardsForCleanup());
        var opponentCards = new List<Card>();
        CurrentRound.TakeOpponentCardsForCleanup(playerCards, opponentCards);
        _playerDeck.DiscardRange(playerCards);
        _opponentDeck.DiscardRange(opponentCards);
        CurrentRound.ClearRoundOnlyState();
        CurrentRound = null;
        if (IsBattleOver)
            EndBattle();
        else
            SetPhase(BattlePhase.PreRound);
    }

    public BattleResult EndBattle()
    {
        if (Phase == BattlePhase.BattleEnd)
            return GetBattleResult();

        SetPhase(BattlePhase.BattleEnd);
        BattleResult result = GetBattleResult();
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.BattleEnded, result.PlayerWon.ToString()));
        Dispose();
        return result;
    }

    internal BattleResult GetBattleResult()
    {
        return new BattleResult(OpponentMoney <= 0 && PlayerMoney > 0, RoundNumber, PlayerMoney, OpponentMoney);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Config.DevilStrategy.UnregisterAffinityHooks(this);
        _effectRuntime.Dispose();
        for (int i = 0; i < _relicRuntimes.Count; i++)
            _relicRuntimes[i].Dispose();
        global::EventBus.Unsubscribe<RelicAddedEvent>(OnRelicAdded);
        global::EventBus.Unsubscribe<RelicRemovedEvent>(OnRelicRemoved);
        global::EventBus.Unsubscribe<RankUpgradeChangedEvent>(OnRankUpgradeChanged);
        EventBus.Clear();
        _disposed = true;
    }

    public void AddModifier(Modifier modifier)
    {
        _activeModifiers.Add(modifier);
        EventBus.Publish(new ModifierAddedEvent(modifier));
    }

    public bool TryPlayPlayerHandCardForEffect(int handIndex)
    {
        if (CurrentRound == null)
            return false;

        PlayedPilePlacementResult result = CurrentRound.TryPlayPlayerCardForEffect(
            handIndex,
            ApplyPlayerCardForPlay,
            out Card card);
        if (result == PlayedPilePlacementResult.Unavailable)
            return false;

        if (result == PlayedPilePlacementResult.Overflowed)
        {
            DiscardOverflowCard(Combatant.Player, card);
            return false;
        }

        PublishPlayerCardPlayed(card);
        ResolveScores();
        return true;
    }

    public bool TryPlayRandomLowerRankPlayerHandCard(Rank rank)
    {
        if (CurrentRound == null)
            return false;

        var matchingIndices = new List<int>();
        for (int i = 0; i < CurrentRound.PlayerHand.Count; i++)
        {
            if ((int)CurrentRound.PlayerHand[i].Rank < (int)rank)
                matchingIndices.Add(i);
        }

        if (matchingIndices.Count == 0)
            return false;

        int selected = matchingIndices[_random.Next(matchingIndices.Count)];
        return TryPlayPlayerHandCardForEffect(selected);
    }

    public bool TryPlayRandomPlayerDeckCardAtOrAboveRank(Rank rank)
    {
        return TryPlayRandomPlayerDeckCardWhere(card => IsNumberRank(card.Rank) && card.Rank >= rank);
    }

    public bool TryPlayRandomPlayerDeckCardAtOrBelowRank(Rank rank)
    {
        return TryPlayRandomPlayerDeckCardWhere(card => IsNumberRank(card.Rank) && card.Rank <= rank);
    }

    public bool TryPlayRandomPlayerDeckCardOfSuit(Suit suit)
    {
        return TryPlayRandomPlayerDeckCardWhere(card => card.Suit == suit);
    }

    public bool TryPlayTopPlayerDeckCardForEffect()
    {
        if (CurrentRound == null)
            return false;

        if (!TryDrawCard(Combatant.Player, out Card card))
            return false;

        EventBus.Publish(new CardDrawnEvent(Combatant.Player, card, _playerDeck.RemainingCards));
        card = ApplyPlayerCardForPlay(card);
        return AddBattleOnlyCardToPlayerField(card);
    }

    private bool TryPlayRandomPlayerDeckCardWhere(Predicate<Card> predicate)
    {
        if (CurrentRound == null || predicate == null)
            return false;

        if (!TryDrawPlayerCardWhere(predicate, out Card card))
            return false;

        EventBus.Publish(new CardDrawnEvent(Combatant.Player, card, _playerDeck.RemainingCards));
        card = ApplyPlayerCardForPlay(card);
        return AddBattleOnlyCardToPlayerField(card);
    }

    public bool TryDrawRandomPlayerCardOfRankToHand(Rank rank)
    {
        return TryDrawRandomPlayerCardToHand(card => card.Rank == rank);
    }

    public bool TryDrawRandomPlayerCardOfSuitToHand(Suit suit)
    {
        return TryDrawRandomPlayerCardToHand(card => card.Suit == suit);
    }

    private bool TryDrawRandomPlayerCardToHand(Predicate<Card> predicate)
    {
        if (CurrentRound == null || predicate == null)
            return false;

        if (!TryDrawPlayerCardWhere(predicate, out Card card))
            return false;

        CurrentRound.AddToHand(card);
        EventBus.Publish(new CardDrawnEvent(Combatant.Player, card, _playerDeck.RemainingCards));
        EventBus.Publish(new HandRefilledEvent(CurrentRound.PlayerHand.Count));
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.CardsDrawn, CurrentRound.PlayerHand.Count.ToString()));

        return true;
    }

    public bool TryGetPreviousPlayerPlayedCard(out Card card)
    {
        if (CurrentRound == null)
        {
            card = default;
            return false;
        }

        return CurrentRound.TryGetPreviousPlayerPlayedCard(out card);
    }

    public bool TryMovePreviousPlayerPlayedCardToOpponent(out Card card)
    {
        if (CurrentRound == null)
        {
            card = default;
            return false;
        }

        PlayedPilePlacementResult result = CurrentRound.TryMovePreviousPlayerPlayedCardToOpponentForEffect(
            out card,
            out Combatant discardOwner);
        if (result == PlayedPilePlacementResult.Unavailable)
            return false;

        if (result == PlayedPilePlacementResult.Overflowed)
        {
            DiscardOverflowCard(discardOwner, card);
            ResolveScores();
            return false;
        }

        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.CardsPlayed, card.ToString()));
        ResolveScores();
        return true;
    }

    public bool TryGetPreviousOpponentVisibleCard(out Card card)
    {
        if (CurrentRound == null)
        {
            card = default;
            return false;
        }

        return CurrentRound.TryGetPreviousOpponentVisibleCard(out card);
    }

    public bool TryMovePreviousOpponentVisibleCardToPlayer(out Card card)
    {
        if (CurrentRound == null)
        {
            card = default;
            return false;
        }

        PlayedPilePlacementResult result = CurrentRound.TryMovePreviousOpponentVisibleCardToPlayerForEffect(
            out card,
            out Combatant discardOwner);
        if (result == PlayedPilePlacementResult.Unavailable)
            return false;

        if (result == PlayedPilePlacementResult.Overflowed)
        {
            DiscardOverflowCard(discardOwner, card);
            ResolveScores();
            return false;
        }

        EventBus.Publish(new CardPlayedEvent(Combatant.Player, card));
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.CardsPlayed, card.ToString()));
        ResolveScores();
        return true;
    }

    public bool TryMovePreviousOpponentVisibleCardToOpponentDeckBottom(out Card card)
    {
        if (CurrentRound == null)
        {
            card = default;
            return false;
        }

        if (!CurrentRound.TryRemovePreviousOpponentVisibleCard(out card, out Combatant owner))
            return false;

        Deck deck = owner == Combatant.Player ? _playerDeck : _opponentDeck;
        deck.PlaceAtBottom(card);
        EventBus.Publish(new CardDiscardedEvent(owner, card));
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.CardsPlayed, card.ToString()));
        ResolveScores();
        return true;
    }

    public bool TryPlayRandomPlayerHandCardOfSuit(Suit suit)
    {
        if (CurrentRound == null)
            return false;

        var matchingIndices = new List<int>();
        for (int i = 0; i < CurrentRound.PlayerHand.Count; i++)
        {
            if (CurrentRound.PlayerHand[i].Suit == suit)
                matchingIndices.Add(i);
        }

        if (matchingIndices.Count == 0)
            return false;

        int selected = matchingIndices[_random.Next(matchingIndices.Count)];
        return TryPlayPlayerHandCardForEffect(selected);
    }

    public bool AddBattleOnlyCardToPlayerHand(Card card)
    {
        if (CurrentRound == null)
            return false;

        CurrentRound.AddBattleOnlyPlayerHandCard(card);
        EventBus.Publish(new HandRefilledEvent(CurrentRound.PlayerHand.Count));
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.CardsDrawn, CurrentRound.PlayerHand.Count.ToString()));
        return true;
    }

    public bool AddBattleOnlyCardToPlayerField(Card card)
    {
        if (CurrentRound == null)
            return false;

        PlayedPilePlacementResult result = CurrentRound.TryPlaceEffectCard(
            Combatant.Player,
            card,
            Combatant.Player);
        if (result == PlayedPilePlacementResult.Overflowed)
        {
            DiscardOverflowCard(Combatant.Player, card);
            return false;
        }

        PublishPlayerCardPlayed(card);
        ResolveScores();
        return true;
    }

    public bool TryReplaceLastPlayerPlayedCard(Card card)
    {
        return CurrentRound != null && CurrentRound.TryReplaceLastPlayerPlayedCard(card);
    }

    public bool TryReplaceLastPlayerPlayedCardForEffect(Card card)
    {
        if (!TryReplaceLastPlayerPlayedCard(card))
            return false;

        ResolveScores();
        return true;
    }

    public bool TryDiscardLowestPlayerNumberCard()
    {
        if (CurrentRound == null || !CurrentRound.TryRemoveLowestPlayerNumberCard(out Card card))
            return false;

        _playerDeck.Discard(card);
        EventBus.Publish(new CardDiscardedEvent(Combatant.Player, card));
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.CardsPlayed, card.ToString()));
        ResolveScores();
        return true;
    }

    public bool TrySplitPreviousPlayerNumberCard()
    {
        if (CurrentRound == null || !CurrentRound.TryRemovePreviousPlayerPlayedNumberCard(out Card card))
            return false;

        int value = (int)card.Rank;
        Card lower = new(card.Suit, (Rank)(value / 2));
        Card upper = new(card.Suit, (Rank)(value - (value / 2)));
        _playerDeck.Discard(card);
        EventBus.Publish(new CardDiscardedEvent(Combatant.Player, card));
        AddBattleOnlyCardToPlayerField(lower);
        AddBattleOnlyCardToPlayerField(upper);
        return true;
    }

    private Card ApplyPlayerCardUpgrade(Card card)
    {
        return RunState.TryGetRankUpgrade(card.Rank, out OwnedRankUpgrade upgrade)
            ? CardModifierResolver.Apply(card, upgrade.UpgradeId)
            : card;
    }

    private Card ApplyPlayerCardForPlay(Card card)
    {
        Card transformed = ApplyPlayerCardUpgrade(card);
        for (int i = 0; i < _relicRuntimes.Count; i++)
            transformed = _relicRuntimes[i].TransformPlayerPlayedCard(transformed);

        return transformed;
    }

    public Card PreviewPlayerCardForPlay(Card card)
    {
        Card transformed = ApplyPlayerCardUpgrade(card);
        for (int i = 0; i < _relicRuntimes.Count; i++)
            transformed = _relicRuntimes[i].PreviewPlayerPlayedCard(transformed);

        return transformed;
    }

    public void ApplyRankUpgradeToPlayerBattleCards(Rank rank, string upgradeId)
    {
        if (!Enum.IsDefined(typeof(Rank), rank) || string.IsNullOrWhiteSpace(upgradeId))
            return;

        Card Transform(Card card)
        {
            return card.Rank == rank ? CardModifierResolver.Apply(card, upgradeId) : card;
        }

        _playerDeck.TransformCards(Transform);
        CurrentRound?.TransformPlayerCards(Transform);
        TransformCards(_playerHandCarryover, Transform);
    }

    public int AddPlayerMoney(int amount)
    {
        int finalAmount = Math.Max(0, amount);
        if (finalAmount <= 0)
            return 0;

        RunState.AddMoney(finalAmount);
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.MoneyChanged, $"{Combatant.Player}:{PlayerMoney}"));
        return finalAmount;
    }

    public int LosePlayerMoney(int amount)
    {
        int finalAmount = Math.Min(PlayerMoney, Math.Max(0, amount));
        if (finalAmount <= 0)
            return 0;

        RunState.AddMoney(-finalAmount);
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.MoneyChanged, $"{Combatant.Player}:{PlayerMoney}"));

        if (PlayerMoney <= 0)
            EventBus.Publish(new DeathPreventedEvent(Combatant.Player, false));

        return finalAmount;
    }

    public int LoseOpponentMoney(int amount)
    {
        return AddOpponentMoney(-Math.Max(0, amount)) * -1;
    }

    public int NextRandomInclusive(int minimum, int maximum)
    {
        if (maximum <= minimum)
            return minimum;

        return _random.Next(minimum, maximum + 1);
    }

    public int AddOpponentMoney(int delta)
    {
        int previous = OpponentMoney;
        OpponentMoney = Math.Max(0, OpponentMoney + delta);
        int actualDelta = OpponentMoney - previous;
        if (actualDelta != 0)
            RefreshDevilOpponentField();
        global::EventBus.Publish(new MoneyChangedEvent(Combatant.Opponent, OpponentMoney, actualDelta));
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.MoneyChanged, $"{Combatant.Opponent}:{OpponentMoney}"));
        return actualDelta;
    }

    public bool TryUseActiveItem(string itemId)
    {
        if (!CanUseActiveItem(itemId))
            return false;

        if (!ActiveItemResolver.TryApply(itemId, this))
            return false;

        if (!RunState.RemoveActiveItem(itemId))
            return false;

        global::EventBus.Publish(new ItemUsedEvent(itemId));
        return true;
    }

    public bool CanUseActiveItem(string itemId)
    {
        return !IsBattleOver
            && CurrentRound != null
            && RunState.HasActiveItem(itemId)
            && ActiveItemResolver.CanApply(itemId, this);
    }

    public bool CanDiscardLastPlayerHitCard()
    {
        return CurrentRound != null && CurrentRound.CanRemoveLastPlayerHitCard;
    }

    public bool DiscardLastPlayerHitCard()
    {
        if (CurrentRound == null || !CurrentRound.TryRemoveLastPlayerHitCard(out Card card))
            return false;

        _playerDeck.Discard(card);
        EventBus.Publish(new CardDiscardedEvent(Combatant.Player, card));
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.CardsPlayed, card.ToString()));
        ResolveScoresOnly();
        return true;
    }

    public bool CanDrawPlayerCards()
    {
        return CurrentRound != null && (_playerDeck.RemainingCards > 0 || _playerDeck.DiscardedCards > 0);
    }

    public bool CanDrawPlayerCards(Suit suit)
    {
        return CurrentRound != null && CanDrawPlayerCardWhere(card => card.Suit == suit);
    }

    public int DrawCardsToPlayerHand(int count)
    {
        if (CurrentRound == null || count <= 0)
            return 0;

        int drawn = 0;
        for (int i = 0; i < count; i++)
        {
            if (!TryDrawCard(Combatant.Player, out Card card))
                break;

            CurrentRound.AddToHand(card);
            drawn++;
            EventBus.Publish(new CardDrawnEvent(Combatant.Player, card, _playerDeck.RemainingCards));
        }

        if (drawn > 0)
        {
            EventBus.Publish(new HandRefilledEvent(CurrentRound.PlayerHand.Count));
            CommandQueue.Enqueue(new VisualCommand(VisualCommandType.CardsDrawn, CurrentRound.PlayerHand.Count.ToString()));
        }

        return drawn;
    }

    public int DrawCardsToPlayerHand(Suit suit, int count)
    {
        if (CurrentRound == null || count <= 0)
            return 0;

        int drawn = 0;
        for (int i = 0; i < count; i++)
        {
            if (!TryDrawPlayerCardWhere(card => card.Suit == suit, out Card card))
                break;

            CurrentRound.AddToHand(card);
            drawn++;
            EventBus.Publish(new CardDrawnEvent(Combatant.Player, card, _playerDeck.RemainingCards));
        }

        if (drawn > 0)
        {
            EventBus.Publish(new HandRefilledEvent(CurrentRound.PlayerHand.Count));
            CommandQueue.Enqueue(new VisualCommand(VisualCommandType.CardsDrawn, CurrentRound.PlayerHand.Count.ToString()));
        }

        return drawn;
    }

    public bool CanDoubleCurrentRoundWager()
    {
        return CanSetCurrentRoundWagerMultiplier(2);
    }

    public bool DoubleCurrentRoundWager()
    {
        return SetCurrentRoundWagerMultiplier(2);
    }

    public bool CanTripleCurrentRoundWager()
    {
        return CanSetCurrentRoundWagerMultiplier(3);
    }

    public bool TripleCurrentRoundWager()
    {
        return SetCurrentRoundWagerMultiplier(3);
    }

    public bool CanIncreasePlayerBurstThreshold(int amount)
    {
        return CurrentRound != null && amount > 0;
    }

    public bool IncreasePlayerBurstThreshold(int amount)
    {
        if (!CanIncreasePlayerBurstThreshold(amount))
            return false;

        CurrentRound.SetPlayerBurstThreshold(CurrentRound.PlayerBurstThreshold + amount);
        EventBus.Publish(new BurstThresholdChangedEvent(Combatant.Player, CurrentRound.PlayerBurstThreshold));
        return true;
    }

    public bool CanReturnPlayerFieldCardToHand()
    {
        return CurrentRound != null && CurrentRound.PlayerPlayedCards.Count > 0;
    }

    public bool ReturnPlayerFieldCardToHand()
    {
        if (CurrentRound == null || !CurrentRound.TryReturnLastPlayerFieldCardToHand(out Card card))
            return false;

        EventBus.Publish(new HandRefilledEvent(CurrentRound.PlayerHand.Count));
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.CardsDrawn, CurrentRound.PlayerHand.Count.ToString()));
        ResolveScoresOnly();
        return true;
    }

    private bool CanSetCurrentRoundWagerMultiplier(int targetMultiplier)
    {
        if (CurrentRound == null
            || !CurrentRound.WagerCommitted
            || CurrentRound.BaseWager <= 0
            || targetMultiplier <= CurrentRound.WagerMultiplier)
            return false;

        int extraStake = CurrentRound.BaseWager * (targetMultiplier - CurrentRound.WagerMultiplier);
        return PlayerMoney >= extraStake && OpponentMoney >= extraStake;
    }

    private bool SetCurrentRoundWagerMultiplier(int targetMultiplier)
    {
        if (!CanSetCurrentRoundWagerMultiplier(targetMultiplier))
            return false;

        int playerExtraStake = CurrentRound.BaseWager * (targetMultiplier - CurrentRound.WagerMultiplier);
        int opponentExtraStake = CurrentRound.BaseWager * (targetMultiplier - CurrentRound.WagerMultiplier);
        RunState.AddMoney(-playerExtraStake);
        AddOpponentMoney(-opponentExtraStake);

        if (!CurrentRound.TrySetWagerMultiplier(targetMultiplier, playerExtraStake, opponentExtraStake))
        {
            RunState.AddMoney(playerExtraStake);
            AddOpponentMoney(opponentExtraStake);
            return false;
        }

        return true;
    }

    private bool TryDrawPlayerCardWhere(Predicate<Card> predicate, out Card card)
    {
        if (predicate == null)
        {
            card = default;
            return false;
        }

        if (_playerDeck.RemainingCards == 0 && _playerDeck.DiscardedCards > 0)
        {
            _playerDeck.ReshuffleDiscardIntoDraw();
            ReshuffleCount++;
            EventBus.Publish(new DeckShuffledEvent(Combatant.Player, ReshuffleCount, _playerDeck.RemainingCards));
        }

        return _playerDeck.TryDrawWhere(predicate, _random, out card);
    }

    private bool CanDrawPlayerCardWhere(Predicate<Card> predicate)
    {
        if (predicate == null)
            return false;

        for (int i = 0; i < _playerDeck.DrawPile.Count; i++)
        {
            if (predicate(_playerDeck.DrawPile[i]))
                return true;
        }

        if (_playerDeck.RemainingCards > 0)
            return false;

        for (int i = 0; i < _playerDeck.DiscardPile.Count; i++)
        {
            if (predicate(_playerDeck.DiscardPile[i]))
                return true;
        }

        return false;
    }

    public bool ClearField(Combatant owner)
    {
        if (CurrentRound == null)
            return false;

        if (owner == Combatant.Opponent)
            return ClearOpponentField();

        IReadOnlyList<Card> cards = CurrentRound.TakeFieldCards(owner);
        if (cards.Count == 0)
            return false;

        Deck deck = owner == Combatant.Player ? _playerDeck : _opponentDeck;
        deck.DiscardRange(cards);

        for (int i = 0; i < cards.Count; i++)
            EventBus.Publish(new CardDiscardedEvent(owner, cards[i]));

        return true;
    }

    private bool ClearOpponentField()
    {
        var playerOwnedCards = new List<Card>();
        var opponentOwnedCards = new List<Card>();
        CurrentRound.TakeOpponentCardsForCleanup(playerOwnedCards, opponentOwnedCards);
        if (playerOwnedCards.Count == 0 && opponentOwnedCards.Count == 0)
            return false;

        _playerDeck.DiscardRange(playerOwnedCards);
        _opponentDeck.DiscardRange(opponentOwnedCards);

        for (int i = 0; i < playerOwnedCards.Count; i++)
            EventBus.Publish(new CardDiscardedEvent(Combatant.Player, playerOwnedCards[i]));

        for (int i = 0; i < opponentOwnedCards.Count; i++)
            EventBus.Publish(new CardDiscardedEvent(Combatant.Opponent, opponentOwnedCards[i]));

        return true;
    }

    public bool ClearAllFields()
    {
        bool playerCleared = ClearField(Combatant.Player);
        bool opponentCleared = ClearField(Combatant.Opponent);
        return playerCleared || opponentCleared;
    }

    public int GetDefaultWager()
    {
        if (PlayerMoney <= 0 || OpponentMoney <= 0)
            return 0;

        int wager = Config.DevilStrategy is IDevilRoundWagerModifier wagerModifier
            ? wagerModifier.GetRoundWager(this, Config.BaseWager)
            : Config.BaseWager;
        return Math.Max(1, wager);
    }

    internal int GetModifiedPlayerPokerPayout(RoundState round, int proposedPayout)
    {
        int payout = Math.Max(0, proposedPayout);
        if (Config.DevilStrategy is not IDevilPokerPayoutModifier payoutModifier)
            return payout;

        return Math.Clamp(
            payoutModifier.ModifyPlayerPokerPayout(this, round, payout),
            0,
            payout);
    }

    private void RestoreCarryoverHands()
    {
        for (int i = 0; i < _playerHandCarryover.Count; i++)
            CurrentRound.AddToHand(_playerHandCarryover[i]);

        for (int i = 0; i < _opponentHandCarryover.Count; i++)
            CurrentRound.AddToOpponentHand(_opponentHandCarryover[i]);

        _playerHandCarryover.Clear();
        _opponentHandCarryover.Clear();
    }

    private void RefillPlayerHandIfEmpty()
    {
        if (CurrentRound == null || CurrentRound.PlayerHand.Count > 0)
            return;

        DrawPlayerHand(PlayerDrawValue);
    }

    private void RefillOpponentHandIfEmpty()
    {
        if (CurrentRound == null || CurrentRound.OpponentHand.Count > 0)
            return;

        DrawOpponentHand(Config.DevilStrategy.DrawValue);
    }

    private void RefillHandsForRoundStart(bool playerActsFirst)
    {
        if (playerActsFirst)
        {
            RefillPlayerHandIfEmpty();
            RefillOpponentHandIfEmpty();
        }
        else
        {
            RefillOpponentHandIfEmpty();
            RefillPlayerHandIfEmpty();
        }
    }

    private void DrawPlayerHand(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (!TryDrawCard(Combatant.Player, out Card card))
                break;

            CurrentRound.AddToHand(card);
            EventBus.Publish(new CardDrawnEvent(Combatant.Player, card, _playerDeck.RemainingCards));
        }

        EventBus.Publish(new HandRefilledEvent(CurrentRound.PlayerHand.Count));
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.CardsDrawn, CurrentRound.PlayerHand.Count.ToString()));
    }

    private void DrawOpponentHand(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (!TryDrawCard(Combatant.Opponent, out Card card))
                break;

            CurrentRound.AddToOpponentHand(card);
            EventBus.Publish(new CardDrawnEvent(Combatant.Opponent, card, _opponentDeck.RemainingCards));
        }
    }

    private bool PlayOpponentTurn()
    {
        if (CurrentRound == null || Phase == BattlePhase.BattleEnd)
            return false;

        CurrentRound.BeginOpponentTurn();
        bool canPlaceCard = CurrentRound.CanPlacePlayedCard(Combatant.Opponent);
        if (canPlaceCard)
            RefillOpponentHandIfEmpty();

        Config.DevilStrategy.UpdateDevilState(new DevilStateUpdateContext(
            this,
            CurrentRound,
            DevilStateUpdatePoint.OpponentTurnStarted));
        RefreshDevilOpponentField();
        DevilTurnChoice choice = canPlaceCard
            ? Config.DevilStrategy.ChooseTurnAction(this, CurrentRound)
            : DevilTurnChoice.Stand;
        EventBus.Publish(new DevilTurnChoiceEvent(choice));
        if (choice == DevilTurnChoice.Stand)
        {
            CurrentRound.MarkOpponentStood();
        }
        else if (choice == DevilTurnChoice.Hit)
        {
            PlayOpponentHit();
        }
        else
        {
            PlayOpponentHandCards();
        }

        return CompleteOpponentTurn();
    }

    private bool PlayOpponentHit()
    {
        if (CurrentRound == null || !CurrentRound.CanPlacePlayedCard(Combatant.Opponent))
            return false;

        if (!TryDrawCard(Combatant.Opponent, out Card card))
            return false;

        Card drawnCard = card;
        if (!CurrentRound.TryPlayOpponentHitCard(card))
        {
            DiscardOverflowCard(Combatant.Opponent, card);
            return false;
        }

        RefreshDevilOpponentField();
        card = CurrentRound.OpponentVisibleCards[^1];
        EventBus.Publish(new CardDrawnEvent(Combatant.Opponent, drawnCard, _opponentDeck.RemainingCards));
        EventBus.Publish(new CardPlayedEvent(Combatant.Opponent, card));
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.CardsPlayed, card.ToString()));
        return true;
    }

    private void PlayOpponentHandCards()
    {
        if (CurrentRound == null)
            return;

        RefillOpponentHandIfEmpty();
        if (CurrentRound.OpponentHand.Count == 0)
            return;

        int handIndex = Config.DevilStrategy.ChooseCardIndex(this, CurrentRound);
        if (!CurrentRound.TryPlayOpponentCard(handIndex, out Card card))
        {
            if (handIndex == 0 || !CurrentRound.TryPlayOpponentCard(0, out card))
                return;
        }

        RefreshDevilOpponentField();
        card = CurrentRound.OpponentVisibleCards[^1];
        EventBus.Publish(new CardPlayedEvent(Combatant.Opponent, card));
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.CardsPlayed, card.ToString()));
    }

    private bool CompletePlayerTurn()
    {
        EventBus.Publish(new PlayerTurnEndedEvent());

        if (ResolveTurnOrRoundEnd())
            return true;

        SetPhase(BattlePhase.OpponentPhase);
        return PlayOpponentTurn();
    }

    private bool CompleteOpponentTurn()
    {
        if (ResolveTurnOrRoundEnd())
            return true;

        BeginPlayerTurn();
        return true;
    }

    private void BeginPlayerTurn()
    {
        CurrentRound.BeginPlayerTurn();
        SetPhase(BattlePhase.PlayerPhase);
        EventBus.Publish(new PlayerTurnStartedEvent());
    }

    private bool ResolveTurnOrRoundEnd()
    {
        ResolveScores();

        if (CurrentRound.PlayerStood && CurrentRound.OpponentStood)
        {
            ResolveRound(true);
            return true;
        }

        return false;
    }

    // For altering battle state without triggering any money transfers or round resolution. This is useful for effects that need to know the current score but don't want to end the round.
    // Specifically used for active items
    private void ResolveScoresOnly()
    {
        ScoreResult playerScore = ScoreResolver.Resolve(CurrentRound.PlayerPlayedCards, CurrentRound.ScoringModifiers, CurrentRound.PlayerBurstThreshold);
        ScoreResult opponentScore = ScoreResolver.Resolve(CurrentRound.OpponentVisibleCards, CurrentRound.ScoringModifiers,CurrentRound.OpponentBurstThreshold, GetOpponentBlackjackBonus());
        PokerResult playerPoker = ScoreResolver.ResolvePoker(CurrentRound.PlayerPlayedCards);
        PokerResult opponentPoker = ScoreResolver.ResolvePoker(CurrentRound.OpponentVisibleCards);
        CurrentRound.SetScores(playerScore, opponentScore);

        PublishScoreEvents(Combatant.Player, playerScore, playerPoker);
        PublishScoreEvents(Combatant.Opponent, opponentScore, opponentPoker);
    }

    private void ResolveScores()
    {
        ResolveScoresOnly();

        ResolveRealtimeMoney();
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.ScoresResolved, $"{CurrentRound.PlayerScore.FinalScore}:{CurrentRound.OpponentScore.FinalScore}"));
        //TODO: we require game over check every resolve
    }

    private void ResolveRealtimeMoney()
    {
        if (CurrentRound == null || CurrentRound.EffectiveWager <= 0)
            return;

        bool playerPlayed = CurrentRound.ConsumePlayedThisTurn(Combatant.Player);
        bool opponentPlayed = CurrentRound.ConsumePlayedThisTurn(Combatant.Opponent);
        if (!playerPlayed && !opponentPlayed)
            return;

        if (playerPlayed)
        {
            MoneyResolver.ResolvePlayerPokerPayout(
            this,
            CurrentRound,
            CurrentRound.EffectiveWager);

            MoneyResolver.ResolveBurstTransfers(
            this,
            CurrentRound,
            CurrentRound.EffectiveWager);
        }
        
    }

    private int GetOpponentBlackjackBonus()
    {
        int bonus = 0;
        for (int i = 0; i < _relicRuntimes.Count; i++)
            bonus += _relicRuntimes[i].OpponentBlackjackBonus;

        return bonus;
    }

    private void ResolveRound(bool applyBurstPenalty)
    {
        RoundResolution resolution = MoneyResolver.ResolveRound(this, CurrentRound, applyBurstPenalty);
        _combatHistory.Add(resolution);

        Config.DevilStrategy.UpdateDevilState(new DevilStateUpdateContext(
            this,
            CurrentRound,
            DevilStateUpdatePoint.RoundResolved,
            resolution));
        RefreshDevilOpponentField();
        SetPhase(BattlePhase.PostRound);
        EventBus.Publish(new RoundEndedEvent(RoundNumber));

        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.RoundEnded, RoundNumber.ToString()));

        if (IsBattleOver)
            EndBattle();
        else
            SetPhase(BattlePhase.Cleanup);
    }

    private bool TryDrawCard(Combatant owner, out Card card)
    {
        Deck deck = owner == Combatant.Player ? _playerDeck : _opponentDeck;
        if (deck.RemainingCards == 0 && deck.DiscardedCards > 0)
        {
            deck.ReshuffleDiscardIntoDraw();
            ReshuffleCount++;
            EventBus.Publish(new DeckShuffledEvent(owner, ReshuffleCount, deck.RemainingCards));
        }

        return deck.TryDraw(out card);
    }

    private void PublishPlayerCardPlayed(Card card)
    {
        EventBus.Publish(new CardPlayedEvent(Combatant.Player, card));
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.CardsPlayed, card.ToString()));
    }

    private void DiscardOverflowCard(Combatant owner, Card card)
    {
        Deck deck = owner == Combatant.Player ? _playerDeck : _opponentDeck;
        deck.Discard(card);
        EventBus.Publish(new CardDiscardedEvent(owner, card));
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.CardsPlayed, card.ToString()));
    }

    internal int GetOpponentWinBonus(RoundState round)
    {
        return Config.DevilStrategy is IDevilRoundPayoutModifier payoutModifier
            ? payoutModifier.GetOpponentWinBonus(this, round)
            : 0;
    }

    private void RefreshDevilOpponentField()
    {
        if (CurrentRound != null
            && Config.DevilStrategy is IDevilOpponentFieldModifier fieldModifier)
        {
            fieldModifier.RefreshOpponentField(this, CurrentRound);
        }
    }

    private void PublishScoreEvents(Combatant combatant, ScoreResult score, PokerResult poker)
    {
        EventBus.Publish(new ScoreCalculatedEvent(combatant, score));
        EventBus.Publish(new PokerResolvedEvent(combatant, poker));

        int burstThreshold = CurrentRound.GetBurstThreshold(combatant);
        if (score.FinalScore > burstThreshold)
        {
            EventBus.Publish(new BurstAttemptedEvent(combatant, score.FinalScore, burstThreshold, true));
            EventBus.Publish(new BurstOccurredEvent(combatant, score.FinalScore));
        }

        if (score.IsBlackjack)
            EventBus.Publish(new BlackjackAchievedEvent(combatant, score.FinalScore));
    }

    private void SetPhase(BattlePhase phase)
    {
        Phase = phase;
        EventBus.Publish(new BattlePhaseChangedEvent(phase));
    }

    private void OnCardPlayedForRefill(CardPlayedEvent eventData)
    {
        RefillHandIfEmpty(eventData.Owner);
    }

    private void OnCardDiscardedForRefill(CardDiscardedEvent eventData)
    {
        RefillHandIfEmpty(eventData.Owner);
    }

    private void OnRankUpgradeChanged(RankUpgradeChangedEvent eventData)
    {
        if (eventData.RunState != null && !ReferenceEquals(eventData.RunState, RunState))
            return;

        ApplyRankUpgradeToPlayerBattleCards(eventData.Rank, eventData.UpgradeId);
    }

    private void OnRelicAdded(RelicAddedEvent eventData)
    {
        AddRelicRuntimeIfMissing(eventData.RelicId);
    }

    private void OnRelicRemoved(RelicRemovedEvent eventData)
    {
        for (int i = _relicRuntimes.Count - 1; i >= 0; i--)
        {
            if (_relicRuntimes[i].RelicId != eventData.RelicId)
                continue;

            _relicRuntimes[i].Dispose();
            _relicRuntimes.RemoveAt(i);
        }
    }

    private bool AddRelicRuntimeIfMissing(string relicId)
    {
        if (string.IsNullOrWhiteSpace(relicId) || !RunState.HasRelic(relicId))
            return false;

        for (int i = 0; i < _relicRuntimes.Count; i++)
        {
            if (_relicRuntimes[i].RelicId == relicId)
                return false;
        }

        BattleRelicRuntime runtime = BattleRelicRuntimeFactory.Create(relicId, this);
        if (runtime == null)
            return false;

        _relicRuntimes.Add(runtime);
        return true;
    }

    private void RefillHandIfEmpty(Combatant owner)
    {
        if (CurrentRound == null)
            return;

        if (owner == Combatant.Player)
            RefillPlayerHandIfEmpty();
        else
            RefillOpponentHandIfEmpty();
    }

    private static void TransformCards(List<Card> cards, Func<Card, Card> transform)
    {
        if (transform == null)
            return;

        for (int i = 0; i < cards.Count; i++)
            cards[i] = transform(cards[i]);
    }

    private static bool IsNumberRank(Rank rank)
    {
        return rank >= Rank.Ace && rank <= Rank.Ten;
    }
}
