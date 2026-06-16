using UnityEngine;

public class RunManager : MonoBehaviour, IDataPersistence
{
    [SerializeField] private BattleController battleController;
    [SerializeField] private bool startRunOnAwake = true;
    [SerializeField] private int debugSeed = 12345;
    [SerializeField] private int playerMaxHp = 100;     // deprecated
    [SerializeField] private int startingGold = 100;
    [SerializeField] private int defaultOpponentHp = 100;   // deprecated

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
            StartOrLoadRun(debugSeed);
    }

    public void StartRun(int seed)
    {
        int runStartingMoney = startingGold <= 0 ? 100 : startingGold;
        RunState = new RunState(seed, playerMaxHp, runStartingMoney);
        Debug.Log("Run started with seed: " + seed);
        SetPhase(RunPhase.Init);
        SetPhase(RunPhase.Map);
    }

    public void StartOrLoadRun(int seed)
    {
        if (PersistenceManager.Instance != null && PersistenceManager.Instance.TryLoadRunState(out RunState loadedRunState))
        {
            ApplyLoadedRunState(loadedRunState);
            Debug.Log("Run loaded with seed: " + RunState.Seed);
            return;
        }

        StartRun(seed);
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

        int opponentStartingMoney = defaultOpponentHp <= 0 ? 100 : defaultOpponentHp;
        BattleConfig battleConfig = config ?? new BattleConfig($"encounter_{RunState.EncounterIndex + 1}", opponentStartingMoney);
        battleController.InitializeBattle(RunState, battleConfig);
        SetPhase(RunPhase.Battle);
        EventBus.Publish(new BattleStartedEvent(battleConfig.EncounterId, RunState.CreateBattleSeed(), RunState.PlayerHp, battleConfig.OpponentMaxHp));
        Debug.Log("Battle started against " + battleConfig.EncounterId + ", event published");
    }

    public void AdvanceAfterEncounter()
    {
        if (RunState == null)
            return;

        SetPhase(RunPhase.Rewards);
    }

    public void ReturnToMap()
    {
        if (RunState == null)
            return;

        SetPhase(RunPhase.Map);
        Debug.Log($"Returning to map. Gold: {RunState.Gold}, Encounter Index: {RunState.EncounterIndex}");
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

    public void LoadData(GameData data)
    {
        RunState loadedRunState = data != null ? RunState.FromData(data.runState, playerMaxHp, startingGold) : null;
        if (loadedRunState != null)
            ApplyLoadedRunState(loadedRunState);
    }

    public void SaveData(GameData data)
    {
        if (data == null || RunState == null)
            return;

        data.runState = RunState.ToData();
    }

    private void ApplyLoadedRunState(RunState loadedRunState)
    {
        RunState = loadedRunState;

        if (RunState.Phase == RunPhase.Inactive || RunState.Phase == RunPhase.Init || RunState.Phase == RunPhase.Battle)
            SetPhase(RunPhase.Map);
        else
            EventBus.Publish(new RunPhaseChangedEvent(RunState.Phase));
    }
}
