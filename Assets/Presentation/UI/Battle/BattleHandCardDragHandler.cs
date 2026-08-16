using UnityEngine;
using UnityEngine.EventSystems;

public sealed class BattleHandCardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private BattleUiPresenter _presenter;
    private RectTransform _rectTransform;
    private RectTransform _parent;
    private CanvasGroup _canvasGroup;
    private Vector2 _startAnchoredPosition;
    private Vector3 _dragWorldOffset;
    private int _handIndex = -1;
    private bool _dragging;
    private bool _originalBlocksRaycasts = true;
    private bool _hasCachedPreview;
    private bool _previewVisible;
    private ScoreResult _cachedScoreResult;
    private MoneyDeltaPreview _cachedMoneyPreview;

    public void Configure(BattleUiPresenter presenter, int handIndex)
    {
        ClearCachedPreview();
        _presenter = presenter;
        _handIndex = handIndex;
        enabled = presenter != null && handIndex >= 0;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        ClearCachedPreview();
        if (!enabled || _presenter == null || !_presenter.CanPlayerAct)
            return;

        _rectTransform ??= transform as RectTransform;
        _parent ??= _rectTransform != null ? _rectTransform.parent as RectTransform : null;
        if (_rectTransform == null || _parent == null)
            return;

        _startAnchoredPosition = _rectTransform.anchoredPosition;
        _dragWorldOffset = TryGetPointerWorldPosition(eventData, out Vector3 pointerWorldPosition)
            ? _rectTransform.position - pointerWorldPosition
            : Vector3.zero;
        if (_canvasGroup == null)
            _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _originalBlocksRaycasts = _canvasGroup.blocksRaycasts;
        _canvasGroup.blocksRaycasts = false;
        _dragging = true;
        _hasCachedPreview = _presenter.TryCalculateCardPlayPreview(
            _handIndex,
            out _cachedScoreResult,
            out _cachedMoneyPreview);
        MoveToPointer(eventData);
        UpdatePreviewVisibility(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_dragging)
            return;

        MoveToPointer(eventData);
        UpdatePreviewVisibility(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_dragging)
            return;

        _dragging = false;
        ClearCachedPreview();
        if (_canvasGroup != null)
            _canvasGroup.blocksRaycasts = _originalBlocksRaycasts;

        bool played = _presenter != null
            && _presenter.IsPointerOverPlayerPlayPile(eventData)
            && _presenter.TryPlayDraggedHandCard(_handIndex);

        if (!played && _rectTransform != null)
            _rectTransform.anchoredPosition = _startAnchoredPosition;

        _presenter?.Refresh();
    }

    private void OnDisable()
    {
        if (_dragging && _canvasGroup != null)
            _canvasGroup.blocksRaycasts = _originalBlocksRaycasts;

        _dragging = false;
        ClearCachedPreview();
    }

    private void MoveToPointer(PointerEventData eventData)
    {
        if (_rectTransform == null || _parent == null || eventData == null)
            return;

        if (TryGetPointerWorldPosition(eventData, out Vector3 pointerWorldPosition))
            _rectTransform.position = pointerWorldPosition + _dragWorldOffset;
    }

    private bool TryGetPointerWorldPosition(PointerEventData eventData, out Vector3 worldPosition)
    {
        Camera camera = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;
        return RectTransformUtility.ScreenPointToWorldPointInRectangle(_parent, eventData.position, camera, out worldPosition);
    }

    private void UpdatePreviewVisibility(PointerEventData eventData)
    {
        bool shouldShow = _hasCachedPreview
            && _presenter != null
            && _presenter.IsPointerOverPlayerPlayPile(eventData);
        if (shouldShow == _previewVisible)
            return;

        _previewVisible = shouldShow;
        if (_previewVisible)
            _presenter.ShowPlayerChoiceMoneyPreview(_cachedScoreResult, _cachedMoneyPreview);
        else
            _presenter?.HidePlayerChoiceMoneyPreview();
    }

    private void ClearCachedPreview()
    {
        _presenter?.HidePlayerChoiceMoneyPreview();

        _hasCachedPreview = false;
        _previewVisible = false;
        _cachedScoreResult = default;
        _cachedMoneyPreview = default;
    }
}
