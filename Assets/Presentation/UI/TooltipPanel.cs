using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class TooltipPanel : MonoBehaviour
{
    [SerializeField] private TooltipContentCatalog catalog;
    [SerializeField] private TMP_Text displayNameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Vector2 cursorOffset = new(20f, -20f);

    private RectTransform _rectTransform;
    private RectTransform _canvasRectTransform;
    private Canvas _canvas;
    private TooltipTrigger _owner;

    public Vector2 CursorOffset
    {
        get => cursorOffset;
        set => cursorOffset = value;
    }

    private void Awake()
    {
        EnsureReferences();
    }

    private void LateUpdate()
    {
        if (_owner == null || Pointer.current == null)
            return;

        PositionAt(Pointer.current.position.ReadValue());
    }

    public bool Show(string contentId, TooltipTrigger owner, Vector2 screenPosition)
    {
        return Show(contentId, null, owner, screenPosition);
    }

    public bool Show(string contentId, string fallbackContentId, TooltipTrigger owner, Vector2 screenPosition)
    {
        EnsureReferences();
        if (owner == null || catalog == null || !TryGetDefinition(contentId, fallbackContentId, out TooltipContentDefinition definition))
        {
            Hide(null);
            return false;
        }

        _owner = owner;
        displayNameText.text = definition.DisplayName;
        descriptionText.text = definition.Description;
        gameObject.SetActive(true);
        PositionAt(screenPosition);
        return true;
    }

    public void Hide(TooltipTrigger owner)
    {
        if (owner != null && _owner != owner)
            return;

        _owner = null;
        gameObject.SetActive(false);
    }

    private void PositionAt(Vector2 screenPosition)
    {
        EnsureReferences();
        if (_rectTransform == null || _canvasRectTransform == null)
            return;

        Camera camera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRectTransform, screenPosition, camera, out Vector2 localPoint))
            return;

        _rectTransform.anchoredPosition = localPoint + cursorOffset;
        Canvas.ForceUpdateCanvases();

        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(_canvasRectTransform, _rectTransform);
        Rect canvasBounds = _canvasRectTransform.rect;
        Vector2 correction = Vector2.zero;

        if (bounds.min.x < canvasBounds.xMin)
            correction.x = canvasBounds.xMin - bounds.min.x;
        else if (bounds.max.x > canvasBounds.xMax)
            correction.x = canvasBounds.xMax - bounds.max.x;

        if (bounds.min.y < canvasBounds.yMin)
            correction.y = canvasBounds.yMin - bounds.min.y;
        else if (bounds.max.y > canvasBounds.yMax)
            correction.y = canvasBounds.yMax - bounds.max.y;

        _rectTransform.anchoredPosition += correction;
    }

    private void EnsureReferences()
    {
        _rectTransform ??= transform as RectTransform;
        _canvas ??= GetComponentInParent<Canvas>();
        _canvasRectTransform ??= _canvas != null ? _canvas.transform as RectTransform : null;
        displayNameText ??= FindText("DisplayNameText");
        descriptionText ??= FindText("DescriptionText");

        if (catalog == null)
        {
            TooltipContentCatalog[] catalogs = Resources.FindObjectsOfTypeAll<TooltipContentCatalog>();
            if (catalogs.Length > 0)
                catalog = catalogs[0];
        }
        catalog ??= TooltipContentCatalog.CreateRuntimeDefault();

        CanvasGroup group = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private bool TryGetDefinition(string contentId, string fallbackContentId, out TooltipContentDefinition definition)
    {
        if (catalog.TryGetDefinition(contentId, out definition))
            return true;

        return !string.Equals(contentId, fallbackContentId, System.StringComparison.OrdinalIgnoreCase)
            && catalog.TryGetDefinition(fallbackContentId, out definition);
    }

    private TMP_Text FindText(string objectName)
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].name == objectName)
                return texts[i];
        }
        return null;
    }
}
