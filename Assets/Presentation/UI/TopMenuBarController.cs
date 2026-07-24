using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class TopMenuBarController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RunManager runManager;
    [SerializeField] private GameplayAssetRegistry assetRegistry;
    [SerializeField] private ShopCatalog catalog;
    [SerializeField] private RelicUiView relicPrefab;
    [SerializeField] private ParticleSystem relicActivationEffect;

    [SerializeField] private RectTransform relicEffectRectTransform;

    [Header("Relics")]
    [SerializeField] private RectTransform relicRoot;
    [SerializeField] private Vector2 relicIconSize = new(30f, 30f);

    [Header("Deck")]
    [SerializeField] private Button viewFullDeckButton;
    [SerializeField] private DeckViewPanel deckViewPanel;

    [Header("Menu")]
    [SerializeField] private Button settingMenuButton;
    [SerializeField] private SettingMenuController settingMenuController;

    private readonly List<RelicBinding> _relics = new();
    private readonly Dictionary<string, int> _relicCounterValues = new();

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        EventBus.Subscribe<RelicAddedEvent>(OnRelicAdded);
        EventBus.Subscribe<RelicCounterChangedEvent>(OnRelicCounterChanged);
        EventBus.Subscribe<RunPhaseChangedEvent>(OnRunPhaseChanged);
        EventBus.Subscribe<RelicActivatedEvent>(OnRelicActivated);
        if (viewFullDeckButton != null)
            viewFullDeckButton.onClick.AddListener(ShowFullDeck);
        if (settingMenuButton != null)
            settingMenuButton.onClick.AddListener(ShowSettingMenu);
        Refresh();
    }

    private void Start()
    {
        Refresh();
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<RelicAddedEvent>(OnRelicAdded);
        EventBus.Unsubscribe<RelicCounterChangedEvent>(OnRelicCounterChanged);
        EventBus.Unsubscribe<RunPhaseChangedEvent>(OnRunPhaseChanged);
        EventBus.Unsubscribe<RelicActivatedEvent>(OnRelicActivated);
        if (viewFullDeckButton != null)
            viewFullDeckButton.onClick.RemoveListener(ShowFullDeck);
    }

    public void Refresh()
    {
        ResolveReferences();

        RunState run = runManager != null ? runManager.RunState : null;
        RefreshRelics(run);
        RefreshFullDeckButton(run);
    }

    private void ShowFullDeck()
    {
        RunState run = runManager != null ? runManager.RunState : null;
        if (run == null)
            return;

        deckViewPanel?.Show(run.Deck, DeckViewOptions.Default);
    }

    private void RefreshRelics(RunState run)
    {
        int relicCount = run != null ? run.RelicIds.Count : 0;
        EnsureRelicCount(relicRoot != null ? relicCount : 0);

        for (int i = 0; i < _relics.Count; i++)
        {
            bool visible = i < relicCount;
            RelicBinding relic = _relics[i];
            relic.Root.gameObject.SetActive(visible);
            if (!visible)
            {
                relic.RelicId = null;
                relic.TooltipTrigger.Clear();
                continue;
            }

            BindRelic(relic, run.RelicIds[i]);
        }
    }

    private void EnsureRelicCount(int count)
    {
        while (_relics.Count > count)
        {
            int index = _relics.Count - 1;
            RelicBinding relic = _relics[index];
            _relics.RemoveAt(index);
            if (relic.Root != null)
                Destroy(relic.Root.gameObject);
        }

        if (relicRoot == null)
            return;

        while (_relics.Count < count)
            _relics.Add(CreateRelicBinding(_relics.Count));
    }

    private RelicBinding CreateRelicBinding(int index)
    {
        RelicUiView view = ResolveRelicPrefab() != null ? Instantiate(relicPrefab, relicRoot, false) : CreateFallbackRelicView();
        view.name = $"RelicButton{index + 1}";
        view.gameObject.layer = relicRoot.gameObject.layer;

        RectTransform root = view.RectTransform;
        root.sizeDelta = relicIconSize;

        Button button = view.Button;
        if (button != null)
        {
            button.interactable = true;
            button.onClick.RemoveAllListeners();
        }

        TooltipTrigger tooltipTrigger = view.GetComponent<TooltipTrigger>() ?? view.gameObject.AddComponent<TooltipTrigger>();
        return new RelicBinding(root, view, tooltipTrigger);
    }

    private void BindRelic(RelicBinding relic, string relicId)
    {
        bool occupied = !string.IsNullOrWhiteSpace(relicId);
        relic.Root.gameObject.SetActive(occupied);
        relic.RelicId = occupied ? relicId : null;
        RelicDefinition definition = GetRelicDefinition(relicId);
        int counterValue = _relicCounterValues.TryGetValue(relicId, out int value) ? value : 0;
        relic.View.Bind(occupied && assetRegistry != null ? assetRegistry.GetRelicSprite(relicId) : null, relicId, definition != null && definition.HasCounter, counterValue);
        if (occupied)
            relic.TooltipTrigger.Bind(relicId);
        else
            relic.TooltipTrigger.Clear();
    }

    private void ResolveReferences()
    {
        runManager ??= FindFirstObjectByType<RunManager>();
        assetRegistry ??= FindLoadedRegistry();
        catalog ??= Resources.Load<ShopCatalog>("Shop/ShopCatalog");
        catalog ??= ShopCatalog.CreateRuntimeDefault();
        relicRoot ??= FindChildRecursive(transform, "RelicRoot") as RectTransform;
        viewFullDeckButton ??= FindChildRecursive(transform, "ViewFullDeckButton")?.GetComponent<Button>();
        deckViewPanel ??= FindFirstObjectByType<DeckViewPanel>(FindObjectsInactive.Include);
        settingMenuButton ??= FindChildRecursive(transform, "SettingMenuButton")?.GetComponent<Button>();
        settingMenuController ??= FindFirstObjectByType<SettingMenuController>(FindObjectsInactive.Include);
        relicActivationEffect ??= FindFirstObjectByType<ParticleSystem>(FindObjectsInactive.Include);
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
            return null;
        if (root.name == childName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChildRecursive(root.GetChild(i), childName);
            if (result != null)
                return result;
        }
        return null;
    }

    private static GameplayAssetRegistry FindLoadedRegistry()
    {
        GameplayAssetRegistry[] registries = Resources.FindObjectsOfTypeAll<GameplayAssetRegistry>();
        return registries.Length > 0 ? registries[0] : null;
    }

    private RelicDefinition GetRelicDefinition(string relicId)
    {
        if (catalog != null && catalog.TryGetDefinition(relicId, out ShopContentDefinition definition))
            return definition as RelicDefinition;

        return null;
    }

    private RelicUiView ResolveRelicPrefab()
    {
        if (relicPrefab != null)
            return relicPrefab;

        RelicUiView[] candidates = Resources.FindObjectsOfTypeAll<RelicUiView>();
        for (int i = 0; i < candidates.Length; i++)
        {
            RelicUiView candidate = candidates[i];
            if (candidate != null && candidate.name == "RelicPrefab" && !candidate.gameObject.scene.IsValid())
            {
                relicPrefab = candidate;
                return relicPrefab;
            }
        }

        Debug.LogWarning($"{nameof(TopMenuBarController)} is missing a relic prefab reference.", this);
        return null;
    }

    private RelicUiView CreateFallbackRelicView()
    {
        GameObject rootObject = new("RelicButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(TooltipTrigger), typeof(RelicUiView));
        rootObject.layer = relicRoot.gameObject.layer;
        RectTransform root = rootObject.GetComponent<RectTransform>();
        root.SetParent(relicRoot, false);
        root.sizeDelta = relicIconSize;

        Image image = rootObject.GetComponent<Image>();
        image.preserveAspect = true;
        image.color = Color.white;

        Button button = rootObject.GetComponent<Button>();
        button.targetGraphic = image;

        RelicUiView view = rootObject.GetComponent<RelicUiView>();
        view.Initialize(image, button, null);
        return view;
    }

    private void OnRelicAdded(RelicAddedEvent eventData) => Refresh();
    private void OnRelicCounterChanged(RelicCounterChangedEvent eventData)
    {
        if (string.IsNullOrWhiteSpace(eventData.RelicId))
            return;

        _relicCounterValues[eventData.RelicId] = Mathf.Max(0, eventData.Value);

        for (int i = 0; i < _relics.Count; i++)
        {
            RelicBinding relic = _relics[i];
            if (!string.Equals(relic.RelicId, eventData.RelicId, System.StringComparison.Ordinal))
                continue;

            RelicDefinition definition = GetRelicDefinition(eventData.RelicId);
            if (definition != null && definition.HasCounter)
                relic.View.SetCounter(true, _relicCounterValues[eventData.RelicId]);
        }
    }
    
    private void OnRelicActivated(RelicActivatedEvent eventData)
    {
        for (int i = 0; i < _relics.Count; i++)
        {
            RelicBinding relic = _relics[i];
            if (!string.Equals(relic.RelicId, eventData.RelicId, System.StringComparison.Ordinal))
                continue;

            relicActivationEffect.textureSheetAnimation.SetSprite(0, relic.View.Button.image.sprite);

            relicEffectRectTransform.position = relic.View.RectTransform.position;
 
            relicActivationEffect.Play();
            Debug.Log($"Relic activated: {eventData.RelicId}");
            break;
        }
    }
    private void OnRunPhaseChanged(RunPhaseChangedEvent eventData) => Refresh();

    private void RefreshFullDeckButton(RunState run)
    {
        if (viewFullDeckButton != null)
            viewFullDeckButton.interactable = run != null && run.Deck != null && run.Deck.Count > 0 && deckViewPanel != null;
    }

    private void ShowSettingMenu()
    {
        settingMenuController.gameObject.SetActive(true);
    }

    private sealed class RelicBinding
    {
        public RelicBinding(RectTransform root, RelicUiView view, TooltipTrigger tooltipTrigger)
        {
            Root = root;
            View = view;
            TooltipTrigger = tooltipTrigger;
        }

        public RectTransform Root { get; }
        public RelicUiView View { get; }
        public TooltipTrigger TooltipTrigger { get; }
        public string RelicId { get; set; }
    }
}
