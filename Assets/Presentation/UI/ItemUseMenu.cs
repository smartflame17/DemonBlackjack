using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class ItemUseMenu : MonoBehaviour
{
    [SerializeField] private RunManager runManager;
    [SerializeField] private BattleController battleController;
    [SerializeField] private RectTransform popup;
    [SerializeField] private Button useButton;
    [SerializeField] private Button discardButton;

    private string _selectedItemId;
    private RectTransform _selectedSlot;
    private bool _listenersBound;

    public bool IsOpen => gameObject.activeSelf && !string.IsNullOrWhiteSpace(_selectedItemId);

    private void Awake()
    {
        EnsureReferences();
        BindListeners();
    }

    private void OnEnable()
    {
        EnsureReferences();
        BindListeners();
        RefreshAvailability();
    }

    private void OnDestroy()
    {
        if (!_listenersBound)
            return;
        useButton?.onClick.RemoveListener(UseSelectedItem);
        discardButton?.onClick.RemoveListener(DiscardSelectedItem);
    }

    private void Update()
    {
        if (!IsOpen || !WasPointerPressedThisFrame())
            return;

        Vector2 pointerPosition = GetPointerPosition();
        Camera camera = GetEventCamera();
        bool overPopup = RectTransformUtility.RectangleContainsScreenPoint(popup, pointerPosition, camera);
        bool overSelectedSlot = _selectedSlot != null && RectTransformUtility.RectangleContainsScreenPoint(_selectedSlot, pointerPosition, camera);
        if (!overPopup && !overSelectedSlot)
            Close();
    }

    public void Initialize(RunManager manager, BattleController controller)
    {
        runManager ??= manager;
        battleController ??= controller;
        EnsureReferences();
    }

    public void Toggle(string itemId, RectTransform slot)
    {
        if (IsOpen && string.Equals(_selectedItemId, itemId, System.StringComparison.OrdinalIgnoreCase) && _selectedSlot == slot)
        {
            Close();
            return;
        }

        Open(itemId, slot);
    }

    public void Open(string itemId, RectTransform slot)
    {
        if (string.IsNullOrWhiteSpace(itemId) || slot == null)
            return;

        EnsureReferences();
        _selectedItemId = itemId;
        _selectedSlot = slot;
        gameObject.SetActive(true);
        PositionBelow(slot);
        RefreshAvailability();
    }

    public void Close()
    {
        _selectedItemId = null;
        _selectedSlot = null;
        gameObject.SetActive(false);
    }

    public void RefreshAvailability()
    {
        EnsureReferences();
        RunState run = runManager != null ? runManager.RunState : null;
        bool stillOwned = run != null && run.HasActiveItem(_selectedItemId);
        if (IsOpen && !stillOwned)
        {
            Close();
            return;
        }

        if (useButton != null)
            useButton.interactable = stillOwned && battleController != null && battleController.CanUseActiveItem(_selectedItemId);
        if (discardButton != null)
            discardButton.interactable = stillOwned;
    }

    private void UseSelectedItem()
    {
        if (battleController != null && battleController.TryUseActiveItem(_selectedItemId))
            Close();
        else
            RefreshAvailability();
    }

    private void DiscardSelectedItem()
    {
        RunState run = runManager != null ? runManager.RunState : null;
        if (run != null && run.RemoveActiveItem(_selectedItemId))
            Close();
        else
            RefreshAvailability();
    }

    private void PositionBelow(RectTransform slot)
    {
        if (popup == null || popup.parent is not RectTransform parent)
            return;

        Canvas canvas = popup.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector3 slotBottomWorld = slot.TransformPoint(new Vector3(slot.rect.center.x, slot.rect.yMin, 0f));
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(camera, slotBottomWorld);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, camera, out Vector2 localPoint))
            return;

        popup.anchorMin = new Vector2(0.5f, 0.5f);
        popup.anchorMax = new Vector2(0.5f, 0.5f);
        popup.pivot = new Vector2(0.5f, 1f);

        float width = popup.rect.width;
        float height = popup.rect.height;
        localPoint.x = Mathf.Clamp(localPoint.x, parent.rect.xMin + width * 0.5f, parent.rect.xMax - width * 0.5f);
        localPoint.y = Mathf.Clamp(localPoint.y, parent.rect.yMin + height, parent.rect.yMax);
        popup.anchoredPosition = localPoint;
    }

    private void EnsureReferences()
    {
        popup ??= transform as RectTransform;
        runManager ??= FindFirstObjectByType<RunManager>();
        battleController ??= FindFirstObjectByType<BattleController>();
        useButton ??= FindButton("UseItemButton");
        discardButton ??= FindButton("DiscardItemButton");
    }

    private void BindListeners()
    {
        if (_listenersBound || useButton == null || discardButton == null)
            return;
        useButton.onClick.AddListener(UseSelectedItem);
        discardButton.onClick.AddListener(DiscardSelectedItem);
        _listenersBound = true;
    }

    private Button FindButton(string objectName)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].name == objectName)
                return buttons[i];
        }
        return null;
    }

    private Camera GetEventCamera()
    {
        Canvas canvas = popup != null ? popup.GetComponentInParent<Canvas>() : null;
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
    }

    private static bool WasPointerPressedThisFrame()
    {
        return (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            || (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame);
    }

    private static Vector2 GetPointerPosition()
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            return Touchscreen.current.primaryTouch.position.ReadValue();
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
    }
}
