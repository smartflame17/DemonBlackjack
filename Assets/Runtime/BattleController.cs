using UnityEngine;

public class BattleController : MonoBehaviour
{
    public BattleState BattleState { get; private set; }
    public CommandQueue CommandQueue => BattleState?.CommandQueue;
    public bool HasActiveBattle => BattleState != null && !BattleState.IsBattleOver;
    public bool IsWaitingForVisuals { get; private set; }

    private BattleEndedEvent? _pendingBattleEndedEvent;

    public void InitializeBattle(RunState runState, BattleConfig config)
    {
        CleanupBattle();

        BattleState = new BattleState(runState, config);
        BattleState.EventBus.Subscribe<BattleEndedEvent>(OnBattleEnded);
        BattleState.Initialize();
    }

    public bool StartNextRound(int wager)
    {
        if (BattleState == null || BattleState.IsBattleOver || IsWaitingForVisuals)
            return false;

        bool started = BattleState.StartRound(wager);
        if (started)
            Debug.Log($"<color=blue>[Round]</color> Wager: {wager}, Player phase started");
        return started;
    }

    public bool TryPlayCard(int handIndex)
    {
        return !IsWaitingForVisuals && BattleState != null && BattleState.TryPlayCard(handIndex);
    }

    public bool TryHit()
    {
        if (IsWaitingForVisuals || BattleState == null)
            return false;

        BattleState activeBattle = BattleState;
        bool result = activeBattle.TryHit();
        MarkWaitingForCompletedRound(activeBattle);
        return result;
    }

    public bool TryStand()
    {
        if (IsWaitingForVisuals || BattleState == null)
            return false;

        BattleState activeBattle = BattleState;
        bool result = activeBattle.TryStand();
        MarkWaitingForCompletedRound(activeBattle);
        return result;
    }

    public bool TryUseActiveItem(string itemId)
    {
        return !IsWaitingForVisuals && BattleState != null && BattleState.TryUseActiveItem(itemId);
    }

    public bool CanUseActiveItem(string itemId)
    {
        return !IsWaitingForVisuals && BattleState != null && BattleState.CanUseActiveItem(itemId);
    }

    public RoundResolution EndPlayerPhase()
    {
        if (BattleState == null || IsWaitingForVisuals)
            return default;

        BattleState activeBattle = BattleState;
        RoundResolution resolution = activeBattle.EndPlayerPhase();

        if (BattleState == activeBattle && (activeBattle.Phase == BattlePhase.Cleanup || activeBattle.Phase == BattlePhase.BattleEnd))
            IsWaitingForVisuals = true;

        return resolution;
    }

    public bool TryDequeueVisualCommand(out VisualCommand command)
    {
        if (CommandQueue == null)
        {
            command = default;
            return false;
        }

        return CommandQueue.TryDequeue(out command);
    }

    public void NotifyVisualsResolved(VisualCommandType commandType)
    {
        BattleState?.EventBus.Publish(new VisualsResolvedEvent(commandType));
    }

    public void CompletePendingVisualTransition()
    {
        if (!IsWaitingForVisuals || BattleState == null)
            return;

        BattleState activeBattle = BattleState;
        IsWaitingForVisuals = false;

        if (activeBattle.Phase == BattlePhase.BattleEnd)
        {
            PublishPendingBattleEndedEvent();
        }
        else if (activeBattle.Phase == BattlePhase.Cleanup)
        {
            activeBattle.CleanupRound();
        }
    }

    public void CleanupBattle()
    {
        if (BattleState != null)
        {
            BattleState.EventBus.Unsubscribe<BattleEndedEvent>(OnBattleEnded);
            BattleState.Dispose();
        }

        IsWaitingForVisuals = false;
        _pendingBattleEndedEvent = null;
        BattleState = null;
    }

    private void OnDestroy()
    {
        CleanupBattle();
    }

    private void OnBattleEnded(BattleEndedEvent eventData)
    {
        _pendingBattleEndedEvent = eventData;
        IsWaitingForVisuals = true;
    }

    private void MarkWaitingForCompletedRound(BattleState activeBattle)
    {
        if (BattleState == activeBattle && (activeBattle.Phase == BattlePhase.Cleanup || activeBattle.Phase == BattlePhase.BattleEnd))
            IsWaitingForVisuals = true;
    }

    private void PublishPendingBattleEndedEvent()
    {
        if (!_pendingBattleEndedEvent.HasValue)
            return;

        BattleEndedEvent eventData = _pendingBattleEndedEvent.Value;
        _pendingBattleEndedEvent = null;
        EventBus.Publish(eventData);
    }
}
