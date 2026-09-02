using System.Collections;
using UnityEngine;
using TMPro;
using DG.Tweening;
using EasyTextEffects.Editor.MyBoxCopy.Extensions;
using System.Collections.Generic;
using DamageNumbersPro;

public class MoneyTransferLogger : MonoBehaviour
{
    [SerializeField] private GameObject loggerPanel;
    [SerializeField] private RectTransform defaultPanelPosition;
    [SerializeField] private RectTransform roundEndPanelPosition;
    [SerializeField] private TMP_Text entryFeeText;
    [SerializeField] private TMP_Text blackjackResultText;
    [SerializeField] private TMP_Text burstPenaltyText;
    [SerializeField] private TMP_Text pokerResultText;
    [SerializeField] private BattleController battleController;

    [Header("Text Effects")]
    [SerializeField] private List<RectTransform> textEffectTargets; // List of RectTransforms for text effects
    [SerializeField] private DamageNumber damageNumber;
    [SerializeField, Min(0f)] private float textEffectDelaySeconds = 0.2f;

    private ScopedEventBus subscribedBattleBus;
    private readonly Queue<TextEffectRequest> pendingTextEffects = new();
    private Coroutine textEffectRoutine;
    private int roundResult;
    private int burstPenalty;
    private int pokerResult;

    private readonly struct TextEffectRequest
    {
        public readonly RectTransform Target;
        public readonly Vector2 Offset;
        public readonly float Amount;

        public TextEffectRequest(RectTransform target, Vector2 offset, float amount)
        {
            Target = target;
            Offset = offset;
            Amount = amount;
        }
    }

    void Awake()
    {
        battleController ??= FindFirstObjectByType<BattleController>();

        if (entryFeeText == null || blackjackResultText == null || burstPenaltyText == null || pokerResultText == null)
        {
            Debug.LogError("One or more TMP_Text references are not assigned in the inspector.");
            return;
        }

        ResetTexts();
    }

    void ResetTexts()
    {
        ClearPendingTextEffects();

        entryFeeText.text = "0";
        entryFeeText.color = Color.white; // Reset color to default
        blackjackResultText.text = "0";
        blackjackResultText.color = Color.white; // Reset color to default
        roundResult = 0;
        burstPenaltyText.text = "0";
        burstPenaltyText.color = Color.white; // Reset color to default
        burstPenalty = 0;
        pokerResultText.text = "0";
        pokerResultText.color = Color.white; // Reset color to default
        pokerResult = 0;
        // TODO: Reset text effects if any are applied (e.g., animations, scaling, etc.)
    }

    void OnEnable()
    {
        global::EventBus.Subscribe<BattleStartedEvent>(OnBattleStarted);
        global::EventBus.Subscribe<MoneyTransferReasonEvent>(OnMoneyTransfer);
        global::EventBus.Subscribe<ShopClosedEvent>(OnShopClosed);
        BindBattleBus();
        ApplyRestoredRoundPresentation();
    }

    void OnDisable()
    {
        global::EventBus.Unsubscribe<BattleStartedEvent>(OnBattleStarted);
        global::EventBus.Unsubscribe<MoneyTransferReasonEvent>(OnMoneyTransfer);
        global::EventBus.Unsubscribe<ShopClosedEvent>(OnShopClosed);
        UnbindBattleBus();
        ClearPendingTextEffects();
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
        subscribedBattleBus.Subscribe<RoundStartedEvent>(OnRoundStarted);
        subscribedBattleBus.Subscribe<RoundEndedEvent>(OnRoundEnded);
    }
    private void UnbindBattleBus()
    {
        if (subscribedBattleBus == null)
            return;

        subscribedBattleBus.Unsubscribe<RoundStartedEvent>(OnRoundStarted);
        subscribedBattleBus.Unsubscribe<RoundEndedEvent>(OnRoundEnded);
        subscribedBattleBus = null;
    }

    private void OnRoundStarted(RoundStartedEvent eventData)
    {
        ResetTexts();
    }

    private void OnBattleStarted(BattleStartedEvent eventData)
    {
        BindBattleBus();
        if (!ApplyRestoredRoundPresentation())
            TweenToDefaultPosition();
    }

    private void OnRoundEnded(RoundEndedEvent eventData)
    {
        TweenToRoundEndPosition();
    }

    private void OnShopClosed(ShopClosedEvent @event)
    {
        ResetTexts();
        TweenToDefaultPosition();
    }

    private bool ApplyRestoredRoundPresentation()
    {
        BattleState battle = battleController != null ? battleController.BattleState : null;
        if (battle == null
            || battle.Phase != BattlePhase.PostRound
            || !battleController.IsWaitingForVisuals)
            return false;

        TweenToRoundEndPosition();
        return true;
    }

    private void TweenToRoundEndPosition()
    {
        if (roundEndPanelPosition == null || loggerPanel == null)
            return;

        RectTransform loggerRect = loggerPanel.transform.As<RectTransform>();
        loggerRect.DOKill();
        loggerRect.DOAnchorPos(roundEndPanelPosition.anchoredPosition, 0.5f).SetEase(Ease.InOutQuad);
        loggerRect.DOScale(Vector3.one * 1.5f, 0.25f).SetEase(Ease.InOutQuad);
    }

    private void TweenToDefaultPosition()
    {
        if (defaultPanelPosition == null || loggerPanel == null)
            return;

        RectTransform loggerRect = loggerPanel.transform.As<RectTransform>();
        loggerRect.DOKill();
        loggerRect.DOAnchorPos(defaultPanelPosition.anchoredPosition, 0.5f).SetEase(Ease.InOutQuad);
        loggerRect.DOScale(Vector3.one, 0.25f).SetEase(Ease.InOutQuad);
    }

    private void OnMoneyTransfer(MoneyTransferReasonEvent eventData)
    {
        switch (eventData.Reason)
        {
            case MoneyTransferReason.InitialWager:
                entryFeeText.text = NumberFormatter.Abbreviate(eventData.Delta);
                QueueTextEffect(0, eventData.Delta);
                break;
            case MoneyTransferReason.BlackjackPayout:
                roundResult += eventData.Delta;
                QueueTextEffect(1, eventData.Delta);
                RefreshRoundResultText();
                break;
            case MoneyTransferReason.DevilAbilityPayout:
                roundResult -= eventData.Delta;
                QueueTextEffect(2, -eventData.Delta);
                RefreshRoundResultText();
                break;
            case MoneyTransferReason.BurstPenalty:
                if (eventData.Receiver == Combatant.Opponent)
                {
                    burstPenalty -= eventData.Delta;
                    QueueTextEffect(3, -eventData.Delta);
                }
                else if (eventData.Receiver == Combatant.Player)
                {
                    burstPenalty += eventData.Delta;
                    QueueTextEffect(3, eventData.Delta);
                }
                burstPenaltyText.text = NumberFormatter.Abbreviate(burstPenalty);
                if (burstPenalty < 0)
                    burstPenaltyText.color = Color.red;
                else
                    burstPenaltyText.color = Color.green;
                break;
            case MoneyTransferReason.PokerPayout:
                pokerResult += eventData.Delta;
                QueueTextEffect(4, eventData.Delta);
                pokerResultText.text = NumberFormatter.Abbreviate(pokerResult);
                if (pokerResult < 0)
                    pokerResultText.color = Color.red;
                else
                    pokerResultText.color = Color.green;
                break;
            default:
                Debug.LogWarning($"Unhandled money transfer reason: {eventData.Reason}");
                break;
        }
    }

    private void QueueTextEffect(int targetIndex, float amount)
    {
        if (damageNumber == null
            || textEffectTargets == null
            || targetIndex < 0
            || targetIndex >= textEffectTargets.Count
            || textEffectTargets[targetIndex] == null)
            return;

        pendingTextEffects.Enqueue(new TextEffectRequest(textEffectTargets[targetIndex], Vector2.zero, amount));

        if (textEffectRoutine == null && isActiveAndEnabled)
            textEffectRoutine = StartCoroutine(PlayQueuedTextEffects());
    }

    private IEnumerator PlayQueuedTextEffects()
    {
        while (pendingTextEffects.Count > 0)
        {
            TextEffectRequest request = pendingTextEffects.Dequeue();

            if (textEffectDelaySeconds > 0f)
                yield return new WaitForSeconds(textEffectDelaySeconds);
            else
                yield return null;

            damageNumber.SpawnGUI(request.Target, request.Offset, request.Amount);
        }

        textEffectRoutine = null;
    }

    private void ClearPendingTextEffects()
    {
        if (textEffectRoutine != null)
            StopCoroutine(textEffectRoutine);

        textEffectRoutine = null;
        pendingTextEffects.Clear();
    }

    private void RefreshRoundResultText()
    {
        blackjackResultText.text = NumberFormatter.Abbreviate(roundResult);
        blackjackResultText.color = roundResult < 0 ? Color.red : Color.green;
        if (roundResult == 0) blackjackResultText.color = Color.white; // Reset to default color if zero
    }

    public void HideLoggerPanel()
    {
        if (loggerPanel != null)
        {
            loggerPanel.GetComponent<CanvasGroup>().DOFade(0f, 0.5f);
        }
    }

    public void ShowLoggerPanel()
    {
        if (loggerPanel != null)
        {
            loggerPanel.GetComponent<CanvasGroup>().DOFade(1f, 0.5f);
        }
    }

}
