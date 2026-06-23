using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TopMenuBarController : MonoBehaviour
{
    [SerializeField] private RunManager runManager;
    [SerializeField] private GameplayAssetRegistry assetRegistry;
    [SerializeField] private RectTransform activeItemSlotRoot;
    [SerializeField] private ItemUseMenu itemUseMenu;
    [SerializeField] private TMP_Text playerMoneyText;
    [SerializeField] private Button viewFullDeckButton;
    [SerializeField] private DeckViewPanel deckViewPanel;

    private readonly List<SlotBinding> _slots = new();

    private void Awake()
    {
        ResolveReferences();
        CacheAuthoredSlots();
    }

    private void OnEnable()
    {
        ResolveReferences();
        EventBus.Subscribe<ActiveItemAddedEvent>(OnActiveItemAdded);
        EventBus.Subscribe<ActiveItemRemovedEvent>(OnActiveItemRemoved);
        EventBus.Subscribe<ActiveItemCapacityChangedEvent>(OnActiveItemCapacityChanged);
        EventBus.Subscribe<RunPhaseChangedEvent>(OnRunPhaseChanged);
        EventBus.Subscribe<GoldChangedEvent>(OnGoldChanged);
        if (viewFullDeckButton != null)
            viewFullDeckButton.onClick.AddListener(ShowFullDeck);
        Refresh();
    }

    private void Start()
    {
        Refresh();
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<ActiveItemAddedEvent>(OnActiveItemAdded);
        EventBus.Unsubscribe<ActiveItemRemovedEvent>(OnActiveItemRemoved);
        EventBus.Unsubscribe<ActiveItemCapacityChangedEvent>(OnActiveItemCapacityChanged);
        EventBus.Unsubscribe<RunPhaseChangedEvent>(OnRunPhaseChanged);
        EventBus.Unsubscribe<GoldChangedEvent>(OnGoldChanged);
        if (viewFullDeckButton != null)
            viewFullDeckButton.onClick.RemoveListener(ShowFullDeck);
    }

    public void Refresh()
    {
        ResolveReferences();
        CacheAuthoredSlots();

        RunState run = runManager != null ? runManager.RunState : null;
        int capacity = run != null ? run.MaxActiveItemSlots : RunState.DefaultMaxActiveItemSlots;
        EnsureSlotCount(capacity);

        for (int i = 0; i < _slots.Count; i++)
        {
            bool slotVisible = i < capacity;
            SlotBinding slot = _slots[i];
            slot.Root.gameObject.SetActive(slotVisible);
            if (!slotVisible)
                continue;

            string itemId = run != null && i < run.ActiveItemIds.Count ? run.ActiveItemIds[i] : null;
            BindSlot(slot, itemId);
        }

        itemUseMenu?.RefreshAvailability();
        RefreshFullDeckButton(run);
        RefreshPlayerMoney();
    }

    private void ShowFullDeck()
    {
        RunState run = runManager != null ? runManager.RunState : null;
        if (run == null)
            return;

        deckViewPanel?.Show(run.Deck, DeckViewOptions.Default);
    }

    private void BindSlot(SlotBinding slot, string itemId)
    {
        slot.Button.onClick.RemoveAllListeners();
        bool occupied = !string.IsNullOrWhiteSpace(itemId);
        slot.Button.interactable = occupied;
        slot.Image.sprite = occupied && assetRegistry != null ? assetRegistry.GetActiveItemSprite(itemId) : null;
        slot.Image.color = occupied ? Color.white : Color.clear;
        if (occupied)
            slot.TooltipTrigger.Bind(itemId);
        else
            slot.TooltipTrigger.Clear();
        if (slot.Label != null)
            slot.Label.text = string.Empty;

        if (occupied)
            slot.Button.onClick.AddListener(() => itemUseMenu?.Toggle(itemId, slot.Root));
    }

    private void EnsureSlotCount(int capacity)
    {
        if (capacity <= _slots.Count || _slots.Count == 0)
            return;

        RectTransform template = _slots[0].Root;
        while (_slots.Count < capacity)
        {
            RectTransform clone = Instantiate(template, activeItemSlotRoot);
            clone.name = $"ActiveItemSlot{_slots.Count + 1}";
            if (TryCreateBinding(clone, out SlotBinding binding))
                _slots.Add(binding);
            else
                Destroy(clone.gameObject);
        }
    }

    private void CacheAuthoredSlots()
    {
        if (activeItemSlotRoot == null || _slots.Count > 0)
            return;

        for (int i = 0; i < activeItemSlotRoot.childCount; i++)
        {
            if (activeItemSlotRoot.GetChild(i) is RectTransform child && TryCreateBinding(child, out SlotBinding binding))
                _slots.Add(binding);
        }
    }

    private static bool TryCreateBinding(RectTransform root, out SlotBinding binding)
    {
        Button button = root.GetComponentInChildren<Button>(true);
        Image image = button != null ? button.GetComponent<Image>() : null;
        if (button == null || image == null)
        {
            binding = null;
            return false;
        }

        TooltipTrigger tooltipTrigger = root.GetComponent<TooltipTrigger>() ?? root.gameObject.AddComponent<TooltipTrigger>();
        binding = new SlotBinding(root, button, image, button.GetComponentInChildren<TMP_Text>(true), tooltipTrigger);
        return true;
    }

    private void ResolveReferences()
    {
        runManager ??= FindFirstObjectByType<RunManager>();
        assetRegistry ??= FindLoadedRegistry();
        activeItemSlotRoot ??= FindChildRecursive(transform, "ActiveItemSlotRoot") as RectTransform;
        playerMoneyText ??= FindChildRecursive(transform, "PlayerMoney")?.GetComponent<TMP_Text>();
        viewFullDeckButton ??= FindChildRecursive(transform, "ViewFullDeckButton")?.GetComponent<Button>();
        deckViewPanel ??= FindFirstObjectByType<DeckViewPanel>(FindObjectsInactive.Include);

        if (itemUseMenu == null)
        {
            ItemUseMenu[] menus = Resources.FindObjectsOfTypeAll<ItemUseMenu>();
            if (menus.Length > 0)
                itemUseMenu = menus[0];
        }

        itemUseMenu?.Initialize(runManager, FindFirstObjectByType<BattleController>());
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

    private void OnActiveItemAdded(ActiveItemAddedEvent eventData) => Refresh();
    private void OnActiveItemRemoved(ActiveItemRemovedEvent eventData) => Refresh();
    private void OnActiveItemCapacityChanged(ActiveItemCapacityChangedEvent eventData) => Refresh();
    private void OnRunPhaseChanged(RunPhaseChangedEvent eventData) => Refresh();
    private void OnGoldChanged(GoldChangedEvent eventData) => SetPlayerMoney(eventData.CurrentGold);

    private void RefreshPlayerMoney()
    {
        SetPlayerMoney(runManager != null && runManager.RunState != null ? runManager.RunState.Gold : 0);
    }

    private void RefreshFullDeckButton(RunState run)
    {
        if (viewFullDeckButton != null)
            viewFullDeckButton.interactable = run != null && run.Deck != null && run.Deck.Count > 0 && deckViewPanel != null;
    }

    private void SetPlayerMoney(int amount)
    {
        if (playerMoneyText != null)
            playerMoneyText.text = $"${Mathf.Max(0, amount)}";
    }

    private sealed class SlotBinding
    {
        public SlotBinding(RectTransform root, Button button, Image image, TMP_Text label, TooltipTrigger tooltipTrigger)
        {
            Root = root;
            Button = button;
            Image = image;
            Label = label;
            TooltipTrigger = tooltipTrigger;
        }

        public RectTransform Root { get; }
        public Button Button { get; }
        public Image Image { get; }
        public TMP_Text Label { get; }
        public TooltipTrigger TooltipTrigger { get; }
    }
}
