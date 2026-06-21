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

    public void Initialize(Button button, TMP_Text priceText)
    {
        _button = button;
        _image = button != null ? button.GetComponent<Image>() : null;
        _priceText = priceText;
        _label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
    }

    public void Bind(Sprite sprite, int price, string label, Action purchase)
    {
        gameObject.SetActive(true);
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
        if (_button != null)
            _button.onClick.RemoveAllListeners();
        gameObject.SetActive(false);
    }
}
