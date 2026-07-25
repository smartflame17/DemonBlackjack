#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

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
    public void ShopSeed_ChangesOnceEveryThreeRounds()
    {
        var run = new RunState(7);

        Assert.That(ShopOfferGenerator.CreateSeed(run, 1), Is.EqualTo(ShopOfferGenerator.CreateSeed(run, 2)));
        Assert.That(ShopOfferGenerator.CreateSeed(run, 2), Is.EqualTo(ShopOfferGenerator.CreateSeed(run, 3)));
        Assert.That(ShopOfferGenerator.CreateSeed(run, 3), Is.Not.EqualTo(ShopOfferGenerator.CreateSeed(run, 4)));
    }

    [Test]
    public void ShopUi_ReusesOffersAndSoldSlotsUntilNextThreeRoundCycle()
    {
        GameObject shopObject = new("ShopUiTest");
        GameObject runObject = new("RunManagerTest");
        GameObject battleObject = new("BattleControllerTest");
        ShopCatalog catalog = ShopCatalog.CreateRuntimeDefault();
        shopObject.SetActive(false);

        try
        {
            CreateShopPanel(shopObject.transform);
            Button firstItemButton = CreateItemSlot(shopObject.transform, 1);
            CanvasGroup firstSlotGroup = firstItemButton.transform.parent.GetComponent<CanvasGroup>();

            RunManager runManager = runObject.AddComponent<RunManager>();
            runManager.StartRun(31);

            BattleController battleController = battleObject.AddComponent<BattleController>();
            battleController.InitializeBattle(runManager.RunState, new BattleConfig("test", 100, new StandingDevilStrategy()));
            Assert.That(battleController.BattleState.StartRound(10), Is.True);

            ShopUi shopUi = shopObject.AddComponent<ShopUi>();
            SetField(shopUi, "runManager", runManager);
            SetField(shopUi, "battleController", battleController);
            SetField(shopUi, "catalog", catalog);
            Invoke(shopUi, "BindSceneSlots");
            Invoke(shopUi, "GenerateOffers");

            Assert.That(firstSlotGroup.interactable, Is.True);
            firstItemButton.onClick.Invoke();
            Assert.That(firstSlotGroup.interactable, Is.False);

            Invoke(shopUi, "GenerateOffers");
            Assert.That(firstSlotGroup.interactable, Is.False);

            SetAutoProperty(battleController.BattleState, "RoundNumber", 4);
            Invoke(shopUi, "GenerateOffers");
            Assert.That(firstSlotGroup.interactable, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(battleObject);
            Object.DestroyImmediate(runObject);
            Object.DestroyImmediate(shopObject);
        }
    }

    [Test]
    public void Catalog_DefinitionLookupSupportsAllContentTypesAndCaseInsensitiveIds()
    {
        ShopCatalog catalog = ShopCatalog.CreateRuntimeDefault();

        Assert.That(catalog.TryGetDefinition(ActiveItemResolver.RejectLastHit.ToUpperInvariant(), out ShopContentDefinition item), Is.True);
        Assert.That(item, Is.TypeOf<ActiveItemDefinition>());
        Assert.That(catalog.TryGetDefinition(ActiveItemResolver.LeverageTriple.ToUpperInvariant(), out ShopContentDefinition leverage), Is.True);
        Assert.That(leverage, Is.TypeOf<ActiveItemDefinition>());
        Assert.That(catalog.TryGetDefinition(RelicRuleResolver.BurstTwentyTwo.ToUpperInvariant(), out ShopContentDefinition relic), Is.True);
        Assert.That(relic, Is.TypeOf<RelicDefinition>());
        Assert.That(catalog.TryGetDefinition(CardModifierResolver.RankToHearts.ToUpperInvariant(), out ShopContentDefinition upgrade), Is.True);
        Assert.That(upgrade, Is.TypeOf<CardUpgradeDefinition>());
        Assert.That(catalog.TryGetDefinition(ActiveItemResolver.DoubleWager, out _), Is.False);
        Assert.That(catalog.TryGetDefinition(string.Empty, out _), Is.False);
        Assert.That(catalog.TryGetDefinition("unknown_content", out _), Is.False);

        UnityEngine.Object.DestroyImmediate(catalog);
    }

    [Test]
    public void CardUpgradeAssignment_NumberedCardsAcceptOnlyAceThroughTen()
    {
        CardUpgradeDefinition definition = CreateUpgrade(CardUpgradeAssignmentType.NumberedCards);

        foreach (Rank rank in AllRanks())
            Assert.That(definition.CanApplyToRank(rank), Is.EqualTo((int)rank >= (int)Rank.Ace && (int)rank <= (int)Rank.Ten), rank.ToString());

        UnityEngine.Object.DestroyImmediate(definition);
    }

    [Test]
    public void CardUpgradeAssignment_FaceCardsAcceptOnlyJackQueenKing()
    {
        CardUpgradeDefinition definition = CreateUpgrade(CardUpgradeAssignmentType.FaceCards);

        foreach (Rank rank in AllRanks())
            Assert.That(definition.CanApplyToRank(rank), Is.EqualTo((int)rank >= (int)Rank.Jack && (int)rank <= (int)Rank.King), rank.ToString());

        UnityEngine.Object.DestroyImmediate(definition);
    }

    [Test]
    public void CardUpgradeAssignment_SpecificFaceCardsAcceptOnlyMatchingRank()
    {
        CardUpgradeDefinition jack = CreateUpgrade(CardUpgradeAssignmentType.JackCards);
        CardUpgradeDefinition queen = CreateUpgrade(CardUpgradeAssignmentType.QueenCards);
        CardUpgradeDefinition king = CreateUpgrade(CardUpgradeAssignmentType.KingCards);

        foreach (Rank rank in AllRanks())
        {
            Assert.That(jack.CanApplyToRank(rank), Is.EqualTo(rank == Rank.Jack), $"Jack {rank}");
            Assert.That(queen.CanApplyToRank(rank), Is.EqualTo(rank == Rank.Queen), $"Queen {rank}");
            Assert.That(king.CanApplyToRank(rank), Is.EqualTo(rank == Rank.King), $"King {rank}");
        }

        UnityEngine.Object.DestroyImmediate(jack);
        UnityEngine.Object.DestroyImmediate(queen);
        UnityEngine.Object.DestroyImmediate(king);
    }

    [Test]
    public void CardUpgradeOfferPool_UsesDefinitionAssignmentType()
    {
        CardUpgradeDefinition numbered = CreateUpgrade(CardUpgradeAssignmentType.NumberedCards, "numbered");
        CardUpgradeDefinition jack = CreateUpgrade(CardUpgradeAssignmentType.JackCards, "jack");
        CardUpgradeDefinition queen = CreateUpgrade(CardUpgradeAssignmentType.QueenCards, "queen");
        CardUpgradeDefinition king = CreateUpgrade(CardUpgradeAssignmentType.KingCards, "king");
        var definitions = new List<CardUpgradeDefinition> { numbered, jack, queen, king };

        List<CardUpgradeOffer> offers = ShopOfferGenerator.CreateCardUpgradeOfferPool(definitions);

        Assert.That(offers.Count, Is.EqualTo(13));
        Assert.That(ContainsOffer(offers, numbered, Rank.Ace), Is.True);
        Assert.That(ContainsOffer(offers, numbered, Rank.Ten), Is.True);
        Assert.That(ContainsOffer(offers, numbered, Rank.Jack), Is.False);
        Assert.That(ContainsOffer(offers, jack, Rank.Jack), Is.True);
        Assert.That(ContainsOffer(offers, jack, Rank.Queen), Is.False);
        Assert.That(ContainsOffer(offers, queen, Rank.Queen), Is.True);
        Assert.That(ContainsOffer(offers, queen, Rank.King), Is.False);
        Assert.That(ContainsOffer(offers, king, Rank.King), Is.True);
        Assert.That(ContainsOffer(offers, king, Rank.Jack), Is.False);

        UnityEngine.Object.DestroyImmediate(numbered);
        UnityEngine.Object.DestroyImmediate(jack);
        UnityEngine.Object.DestroyImmediate(queen);
        UnityEngine.Object.DestroyImmediate(king);
    }

    [Test]
    public void ActiveItems_AreCountedConsumables()
    {
        var run = new RunState(1, startingMoney: 100);

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
    public void ActiveItems_DefaultCapacityRejectsFourthPurchaseWithoutCharging()
    {
        var run = new RunState(1, startingMoney: 100);

        Assert.That(run.MaxActiveItemSlots, Is.EqualTo(3));
        Assert.That(run.TryPurchaseActiveItem("first", 10).Succeeded, Is.True);
        Assert.That(run.TryPurchaseActiveItem("second", 10).Succeeded, Is.True);
        Assert.That(run.TryPurchaseActiveItem("third", 10).Succeeded, Is.True);

        ShopPurchaseResult result = run.TryPurchaseActiveItem("fourth", 10);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Failure, Is.EqualTo(ShopPurchaseFailure.ActiveItemSlotsFull));
        Assert.That(run.Money, Is.EqualTo(70));
        Assert.That(run.ActiveItemIds.Count, Is.EqualTo(3));
    }

    [Test]
    public void ActiveItemCapacity_PersistsThroughSaveAndLoad()
    {
        var run = new RunState(1, startingMoney: 100, maxActiveItemSlots: 4);
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
            money = 100,
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
        var run = new RunState(1, startingMoney: 100);

        Assert.That(run.TryPurchaseRelic("relic", 10).Succeeded, Is.True);
        ShopPurchaseResult duplicate = run.TryPurchaseRelic("relic", 10);
        Assert.That(duplicate.Succeeded, Is.False);
        Assert.That(duplicate.Failure, Is.EqualTo(ShopPurchaseFailure.AlreadyOwned));
    }

    [Test]
    public void RelicPurchase_ReplacesExistingRelicWithoutRefund()
    {
        var run = new RunState(1, startingMoney: 100);
        RelicRemovedEvent removed = default;
        bool removedReceived = false;
        EventBus.Subscribe<RelicRemovedEvent>(eventData =>
        {
            removed = eventData;
            removedReceived = true;
        });

        Assert.That(run.TryPurchaseRelic("old", 10).Succeeded, Is.True);
        Assert.That(run.TryPurchaseRelic("new", 20).Succeeded, Is.True);

        Assert.That(run.RelicIds.Count, Is.EqualTo(1));
        Assert.That(run.HasRelic("old"), Is.False);
        Assert.That(run.HasRelic("new"), Is.True);
        Assert.That(run.Money, Is.EqualTo(70));
        Assert.That(removedReceived, Is.True);
        Assert.That(removed.RelicId, Is.EqualTo("old"));
    }

    [Test]
    public void BattleState_CreatesKnownRelicRuntimeWithinRelicLimit()
    {
        var data = new RunStateData
        {
            seed = 1,
            money = 100,
            maxActiveItemSlots = RunState.DefaultMaxActiveItemSlots
        };
        data.deck.Add(CardData(Suit.Clubs, Rank.Two));
        data.relicIds.Add(RelicRuleResolver.SuitOverride);
        data.relicIds.Add("retired_relic");
        data.relicIds.Add(RelicRuleResolver.AddJqk);
        data.relicIds.Add(RelicRuleResolver.AddJqk);
        data.relicIds.Add(RelicRuleResolver.BurstTwentyTwo);
        RunState run = RunState.FromData(data);

        var battle = new BattleState(run, TestBattleConfig());

        Assert.That(run.RelicIds.Count, Is.EqualTo(1));
        Assert.That(battle.RelicRuntimes.Count, Is.EqualTo(1));
        Assert.That(battle.RelicRuntimes[0], Is.TypeOf<SuitOverrideRelicRuntime>());
        Assert.That(battle.RelicRuntimes[0].RelicId, Is.EqualTo(RelicRuleResolver.SuitOverride));

        Assert.That(battle.StartRound(battle.GetDefaultWager()), Is.True);
        Assert.That(battle.CurrentRound.PlayerBurstThreshold, Is.EqualTo(battle.Config.BurstThreshold));
        battle.Dispose();
    }

    [Test]
    public void BattleState_AddsRuntimeForRelicPurchasedAfterConstruction()
    {
        RunState run = CreateRunWithDeck(
            CardData(Suit.Hearts, Rank.Seven),
            CardData(Suit.Spades, Rank.Seven),
            CardData(Suit.Clubs, Rank.Two));
        var battle = new BattleState(run, TestBattleConfig(startingHandSize: 3));
        battle.StartRound(battle.GetDefaultWager());
        battle.StartRound(battle.GetDefaultWager());

        Assert.That(battle.RelicRuntimes.Count, Is.EqualTo(0));
        Assert.That(run.TryPurchaseRelic(RelicRuleResolver.SuitOverride, 0).Succeeded, Is.True);

        Assert.That(battle.RelicRuntimes.Count, Is.EqualTo(1));
        Assert.That(battle.RelicRuntimes[0], Is.TypeOf<SuitOverrideRelicRuntime>());
        Assert.That(battle.TryPlayPlayerHandCardForEffect(IndexOfRankAndSuit(battle.CurrentRound.PlayerHand, Rank.Seven, Suit.Hearts)), Is.True);
        Assert.That(battle.TryPlayPlayerHandCardForEffect(IndexOfRankAndSuit(battle.CurrentRound.PlayerHand, Rank.Seven, Suit.Spades)), Is.True);
        Assert.That(battle.CurrentRound.PlayerPlayedCards[1].Suit, Is.EqualTo(Suit.Hearts));
        battle.Dispose();
    }

    [Test]
    public void BattleState_RemovesExistingRelicRuntimeWhenBuyingNewRelic()
    {
        RunState run = CreateRunWithRelicDeck(RelicRuleResolver.BurstExtend, TenTwos());
        var battle = new BattleState(run, TestBattleConfig(startingHandSize: 1));
        battle.StartRound(battle.GetDefaultWager());
        battle.StartRound(battle.GetDefaultWager());

        Assert.That(battle.TryHit(), Is.True);
        Assert.That(battle.TryHit(), Is.True);
        Assert.That(run.TryPurchaseRelic(RelicRuleResolver.AddJqk, 0).Succeeded, Is.True);

        Assert.That(run.HasRelic(RelicRuleResolver.BurstExtend), Is.False);
        Assert.That(run.HasRelic(RelicRuleResolver.AddJqk), Is.True);
        Assert.That(battle.RelicRuntimes.Count, Is.EqualTo(1));
        Assert.That(battle.RelicRuntimes[0], Is.TypeOf<AddJqkRelicRuntime>());
        Assert.That(run.PlayerBurstThresholdBonus, Is.EqualTo(0));
        battle.Dispose();
    }

    [Test]
    public void RelicRuntimes_CanCombineTransformsAndScoreBonuses()
    {
        RunState run = CreateRunWithDeck(
            CardData(Suit.Hearts, Rank.Seven),
            CardData(Suit.Spades, Rank.Seven),
            CardData(Suit.Clubs, Rank.Two));
        run.AddRelic(RelicRuleResolver.SuitOverride);
        run.AddRelic(RelicRuleResolver.AddJqk);
        var battle = new BattleState(run, TestBattleConfig(startingHandSize: 3));
        battle.StartRound(battle.GetDefaultWager());
        battle.StartRound(battle.GetDefaultWager());

        Assert.That(battle.TryPlayPlayerHandCardForEffect(IndexOfRankAndSuit(battle.CurrentRound.PlayerHand, Rank.Seven, Suit.Hearts)), Is.True);
        Assert.That(battle.TryPlayPlayerHandCardForEffect(IndexOfRankAndSuit(battle.CurrentRound.PlayerHand, Rank.Seven, Suit.Spades)), Is.True);
        battle.CurrentRound.AddToOpponentHand(new Card(Suit.Spades, Rank.Ten));
        Assert.That(battle.CurrentRound.TryPlayOpponentCard(0, out Card ten), Is.True);
        battle.EventBus.Publish(new CardPlayedEvent(Combatant.Opponent, ten));
        battle.CurrentRound.AddToOpponentHand(new Card(Suit.Hearts, Rank.King));
        Assert.That(battle.CurrentRound.TryPlayOpponentCard(0, out Card king), Is.True);
        battle.EventBus.Publish(new CardPlayedEvent(Combatant.Opponent, king));
        battle.CurrentRound.MarkOpponentStood();

        Assert.That(battle.TryStand(), Is.True);

        Assert.That(battle.CurrentRound.PlayerPlayedCards[1].Suit, Is.EqualTo(Suit.Hearts));
        Assert.That(battle.CurrentRound.OpponentScore.BlackjackScore, Is.EqualTo(21));
        battle.Dispose();
    }

    [Test]
    public void AddJqk_AddsOpponentScoreBonusAndCounterForFaceCardsOnly()
    {
        RunState run = CreateRunWithRelicDeck(RelicRuleResolver.AddJqk, CardData(Suit.Clubs, Rank.Two));
        var battle = new BattleState(run, TestBattleConfig());
        battle.StartRound(battle.GetDefaultWager());
        battle.StartRound(battle.GetDefaultWager());
        var counterValues = new List<int>();
        EventBus.Subscribe<RelicCounterChangedEvent>(evt =>
        {
            if (evt.RelicId == RelicRuleResolver.AddJqk)
                counterValues.Add(evt.Value);
        });

        battle.CurrentRound.AddToOpponentHand(new Card(Suit.Spades, Rank.Ten));
        Assert.That(battle.CurrentRound.TryPlayOpponentCard(0, out Card ten), Is.True);
        battle.EventBus.Publish(new CardPlayedEvent(Combatant.Opponent, ten));
        battle.CurrentRound.AddToOpponentHand(new Card(Suit.Hearts, Rank.King));
        Assert.That(battle.CurrentRound.TryPlayOpponentCard(0, out Card king), Is.True);
        battle.EventBus.Publish(new CardPlayedEvent(Combatant.Opponent, king));
        battle.CurrentRound.MarkOpponentStood();

        Assert.That(battle.TryStand(), Is.True);

        Assert.That(battle.CurrentRound.OpponentScore.BlackjackScore, Is.EqualTo(21));
        CollectionAssert.Contains(counterValues, 1);
        battle.Dispose();
    }

    [Test]
    public void AddJqk_ResetsCounterAndBonusNextRound()
    {
        RunState run = CreateRunWithRelicDeck(RelicRuleResolver.AddJqk, CardData(Suit.Clubs, Rank.Two));
        var battle = new BattleState(run, TestBattleConfig());
        var counterValues = new List<int>();
        EventBus.Subscribe<RelicCounterChangedEvent>(evt =>
        {
            if (evt.RelicId == RelicRuleResolver.AddJqk)
                counterValues.Add(evt.Value);
        });
        battle.StartRound(battle.GetDefaultWager());
        battle.StartRound(battle.GetDefaultWager());
        battle.CurrentRound.AddToOpponentHand(new Card(Suit.Hearts, Rank.Queen));
        battle.CurrentRound.TryPlayOpponentCard(0, out Card queen);
        battle.EventBus.Publish(new CardPlayedEvent(Combatant.Opponent, queen));
        battle.CurrentRound.MarkOpponentStood();
        battle.TryStand();

        battle.CleanupRound();
        Assert.That(battle.StartRound(battle.GetDefaultWager()), Is.True);
        battle.CurrentRound.AddToOpponentHand(new Card(Suit.Spades, Rank.Ten));
        battle.CurrentRound.TryPlayOpponentCard(0, out Card ten);
        battle.EventBus.Publish(new CardPlayedEvent(Combatant.Opponent, ten));
        battle.CurrentRound.MarkOpponentStood();
        battle.TryStand();

        Assert.That(battle.CurrentRound.OpponentScore.BlackjackScore, Is.EqualTo(10));
        Assert.That(counterValues[counterValues.Count - 1], Is.EqualTo(0));
        battle.Dispose();
    }

    [Test]
    public void BurstExtend_IncreasesPersistedPlayerBurstThresholdEveryFiveHits()
    {
        RunState run = CreateRunWithRelicDeck(RelicRuleResolver.BurstExtend, TenTwos());
        var battle = new BattleState(run, TestBattleConfig(startingHandSize: 1));
        battle.StartRound(battle.GetDefaultWager());
        battle.StartRound(battle.GetDefaultWager());
        var counterValues = new List<int>();
        EventBus.Subscribe<RelicCounterChangedEvent>(evt =>
        {
            if (evt.RelicId == RelicRuleResolver.BurstExtend)
                counterValues.Add(evt.Value);
        });

        for (int i = 0; i < 10; i++)
            Assert.That(battle.TryHit(), Is.True, $"Hit {i + 1}");

        Assert.That(run.PlayerBurstThresholdBonus, Is.EqualTo(2));
        Assert.That(battle.CurrentRound.PlayerBurstThreshold, Is.EqualTo(23));
        Assert.That(counterValues.FindAll(value => value == 0).Count, Is.GreaterThanOrEqualTo(2));
        battle.Dispose();
    }

    [Test]
    public void BurstExtend_FifthHitAppliesThresholdBeforeBurstResolution()
    {
        RunState run = CreateRunWithRelicDeck(RelicRuleResolver.BurstExtend, RepeatCardData(Suit.Clubs, Rank.Two, 6));
        var battle = new BattleState(run, TestBattleConfig(startingHandSize: 1));
        battle.StartRound(battle.GetDefaultWager());
        battle.StartRound(battle.GetDefaultWager());

        for (int i = 0; i < 5; i++)
            Assert.That(battle.TryHit(), Is.True, $"Hit {i + 1}");

        Assert.That(battle.Phase, Is.Not.EqualTo(BattlePhase.Cleanup));
        Assert.That(battle.CurrentRound.PlayerScore.BlackjackScore, Is.EqualTo(10));
        Assert.That(battle.CurrentRound.PlayerBurstThreshold, Is.EqualTo(22));
        battle.Dispose();
    }

    [Test]
    public void BurstExtend_CounterResetsWhenBattleEnds()
    {
        RunState run = CreateRunWithRelicDeck(RelicRuleResolver.BurstExtend, TenTwos());
        var battle = new BattleState(run, TestBattleConfig(startingHandSize: 1));
        battle.StartRound(battle.GetDefaultWager());
        battle.StartRound(battle.GetDefaultWager());
        var counterValues = new List<int>();
        EventBus.Subscribe<RelicCounterChangedEvent>(evt =>
        {
            if (evt.RelicId == RelicRuleResolver.BurstExtend)
                counterValues.Add(evt.Value);
        });
        Assert.That(battle.TryHit(), Is.True);
        Assert.That(battle.TryHit(), Is.True);

        battle.EndBattle();

        Assert.That(counterValues[counterValues.Count - 1], Is.EqualTo(0));
    }

    [Test]
    public void SuitOverride_TransformsLaterMatchingRankToFirstPlayedSuit()
    {
        RunState run = CreateRunWithRelicDeck(
            RelicRuleResolver.SuitOverride,
            CardData(Suit.Hearts, Rank.Seven),
            CardData(Suit.Spades, Rank.Seven),
            CardData(Suit.Clubs, Rank.Eight),
            CardData(Suit.Diamonds, Rank.Seven));
        var battle = new BattleState(run, TestBattleConfig(startingHandSize: 4));
        battle.StartRound(battle.GetDefaultWager());
        battle.StartRound(battle.GetDefaultWager());

        Assert.That(battle.TryPlayPlayerHandCardForEffect(IndexOfRankAndSuit(battle.CurrentRound.PlayerHand, Rank.Seven, Suit.Hearts)), Is.True);
        Assert.That(battle.TryPlayPlayerHandCardForEffect(IndexOfRankAndSuit(battle.CurrentRound.PlayerHand, Rank.Seven, Suit.Spades)), Is.True);
        Assert.That(battle.TryPlayPlayerHandCardForEffect(IndexOfRankAndSuit(battle.CurrentRound.PlayerHand, Rank.Eight, Suit.Clubs)), Is.True);

        Assert.That(battle.CurrentRound.PlayerPlayedCards[1].Suit, Is.EqualTo(Suit.Hearts));
        Assert.That(battle.CurrentRound.PlayerPlayedCards[2].Suit, Is.EqualTo(Suit.Clubs));
        battle.Dispose();
    }

    [Test]
    public void SuitOverride_ResetsEachRound()
    {
        RunState run = CreateRunWithRelicDeck(
            RelicRuleResolver.SuitOverride,
            CardData(Suit.Hearts, Rank.Seven),
            CardData(Suit.Spades, Rank.Seven),
            CardData(Suit.Clubs, Rank.Seven));
        var battle = new BattleState(run, TestBattleConfig(startingHandSize: 3));
        battle.StartRound(battle.GetDefaultWager());
        battle.StartRound(battle.GetDefaultWager());
        battle.TryPlayPlayerHandCardForEffect(IndexOfRankAndSuit(battle.CurrentRound.PlayerHand, Rank.Seven, Suit.Hearts));
        battle.CleanupRound();

        Assert.That(battle.StartRound(battle.GetDefaultWager()), Is.True);
        battle.CurrentRound.AddToHand(new Card(Suit.Clubs, Rank.Seven));
        Assert.That(battle.TryPlayPlayerHandCardForEffect(IndexOfRankAndSuit(battle.CurrentRound.PlayerHand, Rank.Seven, Suit.Clubs)), Is.True);

        Assert.That(battle.CurrentRound.PlayerPlayedCards[0].Suit, Is.EqualTo(Suit.Clubs));
        battle.Dispose();
    }

    [Test]
    public void RunState_PreservesPlayerBurstThresholdBonus()
    {
        var run = new RunState(1);
        run.IncreasePlayerBurstThreshold(2);

        RunState loaded = RunState.FromData(run.ToData());

        Assert.That(loaded.PlayerBurstThresholdBonus, Is.EqualTo(2));
    }

    [Test]
    public void RankReplacement_UsesFlooredRefundTowardNetCost()
    {
        var run = new RunState(1, startingMoney: 30);
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
        var run = new RunState(1, startingMoney: 100);
        run.TryPurchaseRankUpgrade(Rank.King, CardModifierResolver.RankToHearts, 25);

        RunState loaded = RunState.FromData(run.ToData());

        Assert.That(loaded.TryGetRankUpgrade(Rank.King, out OwnedRankUpgrade owned), Is.True);
        Assert.That(owned.UpgradeId, Is.EqualTo(CardModifierResolver.RankToHearts));
        Assert.That(owned.PaidPrice, Is.EqualTo(25));
    }

    [Test]
    public void RankUpgradePurchase_ImmediatelyTransformsRunDeck()
    {
        var run = new RunState(1, startingMoney: 100);

        ShopPurchaseResult result = run.TryPurchaseRankUpgrade(Rank.Five, CardModifierResolver.RankToSpades, 10);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(ContainsUpgradedRank(run.Deck, Rank.Five, CardModifierResolver.RankToSpades), Is.True);
        Assert.That(ContainsModifier(run.Deck, Rank.Six, CardModifierResolver.RankToSpades), Is.False);
    }

    [Test]
    public void RankUpgradeReplacement_RewritesRunDeckToNewModifier()
    {
        var run = new RunState(1, startingMoney: 100);

        Assert.That(run.TryPurchaseRankUpgrade(Rank.Queen, CardModifierResolver.RankToHearts, 10).Succeeded, Is.True);
        Assert.That(run.TryPurchaseRankUpgrade(Rank.Queen, CardModifierResolver.RankToDiamonds, 10).Succeeded, Is.True);

        Assert.That(ContainsUpgradedRank(run.Deck, Rank.Queen, CardModifierResolver.RankToDiamonds), Is.True);
        Assert.That(ContainsModifier(run.Deck, Rank.Queen, CardModifierResolver.RankToHearts), Is.False);
    }

    [Test]
    public void RankUpgradeLoad_ReconcilesLegacyRawRunDeck()
    {
        var data = new RunStateData
        {
            seed = 1,
            money = 100,
            maxActiveItemSlots = RunState.DefaultMaxActiveItemSlots
        };
        data.deck.Add(CardData(Suit.Clubs, Rank.Seven));
        data.rankModifierIds.Add(new RankModifierData
        {
            rank = Rank.Seven,
            modifierId = CardModifierResolver.RankToClubs,
            paidPrice = 10
        });

        RunState loaded = RunState.FromData(data);

        Assert.That(ContainsUpgradedRank(loaded.Deck, Rank.Seven, CardModifierResolver.RankToClubs), Is.True);
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

    [Test]
    public void NegativeRank_ContributesNegativeBlackjackValue()
    {
        ScoreResult score = ScoreResolver.Resolve(
            new[] { new Card(Suit.Clubs, Rank.Seven, CardModifierResolver.NegativeRank), new Card(Suit.Hearts, Rank.Five) },
            null,
            21,
            21);

        Assert.That(score.BlackjackScore, Is.EqualTo(-2));
        Assert.That(score.FinalScore, Is.EqualTo(0));
    }

    [Test]
    public void NegativeRank_AceScoresNegativeEleven()
    {
        ScoreResult score = ScoreResolver.Resolve(
            new[] { new Card(Suit.Clubs, Rank.Ace, CardModifierResolver.NegativeRank), new Card(Suit.Hearts, Rank.Five) },
            null,
            21,
            21);

        Assert.That(score.BlackjackScore, Is.EqualTo(-6));
    }

    [Test]
    public void UnmodifiedAce_StillUsesSoftAceReduction()
    {
        ScoreResult score = ScoreResolver.Resolve(
            new[] { new Card(Suit.Clubs, Rank.Ace), new Card(Suit.Hearts, Rank.King), new Card(Suit.Spades, Rank.Five) },
            null,
            21,
            21);

        Assert.That(score.BlackjackScore, Is.EqualTo(16));
    }

    [Test]
    public void NumberValueModifiers_AdjustBlackjackScoreBeforeFinalScore()
    {
        ScoreResult doubled = ScoreResolver.Resolve(
            new[] { new Card(Suit.Clubs, Rank.Eight, CardModifierResolver.DoubleCardValue) },
            null,
            21,
            21);
        ScoreResult halved = ScoreResolver.Resolve(
            new[] { new Card(Suit.Clubs, Rank.Nine, CardModifierResolver.HalfCardValue) },
            null,
            21,
            21);
        ScoreResult joker = ScoreResolver.Resolve(
            new[] { new Card(Suit.Clubs, Rank.Two, CardModifierResolver.CreateResolvedJokerValue(21)) },
            null,
            21,
            21);

        Assert.That(doubled.BlackjackScore, Is.EqualTo(16));
        Assert.That(halved.BlackjackScore, Is.EqualTo(4));
        Assert.That(joker.BlackjackScore, Is.EqualTo(21));
        Assert.That(joker.IsBlackjack, Is.True);
    }

    [Test]
    public void CopyQueen_ContributesZeroBlackjackValueButKeepsPokerIdentity()
    {
        ScoreResult score = ScoreResolver.Resolve(
            new[] { new Card(Suit.Clubs, Rank.Queen, CardModifierResolver.CopyQueen), new Card(Suit.Hearts, Rank.Queen) },
            null,
            21,
            21);

        Assert.That(score.BlackjackScore, Is.EqualTo(10));
        Assert.That(score.PokerRank, Is.EqualTo(PokerHandRank.Pair));
    }

    [Test]
    public void FaceUtilityModifiers_ContributeZeroBlackjackValue()
    {
        string[] modifierIds =
        {
            CardModifierResolver.CopyQueen,
            CardModifierResolver.OutsourceOpponentCard,
            CardModifierResolver.DuplicateKing,
            CardModifierResolver.DuplicateOpponentCard,
            CardModifierResolver.HitTopDeckCard,
            CardModifierResolver.SplitPreviousCard,
            CardModifierResolver.ZeroThenForceHit
        };

        for (int i = 0; i < modifierIds.Length; i++)
        {
            ScoreResult score = ScoreResolver.Resolve(
                new[] { new Card(Suit.Clubs, Rank.King, modifierIds[i]) },
                null,
                21,
                21);

            Assert.That(score.BlackjackScore, Is.EqualTo(0), modifierIds[i]);
        }
    }

    [Test]
    public void MoveJack_MovesPreviousPlayerCardToOpponentVisiblePile()
    {
        RunState run = CreateRunWithDeck(CardData(Suit.Hearts, Rank.Five), CardData(Suit.Clubs, Rank.Jack, CardModifierResolver.MoveJack));
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound(battle.GetDefaultWager());
        battle.StartRound(battle.GetDefaultWager());

        Assert.That(battle.TryPlayPlayerHandCardForEffect(IndexOfRank(battle.CurrentRound.PlayerHand, Rank.Five)), Is.True);
        Assert.That(battle.TryPlayPlayerHandCardForEffect(IndexOfRank(battle.CurrentRound.PlayerHand, Rank.Jack)), Is.True);

        Assert.That(ContainsRank(battle.CurrentRound.PlayerPlayedCards, Rank.Five), Is.False);
        Assert.That(ContainsRank(battle.CurrentRound.PlayerPlayedCards, Rank.Jack), Is.True);
        Assert.That(ContainsRank(battle.CurrentRound.OpponentVisibleCards, Rank.Five), Is.True);
        Assert.That(ScoreResolver.Resolve(battle.CurrentRound.OpponentVisibleCards, null, 21, 21).BlackjackScore, Is.EqualTo(5));

        battle.Dispose();
    }

    [Test]
    public void MoveJack_CleansMovedPlayerCardToPlayerDiscardPile()
    {
        RunState run = CreateRunWithDeck(CardData(Suit.Hearts, Rank.Five), CardData(Suit.Clubs, Rank.Jack, CardModifierResolver.MoveJack));
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound(battle.GetDefaultWager());
        battle.StartRound(battle.GetDefaultWager());
        battle.TryPlayPlayerHandCardForEffect(IndexOfRank(battle.CurrentRound.PlayerHand, Rank.Five));
        battle.TryPlayPlayerHandCardForEffect(IndexOfRank(battle.CurrentRound.PlayerHand, Rank.Jack));

        battle.CleanupRound();

        Assert.That(ContainsRank(battle.PlayerDiscardPile, Rank.Five), Is.True);
        Assert.That(ContainsRank(battle.OpponentDiscardPile, Rank.Five), Is.False);

        battle.Dispose();
    }

    [Test]
    public void CopyQueen_PlaysMatchingSuitCardFromPlayerDeck()
    {
        RunState run = CreateRunWithDeck(RepeatCardData(Suit.Hearts, Rank.Two, 5));
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound(battle.GetDefaultWager());
        PlayPlayerCards(battle.CurrentRound, new Card(Suit.Hearts, Rank.Five));
        battle.CurrentRound.AddToHand(new Card(Suit.Clubs, Rank.Queen, CardModifierResolver.CopyQueen));
        int playedEvents = 0;
        battle.EventBus.Subscribe<CardPlayedEvent>(eventData =>
        {
            if (eventData.Owner == Combatant.Player)
                playedEvents++;
        });

        Assert.That(battle.TryPlayPlayerHandCardForEffect(IndexOfModifier(battle.CurrentRound.PlayerHand, CardModifierResolver.CopyQueen)), Is.True);

        Assert.That(ContainsRank(battle.CurrentRound.PlayerPlayedCards, Rank.Two), Is.True);
        Assert.That(playedEvents, Is.EqualTo(2));

        battle.Dispose();
    }

    [Test]
    public void CopyQueen_DoesNothingWhenNoDeckCardMatchesPreviousSuit()
    {
        RunState run = CreateRunWithDeck(RepeatCardData(Suit.Spades, Rank.Three, 5));
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound(battle.GetDefaultWager());
        PlayPlayerCards(battle.CurrentRound, new Card(Suit.Hearts, Rank.Five));
        battle.CurrentRound.AddToHand(new Card(Suit.Clubs, Rank.Queen, CardModifierResolver.CopyQueen));
        int fieldBefore = battle.CurrentRound.PlayerPlayedCards.Count;

        battle.TryPlayPlayerHandCardForEffect(IndexOfModifier(battle.CurrentRound.PlayerHand, CardModifierResolver.CopyQueen));

        Assert.That(battle.CurrentRound.PlayerPlayedCards.Count, Is.EqualTo(fieldBefore + 1));
        Assert.That(ContainsRank(battle.CurrentRound.PlayerPlayedCards, Rank.Three), Is.False);

        battle.Dispose();
    }

    [Test]
    public void DuplicateKing_AddsBattleOnlyDuplicateOfPreviousCardToPlayerField()
    {
        RunState run = CreateRunWithDeck(
            CardData(Suit.Hearts, Rank.Nine, CardModifierResolver.NegativeRank),
            CardData(Suit.Clubs, Rank.King, CardModifierResolver.DuplicateKing));
        int runDeckCount = run.Deck.Count;
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound(battle.GetDefaultWager());
        battle.StartRound(battle.GetDefaultWager());

        battle.TryPlayPlayerHandCardForEffect(IndexOfRank(battle.CurrentRound.PlayerHand, Rank.Nine));
        battle.TryPlayPlayerHandCardForEffect(IndexOfRank(battle.CurrentRound.PlayerHand, Rank.King));

        Assert.That(CountRank(battle.CurrentRound.PlayerPlayedCards, Rank.Nine), Is.EqualTo(2));
        Assert.That(ContainsModifier(battle.CurrentRound.PlayerPlayedCards, Rank.Nine, CardModifierResolver.NegativeRank), Is.True);
        Assert.That(run.Deck.Count, Is.EqualTo(runDeckCount));

        battle.Dispose();
    }

    [Test]
    public void HitLower_PlaysRandomLowerRankFromPlayerDeck()
    {
        RunState run = CreateRunWithDeck(RepeatCardData(Suit.Diamonds, Rank.Two, 5));
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound(battle.GetDefaultWager());
        battle.CurrentRound.AddToHand(new Card(Suit.Clubs, Rank.Six, CardModifierResolver.HitLower));

        Assert.That(battle.TryPlayPlayerHandCardForEffect(IndexOfRank(battle.CurrentRound.PlayerHand, Rank.Six)), Is.True);

        Assert.That(ContainsRank(battle.CurrentRound.PlayerPlayedCards, Rank.Six), Is.True);
        Assert.That(ContainsRank(battle.CurrentRound.PlayerPlayedCards, Rank.Two), Is.True);

        battle.Dispose();
    }

    [Test]
    public void HitLower_DoesNothingWhenNoLowerRankExists()
    {
        RunState run = CreateRunWithDeck(RepeatCardData(Suit.Hearts, Rank.Seven, 5));
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound(battle.GetDefaultWager());
        battle.CurrentRound.AddToHand(new Card(Suit.Clubs, Rank.Six, CardModifierResolver.HitLower));

        Assert.That(battle.TryPlayPlayerHandCardForEffect(IndexOfRank(battle.CurrentRound.PlayerHand, Rank.Six)), Is.True);

        Assert.That(battle.CurrentRound.PlayerPlayedCards.Count, Is.EqualTo(1));
        Assert.That(battle.CurrentRound.PlayerPlayedCards[0].Rank, Is.EqualTo(Rank.Six));

        battle.Dispose();
    }

    [Test]
    public void HitLower_EffectPlayedCardPublishesCardPlayedEvent()
    {
        RunState run = CreateRunWithDeck(RepeatCardData(Suit.Diamonds, Rank.Four, 5));
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound(battle.GetDefaultWager());
        battle.CurrentRound.AddToHand(new Card(Suit.Clubs, Rank.Six, CardModifierResolver.HitLower));
        var playedRanks = new List<Rank>();
        battle.EventBus.Subscribe<CardPlayedEvent>(eventData => playedRanks.Add(eventData.Card.Rank));

        Assert.That(battle.TryPlayPlayerHandCardForEffect(IndexOfRank(battle.CurrentRound.PlayerHand, Rank.Six)), Is.True);

        CollectionAssert.AreEqual(new[] { Rank.Six, Rank.Four }, playedRanks);
        Assert.That(ContainsRank(battle.CurrentRound.PlayerPlayedCards, Rank.Four), Is.True);

        battle.Dispose();
    }

    [Test]
    public void HitHigher_PlaysRandomHigherRankFromPlayerDeck()
    {
        RunState run = CreateRunWithDeck(RepeatCardData(Suit.Spades, Rank.Nine, 5));
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound(battle.GetDefaultWager());
        battle.CurrentRound.AddToHand(new Card(Suit.Clubs, Rank.Six, CardModifierResolver.HitHigher));

        Assert.That(battle.TryPlayPlayerHandCardForEffect(IndexOfRank(battle.CurrentRound.PlayerHand, Rank.Six)), Is.True);

        Assert.That(ContainsRank(battle.CurrentRound.PlayerPlayedCards, Rank.Six), Is.True);
        Assert.That(ContainsRank(battle.CurrentRound.PlayerPlayedCards, Rank.Nine), Is.True);

        battle.Dispose();
    }

    [Test]
    public void DrawRank_DrawsMatchingRankFromPlayerDeckToHand()
    {
        RunState run = CreateRunWithDeck(RepeatCardData(Suit.Diamonds, Rank.Eight, 5));
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound(battle.GetDefaultWager());
        int eightsBefore = CountRank(battle.CurrentRound.PlayerHand, Rank.Eight);
        battle.CurrentRound.AddToHand(new Card(Suit.Clubs, Rank.Eight, CardModifierResolver.DrawRank));

        Assert.That(battle.TryPlayPlayerHandCardForEffect(IndexOfModifier(battle.CurrentRound.PlayerHand, CardModifierResolver.DrawRank)), Is.True);

        Assert.That(CountRank(battle.CurrentRound.PlayerHand, Rank.Eight), Is.EqualTo(eightsBefore + 1));

        battle.Dispose();
    }

    [Test]
    public void MoveOpponentToPlayer_MovesPreviousOpponentCardToPlayerField()
    {
        var battle = new BattleState(new RunState(1), new BattleConfig("test", 100));
        battle.StartRound(battle.GetDefaultWager());
        PlayOpponentCards(battle.CurrentRound, new Card(Suit.Diamonds, Rank.Seven));
        battle.CurrentRound.AddToHand(new Card(Suit.Clubs, Rank.Jack, CardModifierResolver.MoveOpponentToPlayer));

        Assert.That(battle.TryPlayPlayerHandCardForEffect(IndexOfModifier(battle.CurrentRound.PlayerHand, CardModifierResolver.MoveOpponentToPlayer)), Is.True);

        Assert.That(ContainsRank(battle.CurrentRound.OpponentVisibleCards, Rank.Seven), Is.False);
        Assert.That(ContainsRank(battle.CurrentRound.PlayerPlayedCards, Rank.Seven), Is.True);

        battle.Dispose();
    }

    [Test]
    public void DemoteOpponentCard_ReturnsPreviousOpponentCardToOpponentDeckBottom()
    {
        var battle = new BattleState(new RunState(1), new BattleConfig("test", 100));
        battle.StartRound(battle.GetDefaultWager());
        Card demoted = new(Suit.Hearts, Rank.Seven);
        PlayOpponentCards(battle.CurrentRound, demoted);
        battle.CurrentRound.AddToHand(new Card(Suit.Clubs, Rank.Jack, CardModifierResolver.DemoteOpponentCard));

        Assert.That(battle.TryPlayPlayerHandCardForEffect(IndexOfModifier(battle.CurrentRound.PlayerHand, CardModifierResolver.DemoteOpponentCard)), Is.True);

        Assert.That(ContainsRank(battle.CurrentRound.OpponentVisibleCards, Rank.Seven), Is.False);
        Assert.That(battle.OpponentDrawPile[0], Is.EqualTo(demoted));

        battle.Dispose();
    }

    [Test]
    public void DuplicateOpponentCard_AddsCopyOfPreviousOpponentCardToPlayerField()
    {
        var battle = new BattleState(new RunState(1), new BattleConfig("test", 100));
        battle.StartRound(battle.GetDefaultWager());
        PlayOpponentCards(battle.CurrentRound, new Card(Suit.Diamonds, Rank.Eight));
        battle.CurrentRound.AddToHand(new Card(Suit.Clubs, Rank.King, CardModifierResolver.DuplicateOpponentCard));

        Assert.That(battle.TryPlayPlayerHandCardForEffect(IndexOfModifier(battle.CurrentRound.PlayerHand, CardModifierResolver.DuplicateOpponentCard)), Is.True);

        Assert.That(ContainsRank(battle.CurrentRound.OpponentVisibleCards, Rank.Eight), Is.True);
        Assert.That(ContainsRank(battle.CurrentRound.PlayerPlayedCards, Rank.Eight), Is.True);

        battle.Dispose();
    }

    [Test]
    public void HitTopDeckCard_PlaysCurrentPlayerDeckTopCard()
    {
        RunState run = CreateRunWithDeck(RepeatCardData(Suit.Spades, Rank.Five, 5));
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound(battle.GetDefaultWager());
        Card expected = battle.PlayerDrawPile[^1];
        battle.CurrentRound.AddToHand(new Card(Suit.Clubs, Rank.King, CardModifierResolver.HitTopDeckCard));

        Assert.That(battle.TryPlayPlayerHandCardForEffect(IndexOfModifier(battle.CurrentRound.PlayerHand, CardModifierResolver.HitTopDeckCard)), Is.True);

        Assert.That(IndexOfRankAndSuit(battle.CurrentRound.PlayerPlayedCards, expected.Rank, expected.Suit), Is.GreaterThanOrEqualTo(0));

        battle.Dispose();
    }

    [Test]
    public void SplitPreviousCard_ReplacesPreviousNumberWithTwoHalfCards()
    {
        var battle = new BattleState(new RunState(1), new BattleConfig("test", 100));
        battle.StartRound(battle.GetDefaultWager());
        PlayPlayerCards(battle.CurrentRound, new Card(Suit.Hearts, Rank.Nine));
        battle.CurrentRound.AddToHand(new Card(Suit.Clubs, Rank.King, CardModifierResolver.SplitPreviousCard));

        Assert.That(battle.TryPlayPlayerHandCardForEffect(IndexOfModifier(battle.CurrentRound.PlayerHand, CardModifierResolver.SplitPreviousCard)), Is.True);

        Assert.That(ContainsRank(battle.CurrentRound.PlayerPlayedCards, Rank.Nine), Is.False);
        Assert.That(ContainsRank(battle.CurrentRound.PlayerPlayedCards, Rank.Four), Is.True);
        Assert.That(ContainsRank(battle.CurrentRound.PlayerPlayedCards, Rank.Five), Is.True);

        battle.Dispose();
    }

    [Test]
    public void TriggerPreviousEffect_TriggersPreviousCardEffectOnce()
    {
        RunState run = CreateRunWithDeck(RepeatCardData(Suit.Diamonds, Rank.Four, 5));
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound(battle.GetDefaultWager());
        PlayPlayerCards(battle.CurrentRound, new Card(Suit.Clubs, Rank.Six, CardModifierResolver.HitLower));
        battle.CurrentRound.AddToHand(new Card(Suit.Hearts, Rank.Two, CardModifierResolver.TriggerPreviousEffect));

        Assert.That(battle.TryPlayPlayerHandCardForEffect(IndexOfModifier(battle.CurrentRound.PlayerHand, CardModifierResolver.TriggerPreviousEffect)), Is.True);

        Assert.That(ContainsRank(battle.CurrentRound.PlayerPlayedCards, Rank.Four), Is.True);

        battle.Dispose();
    }

    [Test]
    public void TriggerPreviousEffect_DoesNotChainThroughAnotherTriggerPreviousEffect()
    {
        RunState run = CreateRunWithDeck(RepeatCardData(Suit.Diamonds, Rank.Four, 5));
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound(battle.GetDefaultWager());
        PlayPlayerCards(
            battle.CurrentRound,
            new Card(Suit.Clubs, Rank.Six, CardModifierResolver.HitLower),
            new Card(Suit.Hearts, Rank.Two, CardModifierResolver.TriggerPreviousEffect));
        battle.CurrentRound.AddToHand(new Card(Suit.Spades, Rank.Three, CardModifierResolver.TriggerPreviousEffect));

        Assert.That(battle.TryPlayPlayerHandCardForEffect(IndexOfModifier(battle.CurrentRound.PlayerHand, CardModifierResolver.TriggerPreviousEffect)), Is.True);

        Assert.That(ContainsRank(battle.CurrentRound.PlayerPlayedCards, Rank.Four), Is.False);

        battle.Dispose();
    }

    [Test]
    public void DiscardLowestNumber_DiscardsLowestPlayerNumberCard()
    {
        var battle = new BattleState(new RunState(1), new BattleConfig("test", 100));
        battle.StartRound(battle.GetDefaultWager());
        Card lowest = new(Suit.Hearts, Rank.Two);
        PlayPlayerCards(battle.CurrentRound, new Card(Suit.Clubs, Rank.Five), lowest);
        battle.CurrentRound.AddToHand(new Card(Suit.Spades, Rank.Eight, CardModifierResolver.DiscardLowestNumber));

        Assert.That(battle.TryPlayPlayerHandCardForEffect(IndexOfModifier(battle.CurrentRound.PlayerHand, CardModifierResolver.DiscardLowestNumber)), Is.True);

        Assert.That(ContainsRank(battle.CurrentRound.PlayerPlayedCards, Rank.Two), Is.False);
        Assert.That(battle.PlayerDiscardPile[0], Is.EqualTo(lowest));

        battle.Dispose();
    }

    [Test]
    public void JokerValue_ReplacesPlayedCardWithResolvedRandomValue()
    {
        var battle = new BattleState(new RunState(1), new BattleConfig("test", 100));
        battle.StartRound(battle.GetDefaultWager());
        battle.CurrentRound.AddToHand(new Card(Suit.Clubs, Rank.Two, CardModifierResolver.JokerValue));

        Assert.That(battle.TryPlayPlayerHandCardForEffect(IndexOfModifier(battle.CurrentRound.PlayerHand, CardModifierResolver.JokerValue)), Is.True);

        Card resolved = battle.CurrentRound.PlayerPlayedCards[^1];
        Assert.That(CardModifierResolver.TryGetResolvedJokerValue(resolved.ModifierId, out int value), Is.True);
        CollectionAssert.Contains(new[] { 1, 2, 12, 21 }, value);

        battle.Dispose();
    }

    [Test]
    public void ZeroThenForceHit_PlaysTopDeckCardAfterZeroValueTrigger()
    {
        RunState run = CreateRunWithDeck(RepeatCardData(Suit.Spades, Rank.Six, 5));
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound(battle.GetDefaultWager());
        Card expected = battle.PlayerDrawPile[^1];
        battle.CurrentRound.AddToHand(new Card(Suit.Clubs, Rank.Seven, CardModifierResolver.ZeroThenForceHit));

        Assert.That(battle.TryPlayPlayerHandCardForEffect(IndexOfModifier(battle.CurrentRound.PlayerHand, CardModifierResolver.ZeroThenForceHit)), Is.True);

        Assert.That(IndexOfRankAndSuit(battle.CurrentRound.PlayerPlayedCards, expected.Rank, expected.Suit), Is.GreaterThanOrEqualTo(0));
        Assert.That(battle.CurrentRound.PlayerScore.BlackjackScore, Is.EqualTo(expected.BlackjackValue));

        battle.Dispose();
    }

    [Test]
    public void LastHandCardPlay_IsObservedBeforeRefillDraws()
    {
        RunState run = CreateRunWithDeck(
            CardData(Suit.Clubs, Rank.Two),
            CardData(Suit.Diamonds, Rank.Three),
            CardData(Suit.Hearts, Rank.Four),
            CardData(Suit.Spades, Rank.Five));
        var battle = new BattleState(run, TestBattleConfig(startingHandSize: 2));
        battle.StartRound(battle.GetDefaultWager());
        var received = new List<string>();
        battle.EventBus.Subscribe<CardPlayedEvent>(eventData =>
        {
            if (eventData.Owner == Combatant.Player)
                received.Add("played");
        });
        battle.EventBus.Subscribe<CardDrawnEvent>(eventData =>
        {
            if (eventData.Owner == Combatant.Player)
                received.Add("drawn");
        });

        Assert.That(battle.TryPlayPlayerHandCardForEffect(0), Is.True);
        received.Clear();
        Assert.That(battle.TryPlayPlayerHandCardForEffect(0), Is.True);

        CollectionAssert.AreEqual(new[] { "played", "drawn", "drawn" }, received);
        battle.Dispose();
    }

    [Test]
    public void DiscardLastHit_IsObservedBeforeRefillDraw()
    {
        RunState run = CreateRunWithDeck(
            CardData(Suit.Clubs, Rank.Two),
            CardData(Suit.Diamonds, Rank.Three),
            CardData(Suit.Hearts, Rank.Four));
        var battle = new BattleState(run, TestBattleConfig(startingHandSize: 1));
        battle.StartRound(battle.GetDefaultWager());
        Assert.That(battle.CurrentRound.TryPlayCard(0, out _), Is.True);
        Assert.That(battle.CurrentRound.TryPlayHitCard(new Card(Suit.Spades, Rank.Five)), Is.True);
        var received = new List<string>();
        battle.EventBus.Subscribe<CardDiscardedEvent>(eventData =>
        {
            if (eventData.Owner == Combatant.Player)
                received.Add("discarded");
        });
        battle.EventBus.Subscribe<CardDrawnEvent>(eventData =>
        {
            if (eventData.Owner == Combatant.Player)
                received.Add("drawn");
        });

        Assert.That(battle.DiscardLastPlayerHitCard(), Is.True);

        CollectionAssert.AreEqual(new[] { "discarded", "drawn" }, received);
        battle.Dispose();
    }

    [Test]
    public void DrawSuit_DrawsMatchingSuitFromPlayerDrawPile()
    {
        var run = new RunState(1, startingMoney: 100);
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound(battle.GetDefaultWager());
        battle.StartRound(battle.GetDefaultWager());
        Card trigger = new(Suit.Hearts, Rank.Four, CardModifierResolver.DrawSuit);
        int heartsBefore = CountSuit(battle.PlayerDrawPile, Suit.Hearts);
        int handBefore = battle.CurrentRound.PlayerHand.Count;

        battle.CurrentRound.TryPlayHitCard(trigger);
        battle.EventBus.Publish(new CardPlayedEvent(Combatant.Player, trigger));

        Assert.That(CountSuit(battle.PlayerDrawPile, Suit.Hearts), Is.EqualTo(heartsBefore - 1));
        Assert.That(battle.CurrentRound.PlayerHand.Count, Is.EqualTo(handBefore + 1));
        Assert.That(ContainsSuit(battle.CurrentRound.PlayerHand, Suit.Hearts), Is.True);

        battle.Dispose();
    }

    [Test]
    public void DrawSuit_DoesNotReshuffleDiscardWhenNoDrawPileMatchExists()
    {
        RunState run = CreateRunWithDeck(
            CardData(Suit.Spades, Rank.Two),
            CardData(Suit.Spades, Rank.Three),
            CardData(Suit.Spades, Rank.Four),
            CardData(Suit.Spades, Rank.Five),
            CardData(Suit.Spades, Rank.Six),
            CardData(Suit.Spades, Rank.Seven));
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound(battle.GetDefaultWager());
        battle.StartRound(battle.GetDefaultWager());
        battle.CurrentRound.TryPlayHitCard(new Card(Suit.Hearts, Rank.Two));
        battle.ClearField(Combatant.Player);
        Card trigger = new(Suit.Hearts, Rank.Four, CardModifierResolver.DrawSuit);
        int handBefore = battle.CurrentRound.PlayerHand.Count;
        int discardBefore = battle.PlayerDiscardPile.Count;

        battle.CurrentRound.TryPlayHitCard(trigger);
        battle.EventBus.Publish(new CardPlayedEvent(Combatant.Player, trigger));

        Assert.That(battle.CurrentRound.PlayerHand.Count, Is.EqualTo(handBefore));
        Assert.That(battle.PlayerDiscardPile.Count, Is.EqualTo(discardBefore));

        battle.Dispose();
    }

    [Test]
    public void PlayerCanManuallyPlayMultipleCardsInSameTurn()
    {
        RunState run = CreateRunWithDeck(CardData(Suit.Clubs, Rank.Two), CardData(Suit.Hearts, Rank.Three));
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound(battle.GetDefaultWager());
        battle.StartRound(battle.GetDefaultWager());

        Assert.That(battle.TryPlayCard(0), Is.True);
        Assert.That(battle.TryPlayCard(0), Is.True);

        Assert.That(battle.CurrentRound.PlayerPlayedCards.Count, Is.EqualTo(2));

        battle.Dispose();
    }

    [Test]
    public void RankUpgradePurchase_ImmediatelyTransformsPlayerBattleCards()
    {
        var run = new RunState(1, startingMoney: 100);
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound(battle.GetDefaultWager());
        battle.StartRound(battle.GetDefaultWager());
        battle.CurrentRound.AddToHand(new Card(Suit.Clubs, Rank.Five));
        battle.CurrentRound.TryPlayHitCard(new Card(Suit.Diamonds, Rank.Five));
        battle.ClearField(Combatant.Player);
        battle.CurrentRound.AddToHand(new Card(Suit.Hearts, Rank.Six));
        battle.CurrentRound.TryPlayHitCard(new Card(Suit.Hearts, Rank.Five));
        Card opponentCardBefore = battle.OpponentDrawPile[0];

        ShopPurchaseResult result = run.TryPurchaseRankUpgrade(Rank.Five, CardModifierResolver.RankToSpades, 10);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(ContainsUpgradedRank(battle.PlayerDrawPile, Rank.Five, CardModifierResolver.RankToSpades), Is.True);
        Assert.That(ContainsUpgradedRank(battle.PlayerDiscardPile, Rank.Five, CardModifierResolver.RankToSpades), Is.True);
        Assert.That(ContainsUpgradedRank(battle.CurrentRound.PlayerHand, Rank.Five, CardModifierResolver.RankToSpades), Is.True);
        Assert.That(ContainsUpgradedRank(battle.CurrentRound.PlayerPlayedCards, Rank.Five, CardModifierResolver.RankToSpades), Is.True);
        Assert.That(ContainsModifier(battle.CurrentRound.PlayerHand, Rank.Six, CardModifierResolver.RankToSpades), Is.False);
        Assert.That(battle.OpponentDrawPile[0], Is.EqualTo(opponentCardBefore));

        battle.Dispose();
    }

    [Test]
    public void RankUpgradeReplacement_RewritesActiveBattleCardsToNewModifier()
    {
        var run = new RunState(1, startingMoney: 100);
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound(battle.GetDefaultWager());
        battle.StartRound(battle.GetDefaultWager());
        battle.CurrentRound.AddToHand(new Card(Suit.Clubs, Rank.Queen));

        Assert.That(run.TryPurchaseRankUpgrade(Rank.Queen, CardModifierResolver.RankToHearts, 10).Succeeded, Is.True);
        Assert.That(run.TryPurchaseRankUpgrade(Rank.Queen, CardModifierResolver.RankToDiamonds, 10).Succeeded, Is.True);

        Assert.That(ContainsUpgradedRank(battle.CurrentRound.PlayerHand, Rank.Queen, CardModifierResolver.RankToDiamonds), Is.True);
        Assert.That(ContainsModifier(battle.CurrentRound.PlayerHand, Rank.Queen, CardModifierResolver.RankToHearts), Is.False);

        battle.Dispose();
    }

    [Test]
    public void RankUpgradePurchase_TransformsPlayerHandCarryover()
    {
        var run = new RunState(1, startingMoney: 100);
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound(battle.GetDefaultWager());
        battle.StartRound(battle.GetDefaultWager());
        battle.CurrentRound.AddToHand(new Card(Suit.Clubs, Rank.Jack));
        battle.CleanupRound();

        Assert.That(run.TryPurchaseRankUpgrade(Rank.Jack, CardModifierResolver.RankToClubs, 10).Succeeded, Is.True);
        Assert.That(battle.StartRound(battle.GetDefaultWager()), Is.True);

        Assert.That(ContainsUpgradedRank(battle.CurrentRound.PlayerHand, Rank.Jack, CardModifierResolver.RankToClubs), Is.True);

        battle.Dispose();
    }

    [Test]
    public void DrawThree_AddsCardsPastNormalHandSize()
    {
        var run = new RunState(1);
        run.AddActiveItem(ActiveItemResolver.DrawThree);
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound(battle.GetDefaultWager());
        battle.StartRound(battle.GetDefaultWager());

        int before = battle.CurrentRound.PlayerHand.Count;

        Assert.That(battle.TryUseActiveItem(ActiveItemResolver.DrawThree), Is.True);

        Assert.That(battle.CurrentRound.PlayerHand.Count, Is.EqualTo(before + 3));
        Assert.That(run.HasActiveItem(ActiveItemResolver.DrawThree), Is.False);
    }

    [Test]
    public void RejectLastHit_RemovesLatestHitCardFromScoringField()
    {
        var run = new RunState(1);
        run.AddActiveItem(ActiveItemResolver.RejectLastHit);
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound(battle.GetDefaultWager());
        battle.StartRound(battle.GetDefaultWager());
        Card hitCard = new(Suit.Hearts, Rank.Eight);
        battle.CurrentRound.TryPlayHitCard(hitCard);

        Assert.That(battle.TryUseActiveItem(ActiveItemResolver.RejectLastHit), Is.True);

        Assert.That(battle.CurrentRound.PlayerPlayedCards.Count, Is.EqualTo(0));
        Assert.That(battle.PlayerDiscardPile.Count, Is.EqualTo(1));
        Assert.That(battle.PlayerDiscardPile[0], Is.EqualTo(hitCard));
    }

    [Test]
    public void LeverageTriple_AddsTwoExtraMatchingStakes()
    {
        var run = new RunState(1, startingMoney: 100);
        run.AddActiveItem(ActiveItemResolver.LeverageTriple);
        var battle = new BattleState(run, new BattleConfig("test", 100, baseWager: 10));
        battle.StartRound(battle.GetDefaultWager());

        Assert.That(battle.TryUseActiveItem(ActiveItemResolver.LeverageTriple), Is.True);

        Assert.That(battle.CurrentRound.WagerMultiplier, Is.EqualTo(3));
        Assert.That(battle.CurrentRound.EffectiveWager, Is.EqualTo(30));
        Assert.That(battle.CurrentRound.Pot, Is.EqualTo(60));
        Assert.That(run.Money, Is.EqualTo(70));
        Assert.That(battle.OpponentMoney, Is.EqualTo(70));
        Assert.That(run.HasActiveItem(ActiveItemResolver.LeverageTriple), Is.False);

        battle.Dispose();
    }

    [Test]
    public void BurstThresholdPlusThree_IncreasesPlayerBurstThresholdForCurrentRound()
    {
        var run = new RunState(1);
        run.AddActiveItem(ActiveItemResolver.BurstThresholdPlusThree);
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound(battle.GetDefaultWager());
        int before = battle.CurrentRound.PlayerBurstThreshold;

        Assert.That(battle.TryUseActiveItem(ActiveItemResolver.BurstThresholdPlusThree), Is.True);

        Assert.That(battle.CurrentRound.PlayerBurstThreshold, Is.EqualTo(before + 3));
        Assert.That(run.HasActiveItem(ActiveItemResolver.BurstThresholdPlusThree), Is.False);

        battle.Dispose();
    }

    [Test]
    public void ReturnPlayerFieldCardToHand_ReturnsLatestFieldCard()
    {
        var run = new RunState(1);
        run.AddActiveItem(ActiveItemResolver.ReturnPlayerFieldCardToHand);
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound(battle.GetDefaultWager());
        Card fieldCard = new(Suit.Hearts, Rank.Eight);
        battle.CurrentRound.TryPlayHitCard(fieldCard);
        int handBefore = battle.CurrentRound.PlayerHand.Count;

        Assert.That(battle.TryUseActiveItem(ActiveItemResolver.ReturnPlayerFieldCardToHand), Is.True);

        Assert.That(battle.CurrentRound.PlayerPlayedCards.Count, Is.EqualTo(0));
        Assert.That(battle.CurrentRound.PlayerHand.Count, Is.EqualTo(handBefore + 1));
        Assert.That(ContainsRank(battle.CurrentRound.PlayerHand, Rank.Eight), Is.True);
        Assert.That(run.HasActiveItem(ActiveItemResolver.ReturnPlayerFieldCardToHand), Is.False);

        battle.Dispose();
    }

    [Test]
    public void DrawTwoSuitActiveItem_DrawsTwoMatchingSuitCardsPastNormalHandSize()
    {
        RunState run = CreateRunWithDeck(RepeatCardData(Suit.Hearts, Rank.Two, 5));
        run.AddActiveItem(ActiveItemResolver.DrawTwoHearts);
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound(battle.GetDefaultWager());
        int before = battle.CurrentRound.PlayerHand.Count;

        Assert.That(battle.TryUseActiveItem(ActiveItemResolver.DrawTwoHearts), Is.True);

        Assert.That(battle.CurrentRound.PlayerHand.Count, Is.EqualTo(before + 2));
        Assert.That(CountSuit(battle.CurrentRound.PlayerHand, Suit.Hearts), Is.EqualTo(before + 2));
        Assert.That(run.HasActiveItem(ActiveItemResolver.DrawTwoHearts), Is.False);

        battle.Dispose();
    }

    [Test]
    public void StartRound_CreatesCommitsAndBeginsRoundAtomically()
    {
        var run = new RunState(1, startingMoney: 100);
        var battle = new BattleState(run, new BattleConfig("test", 100, baseWager: 25));
        int wagerCommittedEvents = 0;
        int roundStartedEvents = 0;
        int playerTurnStartedEvents = 0;
        WagerCommittedEvent committedWager = default;
        battle.EventBus.Subscribe<WagerCommittedEvent>(eventData =>
        {
            wagerCommittedEvents++;
            committedWager = eventData;
        });
        battle.EventBus.Subscribe<RoundStartedEvent>(_ => roundStartedEvents++);
        battle.EventBus.Subscribe<PlayerTurnStartedEvent>(_ => playerTurnStartedEvents++);

        Assert.That(battle.StartRound(battle.GetDefaultWager()), Is.True);

        Assert.That(battle.RoundNumber, Is.EqualTo(1));
        Assert.That(battle.Phase, Is.EqualTo(BattlePhase.PlayerPhase));
        Assert.That(battle.CurrentRound, Is.Not.Null);
        Assert.That(battle.CurrentRound.WagerCommitted, Is.True);
        Assert.That(battle.CurrentRound.EffectiveWager, Is.EqualTo(25));
        Assert.That(battle.CurrentRound.PlayerStake, Is.EqualTo(25));
        Assert.That(battle.CurrentRound.OpponentStake, Is.EqualTo(25));
        Assert.That(run.Money, Is.EqualTo(75));
        Assert.That(battle.OpponentMoney, Is.EqualTo(75));
        Assert.That(wagerCommittedEvents, Is.EqualTo(1));
        Assert.That(committedWager.WagerAmount, Is.EqualTo(25));
        Assert.That(roundStartedEvents, Is.EqualTo(1));
        Assert.That(playerTurnStartedEvents, Is.EqualTo(1));

        int roundStartedCommands = 0;
        while (battle.CommandQueue.TryDequeue(out VisualCommand command))
        {
            if (command.Type == VisualCommandType.RoundStarted)
                roundStartedCommands++;
        }

        Assert.That(roundStartedCommands, Is.EqualTo(1));
        battle.Dispose();
    }

    [Test]
    public void StartRound_PublishesNewRoundBeforeOpeningShuffleAndDraws()
    {
        RunState run = CreateRunWithDeck(
            CardData(Suit.Clubs, Rank.Two),
            CardData(Suit.Diamonds, Rank.Three));
        var battle = new BattleState(run, TestBattleConfig(startingHandSize: 2));
        Assert.That(battle.StartRound(battle.GetDefaultWager()), Is.True);
        while (battle.CurrentRound.PlayerHand.Count > 0)
            Assert.That(battle.CurrentRound.TryPlayCard(0, out _), Is.True);
        battle.CleanupRound();

        var received = new List<string>();
        int eventRoundNumber = 0;
        int stateRoundNumber = 0;
        RoundState roundSeenBySubscriber = null;
        battle.EventBus.Subscribe<RoundStartedEvent>(eventData =>
        {
            received.Add("round");
            eventRoundNumber = eventData.RoundNumber;
            stateRoundNumber = battle.RoundNumber;
            roundSeenBySubscriber = battle.CurrentRound;
        });
        battle.EventBus.Subscribe<DeckShuffledEvent>(eventData =>
        {
            if (eventData.Owner == Combatant.Player)
                received.Add("shuffle");
        });
        battle.EventBus.Subscribe<CardDrawnEvent>(eventData =>
        {
            if (eventData.Owner == Combatant.Player)
                received.Add("draw");
        });

        Assert.That(battle.StartRound(battle.GetDefaultWager()), Is.True);

        Assert.That(eventRoundNumber, Is.EqualTo(2));
        Assert.That(stateRoundNumber, Is.EqualTo(2));
        Assert.That(roundSeenBySubscriber, Is.SameAs(battle.CurrentRound));
        CollectionAssert.AreEqual(new[] { "round", "shuffle", "draw", "draw" }, received);
        battle.Dispose();
    }

    [Test]
    public void StartRound_InvalidAndDuplicateCallsDoNotMutateBattle()
    {
        var run = new RunState(1, startingMoney: 100);
        var battle = new BattleState(run, new BattleConfig("test", 100, baseWager: 25));
        int roundStartedEvents = 0;
        battle.EventBus.Subscribe<RoundStartedEvent>(_ => roundStartedEvents++);

        Assert.That(battle.StartRound(0), Is.False);
        Assert.That(battle.CurrentRound, Is.Null);
        Assert.That(battle.RoundNumber, Is.EqualTo(0));
        Assert.That(run.Money, Is.EqualTo(100));
        Assert.That(battle.OpponentMoney, Is.EqualTo(100));

        Assert.That(battle.StartRound(battle.GetDefaultWager()), Is.True);
        Assert.That(battle.StartRound(battle.GetDefaultWager()), Is.False);
        Assert.That(battle.RoundNumber, Is.EqualTo(1));
        Assert.That(run.Money, Is.EqualTo(75));
        Assert.That(battle.OpponentMoney, Is.EqualTo(75));
        Assert.That(roundStartedEvents, Is.EqualTo(1));
        battle.Dispose();
    }

    [Test]
    public void StartRound_CapsEachStakeByAvailableMoney()
    {
        var run = new RunState(1, startingMoney: 12);
        var battle = new BattleState(run, new BattleConfig("test", 15, baseWager: 25));

        Assert.That(battle.StartRound(battle.GetDefaultWager()), Is.True);

        Assert.That(battle.CurrentRound.EffectiveWager, Is.EqualTo(25));
        Assert.That(battle.CurrentRound.PlayerStake, Is.EqualTo(12));
        Assert.That(battle.CurrentRound.OpponentStake, Is.EqualTo(15));
        Assert.That(battle.CurrentRound.Pot, Is.EqualTo(27));
        Assert.That(run.Money, Is.EqualTo(0));
        Assert.That(battle.OpponentMoney, Is.EqualTo(0));
        battle.Dispose();
    }

    [Test]
    public void DoubleWager_AddsMatchingExtraStakes()
    {
        var run = new RunState(1, startingMoney: 100);
        run.AddActiveItem(ActiveItemResolver.DoubleWager);
        var battle = new BattleState(run, new BattleConfig("test", 100, baseWager: 10));
        battle.StartRound(battle.GetDefaultWager());
        battle.StartRound(battle.GetDefaultWager());

        Assert.That(battle.TryUseActiveItem(ActiveItemResolver.DoubleWager), Is.True);

        Assert.That(battle.CurrentRound.BaseWager, Is.EqualTo(10));
        Assert.That(battle.CurrentRound.WagerMultiplier, Is.EqualTo(2));
        Assert.That(battle.CurrentRound.EffectiveWager, Is.EqualTo(20));
        Assert.That(battle.CurrentRound.Pot, Is.EqualTo(40));
        Assert.That(run.Money, Is.EqualTo(80));
        Assert.That(battle.OpponentMoney, Is.EqualTo(80));
    }

    [Test]
    public void FixedWager_StartRoundAntesConfiguredDefaultWager()
    {
        var run = new RunState(1, startingMoney: 100);
        var battle = new BattleState(run, new BattleConfig("test", 100, new StandingDevilStrategy(), baseWager: 25));
        battle.StartRound(battle.GetDefaultWager());
        battle.StartRound(battle.GetDefaultWager());

        Assert.That(battle.CurrentRound.EffectiveWager, Is.EqualTo(25));
        Assert.That(battle.CurrentRound.PlayerStake, Is.EqualTo(25));
        Assert.That(battle.CurrentRound.OpponentStake, Is.EqualTo(25));
        Assert.That(battle.CurrentRound.Pot, Is.EqualTo(50));
        Assert.That(run.Money, Is.EqualTo(75));
        Assert.That(battle.OpponentMoney, Is.EqualTo(75));
    }

    [Test]
    public void Burst_DoesNotEndRoundUntilBothCombatantsStand()
    {
        var run = new RunState(1, startingMoney: 500);
        var battle = new BattleState(run, TestBattleConfig());
        battle.StartRound(battle.GetDefaultWager());
        battle.StartRound(battle.GetDefaultWager());
        PlayPlayerCards(battle.CurrentRound, new Card(Suit.Clubs, Rank.King), new Card(Suit.Hearts, Rank.King), new Card(Suit.Spades, Rank.Five));

        Assert.That(battle.EndPlayerPhase().Winner, Is.Null);

        Assert.That(battle.CurrentRound.PlayerScore.IsBurst, Is.True);
        Assert.That(battle.Phase, Is.EqualTo(BattlePhase.PlayerPhase));
        Assert.That(battle.CurrentRound, Is.Not.Null);
    }

    [Test]
    public void BurstPayout_UsesOffsetTimesEffectiveWager()
    {
        var run = new RunState(1, startingMoney: 1000);
        var battle = new BattleState(run, new BattleConfig("test", 1000, new StandingDevilStrategy(), baseWager: 100));
        battle.StartRound(battle.GetDefaultWager());
        battle.StartRound(battle.GetDefaultWager());
        PlayPlayerCards(battle.CurrentRound, new Card(Suit.Clubs, Rank.Nine), new Card(Suit.Hearts, Rank.Eight), new Card(Suit.Spades, Rank.Eight));
        PlayOpponentCards(battle.CurrentRound, new Card(Suit.Diamonds, Rank.Two));
        battle.CurrentRound.MarkOpponentStood();

        Assert.That(battle.TryStand(), Is.True);

        RoundResolution resolution = battle.CombatHistory[^1];
        Assert.That(resolution.PlayerMoneyLost, Is.EqualTo(400));
    }

    [Test]
    public void BlackjackWinner_ReceivesCommittedPotWithoutExtraPayout()
    {
        var run = new RunState(1, startingMoney: 500);
        var battle = new BattleState(run, new BattleConfig("test", 500, new StandingDevilStrategy(), baseWager: 10));
        battle.StartRound(battle.GetDefaultWager());
        battle.StartRound(battle.GetDefaultWager());
        PlayPlayerCards(battle.CurrentRound, new Card(Suit.Clubs, Rank.King), new Card(Suit.Hearts, Rank.Nine));
        PlayOpponentCards(battle.CurrentRound, new Card(Suit.Diamonds, Rank.Eight), new Card(Suit.Spades, Rank.Seven));
        battle.CurrentRound.MarkOpponentStood();

        Assert.That(battle.TryStand(), Is.True);

        Assert.That(battle.CombatHistory[^1].Winner, Is.EqualTo(Combatant.Player));
        Assert.That(battle.CombatHistory[^1].OpponentMoneyLost, Is.EqualTo(0));
        Assert.That(run.Money, Is.EqualTo(510));
        Assert.That(battle.OpponentMoney, Is.EqualTo(490));
    }

    [Test]
    public void PokerPayout_UsesCombinedPlayedOpponentAndSharedCards()
    {
        var run = new RunState(1, startingMoney: 500);
        var battle = new BattleState(run, new BattleConfig("test", 500, new StandingDevilStrategy(), baseWager: 10));
        battle.StartRound(battle.GetDefaultWager());
        battle.StartRound(battle.GetDefaultWager());
        PlayPlayerCards(battle.CurrentRound, new Card(Suit.Clubs, Rank.Two), new Card(Suit.Hearts, Rank.Three));
        PlayOpponentCards(battle.CurrentRound, new Card(Suit.Diamonds, Rank.Nine), new Card(Suit.Spades, Rank.Nine));
        battle.CurrentRound.AddSharedVisibleCard(new Card(Suit.Clubs, Rank.Four));
        battle.CurrentRound.AddSharedVisibleCard(new Card(Suit.Hearts, Rank.Five));
        battle.CurrentRound.AddSharedVisibleCard(new Card(Suit.Diamonds, Rank.Six));
        battle.CurrentRound.MarkOpponentStood();

        Assert.That(battle.TryStand(), Is.True);

        Assert.That(ScoreResolver.ResolvePoker(battle.CurrentRound.GetPokerCardsForPlayerPayout()).Rank, Is.EqualTo(PokerHandRank.Straight));
        Assert.That(battle.CombatHistory[^1].OpponentMoneyLost, Is.EqualTo(40));
        Assert.That(battle.CombatHistory[^1].PlayerMoneyLost, Is.EqualTo(0));
        Assert.That(run.Money, Is.EqualTo(530));
        Assert.That(battle.OpponentMoney, Is.EqualTo(470));
    }

    [Test]
    public void PokerResolvedEvent_IncludesScoringCardIndices()
    {
        var run = new RunState(1, startingMoney: 500);
        var battle = new BattleState(run, new BattleConfig("test", 500, new StandingDevilStrategy(), baseWager: 10));
        battle.StartRound(1);
        PlayPlayerCards(battle.CurrentRound,
            new Card(Suit.Clubs, Rank.Five),
            new Card(Suit.Hearts, Rank.Five),
            new Card(Suit.Spades, Rank.Nine));
        battle.CurrentRound.MarkOpponentStood();
        PokerResult result = default;
        battle.EventBus.Subscribe<PokerResolvedEvent>(eventData =>
        {
            if (eventData.Combatant == Combatant.Player)
                result = eventData.Poker;
        });

        Assert.That(battle.TryStand(), Is.True);

        Assert.That(result.Rank, Is.EqualTo(PokerHandRank.Pair));
        CollectionAssert.AreEqual(new[] { 0, 1 }, result.CardIndices);
    }

    [Test]
    public void PokerPayout_IsSkippedWhenPlayerRunsOutDuringBlackjackResolution()
    {
        var run = new RunState(1, startingMoney: 100);
        var battle = new BattleState(run, new BattleConfig("test", 500, new StandingDevilStrategy(), baseWager: 100));
        battle.StartRound(battle.GetDefaultWager());
        battle.StartRound(battle.GetDefaultWager());
        PlayPlayerCards(battle.CurrentRound, new Card(Suit.Clubs, Rank.King), new Card(Suit.Hearts, Rank.Queen), new Card(Suit.Spades, Rank.Two));
        PlayOpponentCards(battle.CurrentRound, new Card(Suit.Diamonds, Rank.Ace), new Card(Suit.Clubs, Rank.King));
        battle.CurrentRound.MarkOpponentStood();

        Assert.That(battle.TryStand(), Is.True);

        Assert.That(run.Money, Is.EqualTo(0));
        Assert.That(battle.OpponentMoney, Is.EqualTo(600));
        Assert.That(battle.CombatHistory[^1].OpponentMoneyLost, Is.EqualTo(0));
        Assert.That(battle.CombatHistory[^1].PlayerMoneyLost, Is.EqualTo(0));
    }

    [Test]
    public void DevilTurnChoiceEvent_PublishesStandChoiceAndPreservesStandBehavior()
    {
        var run = CreateRunWithDeck(TenTwos());
        var battle = new BattleState(run, new BattleConfig("test", 500, new FixedChoiceDevilStrategy(DevilTurnChoice.Stand), baseWager: 10));
        battle.StartRound(battle.GetDefaultWager());
        battle.StartRound(battle.GetDefaultWager());

        int eventCount = 0;
        DevilTurnChoice receivedChoice = default;
        battle.EventBus.Subscribe<DevilTurnChoiceEvent>(eventData =>
        {
            eventCount++;
            receivedChoice = eventData.Choice;
        });

        Assert.That(battle.TryStand(), Is.True);

        Assert.That(eventCount, Is.EqualTo(1));
        Assert.That(receivedChoice, Is.EqualTo(DevilTurnChoice.Stand));
        Assert.That(battle.CurrentRound.OpponentStood, Is.True);
    }

    [Test]
    public void PlayerTurnEvents_ContinuingRoundPublishesEndThenNextStart()
    {
        var run = CreateRunWithDeck(TenTwos());
        var battle = new BattleState(run, new BattleConfig("test", 500, new FixedChoiceDevilStrategy(DevilTurnChoice.Hit), baseWager: 10));
        var events = new List<string>();
        battle.EventBus.Subscribe<PlayerTurnStartedEvent>(_ => events.Add("started"));
        battle.EventBus.Subscribe<PlayerTurnEndedEvent>(_ => events.Add("ended"));

        battle.StartRound(battle.GetDefaultWager());
        Assert.That(battle.TryStand(), Is.True);

        CollectionAssert.AreEqual(new[] { "started", "ended", "started" }, events);
        Assert.That(battle.Phase, Is.EqualTo(BattlePhase.PlayerPhase));
    }

    [Test]
    public void PlayerTurnEvents_RoundEndingActionDoesNotPublishAnotherStart()
    {
        var run = CreateRunWithDeck(TenTwos());
        var battle = new BattleState(run, new BattleConfig("test", 500, new StandingDevilStrategy(), baseWager: 10));
        int startedEvents = 0;
        int endedEvents = 0;
        battle.EventBus.Subscribe<PlayerTurnStartedEvent>(_ => startedEvents++);
        battle.EventBus.Subscribe<PlayerTurnEndedEvent>(_ => endedEvents++);

        battle.StartRound(battle.GetDefaultWager());
        Assert.That(battle.TryStand(), Is.True);

        Assert.That(startedEvents, Is.EqualTo(1));
        Assert.That(endedEvents, Is.EqualTo(1));
        Assert.That(battle.Phase, Is.EqualTo(BattlePhase.Cleanup));
    }

    [TestCase(DevilTurnChoice.Hit)]
    [TestCase(DevilTurnChoice.Play)]
    public void DevilTurnChoiceEvent_PayloadMatchesStrategyChoice(DevilTurnChoice choice)
    {
        var run = CreateRunWithDeck(TenTwos());
        var battle = new BattleState(run, new BattleConfig("test", 500, new FixedChoiceDevilStrategy(choice), baseWager: 10));
        battle.StartRound(battle.GetDefaultWager());
        battle.StartRound(battle.GetDefaultWager());

        int eventCount = 0;
        DevilTurnChoice receivedChoice = default;
        battle.EventBus.Subscribe<DevilTurnChoiceEvent>(eventData =>
        {
            eventCount++;
            receivedChoice = eventData.Choice;
        });

        Assert.That(battle.TryStand(), Is.True);

        Assert.That(eventCount, Is.EqualTo(1));
        Assert.That(receivedChoice, Is.EqualTo(choice));
    }

    [Test]
    public void DevilState_RoundStartedUpdatesBeforeEventAndOpeningDraws()
    {
        var order = new List<string>();
        var strategy = new RecordingDevilStrategy(order);
        var battle = new BattleState(
            CreateRunWithDeck(TenTwos()),
            new BattleConfig("test", 500, strategy, baseWager: 10));
        battle.EventBus.Subscribe<RoundStartedEvent>(_ => order.Add("event:round-started"));
        battle.EventBus.Subscribe<CardDrawnEvent>(eventData =>
        {
            if (eventData.Owner == Combatant.Player)
                order.Add("event:player-drawn");
        });

        Assert.That(battle.StartRound(battle.GetDefaultWager()), Is.True);

        Assert.That(strategy.RoundStartedRound, Is.SameAs(battle.CurrentRound));
        Assert.That(strategy.RoundStartedRoundNumber, Is.EqualTo(1));
        Assert.That(strategy.RoundStartedOpponentMoney, Is.EqualTo(500));
        Assert.That(strategy.RoundStartedHistoryCount, Is.EqualTo(0));
        Assert.That(strategy.RoundStartedOpponentHandCount, Is.EqualTo(0));
        Assert.That(order[0], Is.EqualTo("update:RoundStarted"));
        Assert.That(order[1], Is.EqualTo("event:round-started"));
        Assert.That(order[2], Is.EqualTo("event:player-drawn"));
    }

    [Test]
    public void DevilState_OpponentTurnUpdatesBeforeChoiceAndChoiceEvent()
    {
        var order = new List<string>();
        var strategy = new RecordingDevilStrategy(order);
        var battle = new BattleState(
            CreateRunWithDeck(TenTwos()),
            new BattleConfig("test", 500, strategy, baseWager: 10));
        DevilTurnChoice receivedChoice = DevilTurnChoice.Hit;
        battle.EventBus.Subscribe<DevilTurnChoiceEvent>(eventData =>
        {
            order.Add("event:devil-choice");
            receivedChoice = eventData.Choice;
        });

        Assert.That(battle.StartRound(battle.GetDefaultWager()), Is.True);
        order.Clear();
        Assert.That(battle.TryStand(), Is.True);

        Assert.That(receivedChoice, Is.EqualTo(DevilTurnChoice.Stand));
        Assert.That(strategy.OpponentTurnUpdateCount, Is.EqualTo(1));
        Assert.That(strategy.OpponentTurnOpponentHandCount, Is.GreaterThan(0));
        CollectionAssert.AreEqual(
            new[]
            {
                "update:OpponentTurnStarted",
                "choose",
                "event:devil-choice",
                "update:RoundResolved"
            },
            order);
    }

    [Test]
    public void DevilState_RoundResolvedSeesFinalStateBeforeRoundEndedEvent()
    {
        var strategy = new RecordingDevilStrategy();
        var battle = new BattleState(
            CreateRunWithDeck(TenTwos()),
            new BattleConfig("test", 100, strategy, baseWager: 10));
        int updateCountSeenByEvent = 0;
        int historyCountSeenByEvent = 0;
        int playerMoneySeenByEvent = 0;
        int opponentMoneySeenByEvent = 0;
        BattlePhase phaseSeenByEvent = BattlePhase.Inactive;
        battle.EventBus.Subscribe<RoundEndedEvent>(_ =>
        {
            updateCountSeenByEvent = strategy.RoundResolvedUpdateCount;
            historyCountSeenByEvent = battle.CombatHistory.Count;
            playerMoneySeenByEvent = battle.PlayerMoney;
            opponentMoneySeenByEvent = battle.OpponentMoney;
            phaseSeenByEvent = battle.Phase;
        });

        Assert.That(battle.StartRound(battle.GetDefaultWager()), Is.True);
        PlayPlayerCards(battle.CurrentRound, new Card(Suit.Clubs, Rank.King));
        Assert.That(battle.TryStand(), Is.True);

        Assert.That(strategy.RoundResolvedUpdateCount, Is.EqualTo(1));
        Assert.That(strategy.RoundResolvedHistoryCount, Is.EqualTo(1));
        Assert.That(strategy.RoundResolvedResolution, Is.EqualTo(battle.CombatHistory[^1]));
        Assert.That(strategy.RoundResolvedPlayerMoney, Is.EqualTo(110));
        Assert.That(strategy.RoundResolvedOpponentMoney, Is.EqualTo(90));
        Assert.That(updateCountSeenByEvent, Is.EqualTo(1));
        Assert.That(historyCountSeenByEvent, Is.EqualTo(1));
        Assert.That(playerMoneySeenByEvent, Is.EqualTo(110));
        Assert.That(opponentMoneySeenByEvent, Is.EqualTo(90));
        Assert.That(phaseSeenByEvent, Is.EqualTo(BattlePhase.PostRound));
    }

    [Test]
    public void DevilState_InvalidStartAndDirectRoundEndDoNotAddExtraUpdates()
    {
        var strategy = new RecordingDevilStrategy();
        var battle = new BattleState(
            CreateRunWithDeck(TenTwos()),
            new BattleConfig("test", 100, strategy, baseWager: 10));

        Assert.That(battle.StartRound(0), Is.False);
        Assert.That(strategy.UpdatePoints, Is.Empty);

        Assert.That(battle.StartRound(battle.GetDefaultWager()), Is.True);
        battle.CurrentRound.MarkOpponentStood();
        Assert.That(battle.TryStand(), Is.True);

        CollectionAssert.AreEqual(
            new[] { DevilStateUpdatePoint.RoundStarted, DevilStateUpdatePoint.RoundResolved },
            strategy.UpdatePoints);
        Assert.That(strategy.OpponentTurnUpdateCount, Is.EqualTo(0));
        Assert.That(strategy.RoundResolvedUpdateCount, Is.EqualTo(1));
    }

    [Test]
    public void Devil1Strategy_ResolvedRoundsAdvanceWinStreakOncePerRound()
    {
        var strategy = new Devil1Strategy(startMoney: 1000);
        var battle = new BattleState(
            CreateRunWithDeck(TenTwos()),
            new BattleConfig("devil1", 1000, strategy, baseWager: 10));

        for (int roundNumber = 1; roundNumber <= 2; roundNumber++)
        {
            Assert.That(battle.StartRound(battle.GetDefaultWager()), Is.True);
            PlayOpponentCards(battle.CurrentRound, new Card(Suit.Hearts, Rank.King));
            battle.CurrentRound.MarkOpponentStood();
            Assert.That(battle.TryStand(), Is.True);

            if (roundNumber < 2)
                battle.CleanupRound();
        }

        Assert.That(strategy.GetDialogueId(), Is.EqualTo("devil1_WinStreak2_Dialogue"));
        Assert.That(strategy.GetDialogueId(), Is.Null);
    }

    [TestCase(3, 0, 40)]
    [TestCase(2, 1, 120)]
    [TestCase(0, 2, 160)]
    [TestCase(0, 3, 640)]
    public void Devil1Strategy_OpponentWinBonusUsesHeartAndSpadeMultipliers(
        int heartCount,
        int spadeCount,
        int expectedBonus)
    {
        RunState run = CreateRunWithDeck(TenTwos());
        run.AddMoney(1000);
        var strategy = new Devil1Strategy(startMoney: 2000);
        var battle = new BattleState(
            run,
            new BattleConfig("devil1", 2000, strategy, baseWager: 10));
        Assert.That(battle.StartRound(battle.GetDefaultWager()), Is.True);
        PlayOpponentSuitCards(battle.CurrentRound, Suit.Hearts, heartCount);
        PlayOpponentSuitCards(battle.CurrentRound, Suit.Spades, spadeCount);
        battle.CurrentRound.SetScores(CreateScoreResult(5), CreateScoreResult(10));

        int abilityDelta = 0;
        EventBus.Subscribe<MoneyTransferReasonEvent>(eventData =>
        {
            if (eventData.Reason == MoneyTransferReason.DevilAbilityPayout)
                abilityDelta += eventData.Delta;
        });
        int playerMoneyBeforeResolution = battle.PlayerMoney;

        RoundResolution resolution = MoneyResolver.ResolveRound(battle, battle.CurrentRound, false);

        Assert.That(resolution.Winner, Is.EqualTo(Combatant.Opponent));
        Assert.That(resolution.PlayerMoneyLost, Is.EqualTo(expectedBonus));
        Assert.That(playerMoneyBeforeResolution - battle.PlayerMoney, Is.EqualTo(expectedBonus));
        Assert.That(abilityDelta, Is.EqualTo(-expectedBonus));
        battle.Dispose();
    }

    [TestCase(3, 1)]
    [TestCase(4, 2)]
    [TestCase(5, 3)]
    [TestCase(7, 3)]
    public void Devil1Strategy_LossStreakForcesCappedTemporarySpades(int lossStreak, int expectedSpades)
    {
        var strategy = new Devil1Strategy(startMoney: 1000);
        var battle = new BattleState(
            CreateRunWithDeck(TenTwos()),
            new BattleConfig("devil1", 1000, strategy, baseWager: 10));
        Assert.That(battle.StartRound(battle.GetDefaultWager()), Is.True);
        PlayOpponentSuitCards(battle.CurrentRound, Suit.Hearts, 4);
        SetField(strategy, "lossStreak", lossStreak);

        ((IDevilOpponentFieldModifier)strategy).RefreshOpponentField(battle, battle.CurrentRound);

        Assert.That(CountSuit(battle.CurrentRound.OpponentVisibleCards, Suit.Spades), Is.EqualTo(expectedSpades));
        Assert.That(CountSuit(battle.CurrentRound.OpponentVisibleCards, Suit.Hearts), Is.EqualTo(4 - expectedSpades));
        battle.Dispose();
    }

    [Test]
    public void Devil1Strategy_LowMoneyForcesOneSpadeAndRestoresCanonicalHeart()
    {
        var strategy = new Devil1Strategy(startMoney: 1000);
        var battle = new BattleState(
            CreateRunWithDeck(TenTwos()),
            new BattleConfig("devil1", 190, strategy, baseWager: 10));
        Assert.That(battle.StartRound(battle.GetDefaultWager()), Is.True);
        PlayOpponentSuitCards(battle.CurrentRound, Suit.Hearts, 1);
        ((IDevilOpponentFieldModifier)strategy).RefreshOpponentField(battle, battle.CurrentRound);
        Assert.That(battle.CurrentRound.OpponentVisibleCards[0].Suit, Is.EqualTo(Suit.Spades));

        battle.AddOpponentMoney(30);
        Assert.That(battle.CurrentRound.OpponentVisibleCards[0].Suit, Is.EqualTo(Suit.Hearts));

        battle.LoseOpponentMoney(30);
        Assert.That(battle.CurrentRound.OpponentVisibleCards[0].Suit, Is.EqualTo(Suit.Spades));
        battle.CleanupRound();

        Assert.That(battle.OpponentDiscardPile.Count, Is.EqualTo(1));
        Assert.That(battle.OpponentDiscardPile[0].Suit, Is.EqualTo(Suit.Hearts));
        battle.Dispose();
    }

    [Test]
    public void Devil1Strategy_TransformsOnlyDevilOwnedCardsButCountsTheWholeOpponentPile()
    {
        var strategy = new Devil1Strategy(startMoney: 1000);
        var battle = new BattleState(
            CreateRunWithDeck(TenTwos()),
            new BattleConfig("devil1", 1000, strategy, baseWager: 10));
        Assert.That(battle.StartRound(battle.GetDefaultWager()), Is.True);
        SetField(strategy, "lossStreak", 3);
        PlayOpponentSuitCards(battle.CurrentRound, Suit.Hearts, 1);

        Assert.That(battle.CurrentRound.TryPlayHitCard(new Card(Suit.Hearts, Rank.Two)), Is.True);
        Assert.That(battle.CurrentRound.TryPlayHitCard(new Card(Suit.Clubs, Rank.King)), Is.True);
        Assert.That(battle.TryMovePreviousPlayerPlayedCardToOpponent(out _), Is.True);
        Assert.That(battle.CurrentRound.TryPlayHitCard(new Card(Suit.Spades, Rank.Three)), Is.True);
        Assert.That(battle.CurrentRound.TryPlayHitCard(new Card(Suit.Diamonds, Rank.Queen)), Is.True);
        Assert.That(battle.TryMovePreviousPlayerPlayedCardToOpponent(out _), Is.True);

        ((IDevilOpponentFieldModifier)strategy).RefreshOpponentField(battle, battle.CurrentRound);

        Assert.That(battle.CurrentRound.OpponentVisibleCards[0].Suit, Is.EqualTo(Suit.Spades));
        Assert.That(battle.CurrentRound.OpponentVisibleCards[1].Suit, Is.EqualTo(Suit.Hearts));
        Assert.That(battle.CurrentRound.OpponentVisibleCards[2].Suit, Is.EqualTo(Suit.Spades));
        Assert.That(strategy.GetOpponentWinBonus(battle, battle.CurrentRound), Is.EqualTo(320));
        battle.Dispose();
    }

    [TestCase(DevilTurnChoice.Hit)]
    [TestCase(DevilTurnChoice.Play)]
    public void OpponentFieldModifier_AppliesToHitAndHandPlayEvents(DevilTurnChoice choice)
    {
        var strategy = new TransformingFixedChoiceDevilStrategy(choice);
        var battle = new BattleState(
            CreateRunWithDeck(TenTwos()),
            new BattleConfig("test", 500, strategy, startingHandSize: 1, baseWager: 10));
        Assert.That(battle.StartRound(battle.GetDefaultWager()), Is.True);

        int playedEvents = 0;
        Card playedCard = default;
        Card drawnCard = default;
        battle.EventBus.Subscribe<CardPlayedEvent>(eventData =>
        {
            if (eventData.Owner != Combatant.Opponent)
                return;

            playedEvents++;
            playedCard = eventData.Card;
        });
        battle.EventBus.Subscribe<CardDrawnEvent>(eventData =>
        {
            if (eventData.Owner == Combatant.Opponent)
                drawnCard = eventData.Card;
        });

        Assert.That(battle.TryStand(), Is.True);

        Assert.That(playedEvents, Is.EqualTo(1));
        Assert.That(playedCard.Suit, Is.EqualTo(Suit.Spades));
        Assert.That(battle.CurrentRound.OpponentVisibleCards[0].Suit, Is.EqualTo(Suit.Spades));
        if (choice == DevilTurnChoice.Hit)
            Assert.That(drawnCard.Suit, Is.EqualTo(Suit.Hearts));
        battle.Dispose();
    }

    [TestCase(10, 5)]
    [TestCase(10, 10)]
    public void Devil1Strategy_DoesNotApplyBonusWithoutOpponentWin(int playerScore, int opponentScore)
    {
        var strategy = new Devil1Strategy(startMoney: 1000);
        var battle = new BattleState(
            CreateRunWithDeck(TenTwos()),
            new BattleConfig("devil1", 1000, strategy, baseWager: 10));
        Assert.That(battle.StartRound(battle.GetDefaultWager()), Is.True);
        PlayOpponentSuitCards(battle.CurrentRound, Suit.Hearts, 3);
        battle.CurrentRound.SetScores(CreateScoreResult(playerScore), CreateScoreResult(opponentScore));
        int abilityEvents = 0;
        EventBus.Subscribe<MoneyTransferReasonEvent>(eventData =>
        {
            if (eventData.Reason == MoneyTransferReason.DevilAbilityPayout)
                abilityEvents++;
        });

        RoundResolution resolution = MoneyResolver.ResolveRound(battle, battle.CurrentRound, false);

        Assert.That(resolution.PlayerMoneyLost, Is.EqualTo(0));
        Assert.That(abilityEvents, Is.EqualTo(0));
        battle.Dispose();
    }

    [Test]
    public void Devil1Strategy_BonusUsesBaseWagerAfterWagerDoubles()
    {
        var strategy = new Devil1Strategy(startMoney: 1000);
        var battle = new BattleState(
            CreateRunWithDeck(TenTwos()),
            new BattleConfig("devil1", 1000, strategy, baseWager: 10));
        Assert.That(battle.StartRound(battle.GetDefaultWager()), Is.True);
        Assert.That(battle.DoubleCurrentRoundWager(), Is.True);
        PlayOpponentSuitCards(battle.CurrentRound, Suit.Hearts, 3);
        battle.CurrentRound.SetScores(CreateScoreResult(5), CreateScoreResult(10));

        RoundResolution resolution = MoneyResolver.ResolveRound(battle, battle.CurrentRound, false);

        Assert.That(battle.CurrentRound.BaseWager, Is.EqualTo(10));
        Assert.That(battle.CurrentRound.EffectiveWager, Is.EqualTo(20));
        Assert.That(resolution.PlayerMoneyLost, Is.EqualTo(40));
        battle.Dispose();
    }

    [Test]
    public void Devil1Strategy_BonusIsCappedByRemainingPlayerMoney()
    {
        RunState run = CreateRunWithDeck(TenTwos());
        run.AddMoney(-75);
        var strategy = new Devil1Strategy(startMoney: 1000);
        var battle = new BattleState(
            run,
            new BattleConfig("devil1", 1000, strategy, baseWager: 10));
        Assert.That(battle.StartRound(battle.GetDefaultWager()), Is.True);
        PlayOpponentSuitCards(battle.CurrentRound, Suit.Hearts, 3);
        battle.CurrentRound.SetScores(CreateScoreResult(5), CreateScoreResult(10));

        RoundResolution resolution = MoneyResolver.ResolveRound(battle, battle.CurrentRound, false);

        Assert.That(resolution.PlayerMoneyLost, Is.EqualTo(15));
        Assert.That(battle.PlayerMoney, Is.EqualTo(0));
        battle.Dispose();
    }

    [Test]
    public void BattleController_StartNextRoundHonorsLifecycleGuards()
    {
        var controllerObject = new UnityEngine.GameObject("Controller");
        try
        {
            BattleController controller = controllerObject.AddComponent<BattleController>();
            Assert.That(controller.StartNextRound(10), Is.False);

            controller.InitializeBattle(CreateRunWithDeck(TenTwos()), new BattleConfig("test", 500, new StandingDevilStrategy(), baseWager: 10));
            Assert.That(controller.StartNextRound(controller.BattleState.GetDefaultWager()), Is.True);
            Assert.That(controller.StartNextRound(controller.BattleState.GetDefaultWager()), Is.False);

            Assert.That(controller.TryStand(), Is.True);
            Assert.That(controller.IsWaitingForVisuals, Is.True);
            Assert.That(controller.StartNextRound(controller.BattleState.GetDefaultWager()), Is.False);

            controller.CompletePendingVisualTransition();
            Assert.That(controller.IsWaitingForVisuals, Is.False);
            Assert.That(controller.BattleState.Phase, Is.EqualTo(BattlePhase.PreRound));
            Assert.That(controller.StartNextRound(controller.BattleState.GetDefaultWager()), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(controllerObject);
        }
    }

    [Test]
    public void DevilStandIndicator_SetsImageRedWhenBattleEventPublishesStandChoice()
    {
        var controllerObject = new UnityEngine.GameObject("Controller");
        var indicatorObject = new UnityEngine.GameObject("Indicator");
        try
        {
            BattleController controller = controllerObject.AddComponent<BattleController>();
            controller.InitializeBattle(CreateRunWithDeck(TenTwos()), new BattleConfig("test", 500, new StandingDevilStrategy()));

            UnityEngine.UI.Image image = indicatorObject.AddComponent<UnityEngine.UI.Image>();
            image.color = UnityEngine.Color.gray;
            DevilStandIndicator indicator = indicatorObject.AddComponent<DevilStandIndicator>();
            indicator.Configure(controller);

            controller.BattleState.EventBus.Publish(new DevilTurnChoiceEvent(DevilTurnChoice.Stand));

            Assert.That(image.color, Is.EqualTo(UnityEngine.Color.red));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(indicatorObject);
            UnityEngine.Object.DestroyImmediate(controllerObject);
        }
    }

    [Test]
    public void BattleUiPresenter_DragMediatorsRejectWhenPlayerCannotAct()
    {
        var target = new UnityEngine.GameObject("Presenter");
        try
        {
            BattleUiPresenter presenter = target.AddComponent<BattleUiPresenter>();

            Assert.That(presenter.CanPlayerAct, Is.False);
            Assert.That(presenter.TryPlayDraggedHandCard(0), Is.False);
            Assert.That(presenter.TryHitFromDraggedDeck(), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void BattleUiPresenter_OpponentTurnDelayDefaultsAndReversedBoundsAreNormalized()
    {
        var target = new UnityEngine.GameObject("Presenter");
        try
        {
            BattleUiPresenter presenter = target.AddComponent<BattleUiPresenter>();

            Assert.That(GetField<float>(presenter, "opponentTurnDelayMinSeconds"), Is.EqualTo(0.75f));
            Assert.That(GetField<float>(presenter, "opponentTurnDelayMaxSeconds"), Is.EqualTo(1.5f));

            SetField(presenter, "opponentTurnDelayMinSeconds", 2f);
            SetField(presenter, "opponentTurnDelayMaxSeconds", 0.5f);
            Invoke(presenter, "OnValidate");

            Assert.That(GetField<float>(presenter, "opponentTurnDelayMinSeconds"), Is.EqualTo(0.5f));
            Assert.That(GetField<float>(presenter, "opponentTurnDelayMaxSeconds"), Is.EqualTo(2f));

            SetField(presenter, "opponentTurnDelayMinSeconds", -2f);
            SetField(presenter, "opponentTurnDelayMaxSeconds", -1f);
            Invoke(presenter, "OnValidate");

            Assert.That(GetField<float>(presenter, "opponentTurnDelayMinSeconds"), Is.Zero);
            Assert.That(GetField<float>(presenter, "opponentTurnDelayMaxSeconds"), Is.Zero);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void BattleUiPresenter_PendingHandoffLocksInputAndCancelsWhenDisabled()
    {
        var controllerObject = new UnityEngine.GameObject("Controller");
        var presenterObject = new UnityEngine.GameObject("Presenter");
        try
        {
            var strategy = new SequenceDevilStrategy(DevilTurnChoice.Stand);
            BattleController controller = controllerObject.AddComponent<BattleController>();
            controller.InitializeBattle(
                CreateRunWithDeck(TenTwos()),
                new BattleConfig("test", 500, strategy, baseWager: 10));
            Assert.That(controller.StartNextRound(controller.BattleState.GetDefaultWager()), Is.True);

            BattleUiPresenter presenter = presenterObject.AddComponent<BattleUiPresenter>();
            ConfigureTurnHandoffTestPresenter(presenter, controller);

            Assert.That(presenter.CanPlayerAct, Is.True);
            Assert.That(presenter.TryHitFromDraggedDeck(), Is.True);
            Assert.That(presenter.CanPlayerAct, Is.False);
            Assert.That(presenter.TryHitFromDraggedDeck(), Is.False);
            Assert.That(strategy.TurnCount, Is.Zero);
            Assert.That(controller.BattleState.Phase, Is.EqualTo(BattlePhase.PlayerPhase));

            Invoke(presenter, "OnDisable");
            presenterObject.SetActive(false);

            Assert.That(GetField<bool>(presenter, "_turnHandoffPending"), Is.False);
            Assert.That(strategy.TurnCount, Is.Zero);
            Assert.That(controller.BattleState.Phase, Is.EqualTo(BattlePhase.PlayerPhase));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(presenterObject);
            UnityEngine.Object.DestroyImmediate(controllerObject);
        }
    }

    [TestCase("Hit")]
    [TestCase("EndPlayerPhase")]
    public void BattleUiPresenter_NonStandHandoffAdvancesOpponentExactlyOnce(string actionName)
    {
        var controllerObject = new UnityEngine.GameObject("Controller");
        var presenterObject = new UnityEngine.GameObject("Presenter");
        try
        {
            var strategy = new SequenceDevilStrategy(DevilTurnChoice.Stand);
            BattleController controller = controllerObject.AddComponent<BattleController>();
            controller.InitializeBattle(
                CreateRunWithDeck(TenTwos()),
                new BattleConfig("test", 500, strategy, baseWager: 10));
            Assert.That(controller.StartNextRound(controller.BattleState.GetDefaultWager()), Is.True);

            BattleUiPresenter presenter = presenterObject.AddComponent<BattleUiPresenter>();
            ConfigureTurnHandoffTestPresenter(presenter, controller);

            BattleState battle = controller.BattleState;
            RoundState round = battle.CurrentRound;
            if (actionName == "EndPlayerPhase")
                Assert.That(controller.TryPlayCard(0), Is.True);

            PrepareTurnHandoff(presenter, battle, round);
            IEnumerator routine = CreateTurnHandoffRoutine(presenter, battle, round, actionName);
            int delayCount = RunRoutineToCompletion(routine);

            Assert.That(delayCount, Is.EqualTo(1));
            Assert.That(strategy.TurnCount, Is.EqualTo(1));
            Assert.That(GetField<bool>(presenter, "_turnHandoffPending"), Is.False);
            Assert.That(battle.Phase, Is.EqualTo(BattlePhase.PlayerPhase));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(presenterObject);
            UnityEngine.Object.DestroyImmediate(controllerObject);
        }
    }

    [Test]
    public void BattleUiPresenter_StandAutomaticallyAdvancesUntilOpponentStands()
    {
        var controllerObject = new UnityEngine.GameObject("Controller");
        var presenterObject = new UnityEngine.GameObject("Presenter");
        try
        {
            var strategy = new SequenceDevilStrategy(
                DevilTurnChoice.Hit,
                DevilTurnChoice.Play,
                DevilTurnChoice.Stand);
            BattleController controller = controllerObject.AddComponent<BattleController>();
            controller.InitializeBattle(
                CreateRunWithDeck(TenTwos()),
                new BattleConfig("test", 500, strategy, baseWager: 10));
            Assert.That(controller.StartNextRound(controller.BattleState.GetDefaultWager()), Is.True);

            BattleUiPresenter presenter = presenterObject.AddComponent<BattleUiPresenter>();
            ConfigureTurnHandoffTestPresenter(presenter, controller);

            BattleState battle = controller.BattleState;
            RoundState round = battle.CurrentRound;
            PrepareTurnHandoff(presenter, battle, round);
            IEnumerator routine = CreateTurnHandoffRoutine(presenter, battle, round, "Stand");
            int delayCount = RunRoutineToCompletion(routine);

            Assert.That(delayCount, Is.EqualTo(3));
            Assert.That(strategy.TurnCount, Is.EqualTo(3));
            Assert.That(round.PlayerStood, Is.True);
            Assert.That(round.OpponentStood, Is.True);
            Assert.That(battle.Phase, Is.EqualTo(BattlePhase.Cleanup));
            Assert.That(controller.IsWaitingForVisuals, Is.True);
            Assert.That(GetField<bool>(presenter, "_turnHandoffPending"), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(presenterObject);
            UnityEngine.Object.DestroyImmediate(controllerObject);
        }
    }

    [Test]
    public void BattleUiPresenter_StaleHandoffDoesNotActOnReplacementBattle()
    {
        var controllerObject = new UnityEngine.GameObject("Controller");
        var presenterObject = new UnityEngine.GameObject("Presenter");
        try
        {
            var originalStrategy = new SequenceDevilStrategy(DevilTurnChoice.Stand);
            BattleController controller = controllerObject.AddComponent<BattleController>();
            controller.InitializeBattle(
                CreateRunWithDeck(TenTwos()),
                new BattleConfig("original", 500, originalStrategy, baseWager: 10));
            Assert.That(controller.StartNextRound(controller.BattleState.GetDefaultWager()), Is.True);

            BattleUiPresenter presenter = presenterObject.AddComponent<BattleUiPresenter>();
            ConfigureTurnHandoffTestPresenter(presenter, controller);

            BattleState originalBattle = controller.BattleState;
            RoundState originalRound = originalBattle.CurrentRound;
            PrepareTurnHandoff(presenter, originalBattle, originalRound);
            IEnumerator routine = CreateTurnHandoffRoutine(presenter, originalBattle, originalRound, "Stand");
            Assert.That(routine.MoveNext(), Is.True);

            var replacementStrategy = new SequenceDevilStrategy(DevilTurnChoice.Stand);
            controller.InitializeBattle(
                CreateRunWithDeck(TenTwos()),
                new BattleConfig("replacement", 500, replacementStrategy, baseWager: 10));
            Assert.That(controller.StartNextRound(controller.BattleState.GetDefaultWager()), Is.True);

            Assert.That(routine.MoveNext(), Is.False);

            Assert.That(originalStrategy.TurnCount, Is.Zero);
            Assert.That(replacementStrategy.TurnCount, Is.Zero);
            Assert.That(controller.BattleState.Phase, Is.EqualTo(BattlePhase.PlayerPhase));
            Assert.That(GetField<bool>(presenter, "_turnHandoffPending"), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(presenterObject);
            UnityEngine.Object.DestroyImmediate(controllerObject);
        }
    }

    private static bool ContainsUpgradedRank(IReadOnlyList<Card> cards, Rank rank, string modifierId)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            Card card = cards[i];
            if (card.Rank == rank && card.ModifierId == modifierId && card.Suit == SuitForModifier(modifierId))
                return true;
        }

        return false;
    }

    private static bool ContainsModifier(IReadOnlyList<Card> cards, Rank rank, string modifierId)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            Card card = cards[i];
            if (card.Rank == rank && card.ModifierId == modifierId)
                return true;
        }

        return false;
    }

    private static bool ContainsRank(IReadOnlyList<Card> cards, Rank rank)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i].Rank == rank)
                return true;
        }

        return false;
    }

    private static bool ContainsSuit(IReadOnlyList<Card> cards, Suit suit)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i].Suit == suit)
                return true;
        }

        return false;
    }

    private static int CountSuit(IReadOnlyList<Card> cards, Suit suit)
    {
        int count = 0;
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i].Suit == suit)
                count++;
        }

        return count;
    }

    private static int IndexOfRank(IReadOnlyList<Card> cards, Rank rank)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i].Rank == rank)
                return i;
        }

        return -1;
    }

    private static int IndexOfModifier(IReadOnlyList<Card> cards, string modifierId)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i].ModifierId == modifierId)
                return i;
        }

        return -1;
    }

    private static int CountRank(IReadOnlyList<Card> cards, Rank rank)
    {
        int count = 0;
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i].Rank == rank)
                count++;
        }

        return count;
    }

    private static Suit SuitForModifier(string modifierId)
    {
        return modifierId switch
        {
            CardModifierResolver.RankToHearts => Suit.Hearts,
            CardModifierResolver.RankToDiamonds => Suit.Diamonds,
            CardModifierResolver.RankToClubs => Suit.Clubs,
            CardModifierResolver.RankToSpades => Suit.Spades,
            _ => default
        };
    }

    private static CardUpgradeDefinition CreateUpgrade(CardUpgradeAssignmentType assignmentType, string id = "upgrade")
    {
        CardUpgradeDefinition definition = UnityEngine.ScriptableObject.CreateInstance<CardUpgradeDefinition>();
        definition.Initialize(id, id, id, 10, assignmentType);
        return definition;
    }

    private static IEnumerable<Rank> AllRanks()
    {
        yield return Rank.Ace;
        yield return Rank.Two;
        yield return Rank.Three;
        yield return Rank.Four;
        yield return Rank.Five;
        yield return Rank.Six;
        yield return Rank.Seven;
        yield return Rank.Eight;
        yield return Rank.Nine;
        yield return Rank.Ten;
        yield return Rank.Jack;
        yield return Rank.Queen;
        yield return Rank.King;
    }

    private static bool ContainsOffer(IReadOnlyList<CardUpgradeOffer> offers, CardUpgradeDefinition definition, Rank rank)
    {
        for (int i = 0; i < offers.Count; i++)
        {
            CardUpgradeOffer offer = offers[i];
            if (offer.Definition == definition && offer.Rank == rank)
                return true;
        }

        return false;
    }

    private static BattleConfig TestBattleConfig(int startingHandSize = 3)
    {
        return new BattleConfig("test", 100, new StandingDevilStrategy(), startingHandSize: startingHandSize);
    }

    private static RunState CreateRunWithRelicDeck(string relicId, params RunCardData[] cards)
    {
        RunState run = CreateRunWithDeck(cards);
        run.AddRelic(relicId);
        return run;
    }

    private static RunCardData[] TenTwos()
    {
        return RepeatCardData(Suit.Clubs, Rank.Two, 12);
    }

    private static RunCardData[] RepeatCardData(Suit suit, Rank rank, int count)
    {
        var cards = new RunCardData[count];
        for (int i = 0; i < count; i++)
            cards[i] = CardData(suit, rank);

        return cards;
    }

    private static int IndexOfRankAndSuit(IReadOnlyList<Card> cards, Rank rank, Suit suit)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i].Rank == rank && cards[i].Suit == suit)
                return i;
        }

        return -1;
    }

    private static RunState CreateRunWithDeck(params RunCardData[] cards)
    {
        var data = new RunStateData
        {
            seed = 1,
            money = 100,
            maxActiveItemSlots = RunState.DefaultMaxActiveItemSlots
        };

        data.deck.AddRange(cards);
        return RunState.FromData(data);
    }

    private static RunCardData CardData(Suit suit, Rank rank, string modifierId = null)
    {
        return new RunCardData
        {
            instanceId = RunCard.CreateInstanceId(suit, rank),
            suit = suit,
            rank = rank,
            modifierId = modifierId
        };
    }

    private static void PlayPlayerCards(RoundState round, params Card[] cards)
    {
        for (int i = 0; i < cards.Length; i++)
            Assert.That(round.TryPlayHitCard(cards[i]), Is.True);
    }

    private static void PlayOpponentCards(RoundState round, params Card[] cards)
    {
        for (int i = 0; i < cards.Length; i++)
        {
            round.AddToOpponentHand(cards[i]);
            Assert.That(round.TryPlayOpponentCard(round.OpponentHand.Count - 1, out _), Is.True);
        }
    }

    private static void PlayOpponentSuitCards(RoundState round, Suit suit, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Rank rank = (Rank)((i % 13) + 1);
            PlayOpponentCards(round, new Card(suit, rank));
        }
    }

    private static ScoreResult CreateScoreResult(int score)
    {
        return new ScoreResult(score, score, PokerHandRank.HighCard, 0, false, false);
    }

    private static void CreateShopPanel(Transform parent)
    {
        GameObject panel = new("ShopPanel");
        panel.AddComponent<RectTransform>();
        panel.transform.SetParent(parent);
    }

    private static Button CreateItemSlot(Transform parent, int index)
    {
        GameObject slot = new($"item{index}Slot");
        slot.transform.SetParent(parent);
        slot.AddComponent<CanvasGroup>();

        GameObject sprite = new($"Item{index}Sprite");
        sprite.transform.SetParent(slot.transform);
        sprite.AddComponent<Image>();
        Button button = sprite.AddComponent<Button>();

        GameObject label = new("Label");
        label.transform.SetParent(sprite.transform);
        label.AddComponent<TextMeshProUGUI>();

        GameObject price = new($"item{index}PriceText");
        price.transform.SetParent(slot.transform);
        price.AddComponent<TextMeshProUGUI>();

        return button;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }

    private static T GetField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        return (T)field.GetValue(target);
    }

    private static void PrepareTurnHandoff(BattleUiPresenter presenter, BattleState battle, RoundState round)
    {
        SetField(presenter, "_turnHandoffPending", true);
        SetField(presenter, "_scheduledTurnBattle", battle);
        SetField(presenter, "_scheduledTurnRound", round);
    }

    private static void ConfigureTurnHandoffTestPresenter(BattleUiPresenter presenter, BattleController controller)
    {
        SetField(presenter, "battleController", controller);
        SetField(presenter, "opponentTurnDelayMinSeconds", 0f);
        SetField(presenter, "opponentTurnDelayMaxSeconds", 0f);

        var labelObject = new GameObject(
            "BackToMapButtonText",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(presenter.transform);
        SetField(presenter, "backToMapButtonText", labelObject.GetComponent<TextMeshProUGUI>());
    }

    private static IEnumerator CreateTurnHandoffRoutine(
        BattleUiPresenter presenter,
        BattleState battle,
        RoundState round,
        string actionName)
    {
        System.Type actionType = typeof(BattleUiPresenter).GetNestedType("TurnHandoffAction", BindingFlags.NonPublic);
        Assert.That(actionType, Is.Not.Null);
        object action = System.Enum.Parse(actionType, actionName);
        MethodInfo method = typeof(BattleUiPresenter).GetMethod("RunTurnHandoff", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return (IEnumerator)method.Invoke(presenter, new[] { battle, round, action });
    }

    private static int RunRoutineToCompletion(IEnumerator routine, int maximumSteps = 20)
    {
        int delayCount = 0;
        while (delayCount < maximumSteps && routine.MoveNext())
        {
            Assert.That(routine.Current, Is.Null, "Zero-delay handoffs should yield one frame.");
            delayCount++;
        }

        Assert.That(delayCount, Is.LessThan(maximumSteps), "Turn handoff routine did not terminate.");
        return delayCount;
    }

    private static void SetAutoProperty(object target, string propertyName, object value)
    {
        FieldInfo field = target.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, propertyName);
        field.SetValue(target, value);
    }

    private static void Invoke(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, methodName);
        method.Invoke(target, null);
    }

    private sealed class StandingDevilStrategy : IDevilStrategy
    {
        public int DrawValue => 0;
        public DevilTurnChoice ChooseTurnAction(BattleState battle, RoundState round) => DevilTurnChoice.Stand;
        public int ChooseCardIndex(BattleState battle, RoundState round) => 0;
        public IEnumerable<Card> CreateStartingDeck(RunState runState, BattleConfig config) => new List<Card>();
        public void RegisterAffinityHooks(BattleState battle) { }
        public void UnregisterAffinityHooks(BattleState battle) { }
        public void UpdateDevilState(DevilStateUpdateContext context) { }
        public string GetDialogueId() => "test";
        public IEnumerable<Modifier> GetGlobalModifiers(RunState runState) => new List<Modifier>();
    }

    private sealed class FixedChoiceDevilStrategy : IDevilStrategy
    {
        private readonly DevilTurnChoice _choice;

        public FixedChoiceDevilStrategy(DevilTurnChoice choice)
        {
            _choice = choice;
        }

        public int DrawValue => 0;
        public DevilTurnChoice ChooseTurnAction(BattleState battle, RoundState round) => _choice;
        public int ChooseCardIndex(BattleState battle, RoundState round) => 0;
        public IEnumerable<Card> CreateStartingDeck(RunState runState, BattleConfig config) => new List<Card>();
        public void RegisterAffinityHooks(BattleState battle) { }
        public void UnregisterAffinityHooks(BattleState battle) { }
        public void UpdateDevilState(DevilStateUpdateContext context) { }
        public string GetDialogueId() => "test";
        public IEnumerable<Modifier> GetGlobalModifiers(RunState runState) => new List<Modifier>();
    }

    private sealed class SequenceDevilStrategy : IDevilStrategy
    {
        private readonly DevilTurnChoice[] _choices;

        public SequenceDevilStrategy(params DevilTurnChoice[] choices)
        {
            _choices = choices;
        }

        public int DrawValue => 3;
        public int TurnCount { get; private set; }

        public DevilTurnChoice ChooseTurnAction(BattleState battle, RoundState round)
        {
            int choiceIndex = System.Math.Min(TurnCount, _choices.Length - 1);
            TurnCount++;
            return _choices[choiceIndex];
        }

        public int ChooseCardIndex(BattleState battle, RoundState round) => 0;
        public IEnumerable<Card> CreateStartingDeck(RunState runState, BattleConfig config) =>
            new[]
            {
                new Card(Suit.Hearts, Rank.Two),
                new Card(Suit.Hearts, Rank.Three),
                new Card(Suit.Hearts, Rank.Four),
                new Card(Suit.Hearts, Rank.Five),
                new Card(Suit.Hearts, Rank.Six),
                new Card(Suit.Hearts, Rank.Seven)
            };
        public void RegisterAffinityHooks(BattleState battle) { }
        public void UnregisterAffinityHooks(BattleState battle) { }
        public void UpdateDevilState(DevilStateUpdateContext context) { }
        public string GetDialogueId() => "test";
        public IEnumerable<Modifier> GetGlobalModifiers(RunState runState) => new List<Modifier>();
    }

    private sealed class TransformingFixedChoiceDevilStrategy : IDevilStrategy, IDevilOpponentFieldModifier
    {
        private readonly DevilTurnChoice _choice;

        public TransformingFixedChoiceDevilStrategy(DevilTurnChoice choice)
        {
            _choice = choice;
        }

        public int DrawValue => 1;
        public DevilTurnChoice ChooseTurnAction(BattleState battle, RoundState round) => _choice;
        public int ChooseCardIndex(BattleState battle, RoundState round) => 0;
        public IEnumerable<Card> CreateStartingDeck(RunState runState, BattleConfig config) =>
            new[]
            {
                new Card(Suit.Hearts, Rank.Two),
                new Card(Suit.Hearts, Rank.Three),
                new Card(Suit.Hearts, Rank.Four),
                new Card(Suit.Hearts, Rank.Five)
            };
        public void RegisterAffinityHooks(BattleState battle) { }
        public void UnregisterAffinityHooks(BattleState battle) { }
        public void UpdateDevilState(DevilStateUpdateContext context) { }
        public string GetDialogueId() => "test";
        public IEnumerable<Modifier> GetGlobalModifiers(RunState runState) => new List<Modifier>();

        public void RefreshOpponentField(BattleState battle, RoundState round)
        {
            round.RefreshOpponentVisibleCards((card, owner) =>
                owner == Combatant.Opponent && card.Suit == Suit.Hearts
                    ? new Card(Suit.Spades, card.Rank, card.ModifierId)
                    : card);
        }
    }

    private sealed class RecordingDevilStrategy : IDevilStrategy
    {
        private readonly List<string> _order;
        private bool _opponentTurnUpdated;

        public RecordingDevilStrategy(List<string> order = null)
        {
            _order = order;
        }

        public int DrawValue => 1;
        public List<DevilStateUpdatePoint> UpdatePoints { get; } = new();
        public RoundState RoundStartedRound { get; private set; }
        public int RoundStartedRoundNumber { get; private set; }
        public int RoundStartedOpponentMoney { get; private set; }
        public int RoundStartedHistoryCount { get; private set; }
        public int RoundStartedOpponentHandCount { get; private set; }
        public int OpponentTurnUpdateCount { get; private set; }
        public int OpponentTurnOpponentHandCount { get; private set; }
        public int RoundResolvedUpdateCount { get; private set; }
        public int RoundResolvedHistoryCount { get; private set; }
        public int RoundResolvedPlayerMoney { get; private set; }
        public int RoundResolvedOpponentMoney { get; private set; }
        public RoundResolution? RoundResolvedResolution { get; private set; }

        public void UpdateDevilState(DevilStateUpdateContext context)
        {
            UpdatePoints.Add(context.UpdatePoint);
            _order?.Add($"update:{context.UpdatePoint}");

            if (context.UpdatePoint == DevilStateUpdatePoint.RoundStarted)
            {
                RoundStartedRound = context.Round;
                RoundStartedRoundNumber = context.Round.RoundNumber;
                RoundStartedOpponentMoney = context.Battle.OpponentMoney;
                RoundStartedHistoryCount = context.Battle.CombatHistory.Count;
                RoundStartedOpponentHandCount = context.Round.OpponentHand.Count;
            }
            else if (context.UpdatePoint == DevilStateUpdatePoint.OpponentTurnStarted)
            {
                _opponentTurnUpdated = true;
                OpponentTurnUpdateCount++;
                OpponentTurnOpponentHandCount = context.Round.OpponentHand.Count;
            }
            else if (context.UpdatePoint == DevilStateUpdatePoint.RoundResolved)
            {
                RoundResolvedUpdateCount++;
                RoundResolvedHistoryCount = context.Battle.CombatHistory.Count;
                RoundResolvedPlayerMoney = context.Battle.PlayerMoney;
                RoundResolvedOpponentMoney = context.Battle.OpponentMoney;
                RoundResolvedResolution = context.Resolution;
            }
        }

        public DevilTurnChoice ChooseTurnAction(BattleState battle, RoundState round)
        {
            _order?.Add("choose");
            return _opponentTurnUpdated ? DevilTurnChoice.Stand : DevilTurnChoice.Hit;
        }

        public int ChooseCardIndex(BattleState battle, RoundState round) => 0;
        public IEnumerable<Card> CreateStartingDeck(RunState runState, BattleConfig config) =>
            new[] { new Card(Suit.Hearts, Rank.Two) };
        public void RegisterAffinityHooks(BattleState battle) { }
        public void UnregisterAffinityHooks(BattleState battle) { }
        public string GetDialogueId() => "test";
        public IEnumerable<Modifier> GetGlobalModifiers(RunState runState) => new List<Modifier>();
    }
}
#endif
