using UnityEngine;

public class BattleController : MonoBehaviour
{
    [SerializeField] private bool startFirstRoundOnInitialize = true;

    public BattleState BattleState { get; private set; }
    public CommandQueue CommandQueue => BattleState?.CommandQueue;
    public bool HasActiveBattle => BattleState != null && !BattleState.IsBattleOver;

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
        if (BattleState == null || BattleState.IsBattleOver)
            return false;

        return BattleState.StartRound(wager);
    }

    public bool TryPlayCard(int handIndex)
    {
        return BattleState != null && BattleState.TryPlayCard(handIndex);
    }

    public bool TryHit()
    {
        return BattleState != null && BattleState.TryHit();
    }

    public RoundResolution EndPlayerPhase()
    {
        if (BattleState == null)
            return default;

        BattleState activeBattle = BattleState;
        RoundResolution resolution = activeBattle.EndPlayerPhase();

        if (BattleState == activeBattle && activeBattle.Phase == BattlePhase.Cleanup)
            activeBattle.CleanupRound();

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

    public void CleanupBattle()
    {
        if (BattleState != null)
            BattleState.EventBus.Unsubscribe<BattleEndedEvent>(OnBattleEnded);

        BattleState = null;
    }

    private void OnDestroy()
    {
        CleanupBattle();
    }

    private void OnBattleEnded(BattleEndedEvent eventData)
    {
        EventBus.Publish(eventData);
    }
}
