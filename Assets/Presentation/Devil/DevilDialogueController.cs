using System.Collections;
using UnityEngine;
using PixelCrushers.DialogueSystem;

// Handles the dialogue, as well as one-off barks
[RequireComponent(typeof(DialogueActor))]
public class DevilDialogueController : MonoBehaviour
{
    [SerializeField] private DialogueActor dialogueActor;
    [SerializeField] private BattleController battleController;
    [SerializeField] private string barkConversationId; // id of conversation pool of barks
    [SerializeField] private string roundEndConversationId; // id of conversation pool of idle barks
    [SerializeField, Min(0f)] private float idleBarkDelaySeconds = 10f;

    private string _currentDevilId;
    private ScopedEventBus battleBus;
    private Coroutine idleBarkCoroutine;
    private float idleElapsedTime;
    private bool idleBarkedThisTurn;
    private PokerHandRank lastPokerRank;
    private IDevilStrategy _devilStrategy;   // This will house all devil-specific logic for long conversations

    void Awake()
    {
        if (dialogueActor == null)
            dialogueActor = GetComponent<DialogueActor>();
        if (battleController == null)
            battleController = FindFirstObjectByType<BattleController>();
    }

    void OnEnable()
    {
        EventBus.Subscribe<BattleStartedEvent>(OnBattleStarted);
        EventBus.Subscribe<BattleEndedEvent>(OnBattleEnded);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe<BattleStartedEvent>(OnBattleStarted);
        EventBus.Unsubscribe<BattleEndedEvent>(OnBattleEnded);
        StopIdleBarkTimer();
        UnsubscribeFromBattleBus();
    }

    private void OnBattleStarted(BattleStartedEvent @event)
    {
        BattleState battle = battleController != null ? battleController.BattleState : null;
        if (battle == null) return;

        StopIdleBarkTimer();
        UnsubscribeFromBattleBus();
        _currentDevilId = battle.Config.DevilId;
        _devilStrategy = battle.Config.DevilStrategy;

        battleBus = battle.EventBus;
        battleBus.Subscribe<RoundStartedEvent>(OnRoundStarted);
        battleBus.Subscribe<RoundEndedEvent>(OnRoundEnded);
        battleBus.Subscribe<PlayerTurnStartedEvent>(OnPlayerTurnStarted);
        battleBus.Subscribe<PlayerTurnEndedEvent>(OnPlayerTurnEnded);
        battleBus.Subscribe<CardPlayedEvent>(OnCardPlayed);
        battleBus.Subscribe<DevilTurnChoiceEvent>(OnDevilTurnChoice);
        battleBus.Subscribe<BurstAttemptedEvent>(OnBurstAttempted);
        battleBus.Subscribe<PokerResolvedEvent>(OnPokerResolved);

        barkConversationId = $"{_currentDevilId}_BattleStart";
        DialogueManager.Bark(barkConversationId, transform);
        // TODO: delayed second bark
    }

    private void OnRoundStarted(RoundStartedEvent @event)
    {
        barkConversationId = $"{_currentDevilId}_RoundStart";
        DialogueManager.Bark(barkConversationId, transform);

        lastPokerRank = PokerHandRank.HighCard; // Reset last poker rank at the start of each round
    }

    private void OnPlayerTurnStarted(PlayerTurnStartedEvent @event)
    {
        StopIdleBarkTimer();
        idleElapsedTime = 0f;
        idleBarkedThisTurn = false;

        if (string.IsNullOrWhiteSpace(_currentDevilId))
            return;

        idleBarkCoroutine = StartCoroutine(WaitForPlayerIdle());
    }

    private void OnPlayerTurnEnded(PlayerTurnEndedEvent @event)
    {
        RoundState round = battleController != null ? battleController.BattleState?.CurrentRound : null;
        if (round != null && round.PlayerPlayedThisTurn)
            idleElapsedTime = 0f;

        StopIdleBarkTimer();
    }

    private void OnCardPlayed(CardPlayedEvent @event)
    {
        if (@event.Owner == Combatant.Player && idleBarkCoroutine != null && !idleBarkedThisTurn)
            idleElapsedTime = 0f;
    }

    private IEnumerator WaitForPlayerIdle()
    {
        while (idleElapsedTime < idleBarkDelaySeconds)
        {
            yield return null;
            idleElapsedTime += Time.deltaTime;
        }

        BattleState battle = battleController != null ? battleController.BattleState : null;
        if (!idleBarkedThisTurn && battle != null && battle.Phase == BattlePhase.PlayerPhase)
        {
            idleBarkedThisTurn = true;
            barkConversationId = $"{_currentDevilId}_Idle";
            DialogueManager.Bark(barkConversationId, transform);
        }

        idleBarkCoroutine = null;
    }

    private void OnRoundEnded(RoundEndedEvent @event)
    {
        // TODO: add IDevilStrategy interface to get the correct conversation ID for round end
        roundEndConversationId = _devilStrategy.GetDialogueId();
        Debug.Log($"DevilDialogueController: OnRoundEnded, using conversation ID: {roundEndConversationId}");
        DialogueManager.StartConversation(roundEndConversationId, transform);
    }

    private void OnDevilTurnChoice(DevilTurnChoiceEvent @event)
    {
        barkConversationId = $"{_currentDevilId}_DevilTurn";
        DialogueManager.Bark(barkConversationId, transform);
    }

    private void OnBurstAttempted(BurstAttemptedEvent @event)
    {
        if (@event.Combatant == Combatant.Opponent) return;

        barkConversationId = $"{_currentDevilId}_OverBurst";
        DialogueManager.Bark(barkConversationId, transform);
    }

    private void OnBattleEnded(BattleEndedEvent @event)
    {
        StopIdleBarkTimer();
        UnsubscribeFromBattleBus();
        _currentDevilId = null;
        _devilStrategy = null;
        roundEndConversationId = null;
    }

    private void OnPokerResolved(PokerResolvedEvent @event)
    {
        if (@event.Combatant == Combatant.Opponent) return;

        // TODO: add limits to how high the poker rank should be to trigger this bark
        if (@event.Poker.Rank <= lastPokerRank) return;
  
        lastPokerRank = @event.Poker.Rank;
        barkConversationId = $"{_currentDevilId}_PokerAchieved";
        DialogueManager.Bark(barkConversationId, transform);
    }

    private void StopIdleBarkTimer()
    {
        if (idleBarkCoroutine == null)
            return;

        StopCoroutine(idleBarkCoroutine);
        idleBarkCoroutine = null;
    }

    private void UnsubscribeFromBattleBus()
    {
        if (battleBus == null)
            return;

        battleBus.Unsubscribe<RoundStartedEvent>(OnRoundStarted);
        battleBus.Unsubscribe<RoundEndedEvent>(OnRoundEnded);
        battleBus.Unsubscribe<PlayerTurnStartedEvent>(OnPlayerTurnStarted);
        battleBus.Unsubscribe<PlayerTurnEndedEvent>(OnPlayerTurnEnded);
        battleBus.Unsubscribe<CardPlayedEvent>(OnCardPlayed);
        battleBus.Unsubscribe<DevilTurnChoiceEvent>(OnDevilTurnChoice);
        battleBus.Unsubscribe<BurstAttemptedEvent>(OnBurstAttempted);
        battleBus.Unsubscribe<PokerResolvedEvent>(OnPokerResolved);
        battleBus = null;
    }
}

// TODO: For all DialogueManager.Bark calls, we need to check the game state to override the affinity variable based on money left
// bool affinity = DialogueLua.GetVariable(”OverrideToMidAffinity”).asBool;
// DialogueLua.SetVariable(”OverrideToMidAffinity”, true)
// mid and low override should be explicitly set (no 2 trues at the same time)
// for save compatibility, this set should be done per bark call that changes with affinity, and reset to false after the bark is done.
