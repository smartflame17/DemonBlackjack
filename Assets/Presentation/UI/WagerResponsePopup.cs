using UnityEngine;
using DG.Tweening;
using TMPro;

public class WagerResponsePopup : MonoBehaviour
{
    [SerializeField] private BattleController battleController;
    [SerializeField] private TMP_Text messageText;

    private ScopedEventBus _eventBus = null;
    private RectTransform _rectTransform;

    private void Awake()
    {
        if (battleController == null)
            battleController = FindFirstObjectByType<BattleController>();

        if (messageText == null)
            messageText = GetComponent<TMP_Text>();

        _rectTransform = GetComponent<RectTransform>();
    }

    // By the time this panel is enabled, battle state should already be created by battleController
    private void OnEnable()
    {
        if (battleController.BattleState != null)
        {
            _eventBus ??= battleController.BattleState.EventBus;
            _eventBus.Subscribe<WagerCommittedEvent>(OnWagerCommitted);
        }
    }

    private void OnDisable()
    {
        _eventBus?.Unsubscribe<WagerCommittedEvent>(OnWagerCommitted);
        _eventBus = null;
    }

    private void OnWagerCommitted(WagerCommittedEvent evt)
    {
        messageText.text = $"Wager {evt.WagerAmount}";

        Sequence _popupSequence = DOTween.Sequence().SetAutoKill(false)
            .OnStart(() => _rectTransform.DOAnchorPos(new Vector2(-200f, _rectTransform.anchoredPosition.y), 0.5f).SetEase(Ease.OutQuart))
            .AppendInterval(2.0f)
            .Append(_rectTransform.DOAnchorPos(new Vector2(200f, _rectTransform.anchoredPosition.y), 0.5f).SetEase(Ease.InQuart));
    }
}
