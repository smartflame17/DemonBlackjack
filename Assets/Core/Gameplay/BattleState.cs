using System;
using System.Collections.Generic;

public sealed class BattleState
{
    private readonly Deck _deck;
    private readonly List<Modifier> _activeModifiers = new();
    private readonly List<RoundResolution> _combatHistory = new();

    public BattleState(RunState runState, BattleConfig config)
    {
        RunState = runState ?? throw new ArgumentNullException(nameof(runState));
        Config = config ?? throw new ArgumentNullException(nameof(config));
        EventBus = new ScopedEventBus();
        CommandQueue = new CommandQueue();
        PlayerHp = runState.PlayerHp;
        PlayerMaxHp = runState.MaxPlayerHp;
        OpponentHp = config.OpponentMaxHp;
        OpponentMaxHp = config.OpponentMaxHp;
        BattleSeed = runState.CreateBattleSeed();
        _deck = new Deck(runState.Deck, BattleSeed);
        _activeModifiers.AddRange(config.InitialModifiers);
    }

    public RunState RunState { get; }
    public BattleConfig Config { get; }
    public ScopedEventBus EventBus { get; }
    public CommandQueue CommandQueue { get; }
    public BattlePhase Phase { get; private set; } = BattlePhase.Inactive;
    public int PlayerHp { get; private set; }
    public int PlayerMaxHp { get; }
    public int OpponentHp { get; private set; }
    public int OpponentMaxHp { get; }
    public int RoundNumber { get; private set; }
    public int ReshuffleCount { get; private set; }
    public int BattleSeed { get; }
    public bool IsBattleOver => PlayerHp <= 0 || OpponentHp <= 0 || Phase == BattlePhase.BattleEnd;
    public RoundState CurrentRound { get; private set; }
    public IReadOnlyList<Modifier> ActiveModifiers => _activeModifiers;
    public IReadOnlyList<RoundResolution> CombatHistory => _combatHistory;

    public void Initialize()
    {
        SetPhase(BattlePhase.Init);
        EventBus.Publish(new BattleStartedEvent(Config.EncounterId, BattleSeed, PlayerHp, OpponentHp));
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.BattleStarted, Config.EncounterId));
        SetPhase(BattlePhase.PreRound);
    }

    public void StartRound()
    {
        if (IsBattleOver)
            return;

        SetPhase(BattlePhase.PreRound);
        RoundNumber++;
        CurrentRound = new RoundState(RoundNumber, Config.TargetScore, Config.BurstThreshold, Config.BaseWager);
        EventBus.Publish(new RoundStartedEvent(RoundNumber));
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.RoundStarted, RoundNumber.ToString()));
        DrawPlayerHand(Config.StartingHandSize);
        DrawSharedCards(1);
        SetPhase(BattlePhase.PlayerPhase);
    }

    public bool TryPlayCard(int handIndex)
    {
        if (Phase != BattlePhase.PlayerPhase || CurrentRound == null)
            return false;

        if (!CurrentRound.TryPlayCard(handIndex, out Card card))
            return false;

        EventBus.Publish(new CardPlayedEvent(Combatant.Player, card));
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.CardsPlayed, card.ToString()));
        return true;
    }

    public RoundResolution EndPlayerPhase()
    {
        if (Phase != BattlePhase.PlayerPhase || CurrentRound == null)
            return default;

        EventBus.Publish(new RoundEndedEvent(RoundNumber));
        SetPhase(BattlePhase.PostRound);

        IReadOnlyList<Card> opponentCards = Config.DevilStrategy.ChooseCards(this, CurrentRound);
        CurrentRound.SetOpponentCards(opponentCards);

        foreach (Card card in opponentCards)
            EventBus.Publish(new CardDrawnEvent(Combatant.Opponent, card, _deck.RemainingCards));

        ScoreResult playerScore = ScoreResolver.Resolve(CurrentRound.PlayerPlayedCards, CurrentRound.ScoringModifiers, CurrentRound.TargetScore, CurrentRound.BurstThreshold);
        ScoreResult opponentScore = ScoreResolver.Resolve(CurrentRound.OpponentVisibleCards, CurrentRound.ScoringModifiers, CurrentRound.TargetScore, CurrentRound.BurstThreshold);
        CurrentRound.SetScores(playerScore, opponentScore);

        PublishScoreEvents(Combatant.Player, playerScore);
        PublishScoreEvents(Combatant.Opponent, opponentScore);
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.ScoresResolved, $"{playerScore.FinalScore}:{opponentScore.FinalScore}"));

        RoundResolution resolution = HealthResolver.ResolveRound(this, CurrentRound);
        _combatHistory.Add(resolution);
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.RoundEnded, RoundNumber.ToString()));

        if (IsBattleOver)
            EndBattle();
        else
            SetPhase(BattlePhase.Cleanup);

        return resolution;
    }

    public void CleanupRound()
    {
        if (CurrentRound == null)
            return;

        _deck.DiscardRange(CurrentRound.TakeCardsForCleanup());
        CurrentRound = null;

        if (IsBattleOver)
        {
            EndBattle();
        }
        else
        {
            SetPhase(BattlePhase.PreRound);
        }
    }

    public BattleResult EndBattle()
    {
        SetPhase(BattlePhase.BattleEnd);
        var result = new BattleResult(OpponentHp <= 0 && PlayerHp > 0, RoundNumber, PlayerHp, OpponentHp);
        EventBus.Publish(new BattleEndedEvent(result));
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.BattleEnded, result.PlayerWon.ToString()));
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
        int finalAmount = Math.Max(0, amount);
        PlayerHp = Math.Max(0, PlayerHp - finalAmount);
        EventBus.Publish(new DamageTakenEvent(Combatant.Player, finalAmount, PlayerHp));
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.HealthChanged, $"{Combatant.Player}:{PlayerHp}"));

        if (PlayerHp <= 0)
            EventBus.Publish(new DeathPreventedEvent(Combatant.Player, false));
    }

    public void DamageOpponent(int amount)
    {
        int finalAmount = Math.Max(0, amount);
        OpponentHp = Math.Max(0, OpponentHp - finalAmount);
        EventBus.Publish(new DamageTakenEvent(Combatant.Opponent, finalAmount, OpponentHp));
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.HealthChanged, $"{Combatant.Opponent}:{OpponentHp}"));
    }

    public void HealPlayer(int amount)
    {
        int finalAmount = Math.Max(0, amount);
        PlayerHp = Math.Min(PlayerMaxHp, PlayerHp + finalAmount);
        EventBus.Publish(new HealingReceivedEvent(Combatant.Player, finalAmount, PlayerHp));
    }

    public bool TryDrawForOpponent(out Card card)
    {
        return TryDrawCard(out card);
    }

    private void DrawPlayerHand(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (!TryDrawCard(out Card card))
                break;

            CurrentRound.AddToHand(card);
            EventBus.Publish(new CardDrawnEvent(Combatant.Player, card, _deck.RemainingCards));
        }

        EventBus.Publish(new HandRefilledEvent(CurrentRound.PlayerHand.Count));
        CommandQueue.Enqueue(new VisualCommand(VisualCommandType.CardsDrawn, CurrentRound.PlayerHand.Count.ToString()));
    }

    private void DrawSharedCards(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (!TryDrawCard(out Card card))
                break;

            CurrentRound.AddSharedVisibleCard(card);
            EventBus.Publish(new CardDrawnEvent(Combatant.Opponent, card, _deck.RemainingCards));
        }
    }

    private bool TryDrawCard(out Card card)
    {
        if (_deck.RemainingCards == 0 && _deck.DiscardedCards > 0)
        {
            _deck.ReshuffleDiscardIntoDraw();
            ReshuffleCount++;
            EventBus.Publish(new DeckShuffledEvent(ReshuffleCount, _deck.RemainingCards));
        }

        return _deck.TryDraw(out card);
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
}
