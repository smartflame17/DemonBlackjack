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

    private ScopedEventBus subscribedBattleBus;
    private int roundResult;
    private int blackjackResult;
    private int burstPenalty;
    private int pokerResult;

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
        entryFeeText.text = "0";
        entryFeeText.color = Color.white; // Reset color to default
        blackjackResultText.text = "0";
        blackjackResultText.color = Color.white; // Reset color to default
        roundResult = 0;
        burstPenaltyText.text = "0";
        burstPenaltyText.color = Color.white; // Reset color to default
        pokerResultText.text = "0";
        pokerResultText.color = Color.white; // Reset color to default
        // TODO: Reset text effects if any are applied (e.g., animations, scaling, etc.)
    }

    void OnEnable()
    {
        global::EventBus.Subscribe<MoneyTransferReasonEvent>(OnMoneyTransfer);
        global::EventBus.Subscribe<ShopClosedEvent>(OnShopClosed);
        BindBattleBus();
    }

    void OnDisable()
    {
        global::EventBus.Unsubscribe<MoneyTransferReasonEvent>(OnMoneyTransfer);
        global::EventBus.Unsubscribe<ShopClosedEvent>(OnShopClosed);
        UnbindBattleBus();
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

    private void OnRoundEnded(RoundEndedEvent eventData)
    {
        // Move the logger panel to the round end position
        if (roundEndPanelPosition != null && loggerPanel != null)
        {
            loggerPanel.transform.As<RectTransform>().DOAnchorPos(roundEndPanelPosition.anchoredPosition, 0.5f).SetEase(Ease.InOutQuad);
            loggerPanel.transform.As<RectTransform>().DOScale(Vector3.one * 1.5f, 0.25f).SetEase(Ease.InOutQuad);
        }
    }

    private void OnShopClosed(ShopClosedEvent @event)
    {
        ResetTexts();
        // Move the logger panel to the default position
        if (defaultPanelPosition != null && loggerPanel != null)
        {
            loggerPanel.transform.As<RectTransform>().DOAnchorPos(defaultPanelPosition.anchoredPosition, 0.5f).SetEase(Ease.InOutQuad);
            loggerPanel.transform.As<RectTransform>().DOScale(Vector3.one, 0.25f).SetEase(Ease.InOutQuad);
        }
    }

    private void OnMoneyTransfer(MoneyTransferReasonEvent eventData)
    {
        // Create a damage number for the transfer
        Vector2 offset = new Vector2(0, 0); // You can customize this offset if needed

        // update corresponding text fields
        switch (eventData.Reason)
        {
            case MoneyTransferReason.InitialWager:
                entryFeeText.text = $"{eventData.Delta}";
                damageNumber.SpawnGUI(textEffectTargets[0],offset, (float)eventData.Delta);
                break;
            case MoneyTransferReason.BlackjackPayout:
                roundResult += eventData.Delta;
                damageNumber.SpawnGUI(textEffectTargets[1],offset, (float)eventData.Delta);
                RefreshRoundResultText();
                break;
            case MoneyTransferReason.DevilAbilityPayout:
                roundResult -= eventData.Delta;
                damageNumber.SpawnGUI(textEffectTargets[2],offset, (float)-eventData.Delta);
                RefreshRoundResultText();
                break;
            case MoneyTransferReason.BurstPenalty:
                burstPenalty += eventData.Delta;
                damageNumber.SpawnGUI(textEffectTargets[3],offset, (float)-eventData.Delta);
                burstPenaltyText.text = $"-{burstPenalty}";
                if (burstPenalty > 0)
                    burstPenaltyText.color = Color.red; // Set color to red for negative values
                else
                    burstPenaltyText.color = Color.green; // Set color to green for positive values
                break;
            case MoneyTransferReason.PokerPayout:
                pokerResult += eventData.Delta;
                damageNumber.SpawnGUI(textEffectTargets[4],offset, (float)eventData.Delta);
                pokerResultText.text = $"{pokerResult}";
                if (pokerResult < 0)
                    pokerResultText.color = Color.red; // Set color to red for negative values
                else
                    pokerResultText.color = Color.green; // Set color to green for positive values
                break;
            default:
                Debug.LogWarning($"Unhandled money transfer reason: {eventData.Reason}");
                break;
        }
    }

    private void RefreshRoundResultText()
    {
        blackjackResultText.text = $"{roundResult}";
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
