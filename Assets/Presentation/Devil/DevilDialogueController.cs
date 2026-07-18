using UnityEngine;
using PixelCrushers.DialogueSystem;
using System;

// Handles the dialogue, as well as one-off barks
[RequireComponent(typeof(DialogueActor))]
public class DevilDialogueController : MonoBehaviour
{
    [SerializeField] private DialogueActor dialogueActor;
    [SerializeField] private BattleController battleController;
    [SerializeField] private string barkConversationId; // id of conversation pool of barks

    private string _currentDevilId;
    private ScopedEventBus battleBus;

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
    }

    private void OnBattleStarted(BattleStartedEvent @event)
    {
        BattleState battle = battleController != null ? battleController.BattleState : null;
        if (battle == null) return;
        _currentDevilId = battle.Config.DevilId;

        battleBus = battle.EventBus;
        battleBus.Subscribe<RoundStartedEvent>(OnRoundStarted);
        battleBus.Subscribe<RoundEndedEvent>(OnRoundEnded);
        battleBus.Subscribe<DevilTurnChoiceEvent>(OnDevilTurnChoice);
        battleBus.Subscribe<BurstAttemptedEvent>(OnBurstAttempted);
        // TODO: add poker events here later

        barkConversationId = $"{_currentDevilId}_BattleStart";
        DialogueManager.Bark(barkConversationId, transform);
        // TODO: delayed second bark
    }

    private void OnRoundStarted(RoundStartedEvent @event)
    {
        barkConversationId = $"{_currentDevilId}_RoundStart";
        DialogueManager.Bark(barkConversationId, transform);
    }

    private void OnRoundEnded(RoundEndedEvent @event)
    {
        // TODO: add IDevilStrategy interface to get the correct conversation ID for round end
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
        _currentDevilId = null;

        battleBus.Unsubscribe<RoundStartedEvent>(OnRoundStarted);
        battleBus.Unsubscribe<RoundEndedEvent>(OnRoundEnded);
        battleBus.Unsubscribe<DevilTurnChoiceEvent>(OnDevilTurnChoice);
        battleBus.Unsubscribe<BurstAttemptedEvent>(OnBurstAttempted);
        battleBus = null;
    }
}

// TODO: For all DialogueManager.Bark calls, we need to check the game state to override the affinity variable based on money left
// bool affinity = DialogueLua.GetVariable(”OverrideToMidAffinity”).asBool;
// DialogueLua.SetVariable(”OverrideToMidAffinity”, true)
// mid and low override should be explicitly set (no 2 trues at the same time)