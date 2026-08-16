using UnityEngine;
using GameplayConstants;

public class RunManager : MonoBehaviour, IDataPersistence
{
    [SerializeField] private BattleController battleController;
    [SerializeField] private bool startRunOnAwake = true;
    [SerializeField] private bool persistenceEnabled = true;
    [SerializeField] private bool setRandomSeed = false;
    [SerializeField] private int debugSeed = 12345;
    private int startingMoney = GameplayConstants.GameSettingConfig.PlayerStartingMoney;
    [SerializeField] private int defaultOpponentStartingMoney = 100;

    public RunState RunState { get; private set; }
    private BattleState checkpointBattle;

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
        UnsubscribeFromBattleCheckpoint();
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
        UnsubscribeFromBattleCheckpoint();
        battleController?.CleanupBattle();
        int runStartingMoney = startingMoney <= 0 ? 100 : startingMoney;
        RunState = new RunState(seed, runStartingMoney);
        Debug.Log("Run started with seed: " + seed);
        SetPhase(RunPhase.Init);
        SetPhase(RunPhase.Map);
    }

    public void StartOrLoadRun(int seed)
    {
        if (persistenceEnabled
            && PersistenceManager.Instance != null
            && PersistenceManager.Instance.TryLoadRunState(out RunState loadedRunState))
        {
            BattleStateData battleData = PersistenceManager.Instance.GetGameData()?.runState?.battleState;
            ApplyLoadedRunState(loadedRunState, battleData);
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

        UnsubscribeFromBattleCheckpoint();
        int opponentStartingMoney = defaultOpponentStartingMoney <= 0 ? 100 : defaultOpponentStartingMoney;
        BattleConfig battleConfig = config ?? new BattleConfig($"encounter_{RunState.EncounterIndex + 1}", opponentStartingMoney);
        battleController.InitializeBattle(RunState, battleConfig);
        SetPhase(RunPhase.Battle);
        BattleState battle = battleController.BattleState;
        SubscribeToBattleCheckpoint(battle);

        if (persistenceEnabled)
            PersistenceManager.Instance?.SaveGame();

        EventBus.Publish(new BattleStartedEvent(
            battle.Config.EncounterId,
            battle.BattleSeed,
            battle.PlayerMoney,
            battle.OpponentMoney));
        Debug.Log("Battle started against " + battleConfig.EncounterId + ", event published");

        SoundManager.Instance?.PlayBGM(EBgm.GAME);
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
        if (SoundManager.Instance != null && SoundManager.Instance.CurrentBGM != EBgm.NONE)
            SoundManager.Instance?.FadeOutBGM(1.0f);
    }

    public bool OpenShop()
    {
        BattleState battle = battleController != null ? battleController.BattleState : null;
        if (RunState == null
            || battle == null
            || battle.IsBattleOver
            || (battle.Phase != BattlePhase.Cleanup && battle.Phase != BattlePhase.PostRound))
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
        if (battleController.BattleState == null)
            return true;

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
        if (battleController.BattleState == null)
            return true;

        SetPhase(RunPhase.Battle);
        EventBus.Publish(new ShopClosedEvent(roundNumber));
        return true;
    }

    private void OnBattleEnded(BattleEndedEvent eventData)
    {
        if (RunState == null)
            return;

        UnsubscribeFromBattleCheckpoint();
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
        if (!persistenceEnabled)
            return;

        RunState loadedRunState = data != null ? RunState.FromData(data.runState, startingMoney) : null;
        if (loadedRunState != null)
            ApplyLoadedRunState(loadedRunState, data.runState?.battleState);
    }

    public void SaveData(GameData data)
    {
        if (!persistenceEnabled || data == null || RunState == null)
            return;

        BattleState activeBattle = battleController != null
            ? battleController.BattleState
            : null;
        data.runState = RunState.ToDataWithBattleState(activeBattle);
    }

    private void ApplyLoadedRunState(RunState loadedRunState, BattleStateData battleData)
    {
        UnsubscribeFromBattleCheckpoint();
        RunState = loadedRunState;

        if (persistenceEnabled
            && battleData != null
            && battleController != null
            && battleController.RestoreBattle(RunState, battleData))
        {
            BattleState restoredBattle = battleController.BattleState;
            SubscribeToBattleCheckpoint(restoredBattle);
            SetPhase(RunPhase.Battle);
            EventBus.Publish(new BattleStartedEvent(
                restoredBattle.Config.EncounterId,
                restoredBattle.BattleSeed,
                restoredBattle.PlayerMoney,
                restoredBattle.OpponentMoney));
            SoundManager.Instance?.PlayBGM(EBgm.GAME);
            Debug.Log($"Restored battle {restoredBattle.Config.EncounterId} at round {restoredBattle.RoundNumber} PostRound.");
            return;
        }

        battleController?.CleanupBattle();
        if (battleData != null)
            Debug.LogWarning("Saved battle checkpoint was invalid and has been discarded; returning to the map.");

        if (RunState.Phase == RunPhase.Inactive
            || RunState.Phase == RunPhase.Init
            || RunState.Phase == RunPhase.Battle
            || RunState.Phase == RunPhase.Shop)
            SetPhase(RunPhase.Map);
        else
            EventBus.Publish(new RunPhaseChangedEvent(RunState.Phase));
    }

    private void SubscribeToBattleCheckpoint(BattleState battle)
    {
        if (!persistenceEnabled || battle == null || ReferenceEquals(checkpointBattle, battle))
            return;

        UnsubscribeFromBattleCheckpoint();
        checkpointBattle = battle;
        checkpointBattle.RoundCheckpointReady += OnRoundCheckpointReady;
    }

    private void UnsubscribeFromBattleCheckpoint()
    {
        if (checkpointBattle != null)
            checkpointBattle.RoundCheckpointReady -= OnRoundCheckpointReady;
        checkpointBattle = null;
    }

    private void OnRoundCheckpointReady(BattleState battle)
    {
        if (!persistenceEnabled || !ReferenceEquals(checkpointBattle, battle))
            return;

        PersistenceManager.Instance?.SaveGame();
    }
}
