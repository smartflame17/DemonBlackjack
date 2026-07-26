#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class TutorialSystemTests
{
    [SetUp]
    public void SetUp()
    {
        EventBus.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        EventBus.Clear();
    }

    [Test]
    public void Deck_FixedDrawOrderDrawsFirstCardFirst()
    {
        var cards = new List<Card>
        {
            new(Suit.Hearts, Rank.Four),
            new(Suit.Diamonds, Rank.Three),
            new(Suit.Spades, Rank.Ten)
        };
        var deck = new Deck(cards, 1, shuffle: false, cardsAreDrawOrder: true);

        Assert.That(deck.TryDraw(out Card first), Is.True);
        Assert.That(first.Rank, Is.EqualTo(Rank.Four));
        Assert.That(deck.TryDraw(out Card second), Is.True);
        Assert.That(second.Rank, Is.EqualTo(Rank.Three));
        Assert.That(deck.TryDraw(out Card third), Is.True);
        Assert.That(third.Rank, Is.EqualTo(Rank.Ten));
    }

    [Test]
    public void BattleConfig_DeckOverridesDoNotMutateRunDeck()
    {
        var run = new RunState(10);
        var config = new BattleConfig(
            "tutorial_test",
            1000,
            new TutorialDevilStrategy(3, 15),
            startingHandSize: 3,
            playerDeckOverride: new[]
            {
                new Card(Suit.Hearts, Rank.Four),
                new Card(Suit.Diamonds, Rank.Four),
                new Card(Suit.Clubs, Rank.Three)
            },
            opponentDeckOverride: new[]
            {
                new Card(Suit.Clubs, Rank.Ten),
                new Card(Suit.Diamonds, Rank.Seven),
                new Card(Suit.Hearts, Rank.Six)
            });
        var battle = new BattleState(run, config);

        try
        {
            battle.Initialize();
            Assert.That(battle.StartRound(10), Is.True);
            Assert.That(Ranks(battle.CurrentRound.PlayerHand), Is.EqualTo(new[] { Rank.Four, Rank.Four, Rank.Three }));
            Assert.That(Ranks(battle.CurrentRound.OpponentHand), Is.EqualTo(new[] { Rank.Ten, Rank.Seven, Rank.Six }));
            Assert.That(run.Deck.Count, Is.EqualTo(RunState.StandardDeckSize));
        }
        finally
        {
            battle.Dispose();
        }
    }

    [Test]
    public void TutorialDefaultScenario_ContainsConversationAndGameplaySteps()
    {
        TutorialScenarioConfig config = TutorialScenarioConfig.CreateDefaultRuntime();

        try
        {
            Assert.That(config.Steps.Count, Is.EqualTo(21));
            Assert.That(config.TryGetStep(TutorialStepKey.SceneBoot, out TutorialStepSpec sceneBoot), Is.True);
            Assert.That(sceneBoot.Mode, Is.EqualTo(TutorialOverlayMode.Choice));
            Assert.That(sceneBoot.Choices.Count, Is.EqualTo(2));
            Assert.That(config.TryGetStep(TutorialStepKey.R1PlayThree, out TutorialStepSpec playThree), Is.True);
            Assert.That(playThree.Mode, Is.EqualTo(TutorialOverlayMode.GameplayGuide));
            Assert.That(playThree.GuideTarget, Is.EqualTo(TutorialGuideTarget.PlayerHand));
            Assert.That(config.TryGetStep(TutorialStepKey.R2PairExplanation, out TutorialStepSpec pair), Is.True);
            Assert.That(pair.Body, Does.Contain("페어"));
            Assert.That(config.TryGetStep(TutorialStepKey.R2TripleExplanation, out TutorialStepSpec triple), Is.True);
            Assert.That(triple.Body, Does.Contain("트리플"));
            Assert.That(config.TryGetStep(TutorialStepKey.R2FourOfAKindExplanation, out TutorialStepSpec four), Is.True);
            Assert.That(four.Body, Does.Contain("포카드"));
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void TutorialDefaultScenario_UsesOneDeckAcrossTwoRounds()
    {
        TutorialScenarioConfig config = TutorialScenarioConfig.CreateDefaultRuntime();

        try
        {
            Assert.That(config.TryGetRound(0, out TutorialRoundSpec first), Is.True);
            Assert.That(
                Ranks(first.CreatePlayerDeck()),
                Is.EqualTo(new[]
                {
                    Rank.Four,
                    Rank.Four,
                    Rank.Three,
                    Rank.Nine,
                    Rank.Ten,
                    Rank.Four,
                    Rank.Two,
                    Rank.Seven,
                    Rank.Four
                }));
            Assert.That(
                Ranks(first.CreateOpponentDeck()),
                Is.EqualTo(new[]
                {
                    Rank.Ten,
                    Rank.Seven,
                    Rank.Six,
                    Rank.Nine,
                    Rank.Two,
                    Rank.Three
                }));
            Assert.That(config.TryGetRound(1, out TutorialRoundSpec second), Is.True);
            Assert.That(second.CreatePlayerDeck(), Is.Empty);
            Assert.That(second.CreateOpponentDeck(), Is.Empty);
            Assert.That(second.PlayerActions.Count, Is.EqualTo(5));
            Assert.That(second.PlayerActions[0].ActionType, Is.EqualTo(TutorialPlayerActionType.HitRank));
            Assert.That(second.PlayerActions[0].Rank, Is.EqualTo(Rank.Four));
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void TutorialInputSequence_AllowsOnlyConfiguredActions()
    {
        TutorialScenarioConfig config = TutorialScenarioConfig.CreateDefaultRuntime();

        try
        {
            Assert.That(config.TryGetRound(0, out TutorialRoundSpec round), Is.True);
            var sequence = new TutorialInputSequence();
            var hand = new List<Card>
            {
                new(Suit.Hearts, Rank.Four),
                new(Suit.Diamonds, Rank.Four),
                new(Suit.Clubs, Rank.Three)
            };
            var nine = new Card(Suit.Spades, Rank.Nine);
            var ten = new Card(Suit.Hearts, Rank.Ten);

            sequence.BeginRound(round);
            Assert.That(sequence.CanPlayCard(hand, 0), Is.False);
            Assert.That(sequence.CanPlayCard(hand, 2), Is.True);
            sequence.NotifyCardPlayed(hand[2]);
            Assert.That(sequence.CanHit(new List<Card> { nine }), Is.True);
            sequence.NotifyPlayerHitUsed(nine);
            sequence.NotifyCardPlayed(nine);
            Assert.That(sequence.CanHit(new List<Card> { ten }), Is.True);
            sequence.NotifyPlayerHitUsed(ten);
            sequence.NotifyCardPlayed(ten);
            Assert.That(sequence.CanStand(), Is.True);
            sequence.NotifyStandAccepted();
            Assert.That(sequence.IsComplete, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void TutorialBattle_TwoRoundsPreserveHandsDeckAndPokerProgression()
    {
        TutorialScenarioConfig scenario = TutorialScenarioConfig.CreateDefaultRuntime();
        Assert.That(scenario.TryGetRound(0, out TutorialRoundSpec round), Is.True);
        var battle = new BattleState(
            new RunState(scenario.Seed),
            new BattleConfig(
                round.EncounterId,
                round.OpponentStartingMoney,
                new TutorialDevilStrategy(round.OpponentDrawValue, round.OpponentStandScore),
                round.PlayerStartingHandSize,
                round.TargetScore,
                round.BurstThreshold,
                round.BaseWager,
                round.DevilId,
                playerDeckOverride: round.CreatePlayerDeck(),
                opponentDeckOverride: round.CreateOpponentDeck()));
        var pokerRanks = new List<PokerHandRank>();
        int refillCount = 0;
        int roundEndedCount = 0;

        try
        {
            battle.Initialize();
            battle.EventBus.Subscribe<PokerResolvedEvent>(eventData =>
            {
                if (eventData.Combatant == Combatant.Player)
                    pokerRanks.Add(eventData.Poker.Rank);
            });
            battle.EventBus.Subscribe<HandRefilledEvent>(_ => refillCount++);
            battle.EventBus.Subscribe<RoundEndedEvent>(_ => roundEndedCount++);

            Assert.That(battle.StartRound(round.BaseWager), Is.True);
            Assert.That(Ranks(battle.CurrentRound.PlayerHand), Is.EqualTo(new[] { Rank.Four, Rank.Four, Rank.Three }));
            Assert.That(battle.TryPlayCard(2), Is.True);
            Assert.That(battle.TryHit(), Is.True);
            Assert.That(battle.CurrentRound.PlayerPlayedCards[^1].Rank, Is.EqualTo(Rank.Nine));
            Assert.That(battle.TryHit(), Is.True);
            Assert.That(battle.CurrentRound.PlayerScore.BlackjackScore, Is.EqualTo(22));
            Assert.That(battle.TryStand(), Is.True);
            Assert.That(roundEndedCount, Is.EqualTo(1));
            Assert.That(battle.Phase, Is.EqualTo(BattlePhase.Cleanup));
            Assert.That(Ranks(battle.CurrentRound.PlayerHand), Is.EqualTo(new[] { Rank.Four, Rank.Four }));

            battle.CleanupRound();
            Assert.That(battle.StartRound(round.BaseWager), Is.True);
            Assert.That(Ranks(battle.CurrentRound.PlayerHand), Is.EqualTo(new[] { Rank.Four, Rank.Four }));
            Assert.That(Ranks(battle.CurrentRound.OpponentHand), Is.EqualTo(new[] { Rank.Six }));

            pokerRanks.Clear();
            refillCount = 0;
            Assert.That(battle.TryHit(), Is.True);
            Assert.That(Ranks(battle.CurrentRound.PlayerPlayedCards), Is.EqualTo(new[] { Rank.Four }));
            Assert.That(Ranks(battle.CurrentRound.OpponentVisibleCards), Is.EqualTo(new[] { Rank.Six }));
            Assert.That(battle.TryPlayCard(0), Is.True);
            Assert.That(ScoreResolver.ResolvePoker(battle.CurrentRound.PlayerPlayedCards).Rank, Is.EqualTo(PokerHandRank.Pair));
            Assert.That(battle.TryPlayCard(0), Is.True);
            Assert.That(ScoreResolver.ResolvePoker(battle.CurrentRound.PlayerPlayedCards).Rank, Is.EqualTo(PokerHandRank.ThreeOfAKind));
            Assert.That(Ranks(battle.CurrentRound.PlayerHand), Is.EqualTo(new[] { Rank.Two, Rank.Seven, Rank.Four }));
            Assert.That(refillCount, Is.EqualTo(1));
            Assert.That(battle.TryPlayCard(2), Is.True);
            Assert.That(ScoreResolver.ResolvePoker(battle.CurrentRound.PlayerPlayedCards).Rank, Is.EqualTo(PokerHandRank.FourOfAKind));
            Assert.That(Ranks(battle.CurrentRound.PlayerHand), Is.EqualTo(new[] { Rank.Two, Rank.Seven }));
            Assert.That(pokerRanks, Does.Contain(PokerHandRank.Pair));
            Assert.That(pokerRanks, Does.Contain(PokerHandRank.ThreeOfAKind));
            Assert.That(pokerRanks, Does.Contain(PokerHandRank.FourOfAKind));

            Assert.That(battle.TryStand(), Is.True);
            Assert.That(battle.Phase, Is.EqualTo(BattlePhase.PlayerPhase));
            Assert.That(battle.CurrentRound.PlayerScore.BlackjackScore, Is.EqualTo(16));
            Assert.That(battle.CurrentRound.OpponentScore.BlackjackScore, Is.EqualTo(15));
            Assert.That(battle.TryStand(), Is.True);
            Assert.That(roundEndedCount, Is.EqualTo(2));
            Assert.That(battle.CombatHistory[^1].Winner, Is.EqualTo(Combatant.Player));
        }
        finally
        {
            battle.Dispose();
            Object.DestroyImmediate(scenario);
        }
    }

    [Test]
    public void BattleController_InputGateBlocksUnapprovedActions()
    {
        var controllerObject = new GameObject("Controller");

        try
        {
            BattleController controller = controllerObject.AddComponent<BattleController>();
            var gate = new TestBattleInputGate();
            controller.InitializeBattle(
                new RunState(20),
                new BattleConfig(
                    "tutorial_gate_test",
                    1000,
                    new TutorialDevilStrategy(3, 15),
                    startingHandSize: 1,
                    playerDeckOverride: new[]
                    {
                        new Card(Suit.Hearts, Rank.Three),
                        new Card(Suit.Spades, Rank.Nine)
                    },
                    opponentDeckOverride: new[]
                    {
                        new Card(Suit.Clubs, Rank.Ten)
                    }));
            controller.InputGate = gate;

            gate.AllowStartRound = true;
            Assert.That(controller.StartNextRound(10), Is.True);
            Assert.That(controller.TryPlayCard(0), Is.False);
            gate.AllowPlayCard = true;
            Assert.That(controller.TryPlayCard(0), Is.True);
            Assert.That(controller.TryHitWithoutEndingTurn(), Is.False);
            gate.AllowHit = true;
            Assert.That(controller.TryHitWithoutEndingTurn(), Is.True);
            Assert.That(controller.TryStand(), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(controllerObject);
        }
    }

    [Test]
    public void TutorialOverlay_CreatesOnlyNonBlockingHighlightCanvas()
    {
        var overlayObject = new GameObject("TutorialOverlay");

        try
        {
            TutorialInstructionOverlay overlay = overlayObject.AddComponent<TutorialInstructionOverlay>();
            TutorialStepSpec step = TutorialStepSpec.Create(
                TutorialStepKey.R1PlayThree,
                TutorialOverlayMode.GameplayGuide,
                "딜러",
                "3부터 내봐.",
                string.Empty,
                TutorialGuideTarget.None);

            overlay.Show(step, string.Empty, null, null);

            Assert.That(overlay.UsesSceneDialoguePanel, Is.False);
            Assert.That(FindChild(overlayObject.transform, "TutorialHighlightCanvas"), Is.Not.Null);
            Assert.That(FindChild(overlayObject.transform, "CoverPanel"), Is.Null);
            Assert.That(FindChild(overlayObject.transform, "DialoguePanel"), Is.Null);
            Assert.That(FindChild(overlayObject.transform, "GuidePanel"), Is.Null);
            Assert.That(FindChild(overlayObject.transform, "RewardPanel"), Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(overlayObject);
        }
    }

    private static List<Rank> Ranks(IReadOnlyList<Card> cards)
    {
        var ranks = new List<Rank>(cards.Count);
        for (int i = 0; i < cards.Count; i++)
            ranks.Add(cards[i].Rank);
        return ranks;
    }

    private static Transform FindChild(Transform root, string name)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == name)
                return children[i];
        }

        return null;
    }

    private sealed class TestBattleInputGate : IBattleInputGate
    {
        public bool AllowStartRound { get; set; }
        public bool AllowPlayCard { get; set; }
        public bool AllowHit { get; set; }

        public bool CanStartRound(BattleState battle, int wager)
        {
            return AllowStartRound;
        }

        public bool CanPlayCard(BattleState battle, int handIndex)
        {
            return AllowPlayCard;
        }

        public bool CanHit(BattleState battle)
        {
            return AllowHit;
        }

        public bool CanStand(BattleState battle)
        {
            return false;
        }

        public bool CanUseActiveItem(BattleState battle, string itemId)
        {
            return false;
        }

        public void NotifyStandAccepted(BattleState battle)
        {
        }
    }
}
#endif
