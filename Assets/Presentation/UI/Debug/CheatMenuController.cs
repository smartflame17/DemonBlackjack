using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class CheatMenuController :
    MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    [Header("Gameplay References")]
    [SerializeField] private RunManager runManager;
    [SerializeField] private BattleController battleController;
    [SerializeField] private BattleUiPresenter battleUiPresenter;
    [SerializeField] private ShopCatalog catalog;

    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text currentMoneyText;
    [SerializeField] private TMP_InputField moneyInput;
    [SerializeField] private Button setMoneyButton;
    [SerializeField] private Button subtractHundredButton;
    [SerializeField] private Button addHundredButton;
    [SerializeField] private Button addThousandButton;

    [Header("Purchases")]
    [SerializeField] private TMP_Dropdown activeItemDropdown;
    [SerializeField] private Button grantActiveItemButton;
    [SerializeField] private TMP_Dropdown relicDropdown;
    [SerializeField] private Button grantRelicButton;
    [SerializeField] private TMP_Dropdown upgradeDropdown;
    [SerializeField] private TMP_Dropdown rankDropdown;
    [SerializeField] private Button grantUpgradeButton;
    [SerializeField] private TMP_Text statusText;

    private readonly List<ActiveItemDefinition> _activeItems = new();
    private readonly List<RelicDefinition> _relics = new();
    private readonly List<CardUpgradeDefinition> _upgrades = new();
    private readonly List<Rank> _compatibleRanks = new();
    private bool _subscribed;
    private ShopCatalog _runtimeCatalog;
    private RectTransform _draggedPanel;
    private RectTransform _dragParent;
    private Vector2 _dragStartPointerPosition;
    private Vector2 _dragStartPanelPosition;
    private bool _isDragging;

    private static bool IsAvailableInCurrentBuild =>
        IsAvailableForBuild(Application.isEditor, Debug.isDebugBuild);

    internal static bool IsAvailableForBuild(bool isEditor, bool isDebugBuild) =>
        isEditor || isDebugBuild;

    private RunState CurrentRun => runManager != null ? runManager.RunState : null;

    private void Awake()
    {
        if (!IsAvailableInCurrentBuild)
        {
            RemoveDebugCanvasFromReleaseBuild();
            return;
        }

        ResolveReferences();
        PopulateCatalogOptions();
    }

    private void OnEnable()
    {
        if (!IsAvailableInCurrentBuild)
        {
            RemoveDebugCanvasFromReleaseBuild();
            return;
        }

        ResolveReferences();
        PopulateCatalogOptions();
        AddUiListeners();
        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        RemoveUiListeners();
        Unsubscribe();
    }

    private void OnDestroy()
    {
        if (_runtimeCatalog != null)
            Destroy(_runtimeCatalog);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _draggedPanel = panelRoot != null ? panelRoot.transform as RectTransform : null;
        _dragParent = _draggedPanel != null ? _draggedPanel.parent as RectTransform : null;
        if (_draggedPanel == null
            || _dragParent == null
            || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _dragParent,
                eventData.position,
                eventData.pressEventCamera,
                out _dragStartPointerPosition))
        {
            _isDragging = false;
            return;
        }

        _dragStartPanelPosition = _draggedPanel.anchoredPosition;
        _isDragging = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging
            || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _dragParent,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 pointerPosition))
        {
            return;
        }

        _draggedPanel.anchoredPosition =
            _dragStartPanelPosition + pointerPosition - _dragStartPointerPosition;
        KeepPanelInsideParent();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _isDragging = false;
    }

    private void KeepPanelInsideParent()
    {
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
            _dragParent,
            _draggedPanel);
        Rect parentRect = _dragParent.rect;
        Vector2 correction = Vector2.zero;

        if (bounds.min.x < parentRect.xMin)
            correction.x = parentRect.xMin - bounds.min.x;
        else if (bounds.max.x > parentRect.xMax)
            correction.x = parentRect.xMax - bounds.max.x;

        if (bounds.min.y < parentRect.yMin)
            correction.y = parentRect.yMin - bounds.min.y;
        else if (bounds.max.y > parentRect.yMax)
            correction.y = parentRect.yMax - bounds.max.y;

        _draggedPanel.anchoredPosition += correction;
    }

    private void AddUiListeners()
    {
        RemoveUiListeners();
        setMoneyButton?.onClick.AddListener(ApplyMoneyInput);
        subtractHundredButton?.onClick.AddListener(SubtractHundred);
        addHundredButton?.onClick.AddListener(AddHundred);
        addThousandButton?.onClick.AddListener(AddThousand);
        grantActiveItemButton?.onClick.AddListener(GrantActiveItem);
        grantRelicButton?.onClick.AddListener(GrantRelic);
        grantUpgradeButton?.onClick.AddListener(GrantUpgrade);
        upgradeDropdown?.onValueChanged.AddListener(OnUpgradeSelectionChanged);
    }

    private void RemoveUiListeners()
    {
        setMoneyButton?.onClick.RemoveListener(ApplyMoneyInput);
        subtractHundredButton?.onClick.RemoveListener(SubtractHundred);
        addHundredButton?.onClick.RemoveListener(AddHundred);
        addThousandButton?.onClick.RemoveListener(AddThousand);
        grantActiveItemButton?.onClick.RemoveListener(GrantActiveItem);
        grantRelicButton?.onClick.RemoveListener(GrantRelic);
        grantUpgradeButton?.onClick.RemoveListener(GrantUpgrade);
        upgradeDropdown?.onValueChanged.RemoveListener(OnUpgradeSelectionChanged);
    }

    private void Subscribe()
    {
        if (_subscribed)
            return;

        EventBus.Subscribe<MoneyChangedEvent>(OnMoneyChanged);
        EventBus.Subscribe<RunPhaseChangedEvent>(OnRunPhaseChanged);
        EventBus.Subscribe<BattleStartedEvent>(OnBattleStarted);
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
            return;

        EventBus.Unsubscribe<MoneyChangedEvent>(OnMoneyChanged);
        EventBus.Unsubscribe<RunPhaseChangedEvent>(OnRunPhaseChanged);
        EventBus.Unsubscribe<BattleStartedEvent>(OnBattleStarted);
        _subscribed = false;
    }

    private void ResolveReferences()
    {
        runManager ??= FindFirstObjectByType<RunManager>(FindObjectsInactive.Include);
        battleController ??= FindFirstObjectByType<BattleController>(FindObjectsInactive.Include);
        battleUiPresenter ??= FindFirstObjectByType<BattleUiPresenter>(FindObjectsInactive.Include);

        if (catalog == null)
        {
            ShopCatalog[] loadedCatalogs = Resources.FindObjectsOfTypeAll<ShopCatalog>();
            if (loadedCatalogs.Length > 0)
                catalog = loadedCatalogs[0];
        }

        if (catalog == null)
        {
            _runtimeCatalog = ShopCatalog.CreateRuntimeDefault();
            catalog = _runtimeCatalog;
        }
    }

    private void PopulateCatalogOptions()
    {
        _activeItems.Clear();
        _relics.Clear();
        _upgrades.Clear();

        if (catalog != null)
        {
            AddDefinitions(catalog.ActiveItems, _activeItems);
            AddDefinitions(catalog.Relics, _relics);
            AddDefinitions(catalog.CardUpgrades, _upgrades);
        }

        SetDefinitionOptions(activeItemDropdown, _activeItems);
        SetDefinitionOptions(relicDropdown, _relics);
        SetDefinitionOptions(upgradeDropdown, _upgrades);
        RefreshCompatibleRanks();
    }

    private static void AddDefinitions<T>(IReadOnlyList<T> source, List<T> destination)
        where T : ShopContentDefinition
    {
        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] != null)
                destination.Add(source[i]);
        }
    }

    private static void SetDefinitionOptions<T>(TMP_Dropdown dropdown, IReadOnlyList<T> definitions)
        where T : ShopContentDefinition
    {
        if (dropdown == null)
            return;

        var options = new List<TMP_Dropdown.OptionData>(definitions.Count);
        for (int i = 0; i < definitions.Count; i++)
        {
            T definition = definitions[i];
            options.Add(new TMP_Dropdown.OptionData($"{definition.DisplayName} [{definition.Id}]"));
        }

        dropdown.ClearOptions();
        dropdown.AddOptions(options);
        dropdown.SetValueWithoutNotify(0);
        dropdown.RefreshShownValue();
    }

    private void OnUpgradeSelectionChanged(int _)
    {
        RefreshCompatibleRanks();
        RefreshControlState();
    }

    private void RefreshCompatibleRanks()
    {
        _compatibleRanks.Clear();

        CardUpgradeDefinition upgrade = GetSelected(_upgrades, upgradeDropdown);
        if (upgrade != null)
        {
            foreach (Rank rank in Enum.GetValues(typeof(Rank)))
            {
                if (upgrade.CanApplyToRank(rank))
                    _compatibleRanks.Add(rank);
            }
        }

        if (rankDropdown == null)
            return;

        var options = new List<string>(_compatibleRanks.Count);
        for (int i = 0; i < _compatibleRanks.Count; i++)
            options.Add(_compatibleRanks[i].ToString());

        rankDropdown.ClearOptions();
        rankDropdown.AddOptions(options);
        rankDropdown.SetValueWithoutNotify(0);
        rankDropdown.RefreshShownValue();
    }

    private void Refresh()
    {
        ResolveReferences();

        RunState run = CurrentRun;
        int money = run != null ? run.Money : 0;
        if (currentMoneyText != null)
            currentMoneyText.text = run != null ? $"Current Money: ${money}" : "Current Money: Run not initialized";

        if (moneyInput != null && !moneyInput.isFocused)
            moneyInput.SetTextWithoutNotify(money.ToString());

        RefreshControlState();
    }

    private void RefreshControlState()
    {
        bool hasRun = CurrentRun != null;
        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (moneyInput != null)
            moneyInput.interactable = hasRun;
        SetInteractable(setMoneyButton, hasRun);
        SetInteractable(subtractHundredButton, hasRun);
        SetInteractable(addHundredButton, hasRun);
        SetInteractable(addThousandButton, hasRun);

        if (activeItemDropdown != null)
            activeItemDropdown.interactable = hasRun && _activeItems.Count > 0;
        SetInteractable(grantActiveItemButton, hasRun && _activeItems.Count > 0);

        if (relicDropdown != null)
            relicDropdown.interactable = hasRun && _relics.Count > 0;
        SetInteractable(grantRelicButton, hasRun && _relics.Count > 0);

        if (upgradeDropdown != null)
            upgradeDropdown.interactable = hasRun && _upgrades.Count > 0;
        if (rankDropdown != null)
            rankDropdown.interactable = hasRun && _compatibleRanks.Count > 0;
        SetInteractable(grantUpgradeButton, hasRun && _upgrades.Count > 0 && _compatibleRanks.Count > 0);
    }

    private static void SetInteractable(Selectable selectable, bool interactable)
    {
        if (selectable != null)
            selectable.interactable = interactable;
    }

    private void ApplyMoneyInput()
    {
        if (CurrentRun == null)
        {
            SetStatus("A run must be initialized first.", StatusKind.Error);
            return;
        }

        if (moneyInput == null || !long.TryParse(moneyInput.text, out long parsed))
        {
            SetStatus("Enter a valid whole-number money value.", StatusKind.Error);
            return;
        }

        int target = (int)Math.Clamp(parsed, 0L, int.MaxValue);
        SetPlayerMoney(target);
        SetStatus($"Player money set to ${target}.", StatusKind.Success);
    }

    private void SubtractHundred() => AdjustPlayerMoney(-100);
    private void AddHundred() => AdjustPlayerMoney(100);
    private void AddThousand() => AdjustPlayerMoney(1000);

    private void AdjustPlayerMoney(int delta)
    {
        RunState run = CurrentRun;
        if (run == null)
        {
            SetStatus("A run must be initialized first.", StatusKind.Error);
            return;
        }

        int target = (int)Math.Clamp((long)run.Money + delta, 0L, int.MaxValue);
        SetPlayerMoney(target);
        SetStatus($"Player money adjusted to ${target}.", StatusKind.Success);
    }

    private void SetPlayerMoney(int target)
    {
        RunState run = CurrentRun;
        if (run == null)
            return;

        target = Math.Max(0, target);
        int delta = target - run.Money;
        BattleState battle = battleController != null ? battleController.BattleState : null;
        bool canUseBattleMoneyHooks = battle != null && ReferenceEquals(battle.RunState, run);

        if (delta > 0 && canUseBattleMoneyHooks)
            battle.AddPlayerMoney(delta);
        else if (delta < 0 && canUseBattleMoneyHooks)
            battle.LosePlayerMoney(-delta);
        else if (delta != 0)
            run.SetMoney(target);

        Refresh();
    }

    private void GrantActiveItem()
    {
        ActiveItemDefinition definition = GetSelected(_activeItems, activeItemDropdown);
        RunState run = CurrentRun;
        if (run == null || definition == null)
        {
            SetStatus("Select an active item after the run starts.", StatusKind.Error);
            return;
        }

        ShopPurchaseResult result = run.TryPurchaseActiveItem(definition.Id, 0);
        PublishPurchaseResult(ShopOfferType.ActiveItem, definition.Id, null, result);
        ReportPurchaseResult("active item", definition.DisplayName, result);
    }

    private void GrantRelic()
    {
        RelicDefinition definition = GetSelected(_relics, relicDropdown);
        RunState run = CurrentRun;
        if (run == null || definition == null)
        {
            SetStatus("Select a relic after the run starts.", StatusKind.Error);
            return;
        }

        ShopPurchaseResult result = run.TryPurchaseRelic(definition.Id, 0);
        PublishPurchaseResult(ShopOfferType.Relic, definition.Id, null, result);
        ReportPurchaseResult("relic", definition.DisplayName, result);
    }

    private void GrantUpgrade()
    {
        CardUpgradeDefinition definition = GetSelected(_upgrades, upgradeDropdown);
        RunState run = CurrentRun;
        Rank? rank = GetSelectedRank();
        if (run == null || definition == null || !rank.HasValue)
        {
            SetStatus("Select an upgrade and compatible rank after the run starts.", StatusKind.Error);
            return;
        }

        ShopPurchaseResult result = run.TryPurchaseRankUpgrade(rank.Value, definition.Id, 0);
        PublishPurchaseResult(ShopOfferType.CardUpgrade, definition.Id, rank, result);
        if (result.Succeeded)
        {
            ResolveReferences();
            battleUiPresenter?.Refresh();
        }

        ReportPurchaseResult("upgrade", $"{definition.DisplayName} on {rank.Value}", result);
    }

    private static T GetSelected<T>(IReadOnlyList<T> values, TMP_Dropdown dropdown)
        where T : class
    {
        if (dropdown == null || values == null || dropdown.value < 0 || dropdown.value >= values.Count)
            return null;

        return values[dropdown.value];
    }

    private Rank? GetSelectedRank()
    {
        if (rankDropdown == null || rankDropdown.value < 0 || rankDropdown.value >= _compatibleRanks.Count)
            return null;

        return _compatibleRanks[rankDropdown.value];
    }

    private static void PublishPurchaseResult(ShopOfferType type, string id, Rank? rank, ShopPurchaseResult result)
    {
        if (result.Succeeded)
            EventBus.Publish(new ShopPurchaseSucceededEvent(type, id, rank, result.Price, result.Refund));
        else
            EventBus.Publish(new ShopPurchaseFailedEvent(type, id, rank, result.Failure));
    }

    private void ReportPurchaseResult(string contentType, string displayName, ShopPurchaseResult result)
    {
        if (result.Succeeded)
        {
            SetStatus($"Granted {contentType}: {displayName}.", StatusKind.Success);
            Refresh();
            return;
        }

        SetStatus($"Could not grant {contentType}: {FormatFailure(result.Failure)}.", StatusKind.Error);
    }

    private static string FormatFailure(ShopPurchaseFailure failure)
    {
        return failure switch
        {
            ShopPurchaseFailure.InvalidOffer => "invalid selection",
            ShopPurchaseFailure.AlreadyPurchased => "already purchased",
            ShopPurchaseFailure.AlreadyOwned => "already owned",
            ShopPurchaseFailure.ActiveItemSlotsFull => "active item slots are full",
            ShopPurchaseFailure.InsufficientFunds => "insufficient funds",
            _ => "unknown purchase failure"
        };
    }

    private void SetStatus(string message, StatusKind kind)
    {
        if (statusText == null)
            return;

        statusText.text = message;
        statusText.color = kind switch
        {
            StatusKind.Success => new Color(0.55f, 1f, 0.62f),
            StatusKind.Error => new Color(1f, 0.55f, 0.55f),
            _ => Color.white
        };
    }

    private void OnMoneyChanged(MoneyChangedEvent eventData)
    {
        if (eventData.Owner == Combatant.Player)
            Refresh();
    }

    private void OnRunPhaseChanged(RunPhaseChangedEvent eventData)
    {
        ResolveReferences();
        Refresh();
    }

    private void OnBattleStarted(BattleStartedEvent eventData)
    {
        ResolveReferences();
        Refresh();
    }

    private void RemoveDebugCanvasFromReleaseBuild()
    {
        if (!Application.isPlaying)
            return;

        Canvas parentCanvas = GetComponentInParent<Canvas>();
        GameObject debugCanvas = parentCanvas != null ? parentCanvas.gameObject : gameObject;
        debugCanvas.SetActive(false);
        Destroy(debugCanvas);
    }

    private enum StatusKind
    {
        Neutral,
        Success,
        Error
    }
}
