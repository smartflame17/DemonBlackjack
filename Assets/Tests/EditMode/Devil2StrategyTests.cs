#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;

public sealed class Devil2StrategyTests
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
    public void RoundWager_PlayerWinsCompoundPermanentlyAndOtherResultsDoNotReset()
    {
        var strategy = new Devil2Strategy();
        BattleState battle = CreateBattle(strategy);
        try
        {
            int baseWager = battle.Config.BaseWager;
            int doubledWager = Inflate(baseWager);
            int quadrupledWager = Inflate(doubledWager);

            Assert.That(battle.GetDefaultWager(), Is.EqualTo(baseWager));

            UpdateResolvedRound(strategy, battle, 1, baseWager, Combatant.Player);
            Assert.That(battle.GetDefaultWager(), Is.EqualTo(doubledWager));

            UpdateResolvedRound(strategy, battle, 1, baseWager, Combatant.Player);
            Assert.That(battle.GetDefaultWager(), Is.EqualTo(doubledWager), "A round must only inflate once.");

            UpdateResolvedRound(strategy, battle, 2, doubledWager, Combatant.Player);
            Assert.That(battle.GetDefaultWager(), Is.EqualTo(quadrupledWager));

            UpdateResolvedRound(strategy, battle, 3, quadrupledWager, Combatant.Opponent);
            Assert.That(battle.GetDefaultWager(), Is.EqualTo(quadrupledWager));

            UpdateResolvedRound(strategy, battle, 4, quadrupledWager, null);
            Assert.That(battle.GetDefaultWager(), Is.EqualTo(quadrupledWager));
        }
        finally
        {
            battle.Dispose();
        }
    }

    [Test]
    public void RoundWager_OverflowSaturatesAndInvalidModifierResultRemainsPositive()
    {
        var devil2 = new Devil2Strategy();
        BattleState devil2Battle = CreateBattle(devil2);
        BattleState invalidBattle = CreateBattle(new InvalidWagerStrategy());
        try
        {
            UpdateResolvedRound(devil2, devil2Battle, 1, int.MaxValue, Combatant.Player);

            Assert.That(devil2Battle.GetDefaultWager(), Is.EqualTo(int.MaxValue));
            Assert.That(invalidBattle.GetDefaultWager(), Is.EqualTo(1));
        }
        finally
        {
            devil2Battle.Dispose();
            invalidBattle.Dispose();
        }
    }

    [Test]
    public void PokerPayout_AtThresholdIsUncappedAndCrossingPayoutActivatesTheNextResolution()
    {
        var strategy = new Devil2Strategy();
        int baseWager = GameplayConstants.Devil2Config.Devil2Wager;
        int threshold = GameplayConstants.Devil2Config.Devil2CircuitBreakerThreshold;
        BattleState equalityBattle = CreateBattle(strategy, threshold + baseWager);
        BattleState crossingBattle = CreateBattle(new Devil2Strategy());
        try
        {
            Assert.That(equalityBattle.StartRound(equalityBattle.GetDefaultWager()), Is.True);
            Assert.That(equalityBattle.OpponentMoney, Is.EqualTo(threshold));
            PlayPair(equalityBattle.CurrentRound, Rank.King);
            int equalityPlayerMoney = equalityBattle.PlayerMoney;

            MoneyResolver.ResolvePlayerPokerPayout(
                equalityBattle,
                equalityBattle.CurrentRound,
                equalityBattle.CurrentRound.EffectiveWager);

            Assert.That(equalityBattle.PlayerMoney - equalityPlayerMoney, Is.EqualTo(260));

            Assert.That(crossingBattle.StartRound(crossingBattle.GetDefaultWager()), Is.True);
            PlayRoyalFlush(crossingBattle.CurrentRound);
            int playerMoneyBeforeCrossing = crossingBattle.PlayerMoney;

            MoneyResolver.ResolvePlayerPokerPayout(
                crossingBattle,
                crossingBattle.CurrentRound,
                crossingBattle.CurrentRound.EffectiveWager);
            int crossingPayout = crossingBattle.PlayerMoney - playerMoneyBeforeCrossing;

            Assert.That(crossingPayout, Is.GreaterThan(GameplayConstants.Devil2Config.Devil2PokerPayoutThreshold));
            Assert.That(crossingBattle.OpponentMoney, Is.LessThan(threshold));
            Assert.That(crossingBattle.CurrentRound.PlayerPokerEarnings, Is.EqualTo(crossingPayout));

            int playerMoneyBeforeNextPayout = crossingBattle.PlayerMoney;
            MoneyResolver.ResolvePlayerPokerPayout(
                crossingBattle,
                crossingBattle.CurrentRound,
                crossingBattle.CurrentRound.EffectiveWager);

            Assert.That(crossingBattle.PlayerMoney, Is.EqualTo(playerMoneyBeforeNextPayout));
        }
        finally
        {
            equalityBattle.Dispose();
            crossingBattle.Dispose();
        }
    }

    [Test]
    public void PokerPayout_BelowThresholdTransfersOnlyRemainingRoundAllowance()
    {
        var strategy = new Devil2Strategy();
        BattleState battle = CreateBattle(
            strategy,
            GameplayConstants.Devil2Config.Devil2CircuitBreakerThreshold
                + GameplayConstants.Devil2Config.Devil2Wager
                - 1);
        try
        {
            Assert.That(battle.StartRound(battle.GetDefaultWager()), Is.True);
            Assert.That(
                battle.OpponentMoney,
                Is.LessThan(GameplayConstants.Devil2Config.Devil2CircuitBreakerThreshold));
            battle.CurrentRound.RecordPlayerPokerEarnings(
                GameplayConstants.Devil2Config.Devil2PokerPayoutThreshold - 100);
            PlayPair(battle.CurrentRound, Rank.King);
            int publishedPokerPayout = -1;
            EventBus.Subscribe<MoneyTransferReasonEvent>(eventData =>
            {
                if (eventData.Reason == MoneyTransferReason.PokerPayout)
                    publishedPokerPayout = eventData.Delta;
            });
            int playerMoneyBeforePayout = battle.PlayerMoney;

            MoneyResolver.ResolvePlayerPokerPayout(battle, battle.CurrentRound, battle.CurrentRound.EffectiveWager);

            Assert.That(battle.PlayerMoney - playerMoneyBeforePayout, Is.EqualTo(100));
            Assert.That(publishedPokerPayout, Is.EqualTo(100));
            Assert.That(
                battle.CurrentRound.PlayerPokerEarnings,
                Is.EqualTo(GameplayConstants.Devil2Config.Devil2PokerPayoutThreshold));

            int playerMoneyAfterCap = battle.PlayerMoney;
            MoneyResolver.ResolvePlayerPokerPayout(battle, battle.CurrentRound, battle.CurrentRound.EffectiveWager);
            Assert.That(battle.PlayerMoney, Is.EqualTo(playerMoneyAfterCap));

            var nextRound = new RoundState(2, 21, 21, battle.GetDefaultWager(), false);
            int nextRoundPayout = ((IDevilPokerPayoutModifier)strategy).ModifyPlayerPokerPayout(
                battle,
                nextRound,
                260);
            Assert.That(nextRoundPayout, Is.EqualTo(260));
        }
        finally
        {
            battle.Dispose();
        }
    }

    [Test]
    public void PokerPayout_CircuitBreakerTracksLiveBalanceWithoutResettingRoundEarnings()
    {
        var strategy = new Devil2Strategy();
        int threshold = GameplayConstants.Devil2Config.Devil2CircuitBreakerThreshold;
        BattleState battle = CreateBattle(
            strategy,
            threshold + GameplayConstants.Devil2Config.Devil2Wager - 1);
        try
        {
            Assert.That(battle.StartRound(battle.GetDefaultWager()), Is.True);
            battle.CurrentRound.RecordPlayerPokerEarnings(
                GameplayConstants.Devil2Config.Devil2PokerPayoutThreshold);
            IDevilPokerPayoutModifier modifier = strategy;

            Assert.That(modifier.ModifyPlayerPokerPayout(battle, battle.CurrentRound, 260), Is.Zero);

            battle.AddOpponentMoney(threshold - battle.OpponentMoney);
            Assert.That(battle.OpponentMoney, Is.EqualTo(threshold));
            Assert.That(modifier.ModifyPlayerPokerPayout(battle, battle.CurrentRound, 260), Is.EqualTo(260));

            battle.LoseOpponentMoney(1);
            Assert.That(modifier.ModifyPlayerPokerPayout(battle, battle.CurrentRound, 260), Is.Zero);
        }
        finally
        {
            battle.Dispose();
        }
    }

    [Test]
    public void PokerPayout_PreviewMatchesActualCapWithoutConsumingAllowance()
    {
        var strategy = new Devil2Strategy();
        BattleState battle = CreateBattle(
            strategy,
            GameplayConstants.Devil2Config.Devil2CircuitBreakerThreshold
                + GameplayConstants.Devil2Config.Devil2Wager
                - 1);
        try
        {
            Assert.That(battle.StartRound(battle.GetDefaultWager()), Is.True);
            battle.CurrentRound.RecordPlayerPokerEarnings(
                GameplayConstants.Devil2Config.Devil2PokerPayoutThreshold - 100);
            PlayPair(battle.CurrentRound, Rank.King);
            ScoreResult score = ScoreResolver.Resolve(
                battle.CurrentRound.PlayerPlayedCards,
                battle.CurrentRound.ScoringModifiers,
                battle.CurrentRound.PlayerBurstThreshold);

            MoneyDeltaPreview preview = MoneyResolver.CalculatePlayerRealtimeMoneyPreview(
                battle,
                battle.CurrentRound,
                battle.CurrentRound.GetPokerCardsForPlayerPayout(),
                score);

            Assert.That(preview.PokerDelta, Is.EqualTo(100));
            Assert.That(preview.BurstDelta, Is.Zero);
            Assert.That(
                battle.CurrentRound.PlayerPokerEarnings,
                Is.EqualTo(GameplayConstants.Devil2Config.Devil2PokerPayoutThreshold - 100));

            int playerMoneyBeforePayout = battle.PlayerMoney;
            MoneyResolver.ResolvePlayerPokerPayout(battle, battle.CurrentRound, battle.CurrentRound.EffectiveWager);
            Assert.That(battle.PlayerMoney - playerMoneyBeforePayout, Is.EqualTo(preview.FinalDelta));
            Assert.That(
                battle.CurrentRound.PlayerPokerEarnings,
                Is.EqualTo(GameplayConstants.Devil2Config.Devil2PokerPayoutThreshold));
        }
        finally
        {
            battle.Dispose();
        }
    }

    [TestCase(-1, 0)]
    [TestCase(int.MaxValue, 260)]
    public void PokerPayout_ModifierResultIsClampedToValidTransferRange(int modifiedPayout, int expectedPayout)
    {
        BattleState battle = CreateBattle(new FixedPokerPayoutStrategy(modifiedPayout));
        try
        {
            Assert.That(battle.StartRound(battle.GetDefaultWager()), Is.True);
            PlayPair(battle.CurrentRound, Rank.King);
            int playerMoneyBeforePayout = battle.PlayerMoney;

            MoneyResolver.ResolvePlayerPokerPayout(battle, battle.CurrentRound, battle.CurrentRound.EffectiveWager);

            Assert.That(battle.PlayerMoney - playerMoneyBeforePayout, Is.EqualTo(expectedPayout));
            Assert.That(battle.CurrentRound.PlayerPokerEarnings, Is.EqualTo(expectedPayout));
        }
        finally
        {
            battle.Dispose();
        }
    }

    [Test]
    public void PokerPayout_EachAchievedRankPaysOnlyOncePerRound()
    {
        BattleState battle = CreateBattle(new BasicDevilStrategy());
        try
        {
            Assert.That(battle.StartRound(battle.GetDefaultWager()), Is.True);
            RoundState round = battle.CurrentRound;
            PlayPair(round, Rank.King);

            MoneyResolver.ResolvePlayerPokerPayout(battle, round, round.EffectiveWager);
            int moneyAfterPair = battle.PlayerMoney;
            int earningsAfterPair = round.PlayerPokerEarnings;

            Assert.That(earningsAfterPair, Is.GreaterThan(0));
            Assert.That(round.HasAchievedPlayerPokerHandRank(PokerHandRank.Pair), Is.True);

            Assert.That(round.TryPlayHitCard(new Card(Suit.Spades, Rank.Two)), Is.True);
            MoneyResolver.ResolvePlayerPokerPayout(battle, round, round.EffectiveWager);

            Assert.That(battle.PlayerMoney, Is.EqualTo(moneyAfterPair));
            Assert.That(round.PlayerPokerEarnings, Is.EqualTo(earningsAfterPair));

            Assert.That(round.TryPlayHitCard(new Card(Suit.Spades, Rank.King)), Is.True);
            MoneyResolver.ResolvePlayerPokerPayout(battle, round, round.EffectiveWager);

            Assert.That(battle.PlayerMoney, Is.GreaterThan(moneyAfterPair));
            Assert.That(round.HasAchievedPlayerPokerHandRank(PokerHandRank.ThreeOfAKind), Is.True);
            CollectionAssert.AreEquivalent(
                new[] { PokerHandRank.Pair, PokerHandRank.ThreeOfAKind },
                round.AchievedPlayerPokerHandRanks);
        }
        finally
        {
            battle.Dispose();
        }
    }

    [Test]
    public void PokerPayout_AchievedRanksResetForANewRoundState()
    {
        BattleState battle = CreateBattle(new BasicDevilStrategy());
        try
        {
            Assert.That(battle.StartRound(battle.GetDefaultWager()), Is.True);
            RoundState firstRound = battle.CurrentRound;
            PlayPair(firstRound, Rank.King);
            MoneyResolver.ResolvePlayerPokerPayout(battle, firstRound, firstRound.EffectiveWager);

            var nextRound = new RoundState(2, 21, 21, battle.GetDefaultWager(), false);
            PlayPair(nextRound, Rank.King);
            int moneyBeforeNextRoundPayout = battle.PlayerMoney;

            MoneyResolver.ResolvePlayerPokerPayout(battle, nextRound, battle.GetDefaultWager());

            Assert.That(nextRound.HasAchievedPlayerPokerHandRank(PokerHandRank.Pair), Is.True);
            Assert.That(battle.PlayerMoney, Is.GreaterThan(moneyBeforeNextRoundPayout));
        }
        finally
        {
            battle.Dispose();
        }
    }

    private static BattleState CreateBattle(IDevilStrategy strategy)
    {
        return CreateBattle(strategy, GameplayConstants.Devil2Config.Devil2StartingMoney);
    }

    private static BattleState CreateBattle(
        IDevilStrategy strategy,
        int opponentStartingMoney)
    {
        var run = new RunState(1, startingMoney: 2000000);
        return new BattleState(
            run,
            new BattleConfig(
                "devil2-test",
                opponentStartingMoney,
                strategy,
                baseWager: GameplayConstants.Devil2Config.Devil2Wager));
    }

    private static void UpdateResolvedRound(
        Devil2Strategy strategy,
        BattleState battle,
        int roundNumber,
        int wager,
        Combatant? winner)
    {
        var round = new RoundState(roundNumber, 21, 21, wager, true);
        strategy.UpdateDevilState(new DevilStateUpdateContext(
            battle,
            round,
            DevilStateUpdatePoint.RoundResolved,
            new RoundResolution(winner, 0, 0)));
    }

    private static int Inflate(int wager)
    {
        double result = wager * (double)GameplayConstants.Devil2Config.Devil2WagerInflationRate;
        return result >= int.MaxValue ? int.MaxValue : (int)result;
    }

    private static void PlayPair(RoundState round, Rank rank)
    {
        Assert.That(round.TryPlayHitCard(new Card(Suit.Clubs, rank)), Is.True);
        Assert.That(round.TryPlayHitCard(new Card(Suit.Hearts, rank)), Is.True);
    }

    private static void PlayRoyalFlush(RoundState round)
    {
        Assert.That(round.TryPlayHitCard(new Card(Suit.Spades, Rank.Ten)), Is.True);
        Assert.That(round.TryPlayHitCard(new Card(Suit.Spades, Rank.Jack)), Is.True);
        Assert.That(round.TryPlayHitCard(new Card(Suit.Spades, Rank.Queen)), Is.True);
        Assert.That(round.TryPlayHitCard(new Card(Suit.Spades, Rank.King)), Is.True);
        Assert.That(round.TryPlayHitCard(new Card(Suit.Spades, Rank.Ace)), Is.True);
    }

    private sealed class InvalidWagerStrategy : BasicDevilStrategy, IDevilRoundWagerModifier
    {
        public int GetRoundWager(BattleState battle, int baseWager) => int.MinValue;
    }

    private sealed class FixedPokerPayoutStrategy : BasicDevilStrategy, IDevilPokerPayoutModifier
    {
        private readonly int modifiedPayout;

        public FixedPokerPayoutStrategy(int modifiedPayout)
        {
            this.modifiedPayout = modifiedPayout;
        }

        public int ModifyPlayerPokerPayout(BattleState battle, RoundState round, int proposedPayout)
        {
            return modifiedPayout;
        }
    }
}
#endif
