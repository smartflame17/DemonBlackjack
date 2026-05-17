using UnityEngine;

public class RunManager : MonoBehaviour
{
    [SerializeField] private BattleController battleController;
    [SerializeField] private bool startRunOnAwake = true;
    [SerializeField] private int debugSeed = 12345;
    [SerializeField] private int playerMaxHp = 100;
    [SerializeField] private int startingGold = 0;
    [SerializeField] private int defaultOpponentHp = 30;

    public RunState RunState { get; private set; }

    private void Awake()
    {
        if (battleController == null)
            battleController = FindFirstObjectByType<BattleController>();
    }

    private void OnEnable()
    {
        EventBus.Subscribe<BattleEndedEvent>(OnBattleEnded);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<BattleEndedEvent>(OnBattleEnded);
    }

    private void Start()
    {
        if (startRunOnAwake)
            StartRun(debugSeed);
    }

    public void StartRun(int seed)
    {
        RunState = new RunState(seed, playerMaxHp, startingGold);
        SetPhase(RunPhase.Init);
        SetPhase(RunPhase.Map);
    }

    public void StartBattle(BattleConfig config = null)
    {
        if (RunState == null)
            StartRun(debugSeed);

        if (battleController == null)
        {
            Debug.LogError("RunManager cannot start a battle without a BattleController.");
            return;
        }

        SetPhase(RunPhase.Battle);
        BattleConfig battleConfig = config ?? new BattleConfig($"encounter_{RunState.EncounterIndex + 1}", defaultOpponentHp);
        EventBus.Publish(new BattleStartedEvent(battleConfig.EncounterId, RunState.CreateBattleSeed(), RunState.PlayerHp, battleConfig.OpponentMaxHp));
        battleController.InitializeBattle(RunState, battleConfig);
    }

    public void AdvanceAfterEncounter()
    {
        if (RunState == null)
            return;

        SetPhase(RunPhase.Rewards);
    }

    private void OnBattleEnded(BattleEndedEvent eventData)
    {
        if (RunState == null)
            return;

        RunState.ApplyBattleResult(eventData.Result);
        battleController?.CleanupBattle();
        AdvanceAfterEncounter();
    }

    private void SetPhase(RunPhase phase)
    {
        RunState.SetPhase(phase);
        EventBus.Publish(new RunPhaseChangedEvent(phase));
    }
}
