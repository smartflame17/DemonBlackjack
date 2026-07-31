using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundOnCardPlay : MonoBehaviour
{
    [SerializeField] private BattleController battleController;
    [SerializeField, Min(0f)] private float soundEffectDelaySeconds = 0.1f;

    private BattleState _subscribedBattle;
    private readonly Queue<ESfx> pendingSoundEffects = new();
    private Coroutine soundEffectRoutine;

    private void OnEnable()
    {
        if (battleController == null)
            battleController = FindFirstObjectByType<BattleController>();

        SubscribeToCurrentBattle();
        _subscribedBattle.EventBus.Subscribe<CardPlayedEvent>(OnCardPlayed);
        _subscribedBattle.EventBus.Subscribe<CardDrawnEvent>(OnCardDrawn);
    }

    private void OnCardPlayed(CardPlayedEvent @event)
    {
        QueueSoundEffect(ESfx.CARD_PLAY);
    }

    private void OnCardDrawn(CardDrawnEvent @event)
    {
        if (@event.Owner == Combatant.Opponent) return; // Only play sound for player card draws
        QueueSoundEffect(ESfx.CARD_DRAW);
    }

    private void OnDisable()
    {
        Unsubscribe();
        ClearPendingSoundEffects();
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

        _subscribedBattle.EventBus.Unsubscribe<CardPlayedEvent>(OnCardPlayed);
        _subscribedBattle.EventBus.Unsubscribe<CardDrawnEvent>(OnCardDrawn);
        _subscribedBattle = null;
    }

    private void QueueSoundEffect(ESfx soundEffect)
    {
        pendingSoundEffects.Enqueue(soundEffect);

        if (soundEffectRoutine == null && isActiveAndEnabled)
            soundEffectRoutine = StartCoroutine(PlayQueuedSoundEffects());
    }

    private IEnumerator PlayQueuedSoundEffects()
    {
        while (pendingSoundEffects.Count > 0)
        {
            ESfx soundEffect = pendingSoundEffects.Dequeue();

            if (soundEffectDelaySeconds > 0f)
                yield return new WaitForSeconds(soundEffectDelaySeconds);
            else
                yield return null;

            SoundManager.Instance?.PlaySFX(soundEffect);
        }

        soundEffectRoutine = null;
    }

    private void ClearPendingSoundEffects()
    {
        if (soundEffectRoutine != null)
            StopCoroutine(soundEffectRoutine);

        soundEffectRoutine = null;
        pendingSoundEffects.Clear();
    }
}
