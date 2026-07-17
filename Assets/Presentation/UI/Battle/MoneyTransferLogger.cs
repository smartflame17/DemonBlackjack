using System;
using UnityEngine;
using TMPro;

public class MoneyTransferLogger : MonoBehaviour
{
    [SerializeField] private TMP_Text entryFeeText;
    [SerializeField] private TMP_Text blackjackResultText;
    [SerializeField] private TMP_Text burstPenaltyText;
    [SerializeField] private TMP_Text pokerResultText;
    [SerializeField] private BattleController battleController;

    private ScopedEventBus subscribedBattleBus;

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
        burstPenaltyText.text = "0";
        burstPenaltyText.color = Color.white; // Reset color to default
        pokerResultText.text = "0";
        pokerResultText.color = Color.white; // Reset color to default
        // TODO: Reset text effects if any are applied (e.g., animations, scaling, etc.)
    }

    void OnEnable()
    {
        global::EventBus.Subscribe<MoneyTransferReasonEvent>(OnMoneyTransfer);
        BindBattleBus();
    }

    void OnDisable()
    {
        global::EventBus.Unsubscribe<MoneyTransferReasonEvent>(OnMoneyTransfer);
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
    }
    private void UnbindBattleBus()
    {
        if (subscribedBattleBus == null)
            return;

        subscribedBattleBus.Unsubscribe<RoundStartedEvent>(OnRoundStarted);
        subscribedBattleBus = null;
    }

    private void OnRoundStarted(RoundStartedEvent eventData)
    {
        ResetTexts();
    }

    private void OnMoneyTransfer(MoneyTransferReasonEvent eventData)
    {
        switch (eventData.Reason)
        {
            case MoneyTransferReason.InitialWager:
                entryFeeText.text = $"-{eventData.Delta}";
                if (eventData.Delta > 0)
                    entryFeeText.color = Color.red; // Set color to red for negative values
                else
                    entryFeeText.color = Color.green; // Set color to green for positive values
                break;
            case MoneyTransferReason.BlackjackPayout:
                blackjackResultText.text = $"{eventData.Delta}";
                if (eventData.Delta < 0)
                    blackjackResultText.color = Color.red; // Set color to red for negative values
                else
                    blackjackResultText.color = Color.green; // Set color to green for positive values
                break;
            case MoneyTransferReason.BurstPenalty:
                burstPenaltyText.text = $"{eventData.Delta}";
                if (eventData.Delta < 0)
                    burstPenaltyText.color = Color.red; // Set color to red for negative values
                else
                    burstPenaltyText.color = Color.green; // Set color to green for positive values
                break;
            case MoneyTransferReason.PokerPayout:
                pokerResultText.text = $"{eventData.Delta}";
                if (eventData.Delta < 0)
                    pokerResultText.color = Color.red; // Set color to red for negative values
                else
                    pokerResultText.color = Color.green; // Set color to green for positive values
                break;
            default:
                Debug.LogWarning($"Unhandled money transfer reason: {eventData.Reason}");
                break;
        }
    }

}
