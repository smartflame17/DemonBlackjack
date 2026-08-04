using System;
using UnityEngine;

public class ShakeOnMoneyTransfer : MonoBehaviour
{
    [SerializeField] private BattleController battleController;
    [SerializeField] private int shakeThreshold = 1000; // Minimum amount of money change to trigger shake
    private BattleState _subscribedBattle;

    private void OnEnable()
    {
        if (battleController == null)
            battleController = FindFirstObjectByType<BattleController>();

        SubscribeToCurrentBattle();
        EventBus.Subscribe<MoneyChangedEvent>(OnMoneyChanged);
    }

    private void OnMoneyChanged(MoneyChangedEvent @event)
    {
        if (@event.Owner == Combatant.Opponent) return; // Only shake for player money changes
        if (Math.Abs(@event.Delta) < shakeThreshold) return; // Only shake if the money change exceeds the threshold
        float intensity = 0.5f + (Math.Abs(@event.Delta) / 1000f);
        CinemachineCameraShake.Instance?.ShakeCamera(intensity, 0.5f);
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void SubscribeToCurrentBattle()
    {
        BattleState currentBattle = battleController != null ? battleController.BattleState : null;
        if (_subscribedBattle == currentBattle)
            return;

        Unsubscribe();
        _subscribedBattle = currentBattle;
    }

    private void Unsubscribe()
    {
        if (_subscribedBattle == null)
            return;

        _subscribedBattle = null;
        EventBus.Unsubscribe<MoneyChangedEvent>(OnMoneyChanged);
    }
}
