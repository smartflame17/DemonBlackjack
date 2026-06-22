using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ShopUi : MonoBehaviour
{
    [SerializeField] private RunManager runManager;
    [SerializeField] private BattleController battleController;
    [SerializeField] private GameplayAssetRegistry assetRegistry;
    [SerializeField] private ShopCatalog catalog;
    [SerializeField] private Button continueButton;
    [SerializeField] private int activeItemOfferCount = 3;
    [SerializeField] private int relicOfferCount = 3;
    [SerializeField] private int upgradeOfferCount = 5;

    private readonly List<ShopSlotView> _itemSlots = new();
    private readonly List<ShopSlotView> _relicSlots = new();
    private readonly List<ShopSlotView> _upgradeSlots = new();
    private bool _generated;

    private void Awake()
    {
        runManager ??= FindFirstObjectByType<RunManager>();
        battleController ??= FindFirstObjectByType<BattleController>();
        assetRegistry ??= FindLoadedRegistry();
        catalog ??= Resources.Load<ShopCatalog>("Shop/ShopCatalog");
        catalog ??= ShopCatalog.CreateRuntimeDefault();
        continueButton ??= FindDescendant("ContinueButton")?.GetComponent<Button>();
        BindSceneSlots();
    }

    private void OnEnable()
    {
        if (continueButton != null)
            continueButton.onClick.AddListener(Continue);
        GenerateOffers();
    }

    private void OnDisable()
    {
        if (continueButton != null)
            continueButton.onClick.RemoveListener(Continue);
        _generated = false;
    }

    private void GenerateOffers()
    {
        if (_generated || runManager == null || runManager.RunState == null || battleController == null || battleController.BattleState == null)
            return;

        _generated = true;
        RunState run = runManager.RunState;
        var random = new System.Random(ShopOfferGenerator.CreateSeed(run, battleController.BattleState.RoundNumber));

        List<ActiveItemDefinition> items = ShopOfferGenerator.TakeRandom(catalog.ActiveItems, activeItemOfferCount, random);
        var eligibleRelics = new List<RelicDefinition>();
        for (int i = 0; i < catalog.Relics.Count; i++)
        {
            RelicDefinition relic = catalog.Relics[i];
            if (relic != null && !run.HasRelic(relic.Id))
                eligibleRelics.Add(relic);
        }
        List<RelicDefinition> relics = ShopOfferGenerator.TakeRandom(eligibleRelics, relicOfferCount, random);

        var upgradePool = new List<CardUpgradeOffer>();
        for (int i = 0; i < catalog.CardUpgrades.Count; i++)
        {
            CardUpgradeDefinition definition = catalog.CardUpgrades[i];
            if (definition == null)
                continue;
            foreach (Rank rank in Enum.GetValues(typeof(Rank)))
                upgradePool.Add(new CardUpgradeOffer(definition, rank));
        }
        List<CardUpgradeOffer> upgrades = ShopOfferGenerator.TakeRandom(upgradePool, upgradeOfferCount, random);

        BindItems(items, run);
        BindRelics(relics, run);
        BindUpgrades(upgrades, run);
    }

    private void BindItems(IReadOnlyList<ActiveItemDefinition> offers, RunState run)
    {
        for (int i = 0; i < _itemSlots.Count; i++)
        {
            if (i >= offers.Count || offers[i] == null) { _itemSlots[i].SetUnavailable(); continue; }
            ActiveItemDefinition offer = offers[i];
            ShopSlotView slot = _itemSlots[i];
            slot.Bind(assetRegistry != null ? assetRegistry.GetActiveItemSprite(offer.Id) : null, offer.Price, offer.DisplayName, offer.Id, () => PurchaseItem(slot, offer, run));
        }
    }

    private void BindRelics(IReadOnlyList<RelicDefinition> offers, RunState run)
    {
        for (int i = 0; i < _relicSlots.Count; i++)
        {
            if (i >= offers.Count || offers[i] == null) { _relicSlots[i].SetUnavailable(); continue; }
            RelicDefinition offer = offers[i];
            ShopSlotView slot = _relicSlots[i];
            slot.Bind(assetRegistry != null ? assetRegistry.GetRelicSprite(offer.Id) : null, offer.Price, offer.DisplayName, offer.Id, () => PurchaseRelic(slot, offer, run));
        }
    }

    private void BindUpgrades(IReadOnlyList<CardUpgradeOffer> offers, RunState run)
    {
        for (int i = 0; i < _upgradeSlots.Count; i++)
        {
            if (i >= offers.Count || offers[i].Definition == null) { _upgradeSlots[i].SetUnavailable(); continue; }
            CardUpgradeOffer offer = offers[i];
            ShopSlotView slot = _upgradeSlots[i];
            string label = $"{offer.Rank}\n{offer.Definition.DisplayName}";
            slot.Bind(assetRegistry != null ? assetRegistry.GetCardUpgradeSprite(offer.Definition.Id) : null, offer.Definition.Price, label, offer.Definition.Id, () => PurchaseUpgrade(slot, offer, run));
        }
    }

    private static void PublishResult(ShopOfferType type, string id, Rank? rank, ShopPurchaseResult result)
    {
        if (result.Succeeded)
            EventBus.Publish(new ShopPurchaseSucceededEvent(type, id, rank, result.Price, result.Refund));
        else
            EventBus.Publish(new ShopPurchaseFailedEvent(type, id, rank, result.Failure));
    }

    private static void PurchaseItem(ShopSlotView slot, ActiveItemDefinition offer, RunState run)
    {
        ShopPurchaseResult result = run.TryPurchaseActiveItem(offer.Id, offer.Price);
        PublishResult(ShopOfferType.ActiveItem, offer.Id, null, result);
        if (result.Succeeded) slot.SetUnavailable();
    }

    private static void PurchaseRelic(ShopSlotView slot, RelicDefinition offer, RunState run)
    {
        ShopPurchaseResult result = run.TryPurchaseRelic(offer.Id, offer.Price);
        PublishResult(ShopOfferType.Relic, offer.Id, null, result);
        if (result.Succeeded) slot.SetUnavailable();
    }

    private static void PurchaseUpgrade(ShopSlotView slot, CardUpgradeOffer offer, RunState run)
    {
        ShopPurchaseResult result = run.TryPurchaseRankUpgrade(offer.Rank, offer.Definition.Id, offer.Definition.Price);
        PublishResult(ShopOfferType.CardUpgrade, offer.Definition.Id, offer.Rank, result);
        if (result.Succeeded) slot.SetUnavailable();
    }

    private void Continue() => runManager?.ContinueFromShop();

    private void BindSceneSlots()
    {
        _itemSlots.Clear(); _relicSlots.Clear(); _upgradeSlots.Clear();
        AddSlots(_itemSlots, new[] { "item1Slot", "item2Slot ", "item3Slot" }, new[] { "Item1Sprite", "Item2Sprite", "Item3Sprite" }, new[] { "item1PriceText", "item2PriceText", "item3PriceText" });
        AddSlots(_relicSlots, new[] { "Relic1Slot", "Relic2Slot", "Relic3Slot" }, new[] { "Relic1Sprite", "Relic2Sprite", "Relic3Sprite" }, new[] { "Relic1PriceText", "Relic2PriceText", "Relic3PriceText" });
        AddSlots(_upgradeSlots, new[] { "Upgrade1Slot", "Upgrade2Slot", "Upgrade3Slot", "Upgrade4Slot", "Upgrade5Slot" }, new[] { "Upgrade1Sprite", "Upgrade2Sprite", "Upgrade3Sprite", "Upgrade4Sprite", "Upgrade5Sprite" }, new[] { "Upgrade1PriceText", "Upgrade2PriceText", "Upgrade3PriceText", "Upgrade4PriceText", "Upgrade5PriceText" });
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
