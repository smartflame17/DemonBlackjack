using UnityEngine;
using UnityEngine.EventSystems;

public sealed class PlayerHandHitDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private BattleUiPresenter _presenter;
    private RectTransform _rectTransform;
    private RectTransform _parent;
    private CanvasGroup _canvasGroup;
    private Vector2 _startAnchoredPosition;
    private Vector3 _centerToPivotWorldOffset;
    private bool _startActive;
    private bool _dragging;
    private bool _originalBlocksRaycasts = true;

    public void Configure(BattleUiPresenter presenter)
    {
        _presenter = presenter;
        _rectTransform ??= transform as RectTransform;
        _parent ??= _rectTransform != null ? _rectTransform.parent as RectTransform : null;
    }

    public void BeginExternalDrag(PointerEventData eventData)
    {
        if (_presenter == null || !_presenter.CanPlayerAct)
            return;

        _rectTransform ??= transform as RectTransform;
        _parent ??= _rectTransform != null ? _rectTransform.parent as RectTransform : null;
        if (_rectTransform == null || _parent == null)
            return;

        _startActive = gameObject.activeSelf;
        _startAnchoredPosition = _rectTransform.anchoredPosition;
        gameObject.SetActive(true);
        _centerToPivotWorldOffset = CalculateCenterToPivotWorldOffset();

        _canvasGroup ??= GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        _originalBlocksRaycasts = _canvasGroup.blocksRaycasts;
        _canvasGroup.blocksRaycasts = false;
        _dragging = true;
        MoveToPointer(eventData);
    }

    public void DragExternal(PointerEventData eventData)
    {
        if (_dragging)
            MoveToPointer(eventData);
    }

    public void EndExternalDrag(PointerEventData eventData)
    {
        if (!_dragging)
            return;

        _dragging = false;
        if (_canvasGroup != null)
            _canvasGroup.blocksRaycasts = _originalBlocksRaycasts;

        bool hit = _presenter != null
            && _presenter.IsPointerOverPlayerPlayPile(eventData)
            && _presenter.TryHitFromDraggedDeck();

        RestoreVisual();

        if (!hit)
            _presenter?.Refresh();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        BeginExternalDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        DragExternal(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        EndExternalDrag(eventData);
    }

    private void RestoreVisual()
    {
        if (_rectTransform != null)
            _rectTransform.anchoredPosition = _startAnchoredPosition;

        gameObject.SetActive(_startActive);
    }

    private void MoveToPointer(PointerEventData eventData)
    {
        if (_rectTransform == null || _parent == null || eventData == null)
            return;

        Camera camera = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(_parent, eventData.position, camera, out Vector3 worldPosition))
            _rectTransform.position = worldPosition + _centerToPivotWorldOffset;
    }

    private Vector3 CalculateCenterToPivotWorldOffset()
    {
        if (_rectTransform == null)
            return Vector3.zero;

        var corners = new Vector3[4];
        _rectTransform.GetWorldCorners(corners);
        Vector3 center = (corners[0] + corners[2]) * 0.5f;
        return _rectTransform.position - center;
    }
}
