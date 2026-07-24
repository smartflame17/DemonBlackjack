using UnityEngine;
using TMPro;
using DG.Tweening;
using System;

public class RoundStartPanel : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text roundText;
    [SerializeField] private BattleController battleController;
    [SerializeField] private BattleUiPresenter battleUiPresenter;
    [SerializeField] private float midAnimationDelay = 1.0f;

    private BattleState battleState;
    private Sequence fadeSequence;
    private float animationDuration;
    
    void Awake()
    {
        if (battleController == null)
            battleController = FindFirstObjectByType<BattleController>();
        if (battleUiPresenter == null)
            battleUiPresenter = FindFirstObjectByType<BattleUiPresenter>();

        animationDuration = battleUiPresenter.roundStartDelaySeconds;
        fadeSequence = FadeInOut(canvasGroup, animationDuration, midAnimationDelay).SetAutoKill(false).Pause();
    }

    public static Sequence FadeInOut(CanvasGroup canvasGroup, float duration, float delay)
    {
        delay = Mathf.Clamp(delay, 0f, duration);

        float fadeDuration = (duration - delay) * 0.5f;

        canvasGroup.alpha = 0f;

        return DOTween.Sequence()
            .Append(canvasGroup.DOFade(1f, fadeDuration))
            .AppendInterval(delay)
            .Append(canvasGroup.DOFade(0f, fadeDuration));
    }

    void OnEnable()
    {
        battleState = battleController != null
        ? battleController.BattleState
        : null;
        if (battleState == null) return;

        int roundNumber = battleState.CurrentRound == null
        ? battleState.RoundNumber + 1
        : battleState.RoundNumber;

        roundText.text = $"라운드 {roundNumber}";
        Debug.Log($"Playing round start panel at round {roundNumber}");
        fadeSequence.Restart();
    }
    private void OnDisable()
    {
        fadeSequence?.Rewind();
    }

    void OnDestroy()
    {
        fadeSequence?.Kill();
    }
}
