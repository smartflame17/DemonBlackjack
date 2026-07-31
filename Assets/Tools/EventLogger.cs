using UnityEngine;

public class EventLogger : MonoBehaviour
{
    [SerializeField] private bool logGlobalEvents = true;
    [SerializeField] private bool logBattleEvents = true;
    [SerializeField] private BattleController battleController;

    private BattleState battleState;
    private ScopedEventBus battleBus;

    // This component is for debugging purposes. It subscribes to all C# events and logs them to the editor console.

    private void Awake()
    {
        ResolveBattleController();
    }

    private void OnEnable()
    {
        ResolveBattleController();

        EventBus.Subscribe<BattleStartedEvent>(OnBattleStarted);
        EventBus.Subscribe<BattleEndedEvent>(OnBattleEnded);
        EventBus.Subscribe<MoneyChangedEvent>(OnMoneyChanged);
        EventBus.Subscribe<RunPhaseChangedEvent>(OnRunPhaseChanged);

        BindBattleState(battleController != null ? battleController.BattleState : null);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<BattleStartedEvent>(OnBattleStarted);
        EventBus.Unsubscribe<BattleEndedEvent>(OnBattleEnded);
        EventBus.Unsubscribe<MoneyChangedEvent>(OnMoneyChanged);
        EventBus.Unsubscribe<RunPhaseChangedEvent>(OnRunPhaseChanged);

        UnbindBattleState();
    }

    private void OnBattleStarted(BattleStartedEvent eventData)
    {
        if (logGlobalEvents)
        {
            Debug.Log($"<color=green>[Global]</color>Battle Started: EncounterId={eventData.EncounterId}, Seed={eventData.Seed}, PlayerMoney={eventData.PlayerMoney}, OpponentMoney={eventData.OpponentMoney}");
        }

        ResolveBattleController();
        BattleState activeBattle = battleController != null ? battleController.BattleState : null;
        BindBattleState(activeBattle);

        if (activeBattle == null && logBattleEvents)
            Debug.LogWarning("BattleState is null. EventLogger will not subscribe to battle events.", this);
    }

    private void OnBattleEnded(BattleEndedEvent eventData)
    {
        if (logGlobalEvents)
            Debug.Log($"<color=green>[Global]</color>Battle Ended: Result={eventData.Result}");

        UnbindBattleState();
    }

    private void OnMoneyChanged(MoneyChangedEvent eventData)
    {
        if (!logGlobalEvents)
            return;

        Debug.Log($"<color=green>[Global]</color>Money Changed: Owner={eventData.Owner}, CurrentMoney={eventData.CurrentMoney}, Delta={eventData.Delta}");
    }

    private void OnRunPhaseChanged(RunPhaseChangedEvent eventData)
    {
        if (!logGlobalEvents)
            return;

        Debug.Log($"<color=green>[Global]</color> Run Phase Changed: NewPhase={eventData.Phase}");
    }

    private void ResolveBattleController()
    {
        if (battleController == null)
            battleController = FindFirstObjectByType<BattleController>();
    }

    private void BindBattleState(BattleState activeBattle)
    {
        if (ReferenceEquals(battleState, activeBattle))
            return;

        ScopedEventBus activeBus = activeBattle?.EventBus;
        UnbindBattleState();
        if (activeBus == null)
            return;

        battleState = activeBattle;
        battleBus = activeBus;
        battleBus.Subscribe<BattlePhaseChangedEvent>(OnBattlePhaseChanged);
        battleBus.Subscribe<ItemUsedEvent>(OnItemUsed);
        battleBus.Subscribe<RoundStartedEvent>(OnRoundStarted);
        battleBus.Subscribe<RoundEndedEvent>(OnRoundEnded);
        battleBus.Subscribe<CardPlayedEvent>(OnCardPlayed);
        battleBus.Subscribe<CardDrawnEvent>(OnCardDrawn);
        battleBus.Subscribe<CardDiscardedEvent>(OnCardDiscarded);
        battleBus.Subscribe<HandRefilledEvent>(OnHandRefilled);
        battleBus.Subscribe<DeckShuffledEvent>(OnDeckShuffled);
        battleBus.Subscribe<ScoreCalculatedEvent>(OnScoreCalculated);
        battleBus.Subscribe<BurstOccurredEvent>(OnBurstOccurred);
        battleBus.Subscribe<BurstAttemptedEvent>(OnBurstAttempted);
        battleBus.Subscribe<BlackjackAchievedEvent>(OnBlackjackAchieved);
        battleBus.Subscribe<PokerResolvedEvent>(OnPokerResolved);
    }

    private void UnbindBattleState()
    {
        if (battleBus != null)
        {
            battleBus.Unsubscribe<BattlePhaseChangedEvent>(OnBattlePhaseChanged);
            battleBus.Unsubscribe<ItemUsedEvent>(OnItemUsed);
            battleBus.Unsubscribe<RoundStartedEvent>(OnRoundStarted);
            battleBus.Unsubscribe<RoundEndedEvent>(OnRoundEnded);
            battleBus.Unsubscribe<CardPlayedEvent>(OnCardPlayed);
            battleBus.Unsubscribe<CardDrawnEvent>(OnCardDrawn);
            battleBus.Unsubscribe<CardDiscardedEvent>(OnCardDiscarded);
            battleBus.Unsubscribe<HandRefilledEvent>(OnHandRefilled);
            battleBus.Unsubscribe<DeckShuffledEvent>(OnDeckShuffled);
            battleBus.Unsubscribe<ScoreCalculatedEvent>(OnScoreCalculated);
            battleBus.Unsubscribe<BurstOccurredEvent>(OnBurstOccurred);
            battleBus.Unsubscribe<BurstAttemptedEvent>(OnBurstAttempted);
            battleBus.Unsubscribe<BlackjackAchievedEvent>(OnBlackjackAchieved);
            battleBus.Unsubscribe<PokerResolvedEvent>(OnPokerResolved);
        }

        battleBus = null;
        battleState = null;
    }

    private void OnBattlePhaseChanged(BattlePhaseChangedEvent eventData)
    {
        if (logBattleEvents)
            Debug.Log($"<color=red>[Battle]</color> Battle Phase Changed: NewPhase={eventData.Phase}");
    }

    private void OnItemUsed(ItemUsedEvent eventData)
    {
        if (logBattleEvents)
            Debug.Log($"<color=red>[Battle]</color> Item Used: ItemId={eventData.ItemId}");
    }

    private void OnRoundStarted(RoundStartedEvent eventData)
    {
        if (logBattleEvents)
            Debug.Log($"<color=red>[Battle]</color> Round Started: RoundNumber={eventData.RoundNumber}");
    }

    private void OnRoundEnded(RoundEndedEvent eventData)
    {
        if (logBattleEvents)
            Debug.Log($"<color=red>[Battle]</color> Round Ended: RoundNumber={eventData.RoundNumber}");
    }

    private void OnCardPlayed(CardPlayedEvent eventData)
    {
        if (logBattleEvents)
            Debug.Log($"<color=red>[Battle]</color> Card Played: Card={eventData.Card}, Combatant={eventData.Owner}");
    }

    private void OnCardDrawn(CardDrawnEvent eventData)
    {
        if (logBattleEvents)
            Debug.Log($"<color=red>[Battle]</color> Card Drawn: Card={eventData.Card}, RemainingDeckCount={eventData.RemainingDeckCount}");
    }

    private void OnCardDiscarded(CardDiscardedEvent eventData)
    {
        if (logBattleEvents)
            Debug.Log($"<color=red>[Battle]</color> Card Discarded: Card={eventData.Card}");
    }

    private void OnHandRefilled(HandRefilledEvent eventData)
    {
        if (logBattleEvents)
            Debug.Log($"<color=red>[Battle]</color> Hand Refilled: HandCount={eventData.HandCount}");
    }

    private void OnDeckShuffled(DeckShuffledEvent eventData)
    {
        if (logBattleEvents)
            Debug.Log($"<color=red>[Battle]</color> Deck Shuffled: Owner={eventData.Owner}, ReshuffleCount={eventData.ReshuffleCount}, DeckCount={eventData.DeckCount}");
    }

    private void OnScoreCalculated(ScoreCalculatedEvent eventData)
    {
        if (logBattleEvents)
            Debug.Log($"<color=red>[Battle]</color> Score Calculated: Combatant={eventData.Combatant}, Score={eventData.Score}");
    }

    private void OnBurstOccurred(BurstOccurredEvent eventData)
    {
        if (logBattleEvents)
            Debug.Log($"<color=red>[Battle]</color> Burst Occurred: Combatant={eventData.Combatant}, Score={eventData.Score}");
    }

    private void OnBurstAttempted(BurstAttemptedEvent eventData)
    {
        if (logBattleEvents)
            Debug.Log($"<color=red>[Battle]</color> Burst Attempted: Combatant={eventData.Combatant}, Score={eventData.Score}, Threshold={eventData.Threshold}, WillBurst={eventData.WillBurst}");
    }

    private void OnBlackjackAchieved(BlackjackAchievedEvent eventData)
    {
        if (logBattleEvents)
            Debug.Log($"<color=red>[Battle]</color> Blackjack Achieved: Combatant={eventData.Combatant}, Score={eventData.Score}");
    }

    private void OnPokerResolved(PokerResolvedEvent eventData)
    {
        if (logBattleEvents)
            Debug.Log($"<color=red>[Battle]</color> Poker Resolved: Combatant={eventData.Combatant}, PokerResult={eventData.Poker}");
    }
}
