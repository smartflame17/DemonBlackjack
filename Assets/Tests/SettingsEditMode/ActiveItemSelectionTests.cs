#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class ActiveItemSelectionTests
{
    private Assembly runtimeAssembly;
    private Type runStateType;
    private Type battleConfigType;
    private Type battleStateType;
    private Type cardType;
    private Type suitType;
    private Type rankType;
    private Type resolverType;

    [SetUp]
    public void SetUp()
    {
        runtimeAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => assembly.GetName().Name == "Assembly-CSharp");
        Assert.That(runtimeAssembly, Is.Not.Null);

        runStateType = RequireType("RunState");
        battleConfigType = RequireType("BattleConfig");
        battleStateType = RequireType("BattleState");
        cardType = RequireType("Card");
        suitType = RequireType("Suit");
        rankType = RequireType("Rank");
        resolverType = RequireType("ActiveItemResolver");
    }

    [Test]
    public void SelectionItems_RequireAPlayerFieldCardAndBeginWithoutMutation()
    {
        object run = CreateRun();
        string discardItemId = GetItemId("RejectLastHit");
        string returnItemId = GetItemId("ReturnPlayerFieldCardToHand");
        Invoke(run, "AddActiveItem", discardItemId);
        Invoke(run, "AddActiveItem", returnItemId);
        object battle = CreateStartedBattle(run);

        try
        {
            Assert.That((bool)Invoke(battle, "CanUseActiveItem", discardItemId), Is.False);
            Assert.That((bool)Invoke(battle, "CanUseActiveItem", returnItemId), Is.False);

            object round = GetProperty(battle, "CurrentRound");
            Invoke(round, "TryPlayHitCard", CreateCard("Hearts", "Four"));
            Assert.That((bool)Invoke(battle, "CanUseActiveItem", discardItemId), Is.True);
            Assert.That((bool)Invoke(battle, "CanUseActiveItem", returnItemId), Is.True);

            object[] beginArguments = { discardItemId, null };
            object result = battleStateType.GetMethod("TryBeginActiveItemUse").Invoke(battle, beginArguments);
            Assert.That(result.ToString(), Is.EqualTo("SelectionRequired"));
            Assert.That(GetList(round, "PlayerPlayedCards"), Has.Count.EqualTo(1));
            Assert.That((bool)Invoke(run, "HasActiveItem", discardItemId), Is.True);
            Assert.That((bool)GetProperty(battle, "HasPendingActiveItemSelection"), Is.True);

            object request = beginArguments[1];
            IList snapshot = (IList)GetProperty(request, "Cards");
            Assert.That(snapshot, Has.Count.EqualTo(1));
            Assert.That(snapshot.IsReadOnly, Is.True);

            object[] overlappingArguments = { returnItemId, null };
            object overlappingResult = battleStateType.GetMethod("TryBeginActiveItemUse").Invoke(battle, overlappingArguments);
            Assert.That(overlappingResult.ToString(), Is.EqualTo("Rejected"));

            Assert.That((bool)Invoke(battle, "CancelPendingActiveItemUse"), Is.True);
            Assert.That((bool)GetProperty(battle, "HasPendingActiveItemSelection"), Is.False);
            Assert.That((bool)Invoke(run, "HasActiveItem", discardItemId), Is.True);
        }
        finally
        {
            Invoke(battle, "Dispose");
        }
    }

    [Test]
    public void RejectLastHit_CompletionUsesOriginalNonLastIndex()
    {
        object run = CreateRun();
        string itemId = GetItemId("RejectLastHit");
        Invoke(run, "AddActiveItem", itemId);
        object battle = CreateStartedBattle(run);

        try
        {
            object round = GetProperty(battle, "CurrentRound");
            object first = CreateCard("Hearts", "Four");
            object selected = CreateCard("Spades", "Seven");
            object duplicate = CreateCard("Hearts", "Four");
            Invoke(round, "TryPlayHitCard", first);
            Invoke(round, "TryPlayHitCard", selected);
            Invoke(round, "TryPlayHitCard", duplicate);

            BeginSelection(battle, itemId);
            Assert.That((bool)Invoke(battle, "TryCompletePendingActiveItemUse", new[] { 1 }), Is.True);

            IList playedCards = GetList(round, "PlayerPlayedCards");
            IList discardPile = GetList(battle, "PlayerDiscardPile");
            Assert.That(playedCards, Has.Count.EqualTo(2));
            Assert.That(playedCards[0].ToString(), Is.EqualTo(first.ToString()));
            Assert.That(playedCards[1].ToString(), Is.EqualTo(duplicate.ToString()));
            Assert.That(discardPile[discardPile.Count - 1].ToString(), Is.EqualTo(selected.ToString()));
            Assert.That((bool)Invoke(run, "HasActiveItem", itemId), Is.False);
            Assert.That((bool)GetProperty(battle, "HasPendingActiveItemSelection"), Is.False);
        }
        finally
        {
            Invoke(battle, "Dispose");
        }
    }

    [Test]
    public void ReturnToHand_CompletionUsesOriginalIndexAndInvalidOrStaleResultsRetainItem()
    {
        object run = CreateRun();
        string itemId = GetItemId("ReturnPlayerFieldCardToHand");
        Invoke(run, "AddActiveItem", itemId);
        object battle = CreateStartedBattle(run);

        try
        {
            object round = GetProperty(battle, "CurrentRound");
            object first = CreateCard("Clubs", "Five");
            object second = CreateCard("Diamonds", "Nine");
            Invoke(round, "TryPlayHitCard", first);
            Invoke(round, "TryPlayHitCard", second);

            int handBefore = GetList(round, "PlayerHand").Count;
            BeginSelection(battle, itemId);
            Assert.That((bool)Invoke(battle, "TryCompletePendingActiveItemUse", new[] { 99 }), Is.False);
            Assert.That((bool)Invoke(run, "HasActiveItem", itemId), Is.True);
            Assert.That((bool)GetProperty(battle, "HasPendingActiveItemSelection"), Is.False);

            BeginSelection(battle, itemId);
            Invoke(round, "TryPlayHitCard", CreateCard("Spades", "Two"));
            Assert.That((bool)Invoke(battle, "TryCompletePendingActiveItemUse", new[] { 0 }), Is.False);
            Assert.That((bool)Invoke(run, "HasActiveItem", itemId), Is.True);

            BeginSelection(battle, itemId);
            Assert.That((bool)Invoke(battle, "TryCompletePendingActiveItemUse", new[] { 0 }), Is.True);
            Assert.That(GetList(round, "PlayerHand"), Has.Count.EqualTo(handBefore + 1));
            Assert.That(GetList(round, "PlayerPlayedCards")[0].ToString(), Is.EqualTo(second.ToString()));
            Assert.That((bool)Invoke(run, "HasActiveItem", itemId), Is.False);
        }
        finally
        {
            Invoke(battle, "Dispose");
        }
    }

    [Test]
    public void ActiveItemUseProxy_AppliesImmediateItemsAndRejectsMissingSelectionUiWithoutPendingState()
    {
        Type controllerType = RequireType("BattleController");
        Type proxyType = RequireType("ActiveItemUseProxy");
        GameObject root = new("ActiveItemUseProxyFixture");

        try
        {
            Component controller = root.AddComponent(controllerType);
            Component proxy = root.AddComponent(proxyType);
            object run = CreateRun();
            string immediateItemId = GetItemId("BurstThresholdPlusThree");
            string selectionItemId = GetItemId("RejectLastHit");
            Invoke(run, "AddActiveItem", immediateItemId);
            Invoke(run, "AddActiveItem", selectionItemId);

            object config = CreateBattleConfig();
            controllerType.GetMethod("InitializeBattle").Invoke(controller, new[] { run, config });
            object battle = GetProperty(controller, "BattleState");
            int wager = (int)Invoke(battle, "GetDefaultWager");
            Assert.That((bool)controllerType.GetMethod("StartNextRound").Invoke(controller, new object[] { wager }), Is.True);

            SetField(proxy, "battleController", controller);
            MethodInfo use = proxyType.GetMethod("TryUseActiveItem");
            Assert.That((bool)use.Invoke(proxy, new object[] { immediateItemId }), Is.True);
            Assert.That((bool)Invoke(run, "HasActiveItem", immediateItemId), Is.False);

            object round = GetProperty(battle, "CurrentRound");
            Invoke(round, "TryPlayHitCard", CreateCard("Hearts", "Six"));
            Assert.That((bool)use.Invoke(proxy, new object[] { selectionItemId }), Is.False);
            Assert.That((bool)Invoke(run, "HasActiveItem", selectionItemId), Is.True);
            Assert.That((bool)GetProperty(battle, "HasPendingActiveItemSelection"), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void RoundCleanup_CancelsSelectionAndRetainsItem()
    {
        object run = CreateRun();
        string itemId = GetItemId("RejectLastHit");
        Invoke(run, "AddActiveItem", itemId);
        object battle = CreateStartedBattle(run);

        try
        {
            object round = GetProperty(battle, "CurrentRound");
            Invoke(round, "TryPlayHitCard", CreateCard("Clubs", "Six"));
            BeginSelection(battle, itemId);

            Invoke(battle, "CleanupRound");

            Assert.That((bool)GetProperty(battle, "HasPendingActiveItemSelection"), Is.False);
            Assert.That((bool)Invoke(run, "HasActiveItem", itemId), Is.True);
            Assert.That((bool)Invoke(battle, "TryCompletePendingActiveItemUse", new[] { 0 }), Is.False);
        }
        finally
        {
            Invoke(battle, "Dispose");
        }
    }

    private object CreateRun()
    {
        return Activator.CreateInstance(runStateType, new object[] { 41, 100, 3 });
    }

    private object CreateStartedBattle(object run)
    {
        object battle = Activator.CreateInstance(battleStateType, run, CreateBattleConfig());
        Assert.That((bool)Invoke(battle, "StartRound", 10), Is.True);
        return battle;
    }

    private object CreateBattleConfig()
    {
        return Activator.CreateInstance(
            battleConfigType,
            new object[] { "selection-test", 100, null, 3, 21, 10, null, null, null, null });
    }

    private object CreateCard(string suit, string rank)
    {
        object suitValue = Enum.Parse(suitType, suit);
        object rankValue = Enum.Parse(rankType, rank);
        return Activator.CreateInstance(cardType, suitValue, rankValue, null);
    }

    private void BeginSelection(object battle, string itemId)
    {
        object[] arguments = { itemId, null };
        object result = battleStateType.GetMethod("TryBeginActiveItemUse").Invoke(battle, arguments);
        Assert.That(result.ToString(), Is.EqualTo("SelectionRequired"));
    }

    private string GetItemId(string fieldName)
    {
        return (string)resolverType.GetField(fieldName, BindingFlags.Public | BindingFlags.Static).GetValue(null);
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

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }
}
#endif