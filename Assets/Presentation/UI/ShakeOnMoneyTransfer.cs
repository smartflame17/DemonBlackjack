using System;
using UnityEngine;

public class ShakeOnMoneyTransfer : MonoBehaviour
{
    [SerializeField] private int shakeThreshold = 1000; // Minimum amount of money change to trigger shake

    private void OnEnable()
    {
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
        EventBus.Unsubscribe<MoneyChangedEvent>(OnMoneyChanged);
    }
}
