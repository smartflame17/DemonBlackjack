using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RelicUiView : MonoBehaviour
{
    [SerializeField] private Image image;
    [SerializeField] private TMP_Text counterText;
    [SerializeField] private Button button;
    [SerializeField] private TooltipTrigger tooltipTrigger;

    public Button Button
    {
        get
        {
            EnsureReferences();
            return button;
        }
    }

    public RectTransform RectTransform => transform as RectTransform;

    private void Awake()
    {
        SetCounter(false, 0);
    }

    public void Initialize(Image relicImage, Button relicButton, TMP_Text relicCounterText)
    {
        image = relicImage;
        button = relicButton;
        counterText = relicCounterText;
        tooltipTrigger = GetComponent<TooltipTrigger>() ?? gameObject.AddComponent<TooltipTrigger>();

        if (button != null && button.targetGraphic == null)
            button.targetGraphic = image;
    }

    public void Bind(Sprite sprite, string contentId, bool hasCounter, int counterValue = 0, bool interactable = true)
    {
        EnsureReferences();

        if (image != null)
        {
            image.sprite = sprite;
            image.color = sprite != null ? Color.white : Color.clear;
        }

        if (button != null)
            button.interactable = interactable;

        if (!string.IsNullOrWhiteSpace(contentId))
            tooltipTrigger.Bind(contentId);
        else
            tooltipTrigger.Clear();

        SetCounter(hasCounter, counterValue);
    }

    public void SetCounter(bool visible, int value)
    {
        EnsureReferences();

        if (counterText == null)
            return;

        counterText.gameObject.SetActive(visible);
        counterText.text = value.ToString();
    }

    public void Clear()
    {
        EnsureReferences();

        if (image != null)
        {
            image.sprite = null;
            image.color = Color.clear;
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.interactable = false;
        }

        tooltipTrigger.Clear();
        SetCounter(false, 0);
    }

    private void EnsureReferences()
    {
        if (image == null)
            image = GetComponent<Image>();

        if (button == null)
            button = GetComponent<Button>();

        if (counterText == null)
            counterText = FindChildText("CounterText");

        tooltipTrigger ??= GetComponent<TooltipTrigger>() ?? gameObject.AddComponent<TooltipTrigger>();

        if (button != null && button.targetGraphic == null)
            button.targetGraphic = image;
    }

    private TMP_Text FindChildText(string childName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == childName && children[i].TryGetComponent(out TMP_Text text))
                return text;
        }

        return null;
    }
}
