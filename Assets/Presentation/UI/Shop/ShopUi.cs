using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public sealed class ShopUi : MonoBehaviour
{
    [SerializeField] private RunManager runManager;
    [SerializeField] private BattleController battleController;
    [SerializeField] private GameplayAssetRegistry assetRegistry;
    [SerializeField] private BattleUiCardView cardPrefab;
    [SerializeField] private ShopCatalog catalog;
    [SerializeField] private Button continueButton;
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private GameObject upgradePreviewRoot;
    [SerializeField] private GameObject itemPreviewRoot;
    [SerializeField] private float shopPanelAnimationDuration = 2.0f;
    [SerializeField] private Ease shopPanelAnimationEase = Ease.OutBounce;
    [SerializeField] private int activeItemOfferCount = 3;
    [SerializeField] private int relicOfferCount = 3;
    [SerializeField] private int upgradeOfferCount = 5;

    private readonly List<ShopSlotView> _itemSlots = new();
    private readonly List<ShopSlotView> _relicSlots = new();
    private readonly List<ShopSlotView> _upgradeSlots = new();
    private readonly List<ActiveItemDefinition> _currentItemOffers = new();
    private readonly List<RelicDefinition> _currentRelicOffers = new();
    private readonly List<CardUpgradeOffer> _currentUpgradeOffers = new();
    private readonly HashSet<string> _soldOfferKeys = new();
    private int _generatedShopCycle = -1;
    private RectTransform shopPanelRectTransform => shopPanel != null ? shopPanel.transform as RectTransform : null;

    private void Awake()
    {
        runManager ??= FindFirstObjectByType<RunManager>();
        battleController ??= FindFirstObjectByType<BattleController>();
        assetRegistry ??= FindLoadedRegistry();
        catalog ??= Resources.Load<ShopCatalog>("Shop/ShopCatalog");
        catalog ??= ShopCatalog.CreateRuntimeDefault();
        continueButton ??= FindDescendant("ContinueButton")?.GetComponent<Button>();
        shopPanel ??= FindDescendant("ShopPanel");
        upgradePreviewRoot ??= FindDescendant("UpgradePreviewRoot");
        itemPreviewRoot ??= FindDescendant("ItemPreviewRoot");
        BindSceneSlots();
    }

    private void OnEnable()
    {
        shopPanel.SetActive(true);
        shopPanelRectTransform.DOAnchorPos(new Vector2(0, 0), shopPanelAnimationDuration).SetEase(shopPanelAnimationEase);
        if (continueButton != null)
            continueButton.onClick.AddListener(Continue);
        GenerateOffers();
    }

    private void OnDisable()
    {
        shopPanel.SetActive(false);
        shopPanelRectTransform.DOAnchorPos(new Vector2(0, 1000), shopPanelAnimationDuration).SetEase(shopPanelAnimationEase);
        if (continueButton != null)
            continueButton.onClick.RemoveListener(Continue);
    }

    private void GenerateOffers()
    {
        if (runManager == null || runManager.RunState == null || battleController == null || battleController.BattleState == null)
            return;

        RunState run = runManager.RunState;
        int roundNumber = battleController.BattleState.RoundNumber;
        int shopCycle = ShopOfferGenerator.GetShopCycle(roundNumber);
        if (_generatedShopCycle != shopCycle)
            GenerateNewOfferCycle(run, roundNumber, shopCycle);

        BindPreviews(_currentItemOffers, _currentUpgradeOffers);
        BindItems(_currentItemOffers, run);
        BindRelics(_currentRelicOffers, run);
        BindUpgrades(_currentUpgradeOffers, run);
    }

    private void GenerateNewOfferCycle(RunState run, int roundNumber, int shopCycle)
    {
        _generatedShopCycle = shopCycle;
        _soldOfferKeys.Clear();
        _currentItemOffers.Clear();
        _currentRelicOffers.Clear();
        _currentUpgradeOffers.Clear();

        var random = new System.Random(ShopOfferGenerator.CreateSeed(run, roundNumber));

        _currentItemOffers.AddRange(ShopOfferGenerator.TakeRandom(catalog.ActiveItems, activeItemOfferCount, random));
        var eligibleRelics = new List<RelicDefinition>();
        for (int i = 0; i < catalog.Relics.Count; i++)
        {
            RelicDefinition relic = catalog.Relics[i];
            if (relic != null && !run.HasRelic(relic.Id))
                eligibleRelics.Add(relic);
        }
        _currentRelicOffers.AddRange(ShopOfferGenerator.TakeRandom(eligibleRelics, relicOfferCount, random));

        List<CardUpgradeOffer> upgradePool = ShopOfferGenerator.CreateCardUpgradeOfferPool(catalog.CardUpgrades);
        _currentUpgradeOffers.AddRange(ShopOfferGenerator.TakeRandom(upgradePool, upgradeOfferCount, random));
    }

    private void BindPreviews(IReadOnlyList<ActiveItemDefinition> items, IReadOnlyList<CardUpgradeOffer> upgrades)
    {
        if (upgradePreviewRoot != null)
        {
            int offerIndex = 0;
            Transform root = upgradePreviewRoot.transform;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (!child.TryGetComponent(out Image preview))
                    continue;

                bool hasOffer = upgrades != null
                    && offerIndex < upgrades.Count
                    && upgrades[offerIndex].Definition != null;
                preview.sprite = hasOffer && assetRegistry != null
                    ? assetRegistry.GetCardUpgradeSprite(upgrades[offerIndex].Definition.Id)
                    : null;
                child.gameObject.SetActive(hasOffer);
                offerIndex++;
            }
        }

        if (itemPreviewRoot != null)
        {
            int offerIndex = 0;
            Transform root = itemPreviewRoot.transform;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (!child.TryGetComponent(out Image preview))
                    continue;

                bool hasOffer = items != null
                    && offerIndex < items.Count
                    && items[offerIndex] != null;
                preview.sprite = hasOffer && assetRegistry != null
                    ? assetRegistry.GetActiveItemSprite(items[offerIndex].Id)
                    : null;
                child.gameObject.SetActive(hasOffer);
                offerIndex++;
            }
        }
    }

    private void BindItems(IReadOnlyList<ActiveItemDefinition> offers, RunState run)
    {
        for (int i = 0; i < _itemSlots.Count; i++)
        {
            if (i >= offers.Count || offers[i] == null) { _itemSlots[i].SetUnavailable(); continue; }
            ActiveItemDefinition offer = offers[i];
            ShopSlotView slot = _itemSlots[i];
            string offerKey = CreateOfferKey(ShopOfferType.ActiveItem, offer.Id, null);
            if (_soldOfferKeys.Contains(offerKey)) { slot.SetUnavailable(); continue; }
            slot.Bind(assetRegistry != null ? assetRegistry.GetActiveItemSprite(offer.Id) : null, offer.Price, offer.DisplayName, offer.Id, () => PurchaseItem(slot, offer, run, offerKey));
        }
    }

    private void BindRelics(IReadOnlyList<RelicDefinition> offers, RunState run)
    {
        for (int i = 0; i < _relicSlots.Count; i++)
        {
            if (i >= offers.Count || offers[i] == null) { _relicSlots[i].SetUnavailable(); continue; }
            RelicDefinition offer = offers[i];
            ShopSlotView slot = _relicSlots[i];
            string offerKey = CreateOfferKey(ShopOfferType.Relic, offer.Id, null);
            if (_soldOfferKeys.Contains(offerKey)) { slot.SetUnavailable(); continue; }
            slot.Bind(assetRegistry != null ? assetRegistry.GetRelicSprite(offer.Id) : null, offer.Price, offer.DisplayName, offer.Id, () => PurchaseRelic(slot, offer, run, offerKey));
        }
    }

    private void BindUpgrades(IReadOnlyList<CardUpgradeOffer> offers, RunState run)
    {
        for (int i = 0; i < _upgradeSlots.Count; i++)
        {
            if (i >= offers.Count || offers[i].Definition == null) { _upgradeSlots[i].SetUnavailable(); continue; }
            CardUpgradeOffer offer = offers[i];
            ShopSlotView slot = _upgradeSlots[i];
            Card card = new(Suit.Hearts, offer.Rank, offer.Definition.Id);
            Sprite sprite = assetRegistry != null ? assetRegistry.GetCardFront(card) : null;
            string offerKey = CreateOfferKey(ShopOfferType.CardUpgrade, offer.Definition.Id, offer.Rank);
            if (_soldOfferKeys.Contains(offerKey)) { slot.SetUnavailable(); continue; }
            slot.BindCard(card, sprite, assetRegistry, offer.Definition.Price, offer.Definition.Id, () => PurchaseUpgrade(slot, offer, run, battleController, offerKey));
        }
    }

    private static void PublishResult(ShopOfferType type, string id, Rank? rank, ShopPurchaseResult result)
    {
        if (result.Succeeded)
            EventBus.Publish(new ShopPurchaseSucceededEvent(type, id, rank, result.Price, result.Refund));
        else
            EventBus.Publish(new ShopPurchaseFailedEvent(type, id, rank, result.Failure));
    }

    private void PurchaseItem(ShopSlotView slot, ActiveItemDefinition offer, RunState run, string offerKey)
    {
        ShopPurchaseResult result = run.TryPurchaseActiveItem(offer.Id, offer.Price);
        PublishResult(ShopOfferType.ActiveItem, offer.Id, null, result);
        if (!result.Succeeded)
            return;

        _soldOfferKeys.Add(offerKey);
        slot.SetUnavailable();
    }

    private void PurchaseRelic(ShopSlotView slot, RelicDefinition offer, RunState run, string offerKey)
    {
        ShopPurchaseResult result = run.TryPurchaseRelic(offer.Id, offer.Price);
        PublishResult(ShopOfferType.Relic, offer.Id, null, result);
        if (!result.Succeeded)
            return;

        _soldOfferKeys.Add(offerKey);
        slot.SetUnavailable();
    }

    private void PurchaseUpgrade(ShopSlotView slot, CardUpgradeOffer offer, RunState run, BattleController battleController, string offerKey)
    {
        ShopPurchaseResult result = run.TryPurchaseRankUpgrade(offer.Rank, offer.Definition.Id, offer.Definition.Price);
        PublishResult(ShopOfferType.CardUpgrade, offer.Definition.Id, offer.Rank, result);
        if (!result.Succeeded)
            return;

        _soldOfferKeys.Add(offerKey);
        slot.SetUnavailable();
        BattleUiPresenter presenter = battleController != null
            ? battleController.GetComponentInChildren<BattleUiPresenter>(true)
            : null;
        presenter ??= UnityEngine.Object.FindFirstObjectByType<BattleUiPresenter>(FindObjectsInactive.Include);
        presenter?.Refresh();
    }

    private static string CreateOfferKey(ShopOfferType type, string id, Rank? rank)
    {
        return rank.HasValue ? $"{type}:{id}:{rank.Value}" : $"{type}:{id}";
    }

    private void Continue() => runManager?.ContinueFromShop();

    private void BindSceneSlots()
    {
        _itemSlots.Clear(); _relicSlots.Clear(); _upgradeSlots.Clear();
        AddSlots(_itemSlots, new[] { "item1Slot", "item2Slot ", "item3Slot" }, new[] { "Item1Sprite", "Item2Sprite", "Item3Sprite" }, new[] { "item1PriceText", "item2PriceText", "item3PriceText" });
        AddSlots(_relicSlots, new[] { "Relic1Slot", "Relic2Slot", "Relic3Slot" }, new[] { "Relic1Sprite", "Relic2Sprite", "Relic3Sprite" }, new[] { "Relic1PriceText", "Relic2PriceText", "Relic3PriceText" });
        AddUpgradeSlots(new[] { "Upgrade1Slot", "Upgrade2Slot", "Upgrade3Slot", "Upgrade4Slot", "Upgrade5Slot" }, new[] { "Upgrade1Sprite", "Upgrade2Sprite", "Upgrade3Sprite", "Upgrade4Sprite", "Upgrade5Sprite" }, new[] { "Upgrade1PriceText", "Upgrade2PriceText", "Upgrade3PriceText", "Upgrade4PriceText", "Upgrade5PriceText" });
    }

    private void AddSlots(List<ShopSlotView> target, string[] roots, string[] sprites, string[] prices)
    {
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = FindDescendant(roots[i]);
            if (root == null) continue;
            ShopSlotView view = root.GetComponent<ShopSlotView>() ?? root.AddComponent<ShopSlotView>();
            view.Initialize(FindDescendant(sprites[i])?.GetComponent<Button>(), FindDescendant(prices[i])?.GetComponent<TMP_Text>());
            target.Add(view);
        }
    }

    private void AddUpgradeSlots(string[] roots, string[] legacySprites, string[] prices)
    {
        BattleUiCardView prefab = ResolveCardPrefab();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = FindDescendant(roots[i]);
            if (root == null)
                continue;

            ShopSlotView slot = root.GetComponent<ShopSlotView>() ?? root.AddComponent<ShopSlotView>();
            BattleUiCardView cardView = null;
            if (prefab != null)
            {
                cardView = Instantiate(prefab, root.transform, false);
                cardView.name = "UpgradeCard";
                cardView.transform.SetSiblingIndex(0);
                RectTransform rect = cardView.RectTransform;
                if (rect != null)
                {
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(90f, 126f);
                }

                EnlargeOnHover cardHover = cardView.GetComponent<EnlargeOnHover>();
                if (cardHover != null)
                    cardHover.enabled = false;

                GameObject legacySprite = FindDescendant(legacySprites[i]);
                if (legacySprite != null)
                    legacySprite.SetActive(false);
            }

            slot.Initialize(cardView, FindDescendant(prices[i])?.GetComponent<TMP_Text>());
            _upgradeSlots.Add(slot);
        }
    }

    private BattleUiCardView ResolveCardPrefab()
    {
        if (cardPrefab != null)
            return cardPrefab;

        BattleUiCardView[] candidates = Resources.FindObjectsOfTypeAll<BattleUiCardView>();
        for (int i = 0; i < candidates.Length; i++)
        {
            BattleUiCardView candidate = candidates[i];
            if (candidate != null && candidate.name == "CardPrefab" && !candidate.gameObject.scene.IsValid())
            {
                cardPrefab = candidate;
                return cardPrefab;
            }
        }

        Debug.LogWarning($"{nameof(ShopUi)} is missing a card prefab reference.", this);
        return null;
    }

    private GameObject FindDescendant(string objectName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
            if (children[i].name == objectName) return children[i].gameObject;
        return null;
    }

    private static GameplayAssetRegistry FindLoadedRegistry()
    {
        GameplayAssetRegistry[] registries = Resources.FindObjectsOfTypeAll<GameplayAssetRegistry>();
        return registries.Length > 0 ? registries[0] : null;
    }
}
