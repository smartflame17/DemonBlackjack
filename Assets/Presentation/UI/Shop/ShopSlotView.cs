using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public sealed class ShopSlotView : MonoBehaviour
{
    private Button _button;
    private Image _image;
    private RelicUiView _relicView;
    private TMP_Text _priceText;
    private TMP_Text _label;
    private TooltipTrigger _tooltipTrigger;
    private CanvasGroup _canvasGroup;

    public void Initialize(Button button, TMP_Text priceText)
    {
        _relicView = null;
        _button = button;
        _image = button != null ? button.GetComponent<Image>() : null;
        _priceText = priceText;
        _label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        _tooltipTrigger = GetComponent<TooltipTrigger>() ?? gameObject.AddComponent<TooltipTrigger>();
        _canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
    }

    public void Initialize(RelicUiView relicView, TMP_Text priceText)
    {
        _relicView = relicView;
        _button = relicView != null ? relicView.Button : null;
        _image = null;
        _priceText = priceText;
        _label = null;
        _tooltipTrigger = GetComponent<TooltipTrigger>() ?? gameObject.AddComponent<TooltipTrigger>();
        _canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
    }

    public void Bind(Sprite sprite, int price, string label, string contentId, Action purchase, bool hasCounter = false, int counterValue = 0)
    {
        //gameObject.SetActive(true);
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1.0f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
        }

        _tooltipTrigger ??= GetComponent<TooltipTrigger>() ?? gameObject.AddComponent<TooltipTrigger>();
        _tooltipTrigger.Bind(contentId);
        if (_relicView != null)
            _relicView.Bind(sprite, contentId, hasCounter, counterValue);
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
        _relicView?.Clear();
        if (_button != null)
            _button.onClick.RemoveAllListeners();

        if (_canvasGroup != null)
        {
            //_canvasGroup.alpha = 0.0f;
            _canvasGroup.DOFade(0.0f, 0.5f).SetEase(Ease.InOutQuad);
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }
        //gameObject.SetActive(false);
    }
}
