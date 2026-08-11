using UnityEngine;
using UnityEngine.UI;

public sealed class ScoreProgressionSlider : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private Image fillImage;
    [SerializeField] private BattleController battleController;
    [SerializeField] private Combatant combatant = Combatant.Player;

    private ScopedEventBus subscribedBattleBus;
    private int lastScore;
    private int lastThreshold = 1;

    private void Awake()
    {
        slider ??= GetComponent<Slider>();
        battleController ??= FindFirstObjectByType<BattleController>();

        if (slider == null)
            Debug.LogError("Score progression slider is not assigned.", this);
    }

    private void OnEnable()
    {
        EventBus.Subscribe<BattleStartedEvent>(OnBattleStarted);
        EventBus.Subscribe<BattleEndedEvent>(OnBattleEnded);
        EventBus.Subscribe<ItemUsedEvent>(OnItemUsed);
        BindBattleBus();
        RefreshFromBattleState();
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<BattleStartedEvent>(OnBattleStarted);
        EventBus.Unsubscribe<BattleEndedEvent>(OnBattleEnded);
        EventBus.Unsubscribe<ItemUsedEvent>(OnItemUsed);
        UnbindBattleBus();
        SetProgress(0f);
    }

    private void OnBattleStarted(BattleStartedEvent eventData)
    {
        BindBattleBus();
        RefreshFromBattleState();
    }

    private void OnBattleEnded(BattleEndedEvent eventData)
    {
        UnbindBattleBus();
        lastScore = 0;
        lastThreshold = 1;
        SetProgress(0f);
    }

    private void OnItemUsed(ItemUsedEvent eventData)
    {
        RefreshFromBattleState();
    }

    private void BindBattleBus()
    {
        ScopedEventBus battleBus = battleController != null ? battleController.BattleState?.EventBus : null;
        if (ReferenceEquals(subscribedBattleBus, battleBus))
            return;

        UnbindBattleBus();

        if (battleBus == null)
            return;

        subscribedBattleBus = battleBus;
        subscribedBattleBus.Subscribe<ScoreCalculatedEvent>(OnScoreCalculated);
        subscribedBattleBus.Subscribe<BurstThresholdChangedEvent>(OnBurstThresholdChanged);
        subscribedBattleBus.Subscribe<RoundStartedEvent>(OnRoundStarted);
    }

    private void UnbindBattleBus()
    {
        if (subscribedBattleBus == null)
            return;

        subscribedBattleBus.Unsubscribe<ScoreCalculatedEvent>(OnScoreCalculated);
        subscribedBattleBus.Unsubscribe<BurstThresholdChangedEvent>(OnBurstThresholdChanged);
        subscribedBattleBus.Unsubscribe<RoundStartedEvent>(OnRoundStarted);
        subscribedBattleBus = null;
    }

    private void OnScoreCalculated(ScoreCalculatedEvent eventData)
    {
        if (eventData.Combatant != combatant)
            return;

        lastScore = eventData.Score.BlackjackScore;
        lastThreshold = GetCurrentThreshold();
        RefreshProgress();
    }

    private void OnBurstThresholdChanged(BurstThresholdChangedEvent eventData)
    {
        if (eventData.Combatant != combatant)
            return;

        lastThreshold = eventData.Threshold;
        RefreshProgress();
    }

    private void OnRoundStarted(RoundStartedEvent eventData)
    {
        RefreshFromBattleState();
    }

    private void RefreshFromBattleState()
    {
        RoundState round = battleController != null ? battleController.BattleState?.CurrentRound : null;
        if (round == null)
        {
            lastScore = 0;
            lastThreshold = 1;
            SetProgress(0f);
            return;
        }

        ScoreResult score = combatant == Combatant.Player ? round.PlayerScore : round.OpponentScore;
        lastScore = score.BlackjackScore;
        lastThreshold = round.GetBurstThreshold(combatant);
        RefreshProgress();
    }

    private int GetCurrentThreshold()
    {
        RoundState round = battleController != null ? battleController.BattleState?.CurrentRound : null;
        return round != null ? round.GetBurstThreshold(combatant) : lastThreshold;
    }

    private void RefreshProgress()
    {
        SetProgress(lastThreshold > 0 ? (float)lastScore / lastThreshold : 0f);
    }

    public void SetProgress(float progress)
    {
        if (slider == null)
            return;

        slider.value = Mathf.Clamp01(progress);
    }

    public void SetFillColor(Color color)
    {
        if (fillImage == null)
            return;

        fillImage.color = color;
    }
}
