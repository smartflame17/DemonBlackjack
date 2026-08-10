#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class CardPlayPreviewTests
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
    public void MoneyDeltaPreview_ReportsSignedStagesAndFinalDelta()
    {
        var preview = new MoneyDeltaPreview(260, -200);

        Assert.That(preview.PokerDelta, Is.EqualTo(260));
        Assert.That(preview.BurstDelta, Is.EqualTo(-200));
        Assert.That(preview.FinalDelta, Is.EqualTo(60));
    }

    [Test]
    public void CalculatePlayerRealtimeMoneyPreview_CoversPokerBurstAndTransferCaps()
    {
        Card[] cards =
        {
            new(Suit.Clubs, Rank.King),
            new(Suit.Hearts, Rank.King),
            new(Suit.Spades, Rank.Five)
        };
        ScoreResult score = ScoreResolver.Resolve(cards, null, 21);

        MoneyDeltaPreview uncapped = MoneyResolver.CalculatePlayerRealtimeMoneyPreview(
            cards,
            score,
            21,
            10,
            500,
            500);
        MoneyDeltaPreview capped = MoneyResolver.CalculatePlayerRealtimeMoneyPreview(
            cards,
            score,
            21,
            10,
            50,
            100);

        Assert.That(uncapped.PokerDelta, Is.EqualTo(260));
        Assert.That(uncapped.BurstDelta, Is.EqualTo(-200));
        Assert.That(uncapped.FinalDelta, Is.EqualTo(60));
        Assert.That(capped.PokerDelta, Is.EqualTo(100));
        Assert.That(capped.BurstDelta, Is.EqualTo(-150));
        Assert.That(capped.FinalDelta, Is.EqualTo(-50));
    }

    [Test]
    public void CalculatePlayerRealtimeMoneyPreview_CoversIndividualStagesSharedCardsAndZeroWager()
    {
        Card[] pairCards =
        {
            new(Suit.Clubs, Rank.Five),
            new(Suit.Hearts, Rank.Five)
        };
        ScoreResult pairScore = ScoreResolver.Resolve(pairCards, null, 21);
        MoneyDeltaPreview pokerOnly = MoneyResolver.CalculatePlayerRealtimeMoneyPreview(
            pairCards,
            pairScore,
            21,
            10,
            500,
            500);

        Card[] burstCards =
        {
            new(Suit.Clubs, Rank.King),
            new(Suit.Hearts, Rank.Queen),
            new(Suit.Spades, Rank.Five)
        };
        ScoreResult burstScore = ScoreResolver.Resolve(burstCards, null, 21);
        MoneyDeltaPreview burstOnly = MoneyResolver.CalculatePlayerRealtimeMoneyPreview(
            burstCards,
            burstScore,
            21,
            10,
            500,
            500);

        Card[] playedCards =
        {
            new(Suit.Clubs, Rank.Two),
            new(Suit.Diamonds, Rank.Three)
        };
        Card[] pokerCards =
        {
            playedCards[0],
            playedCards[1],
            new(Suit.Hearts, Rank.Four),
            new(Suit.Spades, Rank.Five),
            new(Suit.Clubs, Rank.Six)
        };
        ScoreResult sharedScore = ScoreResolver.Resolve(playedCards, null, 21);
        MoneyDeltaPreview sharedPoker = MoneyResolver.CalculatePlayerRealtimeMoneyPreview(
            pokerCards,
            sharedScore,
            21,
            10,
            500,
            275);
        MoneyDeltaPreview zeroWager = MoneyResolver.CalculatePlayerRealtimeMoneyPreview(
            pairCards,
            pairScore,
            21,
            0,
            500,
            500);

        Assert.That(pokerOnly.PokerDelta, Is.EqualTo(100));
        Assert.That(pokerOnly.BurstDelta, Is.Zero);
        Assert.That(burstOnly.PokerDelta, Is.Zero);
        Assert.That(burstOnly.BurstDelta, Is.EqualTo(-200));
        Assert.That(sharedPoker.PokerDelta, Is.EqualTo(275));
        Assert.That(sharedPoker.BurstDelta, Is.Zero);
        Assert.That(zeroWager.FinalDelta, Is.Zero);
    }

    [Test]
    public void PreviewPlayerCardForPlay_DoesNotEstablishSuitOverrideAnchor()
    {
        var run = new RunState(1, startingMoney: 500);
        run.AddRelic(RelicRuleResolver.SuitOverride);
        var battle = new BattleState(
            run,
            new BattleConfig(
                "preview",
                500,
                new BasicDevilStrategy(),
                startingHandSize: 1,
                playerDeckOverride: new[] { new Card(Suit.Spades, Rank.Seven) }));

        try
        {
            battle.Initialize();
            Assert.That(battle.StartRound(battle.GetDefaultWager()), Is.True);
            int commandCount = battle.CommandQueue.Count;
            int cardPlayedEvents = 0;
            battle.EventBus.Subscribe<CardPlayedEvent>(_ => cardPlayedEvents++);

            Card firstPreview = battle.PreviewPlayerCardForPlay(new Card(Suit.Hearts, Rank.Seven));

            Assert.That(firstPreview.Suit, Is.EqualTo(Suit.Hearts));
            Assert.That(battle.CommandQueue.Count, Is.EqualTo(commandCount));
            Assert.That(cardPlayedEvents, Is.Zero);
            Assert.That(battle.TryPlayCard(0), Is.True);
            Assert.That(battle.CurrentRound.PlayerPlayedCards[0].Suit, Is.EqualTo(Suit.Spades));
            Assert.That(
                battle.PreviewPlayerCardForPlay(new Card(Suit.Diamonds, Rank.Seven)).Suit,
                Is.EqualTo(Suit.Spades));
        }
        finally
        {
            battle.Dispose();
        }
    }

    [Test]
    public void BattleUiPresenter_CalculatesScoreAndMoneyPreviewWithoutMutation()
    {
        GameObject controllerObject = new("PreviewController");
        GameObject presenterObject = new("PreviewPresenter");
        try
        {
            BattleController controller = controllerObject.AddComponent<BattleController>();
            BattleUiPresenter presenter = presenterObject.AddComponent<BattleUiPresenter>();
            controller.InitializeBattle(
                new RunState(1, startingMoney: 500),
                new BattleConfig(
                    "preview",
                    500,
                    new BasicDevilStrategy(),
                    startingHandSize: 1,
                    playerDeckOverride: new[] { new Card(Suit.Clubs, Rank.King) }));
            Assert.That(controller.StartNextRound(controller.BattleState.GetDefaultWager()), Is.True);
            SetField(presenter, "battleController", controller);

            BattleState battle = controller.BattleState;
            RoundState round = battle.CurrentRound;
            int playerMoney = battle.PlayerMoney;
            int opponentMoney = battle.OpponentMoney;
            int commandCount = battle.CommandQueue.Count;
            int handCount = round.PlayerHand.Count;
            int playedCount = round.PlayerPlayedCards.Count;
            int scoreEvents = 0;
            int moneyEvents = 0;
            battle.EventBus.Subscribe<ScoreCalculatedEvent>(_ => scoreEvents++);
            battle.EventBus.Subscribe<MoneyChangedEvent>(_ => moneyEvents++);

            bool calculated = presenter.TryCalculateCardPlayPreview(0, out ScoreResult score, out MoneyDeltaPreview money);

            Assert.That(calculated, Is.True);
            Assert.That(score.BlackjackScore, Is.EqualTo(10));
            Assert.That(score.IsBurst, Is.False);
            Assert.That(money.FinalDelta, Is.Zero);
            Assert.That(battle.PlayerMoney, Is.EqualTo(playerMoney));
            Assert.That(battle.OpponentMoney, Is.EqualTo(opponentMoney));
            Assert.That(battle.CommandQueue.Count, Is.EqualTo(commandCount));
            Assert.That(round.PlayerHand.Count, Is.EqualTo(handCount));
            Assert.That(round.PlayerPlayedCards.Count, Is.EqualTo(playedCount));
            Assert.That(scoreEvents, Is.Zero);
            Assert.That(moneyEvents, Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(presenterObject);
            Object.DestroyImmediate(controllerObject);
        }
    }

    [Test]
    public void BattleUiPresenter_PreviewMatchesActualDeterministicCardPlayMoneyDelta()
    {
        GameObject controllerObject = new("PreviewController");
        GameObject presenterObject = new("PreviewPresenter");
        try
        {
            BattleController controller = controllerObject.AddComponent<BattleController>();
            BattleUiPresenter presenter = presenterObject.AddComponent<BattleUiPresenter>();
            controller.InitializeBattle(
                new RunState(1, startingMoney: 500),
                new BattleConfig(
                    "preview",
                    500,
                    new BasicDevilStrategy(),
                    startingHandSize: 1,
                    playerDeckOverride: new[] { new Card(Suit.Spades, Rank.Five) }));
            Assert.That(controller.StartNextRound(controller.BattleState.GetDefaultWager()), Is.True);
            SetField(presenter, "battleController", controller);

            BattleState battle = controller.BattleState;
            Assert.That(battle.CurrentRound.TryPlayHitCard(new Card(Suit.Clubs, Rank.King)), Is.True);
            Assert.That(battle.CurrentRound.TryPlayHitCard(new Card(Suit.Hearts, Rank.King)), Is.True);
            Assert.That(
                presenter.TryCalculateCardPlayPreview(0, out ScoreResult score, out MoneyDeltaPreview preview),
                Is.True);
            int playerMoneyBeforePlay = battle.PlayerMoney;

            Assert.That(controller.TryPlayCard(0), Is.True);

            Assert.That(score.BlackjackScore, Is.EqualTo(battle.CurrentRound.PlayerScore.BlackjackScore));
            Assert.That(score.FinalScore, Is.EqualTo(battle.CurrentRound.PlayerScore.FinalScore));
            Assert.That(score.PokerRank, Is.EqualTo(battle.CurrentRound.PlayerScore.PokerRank));
            Assert.That(battle.PlayerMoney - playerMoneyBeforePlay, Is.EqualTo(preview.FinalDelta));
        }
        finally
        {
            Object.DestroyImmediate(presenterObject);
            Object.DestroyImmediate(controllerObject);
        }
    }

    [Test]
    public void BattleUiPresenter_ShowsSignedDeltaWithConfiguredAlphaAndHidesIt()
    {
        GameObject presenterObject = new("PreviewPresenter");
        GameObject textObject = new(
            "PlayerChoiceMoneyPreview",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(presenterObject.transform);

        try
        {
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.color = new Color(0.2f, 0.4f, 0.6f, 1f);
            BattleUiPresenter presenter = presenterObject.AddComponent<BattleUiPresenter>();
            Invoke(presenter, "Awake");

            Assert.That(text.color.a, Is.Zero);

            presenter.ShowPlayerChoiceMoneyPreview(default, new MoneyDeltaPreview(150, -50));

            Assert.That(text.text, Is.EqualTo("+100$"));
            Assert.That(text.color.r, Is.EqualTo(0.2f).Within(0.001f));
            Assert.That(text.color.g, Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(text.color.b, Is.EqualTo(0.6f).Within(0.001f));
            Assert.That(text.color.a, Is.EqualTo(0.3f).Within(0.001f));

            presenter.HidePlayerChoiceMoneyPreview();
            Assert.That(text.color.a, Is.Zero);

            presenter.ShowPlayerChoiceMoneyPreview(default, default);
            Assert.That(text.text, Is.EqualTo("0$"));
        }
        finally
        {
            Object.DestroyImmediate(presenterObject);
        }
    }

    [Test]
    public void BattleHandCardDragHandler_CalculatesPreviewOncePerDragHold()
    {
        GameObject controllerObject = new("PreviewController");
        GameObject presenterObject = new("PreviewPresenter");
        GameObject dragParent = new("DragParent", typeof(RectTransform));
        GameObject cardObject = new("Card", typeof(RectTransform));
        cardObject.transform.SetParent(dragParent.transform);

        try
        {
            BattleController controller = controllerObject.AddComponent<BattleController>();
            BattleUiPresenter presenter = presenterObject.AddComponent<BattleUiPresenter>();
            controller.InitializeBattle(
                new RunState(1, startingMoney: 500),
                new BattleConfig(
                    "preview",
                    500,
                    new BasicDevilStrategy(),
                    startingHandSize: 1,
                    playerDeckOverride: new[] { new Card(Suit.Clubs, Rank.Five) }));
            Assert.That(controller.StartNextRound(controller.BattleState.GetDefaultWager()), Is.True);
            SetField(presenter, "battleController", controller);
            var gate = new CountingInputGate();
            controller.InputGate = gate;

            BattleHandCardDragHandler handler = cardObject.AddComponent<BattleHandCardDragHandler>();
            handler.Configure(presenter, 0);
            var eventData = new PointerEventData(null) { position = Vector2.zero };

            handler.OnBeginDrag(eventData);
            handler.OnDrag(eventData);
            handler.OnDrag(eventData);
            handler.OnDrag(eventData);

            Assert.That(gate.CanPlayCardCallCount, Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(cardObject);
            Object.DestroyImmediate(dragParent);
            Object.DestroyImmediate(presenterObject);
            Object.DestroyImmediate(controllerObject);
        }
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }

    private static void Invoke(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, methodName);
        method.Invoke(target, null);
    }

    private sealed class CountingInputGate : IBattleInputGate
    {
        public int CanPlayCardCallCount { get; private set; }

        public bool CanStartRound(BattleState battle, int wager) => true;

        public bool CanPlayCard(BattleState battle, int handIndex)
        {
            CanPlayCardCallCount++;
            return true;
        }

        public bool CanHit(BattleState battle) => true;
        public bool CanStand(BattleState battle) => true;
        public bool CanUseActiveItem(BattleState battle, string itemId) => true;
        public void NotifyStandAccepted(BattleState battle) { }
    }
}
#endif
