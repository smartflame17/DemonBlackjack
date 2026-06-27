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

        Assert.That(catalog.TryGetDefinition(ActiveItemResolver.RejectLastHit.ToUpperInvariant(), out ShopContentDefinition item), Is.True);
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
    public void RankUpgradePurchase_ImmediatelyTransformsRunDeck()
    {
        var run = new RunState(1, startingGold: 100);

        ShopPurchaseResult result = run.TryPurchaseRankUpgrade(Rank.Five, CardModifierResolver.RankToSpades, 10);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(ContainsUpgradedRank(run.Deck, Rank.Five, CardModifierResolver.RankToSpades), Is.True);
        Assert.That(ContainsModifier(run.Deck, Rank.Six, CardModifierResolver.RankToSpades), Is.False);
    }

    [Test]
    public void RankUpgradeReplacement_RewritesRunDeckToNewModifier()
    {
        var run = new RunState(1, startingGold: 100);

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
            gold = 100,
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
    public void MoveJack_MovesPreviousPlayerCardToOpponentVisiblePile()
    {
        RunState run = CreateRunWithDeck(CardData(Suit.Hearts, Rank.Five), CardData(Suit.Clubs, Rank.Jack, CardModifierResolver.MoveJack));
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound();

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
        battle.StartRound();
        battle.TryPlayPlayerHandCardForEffect(IndexOfRank(battle.CurrentRound.PlayerHand, Rank.Five));
        battle.TryPlayPlayerHandCardForEffect(IndexOfRank(battle.CurrentRound.PlayerHand, Rank.Jack));

        battle.CleanupRound();

        Assert.That(ContainsRank(battle.PlayerDiscardPile, Rank.Five), Is.True);
        Assert.That(ContainsRank(battle.OpponentDiscardPile, Rank.Five), Is.False);

        battle.Dispose();
    }

    [Test]
    public void CopyQueen_PlaysRandomHandCardWithPreviousCardSuit()
    {
        RunState run = CreateRunWithDeck(
            CardData(Suit.Hearts, Rank.Five),
            CardData(Suit.Clubs, Rank.Queen, CardModifierResolver.CopyQueen),
            CardData(Suit.Hearts, Rank.Two),
            CardData(Suit.Spades, Rank.Three));
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound();
        int playedEvents = 0;
        battle.EventBus.Subscribe<CardPlayedEvent>(_ => playedEvents++);

        Assert.That(battle.TryPlayPlayerHandCardForEffect(IndexOfRank(battle.CurrentRound.PlayerHand, Rank.Five)), Is.True);
        Assert.That(battle.TryPlayPlayerHandCardForEffect(IndexOfRank(battle.CurrentRound.PlayerHand, Rank.Queen)), Is.True);

        Assert.That(ContainsRank(battle.CurrentRound.PlayerPlayedCards, Rank.Two), Is.True);
        Assert.That(ContainsRank(battle.CurrentRound.PlayerPlayedCards, Rank.Three), Is.False);
        Assert.That(playedEvents, Is.EqualTo(3));

        battle.Dispose();
    }

    [Test]
    public void CopyQueen_DoesNothingWhenNoHandCardMatchesPreviousSuit()
    {
        RunState run = CreateRunWithDeck(
            CardData(Suit.Hearts, Rank.Five),
            CardData(Suit.Clubs, Rank.Queen, CardModifierResolver.CopyQueen),
            CardData(Suit.Spades, Rank.Three));
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound();

        battle.TryPlayPlayerHandCardForEffect(IndexOfRank(battle.CurrentRound.PlayerHand, Rank.Five));
        battle.TryPlayPlayerHandCardForEffect(IndexOfRank(battle.CurrentRound.PlayerHand, Rank.Queen));

        Assert.That(battle.CurrentRound.PlayerPlayedCards.Count, Is.EqualTo(2));
        Assert.That(ContainsRank(battle.CurrentRound.PlayerPlayedCards, Rank.Three), Is.False);

        battle.Dispose();
    }

    [Test]
    public void DuplicateKing_AddsBattleOnlyDuplicateOfPreviousCardToPlayerHand()
    {
        RunState run = CreateRunWithDeck(
            CardData(Suit.Hearts, Rank.Nine, CardModifierResolver.NegativeRank),
            CardData(Suit.Clubs, Rank.King, CardModifierResolver.DuplicateKing));
        int runDeckCount = run.Deck.Count;
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound();

        battle.TryPlayPlayerHandCardForEffect(IndexOfRank(battle.CurrentRound.PlayerHand, Rank.Nine));
        battle.TryPlayPlayerHandCardForEffect(IndexOfRank(battle.CurrentRound.PlayerHand, Rank.King));

        Assert.That(ContainsModifier(battle.CurrentRound.PlayerHand, Rank.Nine, CardModifierResolver.NegativeRank), Is.True);
        Assert.That(run.Deck.Count, Is.EqualTo(runDeckCount));

        battle.Dispose();
    }

    [Test]
    public void HitLower_PlaysRandomLowerRankFromPlayerHand()
    {
        RunState run = CreateRunWithDeck(
            CardData(Suit.Clubs, Rank.Six, CardModifierResolver.HitLower),
            CardData(Suit.Diamonds, Rank.Two),
            CardData(Suit.Hearts, Rank.Seven));
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound();

        Assert.That(battle.TryPlayPlayerHandCardForEffect(IndexOfRank(battle.CurrentRound.PlayerHand, Rank.Six)), Is.True);

        Assert.That(ContainsRank(battle.CurrentRound.PlayerPlayedCards, Rank.Six), Is.True);
        Assert.That(ContainsRank(battle.CurrentRound.PlayerPlayedCards, Rank.Two), Is.True);
        Assert.That(ContainsRank(battle.CurrentRound.PlayerPlayedCards, Rank.Seven), Is.False);

        battle.Dispose();
    }

    [Test]
    public void HitLower_DoesNothingWhenNoLowerRankExists()
    {
        RunState run = CreateRunWithDeck(CardData(Suit.Clubs, Rank.Six, CardModifierResolver.HitLower));
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound();

        Assert.That(battle.TryPlayPlayerHandCardForEffect(IndexOfRank(battle.CurrentRound.PlayerHand, Rank.Six)), Is.True);

        Assert.That(battle.CurrentRound.PlayerPlayedCards.Count, Is.EqualTo(1));
        Assert.That(battle.CurrentRound.PlayerPlayedCards[0].Rank, Is.EqualTo(Rank.Six));

        battle.Dispose();
    }

    [Test]
    public void HitLower_EffectPlayedCardPublishesCardPlayedEvent()
    {
        RunState run = CreateRunWithDeck(
            CardData(Suit.Clubs, Rank.Six, CardModifierResolver.HitLower),
            CardData(Suit.Diamonds, Rank.Four, CardModifierResolver.HitLower));
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound();
        int playedEvents = 0;
        battle.EventBus.Subscribe<CardPlayedEvent>(_ => playedEvents++);

        Assert.That(battle.TryPlayPlayerHandCardForEffect(IndexOfRank(battle.CurrentRound.PlayerHand, Rank.Six)), Is.True);

        Assert.That(playedEvents, Is.EqualTo(2));
        Assert.That(ContainsModifier(battle.CurrentRound.PlayerPlayedCards, Rank.Four, CardModifierResolver.HitLower), Is.True);

        battle.Dispose();
    }

    [Test]
    public void DrawSuit_DrawsMatchingSuitFromPlayerDrawPile()
    {
        var run = new RunState(1, startingGold: 100);
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound();
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
        battle.StartRound();
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
        battle.StartRound();
        Assert.That(battle.CommitWagerAndBeginRound(1, false), Is.True);

        Assert.That(battle.TryPlayCard(0), Is.True);
        Assert.That(battle.TryPlayCard(0), Is.True);

        Assert.That(battle.CurrentRound.PlayerPlayedCards.Count, Is.EqualTo(2));

        battle.Dispose();
    }

    [Test]
    public void RankUpgradePurchase_ImmediatelyTransformsPlayerBattleCards()
    {
        var run = new RunState(1, startingGold: 100);
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound();
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
        var run = new RunState(1, startingGold: 100);
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound();
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
        var run = new RunState(1, startingGold: 100);
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound();
        battle.CurrentRound.AddToHand(new Card(Suit.Clubs, Rank.Jack));
        battle.CleanupRound();

        Assert.That(run.TryPurchaseRankUpgrade(Rank.Jack, CardModifierResolver.RankToClubs, 10).Succeeded, Is.True);
        Assert.That(battle.StartRound(), Is.True);

        Assert.That(ContainsUpgradedRank(battle.CurrentRound.PlayerHand, Rank.Jack, CardModifierResolver.RankToClubs), Is.True);

        battle.Dispose();
    }

    [Test]
    public void DrawThree_AddsCardsPastNormalHandSize()
    {
        var run = new RunState(1);
        run.AddActiveItem(ActiveItemResolver.DrawThree);
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound();

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
        battle.StartRound();
        Card hitCard = new(Suit.Hearts, Rank.Eight);
        battle.CurrentRound.TryPlayHitCard(hitCard);

        Assert.That(battle.TryUseActiveItem(ActiveItemResolver.RejectLastHit), Is.True);

        Assert.That(battle.CurrentRound.PlayerPlayedCards.Count, Is.EqualTo(0));
        Assert.That(battle.PlayerDiscardPile.Count, Is.EqualTo(1));
        Assert.That(battle.PlayerDiscardPile[0], Is.EqualTo(hitCard));
    }

    [Test]
    public void DoubleWager_AddsMatchingExtraStakes()
    {
        var run = new RunState(1, startingGold: 100);
        run.AddActiveItem(ActiveItemResolver.DoubleWager);
        var battle = new BattleState(run, new BattleConfig("test", 100));
        battle.StartRound();
        Assert.That(battle.CurrentRound.TryCommitWager(10, 10), Is.True);

        Assert.That(battle.TryUseActiveItem(ActiveItemResolver.DoubleWager), Is.True);

        Assert.That(battle.CurrentRound.PlayerStake, Is.EqualTo(20));
        Assert.That(battle.CurrentRound.OpponentStake, Is.EqualTo(20));
        Assert.That(battle.CurrentRound.Pot, Is.EqualTo(40));
        Assert.That(run.Money, Is.EqualTo(90));
        Assert.That(battle.OpponentMoney, Is.EqualTo(90));
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

    private static RunState CreateRunWithDeck(params RunCardData[] cards)
    {
        var data = new RunStateData
        {
            seed = 1,
            gold = 100,
            maxPlayerHp = 100,
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
}
#endif
