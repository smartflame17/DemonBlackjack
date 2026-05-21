using UnityEngine;

public class BattleController : MonoBehaviour
{
    [SerializeField] private bool startFirstRoundOnInitialize = true;
    [SerializeField] private bool autoStartNextRoundAfterVisuals = true;

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

        if (startFirstRoundOnInitialize)
            StartNextRound();
    }

    public void StartNextRound()
    {
        StartNextRound(-1);
    }

    public bool StartNextRound(int wager)
    {
        if (BattleState == null || BattleState.IsBattleOver || IsWaitingForVisuals)
            return false;

        return BattleState.StartRound(wager);
    }

    public bool TryPlayCard(int handIndex)
    {
        return !IsWaitingForVisuals && BattleState != null && BattleState.TryPlayCard(handIndex);
    }

    public bool TryHit()
    {
        return !IsWaitingForVisuals && BattleState != null && BattleState.TryHit();
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

            if (BattleState == activeBattle && !activeBattle.IsBattleOver && autoStartNextRoundAfterVisuals)
                StartNextRound();
        }
    }

    public void CleanupBattle()
    {
        if (BattleState != null)
            BattleState.EventBus.Unsubscribe<BattleEndedEvent>(OnBattleEnded);

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

    private void PublishPendingBattleEndedEvent()
    {
        if (!_pendingBattleEndedEvent.HasValue)
            return;

        BattleEndedEvent eventData = _pendingBattleEndedEvent.Value;
        _pendingBattleEndedEvent = null;
        EventBus.Publish(eventData);
    }
}
