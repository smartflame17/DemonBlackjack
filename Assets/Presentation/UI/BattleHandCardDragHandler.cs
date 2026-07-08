using UnityEngine;
using UnityEngine.EventSystems;

public sealed class BattleHandCardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private BattleUiPresenter _presenter;
    private RectTransform _rectTransform;
    private RectTransform _parent;
    private CanvasGroup _canvasGroup;
    private Vector2 _startAnchoredPosition;
    private int _handIndex = -1;
    private bool _dragging;
    private bool _originalBlocksRaycasts = true;

    public void Configure(BattleUiPresenter presenter, int handIndex)
    {
        _presenter = presenter;
        _handIndex = handIndex;
        enabled = presenter != null && handIndex >= 0;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!enabled || _presenter == null || !_presenter.CanPlayerAct)
            return;

        _rectTransform ??= transform as RectTransform;
        _parent ??= _rectTransform != null ? _rectTransform.parent as RectTransform : null;
        if (_rectTransform == null || _parent == null)
            return;

        _startAnchoredPosition = _rectTransform.anchoredPosition;
        _canvasGroup ??= GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        _originalBlocksRaycasts = _canvasGroup.blocksRaycasts;
        _canvasGroup.blocksRaycasts = false;
        _dragging = true;
        MoveToPointer(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_dragging)
            return;

        MoveToPointer(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_dragging)
            return;

        _dragging = false;
        if (_canvasGroup != null)
            _canvasGroup.blocksRaycasts = _originalBlocksRaycasts;

        bool played = _presenter != null
            && _presenter.IsPointerOverPlayerPlayPile(eventData)
            && _presenter.TryPlayDraggedHandCard(_handIndex);

        if (!played && _rectTransform != null)
            _rectTransform.anchoredPosition = _startAnchoredPosition;

        _presenter?.Refresh();
    }

    private void MoveToPointer(PointerEventData eventData)
    {
        if (_rectTransform == null || _parent == null || eventData == null)
            return;

        Camera camera = eventData.pressEventCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, eventData.position, camera, out Vector2 localPoint))
            _rectTransform.anchoredPosition = localPoint;
    }
}
