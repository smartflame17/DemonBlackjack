#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BurstDenyAndStockTests
{
    [TestCase(-4, 60)]
    [TestCase(-3, 70)]
    [TestCase(-2, 80)]
    [TestCase(-1, 90)]
    [TestCase(0, 100)]
    [TestCase(1, 110)]
    [TestCase(2, 120)]
    [TestCase(3, 130)]
    [TestCase(4, 140)]
    public void StockValueResolver_AppliesEveryAllowedChangeStep(int changeStep, int expectedPercent)
    {
        Assert.That(StockValueResolver.ApplyChangeStep(100, changeStep), Is.EqualTo(expectedPercent));
    }

    [Test]
    public void StockValueResolver_ClampsAndFloorsWithPositiveMinimum()
    {
        Assert.That(StockValueResolver.ApplyChangeStep(10, -4), Is.EqualTo(10));
        Assert.That(StockValueResolver.ApplyChangeStep(200, 4), Is.EqualTo(200));
        Assert.That(StockValueResolver.CalculateCurrentAmount(15, 110), Is.EqualTo(16));
        Assert.That(StockValueResolver.CalculateCurrentAmount(1, 10), Is.EqualTo(1));
    }

    [Test]
    public void StockBuy_CapsToLiveMoneyAndReplacesExactSlot()
    {
        var run = new RunState(11, startingMoney: 100);
        run.AddActiveItem(ActiveItemResolver.StockBuy);
        run.AddActiveItem(ActiveItemResolver.StockBuy);
        var battle = new BattleState(run, new BattleConfig("stock", 100, baseWager: 10));
        battle.StartRound(10);

        Assert.That(
            battle.TryBeginActiveItemUseAtSlot(1, out ActiveItemUseRequest request),
            Is.EqualTo(ActiveItemUseStartResult.MoneyInputRequired));
        Assert.That(request.MaximumAmount, Is.EqualTo(90));
        Assert.That(battle.TryCompletePendingActiveItemMoneyUse(999), Is.True);

        Assert.That(run.Money, Is.Zero);
        Assert.That(run.ActiveItemIds[0], Is.EqualTo(ActiveItemResolver.StockBuy));
        Assert.That(run.TryGetActiveItemAt(1, out ActiveItemRuntimeState holding), Is.True);
        Assert.That(holding.ItemId, Is.EqualTo(ActiveItemResolver.StockSell));
        Assert.That(holding.StockOriginalAmount, Is.EqualTo(90));
        Assert.That(holding.StockPricePercent, Is.EqualTo(100));
        battle.Dispose();
    }

    [Test]
    public void MultipleStockHoldings_KeepIndependentStateAndSellExactSlot()
    {
        var run = new RunState(12, startingMoney: 200);
        run.AddActiveItem(ActiveItemResolver.StockBuy);
        run.AddActiveItem(ActiveItemResolver.StockBuy);
        var battle = new BattleState(run, new BattleConfig("stock", 100, baseWager: 10));
        battle.StartRound(10);

        Assert.That(battle.TryBeginActiveItemUseAtSlot(1, out _), Is.EqualTo(ActiveItemUseStartResult.MoneyInputRequired));
        Assert.That(battle.TryCompletePendingActiveItemMoneyUse(30), Is.True);
        Assert.That(battle.TryBeginActiveItemUseAtSlot(0, out _), Is.EqualTo(ActiveItemUseStartResult.MoneyInputRequired));
        Assert.That(battle.TryCompletePendingActiveItemMoneyUse(50), Is.True);

        Assert.That(run.ActiveItemStates[0].StockOriginalAmount, Is.EqualTo(50));
        Assert.That(run.ActiveItemStates[1].StockOriginalAmount, Is.EqualTo(30));
        Assert.That(battle.TryUseActiveItemAtSlot(1), Is.True);
        Assert.That(run.Money, Is.EqualTo(140));
        Assert.That(run.ActiveItemIds[0], Is.EqualTo(ActiveItemResolver.StockSell));
        Assert.That(run.ActiveItemIds[1], Is.Null);
        battle.Dispose();
    }

    [Test]
    public void StockHoldings_FluctuateIndependentlyAtRoundStartInSlotOrder()
    {
        var run = new RunState(13, startingMoney: 500);
        run.AddActiveItem(ActiveItemResolver.StockBuy);
        run.AddActiveItem(ActiveItemResolver.StockBuy);
        run.TryReplaceActiveItemAt(0, ActiveItemResolver.StockBuy, ActiveItemRuntimeState.CreateStockHolding(100));
        run.TryReplaceActiveItemAt(1, ActiveItemResolver.StockBuy, ActiveItemRuntimeState.CreateStockHolding(200));
        var battle = new BattleState(run, new BattleConfig("stock", 100, baseWager: 10));
        var expectedRandom = new ReplayableRandom(battle.BattleSeed);
        int expectedFirst = StockValueResolver.ApplyChangeStep(100, expectedRandom.Next(-4, 5));
        int expectedSecond = StockValueResolver.ApplyChangeStep(100, expectedRandom.Next(-4, 5));

        battle.StartRound(10);

        Assert.That(run.ActiveItemStates[0].StockPricePercent, Is.EqualTo(expectedFirst));
        Assert.That(run.ActiveItemStates[1].StockPricePercent, Is.EqualTo(expectedSecond));
        battle.Dispose();
    }

    [Test]
    public void NewlyPurchasedStock_FirstFluctuatesAtFollowingRoundStart()
    {
        var run = new RunState(14, startingMoney: 300);
        run.AddActiveItem(ActiveItemResolver.StockBuy);
        var battle = new BattleState(run, new BattleConfig("stock", 100, baseWager: 10));
        battle.StartRound(10);
        Assert.That(battle.TryBeginActiveItemUseAtSlot(0, out _), Is.EqualTo(ActiveItemUseStartResult.MoneyInputRequired));
        Assert.That(battle.TryCompletePendingActiveItemMoneyUse(50), Is.True);
        Assert.That(run.ActiveItemStates[0].StockPricePercent, Is.EqualTo(100));

        var expectedRandom = new ReplayableRandom(battle.BattleSeed);
        int expectedPercent = StockValueResolver.ApplyChangeStep(100, expectedRandom.Next(-4, 5));
        battle.CleanupRound();
        battle.StartRound(10);

        Assert.That(run.ActiveItemStates[0].StockPricePercent, Is.EqualTo(expectedPercent));
        battle.Dispose();
    }

    [Test]
    public void StockState_RoundTripsAndLegacyIdOnlyItemsStillLoad()
    {
        var run = new RunState(15);
        run.AddActiveItem(ActiveItemResolver.StockBuy);
        run.TryReplaceActiveItemAt(
            0,
            ActiveItemResolver.StockBuy,
            ActiveItemRuntimeState.CreateStockHolding(75, 160));

        RunState loaded = RunState.FromData(run.ToData());
        Assert.That(loaded.TryGetActiveItemAt(0, out ActiveItemRuntimeState holding), Is.True);
        Assert.That(holding.StockOriginalAmount, Is.EqualTo(75));
        Assert.That(holding.StockPricePercent, Is.EqualTo(160));
        Assert.That(holding.StockCurrentAmount, Is.EqualTo(120));

        var legacy = new RunStateData
        {
            seed = 16,
            money = 100,
            maxActiveItemSlots = 3,
            activeItemIds = new System.Collections.Generic.List<string> { ActiveItemResolver.DrawThree, null, null },
            activeItemStates = null
        };
        RunState loadedLegacy = RunState.FromData(legacy);
        Assert.That(loadedLegacy.ActiveItemIds[0], Is.EqualTo(ActiveItemResolver.DrawThree));
        Assert.That(loadedLegacy.ActiveItemStates[0].ItemId, Is.EqualTo(ActiveItemResolver.DrawThree));
    }

    [Test]
    public void BurstDeny_StopsFuturePlayerPenaltiesButPreservesOpponentPayoutsAndResets()
    {
        var run = new RunState(17, startingMoney: 500);
        run.AddActiveItem(ActiveItemResolver.BurstDeny);
        var battle = new BattleState(run, new BattleConfig("burst", 500, baseWager: 10));
        battle.StartRound(10);
        RoundState round = battle.CurrentRound;

        round.SetScores(
            new ScoreResult(25, 25, PokerHandRank.HighCard, 1f, true, false),
            new ScoreResult(15, 15, PokerHandRank.HighCard, 1f, false, false));
        MoneyResolver.ResolveBurstTransfers(battle, round, 10);
        int afterExistingPenalty = run.Money;

        Assert.That(battle.TryUseActiveItemAtSlot(0), Is.True);
        MoneyResolver.ResolveBurstTransfers(battle, round, 10);
        Assert.That(run.Money, Is.EqualTo(afterExistingPenalty));

        round.SetScores(
            new ScoreResult(15, 15, PokerHandRank.HighCard, 1f, false, false),
            new ScoreResult(22, 22, PokerHandRank.HighCard, 1f, true, false));
        MoneyResolver.ResolveBurstTransfers(battle, round, 10);
        Assert.That(run.Money, Is.EqualTo(afterExistingPenalty + 50));

        battle.CleanupRound();
        battle.StartRound(10);
        Assert.That(battle.CurrentRound.PlayerBurstPenaltySuppressed, Is.False);
        battle.Dispose();
    }

    [Test]
    public void TooltipFormatter_UsesPerHoldingValuesWithoutMutatingTemplate()
    {
        const string template = "원금 {stock_original_amount} / 현재 {stock_current_amount}";
        ActiveItemRuntimeState holding = ActiveItemRuntimeState.CreateStockHolding(75, 160);

        string formatted = ActiveItemTooltipFormatter.Format(template, holding);

        Assert.That(formatted, Does.Contain("75"));
        Assert.That(formatted, Does.Contain("120"));
        Assert.That(formatted, Does.Not.Contain("{stock_"));
        Assert.That(template, Does.Contain("{stock_original_amount}"));
    }

    [Test]
    public void MoneyInputPanel_ClampsConfirmationAndCancellationDoesNotConfirm()
    {
        GameObject root = new("MoneyInputPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        try
        {
            MoneyInputPanel panel = root.AddComponent<MoneyInputPanel>();
            int confirmed = 0;
            int cancelled = 0;
            panel.Show(50, amount => confirmed = amount, () => cancelled++);

            TMP_InputField input = Find<TMP_InputField>(root.transform, "AmountInput");
            Button confirm = Find<Button>(root.transform, "ConfirmButton");
            input.text = "999";
            Assert.That(input.text, Is.EqualTo("50"));
            confirm.onClick.Invoke();
            Assert.That(confirmed, Is.EqualTo(50));

            panel.Show(25, amount => confirmed = amount, () => cancelled++);
            Assert.That(panel.Cancel(), Is.True);
            Assert.That(cancelled, Is.EqualTo(1));
            Assert.That(confirmed, Is.EqualTo(50));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static T Find<T>(Transform root, string objectName) where T : Component
    {
        T[] components = root.GetComponentsInChildren<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i].name == objectName)
                return components[i];
        }
        return null;
    }
}
#endif