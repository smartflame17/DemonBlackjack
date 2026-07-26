using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TutorialInstructionOverlay : MonoBehaviour
{
    private RectTransform barkRoot;
    private RectTransform barkPanel;
    private TMP_Text barkText;
    private RectTransform dialogueUiRoot;
    private RectTransform dialoguePanel;
    private RectTransform npcSubtitlePanel;
    private RectTransform pcSubtitlePanel;
    private TMP_Text npcSubtitleText;
    private TMP_Text npcNameText;
    private RectTransform responseMenuPanel;
    private RectTransform responseContent;
    private Button responseButtonTemplate;
    private RectTransform responseTimer;
    private Canvas highlightCanvas;
    private RectTransform highlightCanvasRoot;
    private RectTransform highlightRect;
    private readonly List<Button> responseButtons = new();
    private Action continueRequested;
    private Action<int> choiceSelected;

    public TutorialStepKey CurrentStepKey { get; private set; }
    public TutorialOverlayMode CurrentMode { get; private set; }
    public TutorialGuideTarget CurrentTarget { get; private set; }
    public string CurrentTitle { get; private set; }
    public string CurrentBody { get; private set; }
    public string CurrentAction { get; private set; }
    public int ChoiceCount { get; private set; }
    public bool IsVisible => barkPanel != null && barkPanel.gameObject.activeInHierarchy;
    public bool UsesBarkBubble => barkText != null;
    public bool UsesResponseMenu => responseMenuPanel != null && responseMenuPanel.gameObject.activeInHierarchy;
    public bool UsesSceneDialoguePanel => dialoguePanel != null && dialoguePanel.gameObject.activeInHierarchy;

    public void Show(
        TutorialStepSpec step,
        string actionOverride,
        Action onContinue,
        Action<int> onChoiceSelected,
        TutorialGuideTarget? targetOverride = null)
    {
        if (step == null)
            return;

        EnsureBound();
        CurrentStepKey = step.Key;
        CurrentMode = step.Mode;
        CurrentTarget = targetOverride ?? step.GuideTarget;
        CurrentTitle = step.Title ?? string.Empty;
        CurrentBody = step.Body ?? string.Empty;
        CurrentAction = string.IsNullOrWhiteSpace(actionOverride) ? step.ActionText ?? string.Empty : actionOverride;
        continueRequested = onContinue;
        choiceSelected = onChoiceSelected;

        HideResponses();

        if (step.Mode == TutorialOverlayMode.GameplayGuide)
        {
            ShowBark(CurrentBody);
            if (step.Choices != null && step.Choices.Count > 0)
                ShowResponses(step.Choices);
        }
        else
        {
            HideBark();
            ShowDialogue(CurrentTitle, CurrentBody);
            if (step.Choices != null && step.Choices.Count > 0)
                ShowResponses(step.Choices);
            else if (onContinue != null)
                ShowResponses(new[] { string.IsNullOrWhiteSpace(CurrentAction) ? "계속" : CurrentAction });
        }

        ApplyHighlight(CurrentTarget);
    }

    public void Show(string title, string body, string action)
    {
        Show(
            TutorialStepSpec.Create(
                TutorialStepKey.R1PlayThree,
                TutorialOverlayMode.GameplayGuide,
                title,
                body,
                action,
                TutorialGuideTarget.None),
            action,
            null,
            null,
            TutorialGuideTarget.None);
    }

    public void Hide()
    {
        HideResponses();
        SetActive(barkPanel?.gameObject, false);
        SetActive(barkRoot?.gameObject, false);
        SetActive(highlightCanvasRoot?.gameObject, false);
    }

    public void SimulateContinue()
    {
        continueRequested?.Invoke();
    }

    public void SimulateChoice(int index)
    {
        choiceSelected?.Invoke(index);
    }

    private void EnsureBound()
    {
        if (barkText == null)
            BindBarkBubble();

        if (responseButtonTemplate == null)
            BindResponseMenu();

        if (highlightCanvasRoot == null)
            CreateHighlightCanvas();
    }

    private void BindBarkBubble()
    {
        RectTransform root = FindSceneRectByName("Bubble Template Standard Bark UI");
        RectTransform panel = FindChildRect(root, "Bubble Panel");
        TMP_Text text = FindChildText(panel, "Text");
        if (root == null || panel == null || text == null)
            return;

        barkRoot = root;
        barkPanel = panel;
        barkText = text;
        DisableAutomation(barkRoot);
    }

    private void BindResponseMenu()
    {
        RectTransform[] rects = Resources.FindObjectsOfTypeAll<RectTransform>();
        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform candidate = rects[i];
            if (candidate == null
                || candidate.name != "Response Menu Panel"
                || candidate.root == null
                || candidate.root.name != "Dialogue Manager")
                continue;

            RectTransform content = FindChildRect(candidate, "Scroll Content");
            Button template = FindChildButton(content, "Response Button Template");
            if (content == null || template == null)
                continue;

            responseMenuPanel = candidate;
            responseContent = content;
            responseButtonTemplate = template;
            dialoguePanel = FindAncestorRect(candidate, "Dialogue Panel");
            dialogueUiRoot = dialoguePanel != null ? dialoguePanel.parent as RectTransform : null;
            npcSubtitlePanel = FindChildRect(dialoguePanel, "NPC Subtitle Panel");
            pcSubtitlePanel = FindChildRect(dialoguePanel, "PC Subtitle Panel");
            npcSubtitleText = FindChildText(npcSubtitlePanel, "Subtitle Text (TMP)");
            npcNameText = FindChildText(npcSubtitlePanel, "Name Text (TMP)");
            responseTimer = FindChildRect(candidate, "Timer");
            DisableAutomation(dialogueUiRoot);
            HideResponses();
            return;
        }
    }

    private void ShowBark(string body)
    {
        if (barkRoot == null || barkPanel == null || barkText == null)
            return;

        SetActive(barkRoot.gameObject, true);
        SetActive(barkPanel.gameObject, true);
        DisableAutomation(barkRoot);
        SetCanvasesEnabled(barkRoot, true);
        SetCanvasGroups(barkRoot, 1f, false, false);
        SetCanvasGroups(barkPanel, 1f, false, false);
        ConfigureBarkText();
        barkText.text = body;
        barkText.ForceMeshUpdate();
        UpdateBarkHeight();
    }

    private void HideBark()
    {
        SetActive(barkPanel?.gameObject, false);
        SetActive(barkRoot?.gameObject, false);
    }

    private void ShowDialogue(string title, string body)
    {
        if (dialoguePanel == null || npcSubtitlePanel == null || npcSubtitleText == null)
            return;

        SetActive(dialogueUiRoot?.gameObject, true);
        SetActive(dialoguePanel.gameObject, true);
        SetActive(npcSubtitlePanel.gameObject, true);
        SetActive(pcSubtitlePanel?.gameObject, false);
        SetActive(FindChildRect(npcSubtitlePanel, "Portrait Image")?.gameObject, false);
        SetActive(FindChildRect(npcSubtitlePanel, "Portrait Name (TMP)")?.gameObject, false);
        SetActive(FindChildRect(npcSubtitlePanel, "Divider")?.gameObject, false);
        SetActive(FindChildRect(npcSubtitlePanel, "Continue Button")?.gameObject, false);
        SetActive(npcSubtitleText.gameObject, true);
        DisableAutomation(dialogueUiRoot);
        SetCanvasesEnabled(dialoguePanel, true);
        SetCanvasGroups(dialogueUiRoot, 1f, true, true);
        SetCanvasGroups(dialoguePanel, 1f, true, true);
        SetCanvasGroups(npcSubtitlePanel, 1f, true, true);
        npcSubtitleText.text = body;
        npcSubtitleText.maxVisibleCharacters = int.MaxValue;
        npcSubtitleText.maxVisibleWords = int.MaxValue;
        npcSubtitleText.maxVisibleLines = int.MaxValue;
        npcSubtitleText.ForceMeshUpdate();
        if (npcNameText != null)
            npcNameText.text = title;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(npcSubtitlePanel);
    }

    private void ShowResponses(IReadOnlyList<string> choices)
    {
        if (responseMenuPanel == null || responseContent == null || responseButtonTemplate == null)
        {
            ChoiceCount = choices?.Count ?? 0;
            return;
        }

        SetActive(dialogueUiRoot?.gameObject, true);
        SetActive(dialoguePanel?.gameObject, true);
        SetActive(pcSubtitlePanel?.gameObject, false);
        SetActive(responseMenuPanel.gameObject, true);
        SetActive(responseTimer?.gameObject, false);
        SetActive(responseButtonTemplate.gameObject, false);
        DisableAutomation(dialogueUiRoot);
        SetCanvasesEnabled(responseMenuPanel, true);
        SetCanvasGroups(dialogueUiRoot, 1f, true, true);
        SetCanvasGroups(dialoguePanel, 1f, true, true);
        SetCanvasGroups(responseMenuPanel, 1f, true, true);

        ChoiceCount = choices?.Count ?? 0;
        for (int i = 0; i < ChoiceCount; i++)
        {
            int choiceIndex = i;
            Button button = Instantiate(responseButtonTemplate, responseContent);
            button.name = $"Tutorial Response {i + 1}";
            DisableAutomation(button.transform as RectTransform);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = choices[i];

            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => choiceSelected?.Invoke(choiceIndex));
            button.interactable = true;
            button.gameObject.SetActive(true);
            responseButtons.Add(button);
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(responseContent);
    }

    private void HideResponses()
    {
        for (int i = 0; i < responseButtons.Count; i++)
        {
            if (responseButtons[i] != null)
            {
                if (Application.isPlaying)
                    Destroy(responseButtons[i].gameObject);
                else
                    DestroyImmediate(responseButtons[i].gameObject);
            }
        }

        responseButtons.Clear();
        ChoiceCount = 0;
        SetActive(responseButtonTemplate?.gameObject, false);
        SetActive(responseMenuPanel?.gameObject, false);
        SetActive(npcSubtitlePanel?.gameObject, false);
        SetActive(pcSubtitlePanel?.gameObject, false);
        SetActive(dialoguePanel?.gameObject, false);
    }

    private void CreateHighlightCanvas()
    {
        GameObject canvasObject = new(
            "TutorialHighlightCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        highlightCanvasRoot = canvasObject.GetComponent<RectTransform>();
        highlightCanvasRoot.anchorMin = Vector2.zero;
        highlightCanvasRoot.anchorMax = Vector2.one;
        highlightCanvasRoot.offsetMin = Vector2.zero;
        highlightCanvasRoot.offsetMax = Vector2.zero;

        highlightCanvas = canvasObject.GetComponent<Canvas>();
        highlightCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        highlightCanvas.sortingOrder = 9;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject highlightObject = new(
            "TutorialTargetHighlight",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Outline));
        highlightObject.transform.SetParent(highlightCanvasRoot, false);
        highlightRect = highlightObject.GetComponent<RectTransform>();
        Image image = highlightObject.GetComponent<Image>();
        image.color = new Color(0f, 1f, 0.45f, 0.04f);
        image.raycastTarget = false;
        Outline outline = highlightObject.GetComponent<Outline>();
        outline.effectColor = new Color(0f, 1f, 0.45f, 1f);
        outline.effectDistance = new Vector2(4f, -4f);
        highlightObject.SetActive(false);
        canvasObject.SetActive(false);
    }

    private void ApplyHighlight(TutorialGuideTarget target)
    {
        if (highlightCanvasRoot == null || highlightRect == null)
            return;

        RectTransform sceneTarget = FindSceneTarget(target);
        if (target == TutorialGuideTarget.None || sceneTarget == null)
        {
            highlightRect.gameObject.SetActive(false);
            highlightCanvasRoot.gameObject.SetActive(false);
            return;
        }

        highlightCanvasRoot.gameObject.SetActive(true);
        highlightRect.gameObject.SetActive(true);
        Vector3[] corners = new Vector3[4];
        sceneTarget.GetWorldCorners(corners);
        if (!TryConvertScreen(corners[0], out Vector2 min)
            || !TryConvertScreen(corners[2], out Vector2 max))
        {
            highlightRect.gameObject.SetActive(false);
            return;
        }

        highlightRect.anchorMin = new Vector2(0.5f, 0.5f);
        highlightRect.anchorMax = new Vector2(0.5f, 0.5f);
        highlightRect.pivot = new Vector2(0.5f, 0.5f);
        highlightRect.anchoredPosition = (min + max) * 0.5f;
        highlightRect.sizeDelta = new Vector2(
            Mathf.Abs(max.x - min.x) + 24f,
            Mathf.Abs(max.y - min.y) + 24f);
    }

    private bool TryConvertScreen(Vector3 worldPosition, out Vector2 localPosition)
    {
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, worldPosition);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            highlightCanvasRoot,
            screenPoint,
            null,
            out localPosition);
    }

    private static RectTransform FindSceneTarget(TutorialGuideTarget target)
    {
        string[] names = target switch
        {
            TutorialGuideTarget.PlayerHand => new[] { "PlayerHand", "PlayerHandRoot", "PlayerHandArea" },
            TutorialGuideTarget.PlayerPlayPile => new[] { "PlayerPlayPile", "PlayPile", "PlayerField" },
            TutorialGuideTarget.DrawPile => new[] { "ViewPileButton", "ViewDrawPileButton", "PlayerDrawPile", "Deck" },
            TutorialGuideTarget.StandButton => new[] { "StandButton" },
            TutorialGuideTarget.PlayerScore => new[] { "PlayerScore", "PlayerScoreText", "PlayerThreshold" },
            TutorialGuideTarget.PokerResult => new[] { "PlayerPlayPile", "PlayPile", "PlayerField" },
            _ => Array.Empty<string>()
        };

        for (int i = 0; i < names.Length; i++)
        {
            RectTransform result = FindSceneRectByName(names[i]);
            if (result != null)
                return result;
        }

        return null;
    }

    private static void DisableAutomation(RectTransform root)
    {
        if (root == null)
            return;

        Animator[] animators = root.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
            animators[i].enabled = false;

        Behaviour[] behaviours = root.GetComponentsInChildren<Behaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour behaviour = behaviours[i];
            string namespaceName = behaviour.GetType().Namespace;
            if (!string.IsNullOrEmpty(namespaceName)
                && namespaceName.StartsWith("PixelCrushers", StringComparison.Ordinal))
                behaviour.enabled = false;
        }
    }

    private static void SetCanvasGroups(RectTransform root, float alpha, bool interactable, bool blocksRaycasts)
    {
        if (root == null)
            return;

        CanvasGroup[] groups = root.GetComponentsInChildren<CanvasGroup>(true);
        for (int i = 0; i < groups.Length; i++)
        {
            groups[i].alpha = alpha;
            groups[i].interactable = interactable;
            groups[i].blocksRaycasts = blocksRaycasts;
        }
    }

    private static void SetCanvasesEnabled(RectTransform root, bool enabled)
    {
        if (root == null)
            return;

        Canvas[] canvases = root.GetComponentsInParent<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
            canvases[i].enabled = enabled;
    }

    private void ConfigureBarkText()
    {
        if (barkText == null)
            return;

        barkText.textWrappingMode = TextWrappingModes.Normal;
        barkText.overflowMode = TextOverflowModes.Overflow;
        barkText.enableAutoSizing = true;
        barkText.fontSizeMin = 18f;
        barkText.fontSizeMax = Mathf.Max(24f, barkText.fontSize);

        LayoutElement layout = barkText.GetComponent<LayoutElement>();
        if (layout == null)
            layout = barkText.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = 430f;
        layout.flexibleWidth = 0f;
    }

    private void UpdateBarkHeight()
    {
        if (barkText == null || barkPanel == null)
            return;

        LayoutElement layout = barkText.GetComponent<LayoutElement>();
        if (layout == null)
            return;

        layout.preferredHeight = Mathf.Ceil(barkText.textBounds.size.y + 4f);
        layout.flexibleHeight = 0f;
        LayoutRebuilder.ForceRebuildLayoutImmediate(barkPanel);
        if (barkPanel.parent is RectTransform parent)
        {
            ContentSizeFitter fitter = parent.GetComponent<ContentSizeFitter>();
            if (fitter != null)
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
            LayoutRebuilder.ForceRebuildLayoutImmediate(barkPanel);
        }
        Canvas.ForceUpdateCanvases();
    }

    private static RectTransform FindSceneRectByName(string objectName)
    {
        RectTransform[] rects = Resources.FindObjectsOfTypeAll<RectTransform>();
        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform rect = rects[i];
            if (rect != null
                && rect.gameObject.scene.IsValid()
                && rect.name == objectName)
                return rect;
        }

        return null;
    }

    private static RectTransform FindChildRect(Transform root, string childName)
    {
        if (root == null)
            return null;

        RectTransform[] rects = root.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rects.Length; i++)
        {
            if (rects[i].name == childName)
                return rects[i];
        }

        return null;
    }

    private static TMP_Text FindChildText(Transform root, string childName)
    {
        if (root == null)
            return null;

        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].name == childName)
                return texts[i];
        }

        return null;
    }

    private static Button FindChildButton(Transform root, string childName)
    {
        if (root == null)
            return null;

        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].name == childName)
                return buttons[i];
        }

        return null;
    }

    private static RectTransform FindAncestorRect(Transform transform, string ancestorName)
    {
        Transform cursor = transform;
        while (cursor != null)
        {
            if (cursor.name == ancestorName)
                return cursor as RectTransform;
            cursor = cursor.parent;
        }

        return null;
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
            target.SetActive(active);
    }
}
