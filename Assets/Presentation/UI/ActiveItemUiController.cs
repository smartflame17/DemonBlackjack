using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class ActiveItemUiController : MonoBehaviour
{
    [SerializeField] private RunManager runManager;
    [SerializeField] private GameplayAssetRegistry assetRegistry;
    [SerializeField] private RectTransform activeItemSlotRoot;
    [SerializeField] private ItemUseMenu itemUseMenu;

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
        Refresh();
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<ActiveItemAddedEvent>(OnActiveItemAdded);
        EventBus.Unsubscribe<ActiveItemRemovedEvent>(OnActiveItemRemoved);
        EventBus.Unsubscribe<ActiveItemCapacityChangedEvent>(OnActiveItemCapacityChanged);
        EventBus.Unsubscribe<RunPhaseChangedEvent>(OnRunPhaseChanged);
        RemoveSlotListeners();
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
            SlotBinding slot = _slots[i];
            bool slotVisible = i < capacity;
            if (!slotVisible)
            {
                BindSlot(slot, null);
                slot.Root.gameObject.SetActive(false);
                continue;
            }

            slot.Root.gameObject.SetActive(true);
            string itemId = run != null && i < run.ActiveItemIds.Count ? run.ActiveItemIds[i] : null;
            BindSlot(slot, itemId);
        }

        itemUseMenu?.RefreshAvailability();
    }

    private void BindSlot(SlotBinding slot, string itemId)
    {
        slot.Button.onClick.RemoveListener(slot.ClickHandler);
        slot.ItemId = string.IsNullOrWhiteSpace(itemId) ? null : itemId;

        bool occupied = slot.ItemId != null;
        slot.Button.interactable = occupied;
        slot.Image.sprite = occupied && assetRegistry != null ? assetRegistry.GetActiveItemSprite(slot.ItemId) : null;
        slot.Image.color = occupied ? Color.white : Color.clear;

        if (occupied)
            slot.TooltipTrigger.Bind(slot.ItemId);
        else
            slot.TooltipTrigger.Clear();

        if (slot.Label != null)
            slot.Label.text = string.Empty;

        if (occupied)
            slot.Button.onClick.AddListener(slot.ClickHandler);
    }

    private void EnsureSlotCount(int capacity)
    {
        if (capacity <= _slots.Count || _slots.Count == 0 || activeItemSlotRoot == null)
            return;

        SlotBinding templateBinding = _slots[0];
        templateBinding.Button.onClick.RemoveListener(templateBinding.ClickHandler);
        RectTransform template = templateBinding.Root;

        while (_slots.Count < capacity)
        {
            RectTransform clone = Instantiate(template, activeItemSlotRoot);
            clone.name = $"ActiveItemSlot{_slots.Count + 1}";
            if (TryCreateBinding(clone, out SlotBinding binding))
                _slots.Add(binding);
            else
            {
                Destroy(clone.gameObject);
                break;
            }
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

    private bool TryCreateBinding(RectTransform root, out SlotBinding binding)
    {
        Button button = root.GetComponentInChildren<Button>(true);
        Image image = button != null ? button.GetComponent<Image>() : null;
        if (button == null || image == null)
        {
            binding = null;
            return false;
        }

        TooltipTrigger tooltipTrigger = root.GetComponent<TooltipTrigger>() ?? root.gameObject.AddComponent<TooltipTrigger>();
        var createdBinding = new SlotBinding(root, button, image, button.GetComponentInChildren<TMP_Text>(true), tooltipTrigger);
        createdBinding.ClickHandler = () => OnSlotClicked(createdBinding);
        binding = createdBinding;
        return true;
    }

    private void OnSlotClicked(SlotBinding slot)
    {
        if (!string.IsNullOrWhiteSpace(slot.ItemId))
            itemUseMenu?.Toggle(slot.ItemId, slot.Root);
    }

    private void RemoveSlotListeners()
    {
        for (int i = 0; i < _slots.Count; i++)
            _slots[i].Button.onClick.RemoveListener(_slots[i].ClickHandler);
    }

    private void ResolveReferences()
    {
        activeItemSlotRoot ??= transform as RectTransform;
        runManager ??= FindFirstObjectByType<RunManager>();
        assetRegistry ??= FindLoadedRegistry();

        if (itemUseMenu == null)
        {
            ItemUseMenu[] menus = Resources.FindObjectsOfTypeAll<ItemUseMenu>();
            if (menus.Length > 0)
                itemUseMenu = menus[0];
        }

        itemUseMenu?.Initialize(runManager, FindFirstObjectByType<BattleController>());
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
        public UnityAction ClickHandler { get; set; }
        public string ItemId { get; set; }
    }
}
