#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class BurstDenyAndStockFeatureTests
{
    private Assembly runtimeAssembly;
    private Type runStateType;
    private Type battleStateType;
    private Type battleConfigType;
    private Type activeItemResolverType;
    private Type activeItemRuntimeStateType;

    [SetUp]
    public void SetUp()
    {
        runtimeAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => assembly.GetName().Name == "Assembly-CSharp");
        Assert.That(runtimeAssembly, Is.Not.Null);

        runStateType = RequireType("RunState");
        battleStateType = RequireType("BattleState");
        battleConfigType = RequireType("BattleConfig");
        activeItemResolverType = RequireType("ActiveItemResolver");
        activeItemRuntimeStateType = RequireType("ActiveItemRuntimeState");
    }

    [Test]
    public void StockResolver_CoversAllowedStepsClampsAndIntegerRounding()
    {
        Type resolver = RequireType("StockValueResolver");
        MethodInfo apply = resolver.GetMethod("ApplyChangeStep", BindingFlags.Public | BindingFlags.Static);
        MethodInfo calculate = resolver.GetMethod("CalculateCurrentAmount", BindingFlags.Public | BindingFlags.Static);
        int[] expected = { 60, 70, 80, 90, 100, 110, 120, 130, 140 };

        for (int step = -4; step <= 4; step++)
            Assert.That((int)apply.Invoke(null, new object[] { 100, step }), Is.EqualTo(expected[step + 4]));

        Assert.That((int)apply.Invoke(null, new object[] { 10, -4 }), Is.EqualTo(10));
        Assert.That((int)apply.Invoke(null, new object[] { 200, 4 }), Is.EqualTo(200));
        Assert.That((int)calculate.Invoke(null, new object[] { 15, 110 }), Is.EqualTo(16));
        Assert.That((int)calculate.Invoke(null, new object[] { 1, 10 }), Is.EqualTo(1));
    }

    [Test]
    public void StockBuy_CapsLiveMoneyAndTracksTheExactSlot()
    {
        object run = CreateRun(11, 100);
        string stockBuy = GetItemId("StockBuy");
        Invoke(run, "AddActiveItem", stockBuy);
        Invoke(run, "AddActiveItem", stockBuy);
        object battle = CreateStartedBattle(run, 100);

        try
        {
            object[] beginArguments = { 1, null };
            object result = battleStateType.GetMethod("TryBeginActiveItemUseAtSlot").Invoke(battle, beginArguments);
            Assert.That(result.ToString(), Is.EqualTo("MoneyInputRequired"));
            Assert.That((int)GetProperty(beginArguments[1], "MaximumAmount"), Is.EqualTo(90));
            Assert.That((bool)Invoke(battle, "TryCompletePendingActiveItemMoneyUse", 999), Is.True);

            Assert.That((int)GetProperty(run, "Money"), Is.Zero);
            IList ids = GetList(run, "ActiveItemIds");
            Assert.That(ids[0], Is.EqualTo(stockBuy));
            Assert.That(ids[1], Is.EqualTo(GetItemId("StockSell")));
            object holding = GetList(run, "ActiveItemStates")[1];
            Assert.That((int)GetProperty(holding, "StockOriginalAmount"), Is.EqualTo(90));
            Assert.That((int)GetProperty(holding, "StockPricePercent"), Is.EqualTo(100));
        }
        finally
        {
            Invoke(battle, "Dispose");
        }
    }

    [Test]
    public void IndependentHoldings_SellOnlyTheSelectedSlotAndRoundTripThroughSaveData()
    {
        object run = CreateRun(12, 200);
        string stockBuy = GetItemId("StockBuy");
        Invoke(run, "AddActiveItem", stockBuy);
        Invoke(run, "AddActiveItem", stockBuy);
        object battle = CreateStartedBattle(run, 100);

        try
        {
            BeginMoneyUse(battle, 1);
            Assert.That((bool)Invoke(battle, "TryCompletePendingActiveItemMoneyUse", 30), Is.True);
            BeginMoneyUse(battle, 0);
            Assert.That((bool)Invoke(battle, "TryCompletePendingActiveItemMoneyUse", 50), Is.True);

            IList states = GetList(run, "ActiveItemStates");
            Assert.That((int)GetProperty(states[0], "StockOriginalAmount"), Is.EqualTo(50));
            Assert.That((int)GetProperty(states[1], "StockOriginalAmount"), Is.EqualTo(30));
            Assert.That((bool)Invoke(battle, "TryUseActiveItemAtSlot", 1), Is.True);
            Assert.That((int)GetProperty(run, "Money"), Is.EqualTo(140));
            Assert.That(GetList(run, "ActiveItemIds")[0], Is.EqualTo(GetItemId("StockSell")));
            Assert.That(GetList(run, "ActiveItemIds")[1], Is.Null);

            object data = Invoke(run, "ToData");
            object loaded = runStateType.GetMethod("FromData", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new[] { data, (object)100 });
            object loadedHolding = GetList(loaded, "ActiveItemStates")[0];
            Assert.That((int)GetProperty(loadedHolding, "StockOriginalAmount"), Is.EqualTo(50));
            Assert.That((int)GetProperty(loadedHolding, "StockPricePercent"), Is.EqualTo(100));
        }
        finally
        {
            Invoke(battle, "Dispose");
        }
    }

    [Test]
    public void PurchasedStock_FirstFluctuatesAtTheFollowingRoundStart()
    {
        object run = CreateRun(14, 300);
        Invoke(run, "AddActiveItem", GetItemId("StockBuy"));
        object battle = CreateStartedBattle(run, 100);

        try
        {
            BeginMoneyUse(battle, 0);
            Assert.That((bool)Invoke(battle, "TryCompletePendingActiveItemMoneyUse", 50), Is.True);
            object holding = GetList(run, "ActiveItemStates")[0];
            Assert.That((int)GetProperty(holding, "StockPricePercent"), Is.EqualTo(100));

            Invoke(battle, "CleanupRound");
            Assert.That((bool)Invoke(battle, "StartRound", 10), Is.True);
            holding = GetList(run, "ActiveItemStates")[0];
            int pricePercent = (int)GetProperty(holding, "StockPricePercent");
            Assert.That(pricePercent, Is.InRange(60, 140));
            Assert.That(pricePercent % 10, Is.Zero);
        }
        finally
        {
            Invoke(battle, "Dispose");
        }
    }

    [Test]
    public void BurstDeny_SuppressesOnlyFuturePlayerPenaltiesAndResetsNextRound()
    {
        object run = CreateRun(17, 500);
        Invoke(run, "AddActiveItem", GetItemId("BurstDeny"));
        object battle = CreateStartedBattle(run, 500);

        try
        {
            object round = GetProperty(battle, "CurrentRound");
            SetScores(round, 25, true, 15, false);
            ResolveBurstTransfers(battle, round);
            int afterExistingPenalty = (int)GetProperty(run, "Money");

            Assert.That((bool)Invoke(battle, "TryUseActiveItemAtSlot", 0), Is.True);
            ResolveBurstTransfers(battle, round);
            Assert.That((int)GetProperty(run, "Money"), Is.EqualTo(afterExistingPenalty));

            SetScores(round, 15, false, 22, true);
            ResolveBurstTransfers(battle, round);
            Assert.That((int)GetProperty(run, "Money"), Is.EqualTo(afterExistingPenalty + 50));

            Invoke(battle, "CleanupRound");
            Assert.That((bool)Invoke(battle, "StartRound", 10), Is.True);
            Assert.That((bool)GetProperty(GetProperty(battle, "CurrentRound"), "PlayerBurstPenaltySuppressed"), Is.False);
        }
        finally
        {
            Invoke(battle, "Dispose");
        }
    }

    [Test]
    public void MoneyInputPanel_ClampsAndCancelsWithoutConfirmation()
    {
        Type panelType = RequireType("MoneyInputPanel");
        var root = new GameObject("MoneyInputPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        try
        {
            Component panel = root.AddComponent(panelType);
            int confirmed = 0;
            int cancelled = 0;
            panelType.GetMethod("Show").Invoke(panel, new object[]
            {
                50,
                (Action<int>)(amount => confirmed = amount),
                (Action)(() => cancelled++)
            });

            Component input = FindComponent(root.transform, "AmountInput", "TMP_InputField");
            Button confirmButton = Find<Button>(root.transform, "ConfirmButton");
            PropertyInfo textProperty = input.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
            textProperty.SetValue(input, "999");
            Assert.That(textProperty.GetValue(input), Is.EqualTo("50"));
            confirmButton.onClick.Invoke();
            Assert.That(confirmed, Is.EqualTo(50));

            panelType.GetMethod("Show").Invoke(panel, new object[]
            {
                25,
                (Action<int>)(amount => confirmed = amount),
                (Action)(() => cancelled++)
            });
            Assert.That((bool)panelType.GetMethod("Cancel").Invoke(panel, null), Is.True);
            Assert.That(cancelled, Is.EqualTo(1));
            Assert.That(confirmed, Is.EqualTo(50));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void AuthoredCatalogs_ExposeBuyAndBurstButNeverSellStockInShop()
    {
        Type shopCatalogType = RequireType("ShopCatalog");
        Type tooltipCatalogType = RequireType("TooltipContentCatalog");
        UnityEngine.Object shop = AssetDatabase.LoadAssetAtPath(
            "Assets/Content/ShopCatalog.asset",
            shopCatalogType);
        UnityEngine.Object tooltips = AssetDatabase.LoadAssetAtPath(
            "Assets/Content/TooltipContentCatalog.asset",
            tooltipCatalogType);

        Assert.That(TryGetDefinition(shop, GetItemId("BurstDeny")), Is.True);
        Assert.That(TryGetDefinition(shop, GetItemId("StockBuy")), Is.True);
        Assert.That(TryGetDefinition(shop, GetItemId("StockSell")), Is.False);
        Assert.That(TryGetDefinition(tooltips, GetItemId("BurstDeny")), Is.True);
        Assert.That(TryGetDefinition(tooltips, GetItemId("StockBuy")), Is.True);
        Assert.That(TryGetDefinition(tooltips, GetItemId("StockSell")), Is.True);
    }

    private object CreateRun(int seed, int money)
    {
        return Activator.CreateInstance(runStateType, new object[] { seed, money, 3 });
    }

    private object CreateStartedBattle(object run, int opponentMoney)
    {
        object config = Activator.CreateInstance(
            battleConfigType,
            new object[] { "active-item-test", opponentMoney, null, 3, 21, 10, null, null, null, null });
        object battle = Activator.CreateInstance(battleStateType, run, config);
        Assert.That((bool)Invoke(battle, "StartRound", 10), Is.True);
        return battle;
    }

    private void BeginMoneyUse(object battle, int slotIndex)
    {
        object[] arguments = { slotIndex, null };
        object result = battleStateType.GetMethod("TryBeginActiveItemUseAtSlot").Invoke(battle, arguments);
        Assert.That(result.ToString(), Is.EqualTo("MoneyInputRequired"));
    }

    private void SetScores(object round, int playerScore, bool playerBurst, int opponentScore, bool opponentBurst)
    {
        Type scoreType = RequireType("ScoreResult");
        Type pokerRankType = RequireType("PokerHandRank");
        object highCard = Enum.Parse(pokerRankType, "HighCard");
        object player = Activator.CreateInstance(
            scoreType,
            new object[] { playerScore, playerScore, highCard, 1f, playerBurst, false });
        object opponent = Activator.CreateInstance(
            scoreType,
            new object[] { opponentScore, opponentScore, highCard, 1f, opponentBurst, false });
        Invoke(round, "SetScores", player, opponent);
    }

    private void ResolveBurstTransfers(object battle, object round)
    {
        RequireType("MoneyResolver").GetMethod("ResolveBurstTransfers", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new[] { battle, round, (object)10 });
    }

    private bool TryGetDefinition(UnityEngine.Object catalog, string itemId)
    {
        object[] arguments = { itemId, null };
        return (bool)catalog.GetType().GetMethod("TryGetDefinition").Invoke(catalog, arguments);
    }

    private string GetItemId(string fieldName)
    {
        return (string)activeItemResolverType.GetField(fieldName, BindingFlags.Public | BindingFlags.Static).GetValue(null);
    }

    private Type RequireType(string name)
    {
        Type type = runtimeAssembly.GetType(name);
        Assert.That(type, Is.Not.Null, name);
        return type;
    }

    private static object Invoke(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(method, Is.Not.Null, methodName);
        return method.Invoke(target, arguments);
    }

    private static object GetProperty(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(property, Is.Not.Null, propertyName);
        return property.GetValue(target);
    }

    private static IList GetList(object target, string propertyName)
    {
        return (IList)GetProperty(target, propertyName);
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

    private static Component FindComponent(Transform root, string objectName, string typeName)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i].name != objectName)
                continue;

            Component[] components = transforms[i].GetComponents<Component>();
            for (int j = 0; j < components.Length; j++)
            {
                if (components[j] != null && components[j].GetType().Name == typeName)
                    return components[j];
            }
        }

        return null;
    }
}
#endif