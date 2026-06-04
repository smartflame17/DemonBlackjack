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

    public BattleState(RunState runState, BattleConfig config)
    {
        RunState = runState ?? throw new ArgumentNullException(nameof(runState));
        Config = config ?? throw new ArgumentNullException(nameof(config));
        EventBus = new ScopedEventBus();
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
    public int PlayerHp => PlayerMoney;
    public int PlayerMaxHp => Math.Max(RunState.MaxPlayerHp, PlayerMoney);
    public int OpponentHp => OpponentMoney;
    public int OpponentMaxHp => Config.OpponentStartingMoney;
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

    public bool StartRound(int wager = -1)
    {
        return StartRound(wager, true);
    }

    public int GetOpponentWagerOffer()
    {
        if (IsBattleOver || CurrentRound != null || RoundNumber % 2 == 0)
            return 0;

        _pendingOpponentWagerOffer ??= GenerateOpponentWagerOffer();
        return _pendingOpponentWagerOffer.Value;
    }

    public bool StartRound(int wager, bool playerAcceptsOpponentWager)
    {
        if (IsBattleOver || CurrentRound != null)
            return false;

        bool playerActsFirst = RoundNumber % 2 == 0;
        if (!TryNegotiateWager(wager, playerActsFirst, playerAcceptsOpponentWager, out int proposedWager, out int playerStake, out int opponentStake))
            return false;

        RunState.AddMoney(-playerStake);
        AddOpponentMoney(-opponentStake);

        SetPhase(BattlePhase.PreRound);
        RoundNumber++;
        int targetScore = RelicRuleResolver.ResolveTargetScore(RunState, Config.TargetScore);
        int burstThreshold = RelicRuleResolver.ResolveBurstThreshold(RunState, Config.BurstThreshold);
        CurrentRound = new RoundState(RoundNumber, targetScore, burstThreshold, playerStake, opponentStake, playerActsFirst);
        RestoreCarryoverHands();
        EventBus.Publish(new RoundStartedEvent(RoundNumber));
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.RoundStarted, $"{RoundNumber}:{proposedWager}"));
        RefillHandsForRoundStart(playerActsFirst);

        if (playerActsFirst)
        {
            CurrentRound.BeginPlayerTurn();
            SetPhase(BattlePhase.PlayerPhase);
        }
        else
        {
            SetPhase(BattlePhase.OpponentPhase);
            PlayOpponentTurn();
        }

        return true;
    }

    public bool TryPlayCard(int handIndex)
    {
        if (Phase != BattlePhase.PlayerPhase || CurrentRound == null)
            return false;

        if (!CurrentRound.TryPlayCard(handIndex, out Card card))
            return false;

        EventBus.Publish(new CardPlayedEvent(Combatant.Player, card));
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.CardsPlayed, card.ToString()));
        RefillPlayerHandIfEmpty();
        return true;
    }

    public bool TryHit()
    {
        if (Phase != BattlePhase.PlayerPhase || CurrentRound == null)
            return false;

        if (!TryDrawCard(Combatant.Player, out Card card))
            return false;

        if (!CurrentRound.TryPlayHitCard(card))
            return false;

        EventBus.Publish(new CardDrawnEvent(Combatant.Player, card, _playerDeck.RemainingCards));
        EventBus.Publish(new CardPlayedEvent(Combatant.Player, card));
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.CardsPlayed, card.ToString()));
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
        _playerDeck.DiscardRange(CurrentRound.TakePlayerCardsForCleanup());
        _opponentDeck.DiscardRange(CurrentRound.TakeOpponentCardsForCleanup());
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
        Config.DevilStrategy.UnregisterAffinityHooks(this);
        EventBus.Clear();
        return result;
    }

    public void AddModifier(Modifier modifier)
    {
        _activeModifiers.Add(modifier);
        EventBus.Publish(new ModifierAddedEvent(modifier));
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
        EventBus.Publish(new DamageTakenEvent(Combatant.Player, finalAmount, PlayerMoney));
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
        if (IsBattleOver || CurrentRound == null || !RunState.HasActiveItem(itemId))
            return false;

        if (!ActiveItemResolver.TryApply(itemId, this))
            return false;

        RunState.RemoveActiveItem(itemId);
        EventBus.Publish(new ItemUsedEvent(itemId));
        return true;
    }

    public bool ClearField(Combatant owner)
    {
        if (CurrentRound == null)
            return false;

        IReadOnlyList<Card> cards = CurrentRound.TakeFieldCards(owner);
        if (cards.Count == 0)
            return false;

        Deck deck = owner == Combatant.Player ? _playerDeck : _opponentDeck;
        deck.DiscardRange(cards);

        for (int i = 0; i < cards.Count; i++)
            EventBus.Publish(new CardDiscardedEvent(owner, cards[i]));

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
            proposedWager = PlayerMoney < Config.MinWager
                ? PlayerMoney
                : NormalizeWager(requestedWager < 0 ? Config.BaseWager : requestedWager);

            proposedWager = ClampProposedWager(proposedWager, PlayerMoney);
            if (Config.DevilStrategy.ChoosePlayerWagerResponse(this, null, proposedWager) == WagerResponse.Reduce)
                proposedWager = ClampProposedWager(proposedWager - Config.WagerStep, PlayerMoney);
        }
        else
        {
            proposedWager = _pendingOpponentWagerOffer ?? GenerateOpponentWagerOffer();
            _pendingOpponentWagerOffer = null;
            if (!playerAcceptsOpponentWager && Config.DevilStrategy.ChoosePlayerReductionResponse(this, null, proposedWager) == WagerResponse.Accept)
                proposedWager = ClampProposedWager(proposedWager - Config.WagerStep, OpponentMoney);
        }

        playerStake = Math.Min(PlayerMoney, proposedWager);
        opponentStake = Math.Min(OpponentMoney, proposedWager);

        return proposedWager > 0 && playerStake > 0 && opponentStake > 0;
    }

    private int GenerateOpponentWagerOffer()
    {
        int proposedWager = OpponentMoney < Config.MinWager
            ? OpponentMoney
            : Config.DevilStrategy.ChooseWager(this, Config.MinWager, Config.MaxWager, Config.WagerStep);

        return ClampProposedWager(proposedWager, OpponentMoney);
    }

    private int ClampProposedWager(int wager, int deciderMoney)
    {
        if (deciderMoney < Config.MinWager)
            return Math.Max(1, deciderMoney);

        int normalized = NormalizeWager(wager);
        return Math.Clamp(normalized, Config.MinWager, Config.MaxWager);
    }

    private int NormalizeWager(int wager)
    {
        if (wager <= 0)
            return 0;

        return wager - (wager % Config.WagerStep);
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
        int guard = 0;
        while (CurrentRound != null && guard++ < 32)
        {
            RefillOpponentHandIfEmpty();
            if (CurrentRound.OpponentHand.Count == 0)
                return;

            if (Config.DevilStrategy.ChooseTurnAction(this, CurrentRound) != DevilTurnChoice.Play)
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

        if (CurrentRound.PlayerStood && CurrentRound.OpponentStood)
        {
            Combatant? winner = MoneyResolver.DetermineWinner(CurrentRound.PlayerScore, CurrentRound.OpponentScore);
            ResolveRound(new RoundResolution(winner, 0, 0), false);
            return true;
        }

        return false;
    }

    private void ResolveScores()
    {
        ScoreResult playerScore = ScoreResolver.Resolve(CurrentRound.PlayerPlayedCards, CurrentRound.ScoringModifiers, CurrentRound.TargetScore, CurrentRound.BurstThreshold);
        ScoreResult opponentScore = ScoreResolver.Resolve(CurrentRound.OpponentVisibleCards, CurrentRound.ScoringModifiers, CurrentRound.TargetScore, CurrentRound.BurstThreshold);
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

    private void PublishScoreEvents(Combatant combatant, ScoreResult score)
    {
        EventBus.Publish(new ScoreCalculatedEvent(combatant, score));

        if (score.FinalScore > CurrentRound.BurstThreshold)
        {
            EventBus.Publish(new BurstAttemptedEvent(combatant, score.FinalScore, CurrentRound.BurstThreshold, true));
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

    private void RefillHandIfEmpty(Combatant owner)
    {
        if (CurrentRound == null)
            return;

        if (owner == Combatant.Player)
            RefillPlayerHandIfEmpty();
        else
            RefillOpponentHandIfEmpty();
    }
}
