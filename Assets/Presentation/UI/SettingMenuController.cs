using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[Serializable]
internal sealed class SettingMenuTabBinding
{
    [SerializeField] private Button button;
    [SerializeField] private GameObject content;

    internal Button Button => button;
    internal GameObject Content => content;
}

public sealed class SettingMenuController : MonoBehaviour
{
    [Header("Popup")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Vector2 visibleAnchoredPosition = new Vector2(35f, 0f);
    [SerializeField] private Vector2 hiddenAnchoredPosition = new Vector2(35f, -800f);

    [Header("Tabs")]
    [SerializeField] private SettingMenuTabBinding[] tabs;
    [SerializeField, Min(0)] private int defaultTabIndex;

    [Header("Tween Settings")]
    [SerializeField] private float menuPanelAnimationDuration = 1.0f;
    [SerializeField] private Ease menuPanelAnimationEase = Ease.InBack;

    private UnityAction[] tabClickHandlers;
    private RectTransform RectTransform => transform as RectTransform;

    private void Awake()
    {
        if (closeButton == null)
            Debug.LogError("SettingMenuController requires a close button.", this);
        else
            closeButton.onClick.AddListener(CloseMenuPanel);

        RegisterTabListeners();
    }

    private void OnEnable()
    {
        if (closeButton != null)
            closeButton.interactable = true;

        ShowTab(defaultTabIndex);

        if (RectTransform == null)
            return;

        RectTransform.DOKill();
        RectTransform
            .DOAnchorPos(visibleAnchoredPosition, menuPanelAnimationDuration)
            .SetEase(menuPanelAnimationEase);
    }

    private void OnDisable()
    {
        RectTransform?.DOKill();
    }

    private void OnDestroy()
    {
        RectTransform?.DOKill();

        if (closeButton != null)
            closeButton.onClick.RemoveListener(CloseMenuPanel);

        UnregisterTabListeners();
    }

    internal void ShowTab(int tabIndex)
    {
        if (tabs == null || tabs.Length == 0)
            return;

        if (tabIndex < 0 || tabIndex >= tabs.Length)
        {
            Debug.LogError($"Settings tab index {tabIndex} is out of range.", this);
            tabIndex = Mathf.Clamp(defaultTabIndex, 0, tabs.Length - 1);
        }

        for (int i = 0; i < tabs.Length; i++)
        {
            SettingMenuTabBinding tab = tabs[i];
            if (tab == null)
                continue;

            bool selected = i == tabIndex;
            if (tab.Content != null)
                tab.Content.SetActive(selected);
            if (tab.Button != null)
                tab.Button.interactable = !selected;
        }
    }

    private void RegisterTabListeners()
    {
        if (tabs == null || tabs.Length == 0)
        {
            Debug.LogError("SettingMenuController requires at least one tab binding.", this);
            return;
        }

        defaultTabIndex = Mathf.Clamp(defaultTabIndex, 0, tabs.Length - 1);
        tabClickHandlers = new UnityAction[tabs.Length];

        for (int i = 0; i < tabs.Length; i++)
        {
            SettingMenuTabBinding tab = tabs[i];
            if (tab == null || tab.Button == null || tab.Content == null)
            {
                Debug.LogError($"Settings tab binding {i} is incomplete.", this);
                continue;
            }

            int capturedIndex = i;
            UnityAction handler = () => ShowTab(capturedIndex);
            tabClickHandlers[i] = handler;
            tab.Button.onClick.AddListener(handler);
        }
    }

    private void UnregisterTabListeners()
    {
        if (tabs == null || tabClickHandlers == null)
            return;

        int count = Mathf.Min(tabs.Length, tabClickHandlers.Length);
        for (int i = 0; i < count; i++)
        {
            if (tabs[i]?.Button != null && tabClickHandlers[i] != null)
                tabs[i].Button.onClick.RemoveListener(tabClickHandlers[i]);
        }

        tabClickHandlers = null;
    }

    private void CloseMenuPanel()
    {
        if (RectTransform == null)
        {
            gameObject.SetActive(false);
            return;
        }

        if (closeButton != null)
            closeButton.interactable = false;

        RectTransform.DOKill();
        RectTransform
            .DOAnchorPos(hiddenAnchoredPosition, menuPanelAnimationDuration)
            .SetEase(menuPanelAnimationEase)
            .OnComplete(() => gameObject.SetActive(false));
    }
}
