using UnityEngine;
using TMPro;
using EasyTextEffects;

public class ScoreThresholdAnimator : MonoBehaviour
{
    [SerializeField] private TMP_Text _scoreText;
    [SerializeField] private TMP_Text _scoreThresholdText;
    [SerializeField] private BattleController battleController;
    [SerializeField] private Combatant combatant = Combatant.Player;
    [SerializeField] private Color burstColor = Color.red;
    [SerializeField] private TextEffect textEffect;

    private const string ScoreTextShakeEffect = "scoretext_shake";

    private ScopedEventBus subscribedBattleBus;
    private Color defaultColor = Color.white;
    private bool isOverThreshold;
    
    private void Awake()
    {
        battleController ??= FindFirstObjectByType<BattleController>();
        _scoreText ??= GetComponent<TMP_Text>();
        textEffect ??= GetComponent<TextEffect>();

        if (_scoreText == null)
            Debug.LogError("Score text is not assigned.", this);
        if (_scoreThresholdText == null)
            Debug.LogError("Score threshold text is not assigned.", this);

        if (_scoreText != null)
            defaultColor = _scoreText.color;
    }

    private void OnEnable()
    {
        EventBus.Subscribe<BattleStartedEvent>(OnBattleStarted);
        EventBus.Subscribe<BattleEndedEvent>(OnBattleEnded);
        BindBattleBus();
        EvaluateFromText();
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<BattleStartedEvent>(OnBattleStarted);
        EventBus.Unsubscribe<BattleEndedEvent>(OnBattleEnded);
        UnbindBattleBus();
        SetOverThreshold(false);
    }

    private void OnBattleStarted(BattleStartedEvent eventData)
    {
        BindBattleBus();
        EvaluateFromText();
    }

    private void OnBattleEnded(BattleEndedEvent eventData)
    {
        UnbindBattleBus();
        SetOverThreshold(false);
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

        Evaluate(eventData.Score.BlackjackScore, GetThresholdFromText());
    }

    private void OnBurstThresholdChanged(BurstThresholdChangedEvent eventData)
    {
        if (eventData.Combatant != combatant)
            return;

        Evaluate(GetScoreFromText(), eventData.Threshold);
    }

    private void OnRoundStarted(RoundStartedEvent eventData)
    {
        SetOverThreshold(false);
    }

    private void EvaluateFromText()
    {
        Evaluate(GetScoreFromText(), GetThresholdFromText());
    }

    private void Evaluate(int score, int threshold)
    {
        SetOverThreshold(score > threshold);
    }

    private void SetOverThreshold(bool overThreshold)
    {
        if (isOverThreshold == overThreshold)
            return;

        isOverThreshold = overThreshold;

        if (_scoreText != null)
            _scoreText.color = overThreshold ? burstColor : defaultColor;

        if (textEffect == null)
            return;

        if (overThreshold)
            textEffect.StartManualEffect(ScoreTextShakeEffect);
        else
            textEffect.StopManualEffects();
    }

    private int GetScoreFromText()
    {
        return TryParseText(_scoreText, 0);
    }

    private int GetThresholdFromText()
    {
        return TryParseText(_scoreThresholdText, int.MaxValue);
    }

    private static int TryParseText(TMP_Text text, int fallback)
    {
        if (text == null || string.IsNullOrWhiteSpace(text.text))
            return fallback;

        return int.TryParse(text.text, out int value) ? value : fallback;
    }
}
