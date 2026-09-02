using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MoneyInputPanel : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text availableMoneyText;
    [SerializeField] private TMP_InputField amountInput;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private Action<int> _onConfirmed;
    private Action _onCancelled;
    private int _maximumAmount;
    private bool _requestActive;
    private bool _listenersBound;
    private bool _normalizingInput;

    public bool IsOpen => _requestActive;
    public int MaximumAmount => _maximumAmount;

    private void Awake()
    {
        ResolveReferences();
        BindListeners();
        RefreshState();
    }

    private void OnEnable()
    {
        ResolveReferences();
        BindListeners();
        RefreshState();
    }

    private void OnDisable()
    {
        if (!_requestActive)
            return;

        Action callback = _onCancelled;
        CleanupRequest();
        callback?.Invoke();
    }

    private void OnDestroy()
    {
        if (!_listenersBound)
            return;

        amountInput?.onValueChanged.RemoveListener(OnAmountChanged);
        confirmButton?.onClick.RemoveListener(Confirm);
        cancelButton?.onClick.RemoveListener(CancelFromButton);
    }

    public void Show(int maximumAmount, Action<int> onConfirmed, Action onCancelled)
    {
        if (_requestActive)
            throw new InvalidOperationException("A money input request is already active.");
        if (maximumAmount <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumAmount));
        if (onConfirmed == null)
            throw new ArgumentNullException(nameof(onConfirmed));

        ResolveReferences();
        ValidateConfiguration();
        BindListeners();

        _maximumAmount = maximumAmount;
        _onConfirmed = onConfirmed;
        _onCancelled = onCancelled;
        _requestActive = true;
        amountInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        amountInput.SetTextWithoutNotify(string.Empty);

        if (panelRoot != null && !panelRoot.activeSelf)
            panelRoot.SetActive(true);

        RefreshState();
        amountInput.Select();
        amountInput.ActivateInputField();
    }

    public bool Cancel()
    {
        if (!_requestActive)
            return false;

        Action callback = _onCancelled;
        _requestActive = false;
        if (panelRoot != null && panelRoot.activeSelf)
            panelRoot.SetActive(false);
        CleanupRequest();
        callback?.Invoke();
        return true;
    }

    private void OnAmountChanged(string value)
    {
        if (_normalizingInput)
            return;

        if (TryParseAmount(value, out int amount) && amount > _maximumAmount)
        {
            _normalizingInput = true;
            amountInput.SetTextWithoutNotify(_maximumAmount.ToString());
            amountInput.caretPosition = amountInput.text.Length;
            _normalizingInput = false;
        }

        RefreshState();
    }

    private void Confirm()
    {
        if (!_requestActive || !TryParseAmount(amountInput.text, out int amount) || amount <= 0)
            return;

        amount = Mathf.Clamp(amount, 1, _maximumAmount);
        Action<int> callback = _onConfirmed;
        _requestActive = false;
        if (panelRoot != null && panelRoot.activeSelf)
            panelRoot.SetActive(false);
        CleanupRequest();
        callback?.Invoke(amount);
    }

    private void CancelFromButton()
    {
        Cancel();
    }

    private void RefreshState()
    {
        if (availableMoneyText != null)
            availableMoneyText.text = $"보유 금액: {_maximumAmount:N0}";

        bool valid = _requestActive
            && TryParseAmount(amountInput != null ? amountInput.text : null, out int amount)
            && amount > 0
            && amount <= _maximumAmount;
        if (confirmButton != null)
            confirmButton.interactable = valid;
    }

    private void CleanupRequest()
    {
        _requestActive = false;
        _maximumAmount = 0;
        _onConfirmed = null;
        _onCancelled = null;
        if (amountInput != null)
            amountInput.SetTextWithoutNotify(string.Empty);
        if (confirmButton != null)
            confirmButton.interactable = false;
    }

    private void ResolveReferences()
    {
        panelRoot ??= gameObject;
        EnsureRuntimeUi();
        availableMoneyText ??= FindComponent<TMP_Text>("AvailableMoneyText");
        amountInput ??= FindComponent<TMP_InputField>("AmountInput");
        confirmButton ??= FindComponent<Button>("ConfirmButton");
        cancelButton ??= FindComponent<Button>("CancelButton");
    }

    private void EnsureRuntimeUi()
    {
        if (transform.Find("Dialog") != null)
            return;

        if (transform is RectTransform panelRect)
        {
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
        }

        Image overlay = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
        overlay.color = new Color(0.025f, 0.02f, 0.03f, 0.92f);
        overlay.raycastTarget = true;

        RectTransform dialog = CreateRect("Dialog", transform, new Vector2(520f, 360f));
        Image dialogImage = dialog.gameObject.AddComponent<Image>();
        dialogImage.color = new Color(0.3f, 0.3f, 0.4f, 1f);

        TMP_Text title = CreateText("TitleText", dialog, "주식 매수", 34f);
        SetRect(title.rectTransform, new Vector2(0f, 125f), new Vector2(440f, 55f));

        availableMoneyText = CreateText("AvailableMoneyText", dialog, string.Empty, 25f);
        SetRect(availableMoneyText.rectTransform, new Vector2(0f, 65f), new Vector2(440f, 45f));

        amountInput = CreateInputField(dialog);
        SetRect(amountInput.transform as RectTransform, new Vector2(0f, 5f), new Vector2(350f, 55f));

        confirmButton = CreateButton("ConfirmButton", dialog, "확인", new Color(0.25f, 0.55f, 0.42f, 1f));
        SetRect(confirmButton.transform as RectTransform, new Vector2(-105f, -95f), new Vector2(180f, 55f));

        cancelButton = CreateButton("CancelButton", dialog, "취소", new Color(0.55f, 0.28f, 0.28f, 1f));
        SetRect(cancelButton.transform as RectTransform, new Vector2(105f, -95f), new Vector2(180f, 55f));
    }

    private static TMP_InputField CreateInputField(Transform parent)
    {
        RectTransform root = CreateRect("AmountInput", parent, new Vector2(350f, 55f));
        Image background = root.gameObject.AddComponent<Image>();
        background.color = Color.white;

        RectTransform viewport = CreateRect("Text Area", root, Vector2.zero);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(15f, 5f);
        viewport.offsetMax = new Vector2(-15f, -5f);
        viewport.gameObject.AddComponent<RectMask2D>();

        TMP_Text placeholder = CreateText("Placeholder", viewport, "매수 금액을 입력하세요", 22f);
        placeholder.color = new Color(0.25f, 0.25f, 0.25f, 0.55f);
        placeholder.fontStyle = FontStyles.Italic;
        Stretch(placeholder.rectTransform);

        TMP_Text valueText = CreateText("Text", viewport, string.Empty, 24f);
        valueText.color = new Color(0.08f, 0.08f, 0.08f, 1f);
        valueText.alignment = TextAlignmentOptions.MidlineLeft;
        Stretch(valueText.rectTransform);

        TMP_InputField input = root.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = viewport;
        input.textComponent = valueText;
        input.placeholder = placeholder;
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.targetGraphic = background;
        return input;
    }

    private static Button CreateButton(string name, Transform parent, string label, Color color)
    {
        RectTransform root = CreateRect(name, parent, new Vector2(180f, 55f));
        Image image = root.gameObject.AddComponent<Image>();
        image.color = color;
        Button button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        TMP_Text text = CreateText("Label", root, label, 24f);
        text.color = Color.white;
        Stretch(text.rectTransform);
        return button;
    }

    private static TMP_Text CreateText(string name, Transform parent, string value, float fontSize)
    {
        RectTransform rect = CreateRect(name, parent, Vector2.zero);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.color = new Color(0.08f, 0.08f, 0.08f, 1f);
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 size)
    {
        var child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        return rect;
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        if (rect == null)
            return;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void ValidateConfiguration()
    {
        if (panelRoot == null || availableMoneyText == null || amountInput == null || confirmButton == null || cancelButton == null)
            throw new InvalidOperationException($"{nameof(MoneyInputPanel)} is missing one or more required UI references.");
    }

    private void BindListeners()
    {
        if (_listenersBound || amountInput == null || confirmButton == null || cancelButton == null)
            return;

        amountInput.onValueChanged.AddListener(OnAmountChanged);
        confirmButton.onClick.AddListener(Confirm);
        cancelButton.onClick.AddListener(CancelFromButton);
        _listenersBound = true;
    }

    private T FindComponent<T>(string objectName) where T : Component
    {
        T[] components = GetComponentsInChildren<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i].name == objectName)
                return components[i];
        }
        return null;
    }

    private static bool TryParseAmount(string value, out int amount)
    {
        if (long.TryParse(value, out long parsed) && parsed > 0)
        {
            amount = (int)Math.Min(int.MaxValue, parsed);
            return true;
        }

        amount = 0;
        return false;
    }
}
