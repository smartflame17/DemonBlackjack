using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ShopSlotView : MonoBehaviour
{
    private Button _button;
    private Image _image;
    private TMP_Text _priceText;
    private TMP_Text _label;
    private TooltipTrigger _tooltipTrigger;

    public void Initialize(Button button, TMP_Text priceText)
    {
        _button = button;
        _image = button != null ? button.GetComponent<Image>() : null;
        _priceText = priceText;
        _label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        _tooltipTrigger = GetComponent<TooltipTrigger>() ?? gameObject.AddComponent<TooltipTrigger>();
    }

    public void Bind(Sprite sprite, int price, string label, string contentId, Action purchase)
    {
        gameObject.SetActive(true);
        _tooltipTrigger ??= GetComponent<TooltipTrigger>() ?? gameObject.AddComponent<TooltipTrigger>();
        _tooltipTrigger.Bind(contentId);
        if (_image != null)
            _image.sprite = sprite;
        if (_priceText != null)
            _priceText.text = $"${Mathf.Max(0, price)}";
        if (_label != null)
            _label.text = label;
        if (_button == null)
            return;

        _button.onClick.RemoveAllListeners();
        _button.interactable = true;
        _button.onClick.AddListener(() => purchase?.Invoke());
    }

    public void SetUnavailable()
    {
        _tooltipTrigger?.Clear();
        if (_button != null)
            _button.onClick.RemoveAllListeners();
        gameObject.SetActive(false);
    }
}
