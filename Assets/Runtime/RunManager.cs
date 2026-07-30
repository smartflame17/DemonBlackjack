using UnityEngine;
using GameplayConstants;

public class RunManager : MonoBehaviour, IDataPersistence
{
    [SerializeField] private BattleController battleController;
    [SerializeField] private bool startRunOnAwake = true;
    [SerializeField] private bool setRandomSeed = false;
    [SerializeField] private int debugSeed = 12345;
    private int startingMoney = GameplayConstants.GameSettingConfig.PlayerStartingMoney;
    [SerializeField] private int defaultOpponentStartingMoney = 100;

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
        {
            int Seed;

            if (setRandomSeed)
                Seed = System.Environment.TickCount;
            else Seed = debugSeed;

            if (PersistenceManager.Instance != null && PersistenceManager.Instance.PlayerSetSeed != -1)
                Seed = PersistenceManager.Instance.PlayerSetSeed;
            StartOrLoadRun(Seed);
        }
    }

    public void StartRun(int seed)
    {
        int runStartingMoney = startingMoney <= 0 ? 100 : startingMoney;
        RunState = new RunState(seed, runStartingMoney);
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

        int opponentStartingMoney = defaultOpponentStartingMoney <= 0 ? 100 : defaultOpponentStartingMoney;
        BattleConfig battleConfig = config ?? new BattleConfig($"encounter_{RunState.EncounterIndex + 1}", opponentStartingMoney);
        battleController.InitializeBattle(RunState, battleConfig);
        SetPhase(RunPhase.Battle);
        EventBus.Publish(new BattleStartedEvent(battleConfig.EncounterId, RunState.CreateBattleSeed(), RunState.Money, battleConfig.OpponentStartingMoney));
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
        Debug.Log($"Returning to map. Money: {RunState.Money}, Encounter Index: {RunState.EncounterIndex}");
    }

    public bool OpenShop()
    {
        BattleState battle = battleController != null ? battleController.BattleState : null;
        if (RunState == null || battle == null || battle.IsBattleOver || battle.Phase != BattlePhase.Cleanup)
            return false;

        SetPhase(RunPhase.Shop);
        EventBus.Publish(new ShopOpenedEvent(RunState.EncounterIndex, battle.RoundNumber));
        return true;
    }

    public bool ContinueImmediatelyAfterRound()
    {
        if (RunState == null || RunState.Phase != RunPhase.Battle || battleController == null)
            return false;
        int roundNumber = battleController.BattleState != null ? battleController.BattleState.RoundNumber : 0;
        battleController.CompletePendingVisualTransition();
        SetPhase(RunPhase.Battle);
        EventBus.Publish(new ShopClosedEvent(roundNumber));     // We publish event here for compatibility with existing code that expects a ShopClosedEvent after a round ends, even if no shop was opened.
        return true;
    }

    public bool ContinueFromShop()
    {
        if (RunState == null || RunState.Phase != RunPhase.Shop || battleController == null)
            return false;

        int roundNumber = battleController.BattleState != null ? battleController.BattleState.RoundNumber : 0;
        battleController.CompletePendingVisualTransition();
        SetPhase(RunPhase.Battle);
        EventBus.Publish(new ShopClosedEvent(roundNumber));
        return true;
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
        RunState loadedRunState = data != null ? RunState.FromData(data.runState, startingMoney) : null;
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
