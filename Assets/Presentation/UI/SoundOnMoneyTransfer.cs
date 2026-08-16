using System;
using UnityEngine;

public class SoundOnMoneyTransfer : MonoBehaviour
{
    [SerializeField] private int midSoundThreshold = 5000; // Minimum amount of money change to trigger mid sound
    [SerializeField] private int highSoundThreshold = 10000; // Minimum amount of money change to trigger high sound

    private void OnEnable()
    {
        EventBus.Subscribe<MoneyChangedEvent>(OnMoneyChanged);
    }

    private void OnMoneyChanged(MoneyChangedEvent @event)
    {
        if (@event.Owner == Combatant.Opponent) return; // Only play sound for player money changes

        if (Math.Abs(@event.Delta) >= highSoundThreshold)
        {
            SoundManager.Instance?.PlaySFX(ESfx.MONEY_CHANGE_HIGH);
        }
        else if (Math.Abs(@event.Delta) >= midSoundThreshold)
        {
            SoundManager.Instance?.PlaySFX(ESfx.MONEY_CHANGE_MID);
        }
        else
        {
            SoundManager.Instance?.PlaySFX(ESfx.MONEY_CHANGE_LOW);
        }
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<MoneyChangedEvent>(OnMoneyChanged);
    }
}
