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
    private int? _pendingOpponentWagerOffer;
    private readonly BattleEffectRuntime _effectRuntime;
    private readonly BattleRelicRuntime _relicRuntime;
    private bool _disposed;

    public BattleState(RunState runState, BattleConfig config)
    {
        RunState = runState ?? throw new ArgumentNullException(nameof(runState));
        Config = config ?? throw new ArgumentNullException(nameof(config));
        EventBus = new ScopedEventBus();
        global::EventBus.Subscribe<RankUpgradeChangedEvent>(OnRankUpgradeChanged);
        _relicRuntime = new BattleRelicRuntime(this);
        _effectRuntime = new BattleEffectRuntime(this);
        EventBus.Subscribe<CardPlayedEvent>(OnCardPlayedForRefill);
        EventBus.Subscribe<CardDiscardedEvent>(OnCardDiscardedForRefill);
        CommandQueue = new CommandQueue();
        OpponentMoney = config.OpponentStartingMoney;
        BattleSeed = runState.CreateBattleSeed();
        _playerDeck = new Deck(runState.CreateBattleDeck(), BattleSeed);
        _opponentDeck = new Deck(config.DevilStrategy.CreateStartingDeck(runState, config), BattleSeed + 17);
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
    public IReadOnlyList<Card> PlayerDrawPile => _playerDeck.DrawPile;
    public IReadOnlyList<Card> PlayerDiscardPile => _playerDeck.DiscardPile;
    public IReadOnlyList<Card> OpponentDrawPile => _opponentDeck.DrawPile;
    public IReadOnlyList<Card> OpponentDiscardPile => _opponentDeck.DiscardPile;

    public void Initialize()
    {
        SetPhase(BattlePhase.Init);
        EventBus.Publish(new BattleStartedEvent(Config.EncounterId, BattleSeed, PlayerMoney, OpponentMoney));
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.BattleStarted, Config.EncounterId));
        SetPhase(BattlePhase.PreRound);
    }

    public int GetOpponentWagerOffer()
    {
        if (IsBattleOver || CurrentRound == null || CurrentRound.WagerCommitted || CurrentRound.PlayerActsFirst)
            return 0;

        _pendingOpponentWagerOffer ??= Config.DevilStrategy.ChooseWager(this, CurrentRound, GetDefaultWager());
        return _pendingOpponentWagerOffer.Value;
    }

    // creates a new round with the next round number, but does not begin it or commit the wager yet
    public bool StartRound()
    {
        if (IsBattleOver || CurrentRound != null)
            return false;

        bool playerActsFirst = RoundNumber % 2 == 0;

        SetPhase(BattlePhase.PreRound);
        RoundNumber++;
        int targetScore = RelicRuleResolver.ResolveTargetScore(RunState, Config.TargetScore);
        int playerBurstThreshold = RelicRuleResolver.ResolvePlayerBurstThreshold(RunState, Config.BurstThreshold);
        int opponentBurstThreshold = RelicRuleResolver.ResolveOpponentBurstThreshold(RunState, Config.BurstThreshold);
        CurrentRound = new RoundState(RoundNumber, targetScore, playerBurstThreshold, opponentBurstThreshold, 0, 0, playerActsFirst);
        RestoreCarryoverHands();
        RefillHandsForRoundStart(playerActsFirst);
        return true;
    }

    public bool CommitWagerAndBeginRound(int wager, bool playerAcceptsOpponentWager)
    {
        if (IsBattleOver || CurrentRound == null || CurrentRound.WagerCommitted)
            return false;

        bool playerActsFirst = CurrentRound.PlayerActsFirst;
        if (!TryNegotiateWager(wager, playerActsFirst, playerAcceptsOpponentWager, out int proposedWager, out int playerStake, out int opponentStake))
            return false;

        if (!CurrentRound.TryCommitWager(playerStake, opponentStake))
            return false;

        RunState.AddMoney(-playerStake);
        AddOpponentMoney(-opponentStake);

        EventBus.Publish(new RoundStartedEvent(RoundNumber));
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.RoundStarted, $"{RoundNumber}:{proposedWager}"));

        CurrentRound.BeginPlayerTurn();
        SetPhase(BattlePhase.PlayerPhase);

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
        return true;
    }

    public bool TryHit()
    {
        if (Phase != BattlePhase.PlayerPhase || CurrentRound == null)
            return false;

        if (!TryDrawCard(Combatant.Player, out Card card))
            return false;

        card = ApplyPlayerCardForPlay(card);
        if (!CurrentRound.TryPlayHitCard(card))
            return false;

        EventBus.Publish(new CardDrawnEvent(Combatant.Player, card, _playerDeck.RemainingCards));
        EventBus.Publish(new PlayerHitUsedEvent(RoundNumber, card));
        PublishPlayerCardPlayed(card);
        return CompletePlayerTurn();
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
            return new BattleResult(OpponentMoney <= 0 && PlayerMoney > 0, RoundNumber, PlayerMoney, OpponentMoney);

        SetPhase(BattlePhase.BattleEnd);
        var result = new BattleResult(OpponentMoney <= 0 && PlayerMoney > 0, RoundNumber, PlayerMoney, OpponentMoney);
        EventBus.Publish(new BattleEndedEvent(result));
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.BattleEnded, result.PlayerWon.ToString()));
        Dispose();
        return result;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Config.DevilStrategy.UnregisterAffinityHooks(this);
        _effectRuntime.Dispose();
        _relicRuntime.Dispose();
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

        if (!CurrentRound.TryPlayCard(handIndex, ApplyPlayerCardForPlay, out Card card))
            return false;

        PublishPlayerCardPlayed(card);
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

    public bool TryDrawRandomPlayerCardOfSuitToHand(Suit suit)
    {
        if (CurrentRound == null)
            return false;

        if (!_playerDeck.TryDrawWhere(card => card.Suit == suit, _random, out Card card))
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

        return CurrentRound.TryMovePreviousPlayerPlayedCardToOpponent(out card);
    }

    // Used by BattleEffectRuntime to immediately play a random card of the same suit as the previous player played card
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

    // Used by BattleEffectRuntime to add a battle-only card to the player's hand, which will be removed at the end of the round
    public bool AddBattleOnlyCardToPlayerHand(Card card)
    {
        if (CurrentRound == null)
            return false;

        CurrentRound.AddBattleOnlyPlayerHandCard(card);
        EventBus.Publish(new HandRefilledEvent(CurrentRound.PlayerHand.Count));
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.CardsDrawn, CurrentRound.PlayerHand.Count.ToString()));
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
        return _relicRuntime.TransformPlayerPlayedCard(ApplyPlayerCardUpgrade(card));
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

    public void DamagePlayer(int amount)
    {
        LosePlayerMoney(amount);
    }

    public void DamageOpponent(int amount)
    {
        LoseOpponentMoney(amount);
    }

    public void HealPlayer(int amount)
    {
        RunState.AddMoney(Math.Max(0, amount));
    }

    public int LosePlayerMoney(int amount)
    {
        int finalAmount = Math.Min(PlayerMoney, Math.Max(0, amount));
        if (finalAmount <= 0)
            return 0;

        RunState.AddMoney(-finalAmount);
        EventBus.Publish(new MoneyChangedEvent(Combatant.Player, PlayerMoney, -finalAmount));
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.MoneyChanged, $"{Combatant.Player}:{PlayerMoney}"));

        if (PlayerMoney <= 0)
            EventBus.Publish(new DeathPreventedEvent(Combatant.Player, false));

        return finalAmount;
    }

    public int LoseOpponentMoney(int amount)
    {
        return AddOpponentMoney(-Math.Max(0, amount)) * -1;
    }

    public bool TryDrawForOpponent(out Card card)
    {
        return TryDrawCard(Combatant.Opponent, out card);
    }

    public int NextRandomInclusive(int minimum, int maximum)
    {
        if (maximum <= minimum)
            return minimum;

        return _random.Next(minimum, maximum + 1);
    }

    private int AddOpponentMoney(int delta)
    {
        int previous = OpponentMoney;
        OpponentMoney = Math.Max(0, OpponentMoney + delta);
        int actualDelta = OpponentMoney - previous;
        EventBus.Publish(new MoneyChangedEvent(Combatant.Opponent, OpponentMoney, actualDelta));
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.MoneyChanged, $"{Combatant.Opponent}:{OpponentMoney}"));
        return actualDelta;
    }

    public bool TryDrawForPlayer(out Card card)
    {
        return TryDrawCard(Combatant.Player, out card);
    }

    public bool TryUseActiveItem(string itemId)
    {
        if (!CanUseActiveItem(itemId))
            return false;

        if (!ActiveItemResolver.TryApply(itemId, this))
            return false;

        RunState.RemoveActiveItem(itemId);
        EventBus.Publish(new ItemUsedEvent(itemId));
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
        return true;
    }

    public bool CanDrawPlayerCards()
    {
        return CurrentRound != null && (_playerDeck.RemainingCards > 0 || _playerDeck.DiscardedCards > 0);
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

    public bool CanDoubleCurrentRoundWager()
    {
        return CurrentRound != null
            && CurrentRound.WagerCommitted
            && CurrentRound.PlayerStake > 0
            && CurrentRound.OpponentStake > 0
            && PlayerMoney >= CurrentRound.PlayerStake
            && OpponentMoney >= CurrentRound.OpponentStake;
    }

    public bool DoubleCurrentRoundWager()
    {
        if (!CanDoubleCurrentRoundWager())
            return false;

        int playerExtraStake = CurrentRound.PlayerStake;
        int opponentExtraStake = CurrentRound.OpponentStake;
        RunState.AddMoney(-playerExtraStake);
        AddOpponentMoney(-opponentExtraStake);

        if (!CurrentRound.TryIncreaseWager(playerExtraStake, opponentExtraStake))
        {
            RunState.AddMoney(playerExtraStake);
            AddOpponentMoney(opponentExtraStake);
            return false;
        }

        return true;
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

    private bool TryNegotiateWager(int requestedWager, bool playerActsFirst, bool playerAcceptsOpponentWager, out int proposedWager, out int playerStake, out int opponentStake)
    {
        proposedWager = 0;
        playerStake = 0;
        opponentStake = 0;

        if (PlayerMoney <= 0 || OpponentMoney <= 0)
            return false;

        if (playerActsFirst)
        {
            _pendingOpponentWagerOffer = null;
            proposedWager = NormalizeWager(requestedWager < 0 ? GetDefaultWager() : requestedWager);
            WagerResponse response = Config.DevilStrategy.ChoosePlayerWagerResponse(this, CurrentRound, proposedWager);
            UnityEngine.Debug.Log($"Opponent {FormatWagerResponseForLog(response)} player wager offer: {proposedWager}");
            EventBus.Publish(new WagerCommittedEvent(proposedWager, playerActsFirst, response));
            if (response == WagerResponse.Decline)
                proposedWager = GetDefaultWager();
        }
        else
        {
            proposedWager = _pendingOpponentWagerOffer ?? Config.DevilStrategy.ChooseWager(this, CurrentRound, GetDefaultWager());
            _pendingOpponentWagerOffer = null;
            if (!playerAcceptsOpponentWager)
            {
                proposedWager = GetDefaultWager();
            }
            else
            {
                RunState.AddDevilAffinity(Config.DevilId, 5);
            }
            EventBus.Publish(new WagerCommittedEvent(proposedWager, playerActsFirst, playerAcceptsOpponentWager ? WagerResponse.Accept : WagerResponse.Decline));
        }
        
        playerStake = Math.Min(PlayerMoney, proposedWager);
        opponentStake = Math.Min(OpponentMoney, proposedWager);

        return proposedWager > 0 && playerStake > 0 && opponentStake > 0;
    }

    private int NormalizeWager(int wager)
    {
        if (wager <= 0)
            return 0;

        return wager;
    }

    public int GetDefaultWager()
    {
        if (PlayerMoney <= 0)
            return 0;

        return Math.Max(1, PlayerMoney / 10);
    }

    private static string FormatWagerResponseForLog(WagerResponse response)
    {
        return response == WagerResponse.Accept ? "accepted" : "declined";
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
        RefillOpponentHandIfEmpty();

        DevilTurnChoice choice = Config.DevilStrategy.ChooseTurnAction(this, CurrentRound);
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

    private void PlayOpponentHit()
    {
        if (!TryDrawCard(Combatant.Opponent, out Card card))
            return;

        CurrentRound.PlayOpponentHitCard(card);
        EventBus.Publish(new CardDrawnEvent(Combatant.Opponent, card, _opponentDeck.RemainingCards));
        EventBus.Publish(new CardPlayedEvent(Combatant.Opponent, card));
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.CardsPlayed, card.ToString()));
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

        EventBus.Publish(new CardPlayedEvent(Combatant.Opponent, card));
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.CardsPlayed, card.ToString()));
    }

    private bool CompletePlayerTurn()
    {
        if (ResolveTurnOrRoundEnd())
            return true;

        SetPhase(BattlePhase.OpponentPhase);
        return PlayOpponentTurn();
    }

    private bool CompleteOpponentTurn()
    {
        if (ResolveTurnOrRoundEnd())
            return true;

        CurrentRound.BeginPlayerTurn();
        SetPhase(BattlePhase.PlayerPhase);
        return true;
    }

    private bool ResolveTurnOrRoundEnd()
    {
        ResolveScores();

        if (CurrentRound.PlayerScore.IsBurst || CurrentRound.OpponentScore.IsBurst)
        {
            Combatant? winner = MoneyResolver.DetermineWinner(CurrentRound.PlayerScore, CurrentRound.OpponentScore);
            ResolveRound(new RoundResolution(winner, 0, 0), true);
            return true;
        }

        if (CurrentRound.PlayerStood && CurrentRound.OpponentStood) // if both players stand, resolve the round immediately
        {
            Combatant? winner = MoneyResolver.DetermineWinner(CurrentRound.PlayerScore, CurrentRound.OpponentScore);
            ResolveRound(new RoundResolution(winner, 0, 0), false);
            return true;
        }

        return false;
    }

    private void ResolveScores()
    {
        ScoreResult playerScore = ScoreResolver.Resolve(CurrentRound.PlayerPlayedCards, CurrentRound.ScoringModifiers, CurrentRound.TargetScore, CurrentRound.PlayerBurstThreshold);
        ScoreResult opponentScore = ScoreResolver.Resolve(CurrentRound.OpponentVisibleCards, CurrentRound.ScoringModifiers, CurrentRound.TargetScore, CurrentRound.OpponentBurstThreshold, _relicRuntime.OpponentBlackjackBonus);
        CurrentRound.SetScores(playerScore, opponentScore);

        PublishScoreEvents(Combatant.Player, playerScore);
        PublishScoreEvents(Combatant.Opponent, opponentScore);
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.ScoresResolved, $"{playerScore.FinalScore}:{opponentScore.FinalScore}"));
    }

    private void ResolveRound(RoundResolution baseResolution, bool applyBurstPenalty)
    {
        EventBus.Publish(new RoundEndedEvent(RoundNumber));
        SetPhase(BattlePhase.PostRound);

        RoundResolution resolution = applyBurstPenalty
            ? MoneyResolver.ResolveRound(this, CurrentRound, true)
            : baseResolution;

        ResolveRoundBet(resolution);
        _combatHistory.Add(resolution);

        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.RoundEnded, RoundNumber.ToString()));

        if (IsBattleOver)
            EndBattle();
        else
            SetPhase(BattlePhase.Cleanup);
    }

    private void ResolveRoundBet(RoundResolution resolution)
    {
        if (CurrentRound.Pot <= 0)
            return;

        if (resolution.Winner == Combatant.Player)
            RunState.AddMoney(CurrentRound.Pot);
        else if (resolution.Winner == Combatant.Opponent)
            AddOpponentMoney(CurrentRound.Pot);
        else
        {
            RunState.AddMoney(CurrentRound.PlayerStake);
            AddOpponentMoney(CurrentRound.OpponentStake);
        }
    }

    private bool TryDrawCard(Combatant owner, out Card card)
    {
        Deck deck = owner == Combatant.Player ? _playerDeck : _opponentDeck;
        if (deck.RemainingCards == 0 && deck.DiscardedCards > 0)
        {
            deck.ReshuffleDiscardIntoDraw();
            ReshuffleCount++;
            EventBus.Publish(new DeckShuffledEvent(ReshuffleCount, deck.RemainingCards));
        }

        return deck.TryDraw(out card);
    }

    private void PublishPlayerCardPlayed(Card card)
    {
        EventBus.Publish(new CardPlayedEvent(Combatant.Player, card));
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.CardsPlayed, card.ToString()));
    }

    private void PublishScoreEvents(Combatant combatant, ScoreResult score)
    {
        EventBus.Publish(new ScoreCalculatedEvent(combatant, score));

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
}
