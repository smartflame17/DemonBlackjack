#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;

public sealed class CheatMenuControllerTests
{
    private readonly List<UnityEngine.Object> _objects = new();

    [SetUp]
    public void SetUp() => EventBus.Clear();

    [TearDown]
    public void TearDown()
    {
        EventBus.Clear();
        for (int i = _objects.Count - 1; i >= 0; i--)
        {
            if (_objects[i] != null)
                UnityEngine.Object.DestroyImmediate(_objects[i]);
        }
        _objects.Clear();
    }

    [Test]
    public void BuildGate_AllowsEditorAndDevelopmentButRejectsRelease()
    {
        Assert.That(CheatMenuController.IsAvailableForBuild(true, false), Is.True);
        Assert.That(CheatMenuController.IsAvailableForBuild(false, true), Is.True);
        Assert.That(CheatMenuController.IsAvailableForBuild(false, false), Is.False);
    }

    [Test]
    public void MoneyMutation_UsesRunHookOutsideBattleAndScopedHookDuringBattle()
    {
        RunManager runManager = CreateRunManager();
        CheatMenuController controller = CreateController(
            runManager,
            null,
            Track(ShopCatalog.CreateRuntimeDefault()));

        Invoke(controller, "SetPlayerMoney", 250);
        Assert.That(runManager.RunState.Money, Is.EqualTo(250));

        BattleController battleController = Track(new GameObject("BattleController")).AddComponent<BattleController>();
        battleController.InitializeBattle(runManager.RunState, new BattleConfig("cheat_test", 100));
        SetField(controller, "battleController", battleController);

        MoneyChangedEvent scopedEvent = default;
        bool receivedScopedEvent = false;
        battleController.BattleState.EventBus.Subscribe<MoneyChangedEvent>(eventData =>
        {
            if (eventData.Owner != Combatant.Player)
                return;

            scopedEvent = eventData;
            receivedScopedEvent = true;
        });

        Invoke(controller, "SetPlayerMoney", 400);

        Assert.That(runManager.RunState.Money, Is.EqualTo(400));
        Assert.That(receivedScopedEvent, Is.True);
        Assert.That(scopedEvent.CurrentMoney, Is.EqualTo(400));
        Assert.That(scopedEvent.Delta, Is.EqualTo(150));
        battleController.CleanupBattle();
    }

    [Test]
    public void FreeActiveItemGrant_PreservesCapacityAndPurchaseEvents()
    {
        RunManager runManager = CreateRunManager();
        ShopCatalog catalog = Track(ShopCatalog.CreateRuntimeDefault());
        CheatMenuController controller = CreateController(runManager, null, catalog);
        ActiveItemDefinition selected = catalog.ActiveItems[0];

        ShopPurchaseSucceededEvent success = default;
        EventBus.Subscribe<ShopPurchaseSucceededEvent>(eventData => success = eventData);

        Invoke(controller, "GrantActiveItem");

        Assert.That(runManager.RunState.GetActiveItemCount(selected.Id), Is.EqualTo(1));
        Assert.That(runManager.RunState.Money, Is.EqualTo(100));
        Assert.That(success.Type, Is.EqualTo(ShopOfferType.ActiveItem));
        Assert.That(success.ContentId, Is.EqualTo(selected.Id));
        Assert.That(success.Price, Is.Zero);

        Invoke(controller, "GrantActiveItem");
        Invoke(controller, "GrantActiveItem");

        ShopPurchaseFailedEvent failure = default;
        EventBus.Subscribe<ShopPurchaseFailedEvent>(eventData => failure = eventData);
        Invoke(controller, "GrantActiveItem");

        Assert.That(runManager.RunState.GetActiveItemCount(selected.Id), Is.EqualTo(RunState.DefaultMaxActiveItemSlots));
        Assert.That(failure.Failure, Is.EqualTo(ShopPurchaseFailure.ActiveItemSlotsFull));
    }

    [Test]
    public void FreeRelicGrant_PreservesUniqueOwnershipRule()
    {
        RunManager runManager = CreateRunManager();
        ShopCatalog catalog = Track(ShopCatalog.CreateRuntimeDefault());
        CheatMenuController controller = CreateController(runManager, null, catalog);
        RelicDefinition selected = catalog.Relics[0];

        Invoke(controller, "GrantRelic");
        Assert.That(runManager.RunState.HasRelic(selected.Id), Is.True);
        Assert.That(runManager.RunState.Money, Is.EqualTo(100));

        ShopPurchaseFailedEvent failure = default;
        EventBus.Subscribe<ShopPurchaseFailedEvent>(eventData => failure = eventData);
        Invoke(controller, "GrantRelic");

        Assert.That(failure.Type, Is.EqualTo(ShopOfferType.Relic));
        Assert.That(failure.Failure, Is.EqualTo(ShopPurchaseFailure.AlreadyOwned));
    }

    [Test]
    public void FreeUpgradeGrant_FiltersCompatibleRankAndUpdatesActiveBattleDeck()
    {
        RunManager runManager = CreateRunManager();
        BattleController battleController = Track(new GameObject("BattleController")).AddComponent<BattleController>();
        battleController.InitializeBattle(runManager.RunState, new BattleConfig("cheat_test", 100));

        ShopCatalog catalog = Track(ShopCatalog.CreateRuntimeDefault());
        CheatMenuController controller = CreateController(runManager, battleController, catalog);
        CardUpgradeDefinition selected = catalog.CardUpgrades[0];
        Rank selectedRank = FirstCompatibleRank(selected);

        Invoke(controller, "GrantUpgrade");

        Assert.That(runManager.RunState.TryGetRankUpgrade(selectedRank, out OwnedRankUpgrade owned), Is.True);
        Assert.That(owned.UpgradeId, Is.EqualTo(selected.Id));
        Assert.That(owned.PaidPrice, Is.Zero);
        Assert.That(ContainsModifier(battleController.BattleState.PlayerDrawPile, selectedRank, selected.Id), Is.True);
        battleController.CleanupBattle();
    }

    private RunManager CreateRunManager()
    {
        RunManager runManager = Track(new GameObject("RunManager")).AddComponent<RunManager>();
        runManager.StartRun(123);
        return runManager;
    }

    private CheatMenuController CreateController(
        RunManager runManager,
        BattleController battleController,
        ShopCatalog catalog)
    {
        GameObject root = Track(new GameObject("CheatMenuController"));
        root.SetActive(false);
        CheatMenuController controller = root.AddComponent<CheatMenuController>();

        SetField(controller, "runManager", runManager);
        SetField(controller, "battleController", battleController);
        SetField(controller, "catalog", catalog);
        SetField(controller, "activeItemDropdown", CreateDropdown(root.transform, "ActiveItemDropdown"));
        SetField(controller, "relicDropdown", CreateDropdown(root.transform, "RelicDropdown"));
        SetField(controller, "upgradeDropdown", CreateDropdown(root.transform, "UpgradeDropdown"));
        SetField(controller, "rankDropdown", CreateDropdown(root.transform, "RankDropdown"));
        SetField(controller, "statusText", CreateText(root.transform, "StatusText"));

        Invoke(controller, "PopulateCatalogOptions");
        return controller;
    }

    private static TMP_Dropdown CreateDropdown(Transform parent, string name)
    {
        var child = new GameObject(name, typeof(RectTransform), typeof(TMP_Dropdown));
        child.transform.SetParent(parent, false);
        return child.GetComponent<TMP_Dropdown>();
    }

    private static TMP_Text CreateText(Transform parent, string name)
    {
        var child = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        child.transform.SetParent(parent, false);
        return child.GetComponent<TMP_Text>();
    }

    private T Track<T>(T value) where T : UnityEngine.Object
    {
        _objects.Add(value);
        return value;
    }

    private static Rank FirstCompatibleRank(CardUpgradeDefinition definition)
    {
        foreach (Rank rank in Enum.GetValues(typeof(Rank)))
        {
            if (definition.CanApplyToRank(rank))
                return rank;
        }

        Assert.Fail($"Upgrade {definition.Id} has no compatible rank.");
        return default;
    }

    private static bool ContainsModifier(
        IReadOnlyList<Card> cards,
        Rank rank,
        string modifierId)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            Card card = cards[i];
            if (card.Rank == rank && card.ModifierId == modifierId)
                return true;
        }

        return false;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }

    private static void Invoke(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, methodName);
        method.Invoke(target, arguments);
    }
}
#endif
