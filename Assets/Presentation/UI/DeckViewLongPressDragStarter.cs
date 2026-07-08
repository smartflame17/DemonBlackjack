using UnityEngine;
using UnityEngine.EventSystems;

public sealed class DeckViewLongPressDragStarter : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [SerializeField] private BattleUiPresenter presenter;
    [SerializeField] private PlayerHandHitDragHandler hitDragHandler;
    [SerializeField] private float longPressSeconds = 0.25f;

    private bool _pressed;
    private bool _dragStarted;
    private PointerEventData _currentEventData;
    private float _pressStartTime;

    public void Configure(BattleUiPresenter battleUiPresenter, PlayerHandHitDragHandler playerHandHitDragHandler)
    {
        presenter = battleUiPresenter;
        hitDragHandler = playerHandHitDragHandler;
    }

    private void Update()
    {
        if (!_pressed || _dragStarted || presenter == null || !presenter.CanPlayerAct)
            return;

        if (Time.unscaledTime - _pressStartTime < Mathf.Max(0.01f, longPressSeconds))
            return;

        _dragStarted = true;
        hitDragHandler?.BeginExternalDrag(_currentEventData);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _pressed = true;
        _dragStarted = false;
        _currentEventData = eventData;
        _pressStartTime = Time.unscaledTime;
    }

    public void OnDrag(PointerEventData eventData)
    {
        _currentEventData = eventData;
        if (_dragStarted)
            hitDragHandler?.DragExternal(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!_pressed)
            return;

        _pressed = false;
        _currentEventData = eventData;

        if (_dragStarted)
        {
            hitDragHandler?.EndExternalDrag(eventData);
            return;
        }

        presenter?.ShowDrawPile();
    }
}
