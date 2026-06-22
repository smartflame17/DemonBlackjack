#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;

public sealed class ShopSystemTests
{
    [SetUp]
    public void SetUp() => EventBus.Clear();

    [TearDown]
    public void TearDown() => EventBus.Clear();

    [Test]
    public void OfferSelection_IsDeterministicAndWithoutReplacement()
    {
        var source = new List<int> { 1, 2, 3, 4, 5 };
        List<int> first = ShopOfferGenerator.TakeRandom(source, 5, new System.Random(123));
        List<int> second = ShopOfferGenerator.TakeRandom(source, 5, new System.Random(123));

        CollectionAssert.AreEqual(first, second);
        Assert.That(new HashSet<int>(first).Count, Is.EqualTo(first.Count));
    }

    [Test]
    public void Catalog_DefinitionLookupSupportsAllContentTypesAndCaseInsensitiveIds()
    {
        ShopCatalog catalog = ShopCatalog.CreateRuntimeDefault();

        Assert.That(catalog.TryGetDefinition(ActiveItemResolver.ClearPlayerField.ToUpperInvariant(), out ShopContentDefinition item), Is.True);
        Assert.That(item, Is.TypeOf<ActiveItemDefinition>());
        Assert.That(catalog.TryGetDefinition(RelicRuleResolver.BurstTwentyTwo.ToUpperInvariant(), out ShopContentDefinition relic), Is.True);
        Assert.That(relic, Is.TypeOf<RelicDefinition>());
        Assert.That(catalog.TryGetDefinition(CardModifierResolver.RankToHearts.ToUpperInvariant(), out ShopContentDefinition upgrade), Is.True);
        Assert.That(upgrade, Is.TypeOf<CardUpgradeDefinition>());
        Assert.That(catalog.TryGetDefinition(string.Empty, out _), Is.False);
        Assert.That(catalog.TryGetDefinition("unknown_content", out _), Is.False);

        UnityEngine.Object.DestroyImmediate(catalog);
    }

    [Test]
    public void ActiveItems_AreCountedConsumables()
    {
        var run = new RunState(1, startingGold: 100);

        Assert.That(run.TryPurchaseActiveItem("item", 10).Succeeded, Is.True);
        Assert.That(run.TryPurchaseActiveItem("item", 10).Succeeded, Is.True);
        Assert.That(run.GetActiveItemCount("item"), Is.EqualTo(2));
        Assert.That(run.RemoveActiveItem("item"), Is.True);
        Assert.That(run.GetActiveItemCount("item"), Is.EqualTo(1));
    }

    [Test]
    public void RemovingActiveItem_PreservesTheOtherItemsSlot()
    {
        var run = new RunState(1);
        run.AddActiveItem("first");
        run.AddActiveItem("second");

        Assert.That(run.RemoveActiveItem("first"), Is.True);

        Assert.That(run.ActiveItemIds[0], Is.Null);
        Assert.That(run.ActiveItemIds[1], Is.EqualTo("second"));
    }

    [Test]
    public void AddingActiveItem_FillsTheLeftmostEmptySlot()
    {
        var run = new RunState(1);
        run.AddActiveItem("first");
        run.AddActiveItem("second");
        run.RemoveActiveItem("first");

        Assert.That(run.AddActiveItem("replacement"), Is.True);

        Assert.That(run.ActiveItemIds[0], Is.EqualTo("replacement"));
        Assert.That(run.ActiveItemIds[1], Is.EqualTo("second"));
    }

    [Test]
    public void EmptyActiveItemSlot_PersistsThroughSaveAndLoad()
    {
        var run = new RunState(1);
        run.AddActiveItem("first");
        run.AddActiveItem("second");
        run.RemoveActiveItem("first");

        RunState loaded = RunState.FromData(run.ToData());

        Assert.That(loaded.ActiveItemIds[0], Is.Null);
        Assert.That(loaded.ActiveItemIds[1], Is.EqualTo("second"));
    }

    [Test]
    public void ActiveItems_DefaultCapacityRejectsThirdPurchaseWithoutCharging()
    {
        var run = new RunState(1, startingGold: 100);

        Assert.That(run.MaxActiveItemSlots, Is.EqualTo(2));
        Assert.That(run.TryPurchaseActiveItem("first", 10).Succeeded, Is.True);
        Assert.That(run.TryPurchaseActiveItem("second", 10).Succeeded, Is.True);

        ShopPurchaseResult result = run.TryPurchaseActiveItem("third", 10);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Failure, Is.EqualTo(ShopPurchaseFailure.ActiveItemSlotsFull));
        Assert.That(run.Money, Is.EqualTo(80));
        Assert.That(run.ActiveItemIds.Count, Is.EqualTo(2));
    }

    [Test]
    public void ActiveItemCapacity_PersistsThroughSaveAndLoad()
    {
        var run = new RunState(1, startingGold: 100, maxActiveItemSlots: 4);
        run.AddActiveItem("item");

        RunState loaded = RunState.FromData(run.ToData());

        Assert.That(loaded.MaxActiveItemSlots, Is.EqualTo(4));
        CollectionAssert.AreEqual(run.ActiveItemIds, loaded.ActiveItemIds);
    }

    [Test]
    public void ActiveItemCapacity_CanChangeWithoutDroppingItems()
    {
        var run = new RunState(1);
        run.AddActiveItem("item");

        Assert.That(run.TrySetMaxActiveItemSlots(4), Is.True);
        Assert.That(run.MaxActiveItemSlots, Is.EqualTo(4));
        Assert.That(run.TrySetMaxActiveItemSlots(0), Is.False);
        run.AddActiveItem("second");
        Assert.That(run.TrySetMaxActiveItemSlots(1), Is.False);
        Assert.That(run.MaxActiveItemSlots, Is.EqualTo(4));
    }

    [Test]
    public void LegacySave_ExpandsCapacityToPreserveOwnedItems()
    {
        var data = new RunStateData
        {
            seed = 1,
            gold = 100,
            maxActiveItemSlots = 0,
            activeItemIds = new List<string> { "one", "two", "three" }
        };

        RunState loaded = RunState.FromData(data);

        Assert.That(loaded.MaxActiveItemSlots, Is.EqualTo(3));
        CollectionAssert.AreEqual(data.activeItemIds, loaded.ActiveItemIds);
    }

    [Test]
    public void RemovingActiveItem_PublishesRemainingCount()
    {
        var run = new RunState(1);
        run.AddActiveItem("item");
        run.AddActiveItem("item");
        ActiveItemRemovedEvent received = default;
        EventBus.Subscribe<ActiveItemRemovedEvent>(eventData => received = eventData);

        Assert.That(run.RemoveActiveItem("item"), Is.True);

        Assert.That(received.ItemId, Is.EqualTo("item"));
        Assert.That(received.RemainingCount, Is.EqualTo(1));
    }

    [Test]
    public void Relics_AreUnique()
    {
        var run = new RunState(1, startingGold: 100);

        Assert.That(run.TryPurchaseRelic("relic", 10).Succeeded, Is.True);
        ShopPurchaseResult duplicate = run.TryPurchaseRelic("relic", 10);
        Assert.That(duplicate.Succeeded, Is.False);
        Assert.That(duplicate.Failure, Is.EqualTo(ShopPurchaseFailure.AlreadyOwned));
    }

    [Test]
    public void RankReplacement_UsesFlooredRefundTowardNetCost()
    {
        var run = new RunState(1, startingGold: 30);
        Assert.That(run.TryPurchaseRankUpgrade(Rank.Ace, "old", 21).Succeeded, Is.True);

        ShopPurchaseResult replacement = run.TryPurchaseRankUpgrade(Rank.Ace, "new", 19);

        Assert.That(replacement.Succeeded, Is.True);
        Assert.That(replacement.Refund, Is.EqualTo(10));
        Assert.That(replacement.NetCost, Is.EqualTo(9));
        Assert.That(run.Money, Is.EqualTo(0));
        Assert.That(run.TryGetRankUpgrade(Rank.Ace, out OwnedRankUpgrade owned), Is.True);
        Assert.That(owned.UpgradeId, Is.EqualTo("new"));
        Assert.That(owned.PaidPrice, Is.EqualTo(19));
    }

    [Test]
    public void OwnedUpgrade_PersistsPaidPrice()
    {
        var run = new RunState(1, startingGold: 100);
        run.TryPurchaseRankUpgrade(Rank.King, CardModifierResolver.RankToHearts, 25);

        RunState loaded = RunState.FromData(run.ToData());

        Assert.That(loaded.TryGetRankUpgrade(Rank.King, out OwnedRankUpgrade owned), Is.True);
        Assert.That(owned.UpgradeId, Is.EqualTo(CardModifierResolver.RankToHearts));
        Assert.That(owned.PaidPrice, Is.EqualTo(25));
    }

    [Test]
    public void CardUpgrade_TransformsOnlyWhenResolvedForPlay()
    {
        Card original = new(Suit.Clubs, Rank.Five);
        Card played = CardModifierResolver.Apply(original, CardModifierResolver.RankToSpades);

        Assert.That(original.Suit, Is.EqualTo(Suit.Clubs));
        Assert.That(original.HasModifier, Is.False);
        Assert.That(played.Suit, Is.EqualTo(Suit.Spades));
        Assert.That(played.ModifierId, Is.EqualTo(CardModifierResolver.RankToSpades));
    }
}
#endif
