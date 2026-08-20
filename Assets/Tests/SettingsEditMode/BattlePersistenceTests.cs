#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class BattlePersistenceTests
{
    private Assembly runtimeAssembly;
    private Type battleConfigType;
    private Type battleStateType;
    private Type battlePhaseType;
    private Type gameDataType;
    private Type runStateType;

    [SetUp]
    public void SetUp()
    {
        runtimeAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => assembly.GetName().Name == "Assembly-CSharp");
        Assert.That(runtimeAssembly, Is.Not.Null);

        battleConfigType = RequireType("BattleConfig");
        battleStateType = RequireType("BattleState");
        battlePhaseType = RequireType("BattlePhase");
        gameDataType = RequireType("GameData");
        runStateType = RequireType("RunState");
        InvokeStatic(RequireType("EventBus"), "Clear");
    }

    [TearDown]
    public void TearDown()
    {
        InvokeStatic(RequireType("EventBus"), "Clear");
    }

    [Test]
    public void ResolvedBattle_JsonRoundTrip_RestoresExactPostRoundSnapshot()
    {
        object run = CreateRun(8128);
        object battle = CreateResolvedBattle(run);
        object restored = null;

        try
        {
            object battleData = CapturePostRoundData(battle);
            string json = JsonUtility.ToJson(battleData, true);
            object deserializedData = JsonUtility.FromJson(json, battleData.GetType());
            object loadedRun = ReloadRun(run);
            restored = InvokeStatic(battleStateType, "FromData", loadedRun, deserializedData);

            Assert.That(restored, Is.Not.Null);
            Assert.That(GetProperty(restored, "Phase").ToString(), Is.EqualTo("PostRound"));
            Assert.That(GetProperty(restored, "RoundNumber"), Is.EqualTo(GetProperty(battle, "RoundNumber")));
            Assert.That(GetProperty(restored, "PlayerMoney"), Is.EqualTo(GetProperty(battle, "PlayerMoney")));
            Assert.That(GetProperty(restored, "OpponentMoney"), Is.EqualTo(GetProperty(battle, "OpponentMoney")));
            Assert.That(Count(GetProperty(restored, "CombatHistory")), Is.EqualTo(Count(GetProperty(battle, "CombatHistory"))));

            object originalRound = GetProperty(battle, "CurrentRound");
            object restoredRound = GetProperty(restored, "CurrentRound");
            Assert.That(GetProperty(restoredRound, "BaseWager"), Is.EqualTo(GetProperty(originalRound, "BaseWager")));
            Assert.That(GetProperty(restoredRound, "PlayerScore").ToString(), Is.EqualTo(GetProperty(originalRound, "PlayerScore").ToString()));
            AssertCardsEqual(GetProperty(originalRound, "PlayerPlayedCards"), GetProperty(restoredRound, "PlayerPlayedCards"));
            AssertCardsEqual(GetProperty(originalRound, "OpponentVisibleCards"), GetProperty(restoredRound, "OpponentVisibleCards"));
            AssertCardsEqual(GetProperty(battle, "PlayerDrawPile"), GetProperty(restored, "PlayerDrawPile"));
            AssertCardsEqual(GetProperty(battle, "PlayerDiscardPile"), GetProperty(restored, "PlayerDiscardPile"));
            Assert.That(JsonUtility.ToJson(Invoke(restored, "ToData"), true), Is.EqualTo(json));
        }
        finally
        {
            Dispose(restored);
            Dispose(battle);
        }
    }

    [Test]
    public void RestoredBattle_NextRoundMatchesUninterruptedBattle()
    {
        object run = CreateRun(1441);
        object battle = CreateResolvedBattle(run);
        object restored = null;

        try
        {
            object battleData = CapturePostRoundData(battle);
            object loadedRun = ReloadRun(run);
            restored = InvokeStatic(battleStateType, "FromData", loadedRun, battleData);
            Assert.That(restored, Is.Not.Null);

            Invoke(battle, "CleanupRound");
            Invoke(restored, "CleanupRound");
            int originalWager = (int)Invoke(battle, "GetDefaultWager");
            int restoredWager = (int)Invoke(restored, "GetDefaultWager");
            Assert.That(restoredWager, Is.EqualTo(originalWager));
            Assert.That((bool)Invoke(battle, "StartRound", originalWager), Is.True);
            Assert.That((bool)Invoke(restored, "StartRound", restoredWager), Is.True);

            object originalRound = GetProperty(battle, "CurrentRound");
            object restoredRound = GetProperty(restored, "CurrentRound");
            AssertCardsEqual(GetProperty(originalRound, "PlayerHand"), GetProperty(restoredRound, "PlayerHand"));
            AssertCardsEqual(GetProperty(originalRound, "OpponentHand"), GetProperty(restoredRound, "OpponentHand"));
            AssertCardsEqual(GetProperty(battle, "PlayerDrawPile"), GetProperty(restored, "PlayerDrawPile"));
            AssertCardsEqual(GetProperty(battle, "OpponentDrawPile"), GetProperty(restored, "OpponentDrawPile"));
        }
        finally
        {
            Dispose(restored);
            Dispose(battle);
        }
    }

    [Test]
    public void ReplayableRandom_RoundTrip_PreservesNextValue()
    {
        Type randomType = RequireType("ReplayableRandom");
        object original = Activator.CreateInstance(randomType, 991);
        Invoke(original, "Next", 0, 52);
        Invoke(original, "Next", 2, 13);
        object data = Invoke(original, "ToData");
        object restored = InvokeStatic(randomType, "FromData", data, 991);

        Assert.That(Invoke(restored, "Next", 0, 1000), Is.EqualTo(Invoke(original, "Next", 0, 1000)));
        Assert.That(Invoke(restored, "Next", 4, 9), Is.EqualTo(Invoke(original, "Next", 4, 9)));
    }

    [Test]
    public void MutableDevilAndRelicRuntimeState_RoundTripsExactly()
    {
        object run = CreateRun(7717);
        Invoke(run, "AddRelic", "burst_extend");
        object strategy = Activator.CreateInstance(RequireType("Devil2Strategy"), new object[] { 3 });
        object config = Activator.CreateInstance(
            battleConfigType,
            new object[] { "mutable-runtime-test", 100, strategy, 3, 21, 10, "devil2", null, null, null });
        object battle = Activator.CreateInstance(battleStateType, run, config);
        object restored = null;

        try
        {
            Assert.That((bool)Invoke(battle, "StartRound", 10), Is.True);
            object round = GetProperty(battle, "CurrentRound");
            Invoke(round, "MarkOpponentStood");
            Assert.That((bool)Invoke(battle, "TryStand"), Is.True);

            object relicRuntime = ((IEnumerable)GetProperty(battle, "RelicRuntimes")).Cast<object>().Single();
            SetField(relicRuntime, "_hitCount", 3);
            object battleData = CapturePostRoundData(battle);
            object loadedRun = ReloadRun(run);
            restored = InvokeStatic(battleStateType, "FromData", loadedRun, battleData);
            Assert.That(restored, Is.Not.Null);

            object restoredStrategy = GetProperty(GetProperty(restored, "Config"), "DevilStrategy");
            object originalStrategyData = Invoke(strategy, "CapturePersistenceState");
            object restoredStrategyData = Invoke(restoredStrategy, "CapturePersistenceState");
            Assert.That(GetField(restoredStrategyData, "currentWager"), Is.EqualTo(GetField(originalStrategyData, "currentWager")));
            Assert.That(GetField(restoredStrategyData, "lastResolvedRoundNumber"), Is.EqualTo(GetField(originalStrategyData, "lastResolvedRoundNumber")));

            object restoredRelic = ((IEnumerable)GetProperty(restored, "RelicRuntimes")).Cast<object>().Single();
            object restoredRelicData = Invoke(restoredRelic, "CapturePersistenceState");
            Assert.That(GetField(restoredRelicData, "counter"), Is.EqualTo(3));
        }
        finally
        {
            Dispose(restored);
            Dispose(battle);
        }
    }

    [Test]
    public void LegacyBattlePhaseWithoutCheckpoint_FallsBackToMap()
    {
        Type runManagerType = RequireType("RunManager");
        Type battleControllerType = RequireType("BattleController");
        GameObject controllerObject = new("Persistence Test BattleController");
        GameObject managerObject = new("Persistence Test RunManager");

        try
        {
            Component controller = controllerObject.AddComponent(battleControllerType);
            Component manager = managerObject.AddComponent(runManagerType);
            SetField(manager, "battleController", controller);

            object run = CreateRun(77);
            Invoke(run, "SetPhase", Enum.Parse(RequireType("RunPhase"), "Battle"));
            object gameData = Activator.CreateInstance(gameDataType);
            SetField(gameData, "runState", Invoke(run, "ToData"));
            Invoke(manager, "LoadData", gameData);

            object loadedRun = GetProperty(manager, "RunState");
            Assert.That(GetProperty(loadedRun, "Phase").ToString(), Is.EqualTo("Map"));
            Assert.That(GetProperty(controller, "BattleState"), Is.Null);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(managerObject);
            UnityEngine.Object.DestroyImmediate(controllerObject);
        }
    }

    [Test]
    public void UnknownStrategyCheckpoint_IsRejectedWithoutDiscardingRunData()
    {
        object run = CreateRun(818);
        object battle = CreateResolvedBattle(run);

        try
        {
            object battleData = CapturePostRoundData(battle);
            object configData = GetField(battleData, "config");
            SetField(configData, "strategyId", "not-a-real-strategy");

            object loadedRun = ReloadRun(run);
            object restored = InvokeStatic(battleStateType, "FromData", loadedRun, battleData);

            Assert.That(restored, Is.Null);
            Assert.That(GetProperty(loadedRun, "Money"), Is.EqualTo(GetProperty(run, "Money")));
        }
        finally
        {
            Dispose(battle);
        }
    }

    [Test]
    public void PersistenceDisabledRunManager_DoesNotLoadOrOverwriteSaveData()
    {
        Type runManagerType = RequireType("RunManager");
        GameObject managerObject = new("Nonpersistent Tutorial RunManager");

        try
        {
            Component manager = managerObject.AddComponent(runManagerType);
            SetField(manager, "persistenceEnabled", false);

            object gameData = Activator.CreateInstance(gameDataType);
            object savedRunData = Invoke(CreateRun(5150), "ToData");
            SetField(gameData, "runState", savedRunData);

            Invoke(manager, "LoadData", gameData);
            Assert.That(GetProperty(manager, "RunState"), Is.Null);

            Invoke(manager, "StartRun", 77);
            Invoke(manager, "SaveData", gameData);
            Assert.That(GetField(gameData, "runState"), Is.SameAs(savedRunData));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(managerObject);
        }
    }

    [Test]
    public void RoundCheckpointReady_FiresOnceWhileBattleIsPostRound()
    {
        object run = CreateRun(303);
        object config = Activator.CreateInstance(
            battleConfigType,
            new object[] { "checkpoint-event-test", 100, null, 3, 21, 10, null, null, null, null });
        object battle = Activator.CreateInstance(battleStateType, run, config);
        int checkpointCount = 0;
        string phaseAtCheckpoint = null;
        object checkpointData = null;
        Action<object> handler = activeBattle =>
        {
            checkpointCount++;
            phaseAtCheckpoint = GetProperty(activeBattle, "Phase").ToString();
            checkpointData = Invoke(activeBattle, "ToData");
        };

        EventInfo checkpointEvent = battleStateType.GetEvent("RoundCheckpointReady");
        Assert.That(checkpointEvent, Is.Not.Null);
        Assert.That(checkpointEvent.EventHandlerType.IsInstanceOfType(handler), Is.True);
        checkpointEvent.AddEventHandler(battle, handler);

        try
        {
            Assert.That((bool)Invoke(battle, "StartRound", 10), Is.True);
            object round = GetProperty(battle, "CurrentRound");
            Invoke(round, "MarkOpponentStood");
            Assert.That((bool)Invoke(battle, "TryStand"), Is.True);

            Assert.That(checkpointCount, Is.EqualTo(1));
            Assert.That(phaseAtCheckpoint, Is.EqualTo("PostRound"));
            Assert.That(checkpointData, Is.Not.Null);
        }
        finally
        {
            checkpointEvent.RemoveEventHandler(battle, handler);
            Dispose(battle);
        }
    }

    [Test]
    public void RestoredFinalRound_ContinueAppliesBattleResultExactlyOnce()
    {
        Type runManagerType = RequireType("RunManager");
        Type battleControllerType = RequireType("BattleController");
        GameObject controllerObject = new("Final Restore BattleController");
        GameObject managerObject = new("Final Restore RunManager");
        object originalBattle = null;

        try
        {
            object originalRun = CreateRun(909);
            Invoke(originalRun, "SetPhase", Enum.Parse(RequireType("RunPhase"), "Battle"));
            object config = Activator.CreateInstance(
                battleConfigType,
                new object[] { "final-restore-test", 10, null, 3, 21, 10, null, null, null, null });
            originalBattle = Activator.CreateInstance(battleStateType, originalRun, config);
            Assert.That((bool)Invoke(originalBattle, "StartRound", 10), Is.True);
            Invoke(originalBattle, "LoseOpponentMoney", 1000);
            object originalRound = GetProperty(originalBattle, "CurrentRound");
            Invoke(originalRound, "MarkOpponentStood");
            Assert.That((bool)Invoke(originalBattle, "TryStand"), Is.True);
            Invoke(originalBattle, "LoseOpponentMoney", 1000);
            object battleData = CapturePostRoundData(originalBattle);

            object gameData = Activator.CreateInstance(gameDataType);
            object runData = Invoke(originalRun, "ToData");
            SetField(runData, "battleState", battleData);
            SetField(gameData, "runState", runData);

            Component controller = controllerObject.AddComponent(battleControllerType);
            Component manager = managerObject.AddComponent(runManagerType);
            SetField(manager, "battleController", controller);
            InvokeNonPublic(manager, "OnEnable");
            Invoke(manager, "LoadData", gameData);

            Assert.That(GetProperty(GetProperty(manager, "RunState"), "Phase").ToString(), Is.EqualTo("Battle"));
            Assert.That((bool)Invoke(manager, "ContinueImmediatelyAfterRound"), Is.True);

            object loadedRun = GetProperty(manager, "RunState");
            Assert.That(GetProperty(loadedRun, "Phase").ToString(), Is.EqualTo("Rewards"));
            Assert.That(Count(GetProperty(loadedRun, "BattleHistory")), Is.EqualTo(1));
            Assert.That(GetProperty(controller, "BattleState"), Is.Null);
            Assert.That((bool)Invoke(manager, "ContinueImmediatelyAfterRound"), Is.False);
            Assert.That(Count(GetProperty(loadedRun, "BattleHistory")), Is.EqualTo(1));
        }
        finally
        {
            Dispose(originalBattle);
            UnityEngine.Object.DestroyImmediate(managerObject);
            UnityEngine.Object.DestroyImmediate(controllerObject);
        }
    }

    private object CreateRun(int seed)
    {
        return Activator.CreateInstance(runStateType, new object[] { seed, 100, 3 });
    }

    private object CreateResolvedBattle(object run)
    {
        object config = Activator.CreateInstance(
            battleConfigType,
            new object[] { "persistence-test", 100, null, 3, 21, 10, null, null, null, null });
        object battle = Activator.CreateInstance(battleStateType, run, config);
        Assert.That((bool)Invoke(battle, "StartRound", 10), Is.True);
        object round = GetProperty(battle, "CurrentRound");
        Invoke(round, "MarkOpponentStood");
        Assert.That((bool)Invoke(battle, "TryStand"), Is.True);
        Assert.That(Count(GetProperty(battle, "CombatHistory")), Is.EqualTo(1));
        return battle;
    }

    private object CapturePostRoundData(object battle)
    {
        SetProperty(battle, "Phase", Enum.Parse(battlePhaseType, "PostRound"));
        object data = Invoke(battle, "ToData");
        Assert.That(data, Is.Not.Null);
        return data;
    }

    private object ReloadRun(object run)
    {
        return InvokeStatic(runStateType, "FromData", Invoke(run, "ToData"), 100);
    }

    private Type RequireType(string name)
    {
        Type type = runtimeAssembly.GetType(name);
        Assert.That(type, Is.Not.Null, name);
        return type;
    }

    private static object Invoke(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = FindMethod(target.GetType(), methodName, arguments.Length, false);
        Assert.That(method, Is.Not.Null, methodName);
        return method.Invoke(target, arguments);
    }

    private static object InvokeStatic(Type type, string methodName, params object[] arguments)
    {
        MethodInfo method = FindMethod(type, methodName, arguments.Length, true);
        Assert.That(method, Is.Not.Null, methodName);
        return method.Invoke(null, arguments);
    }

    private static MethodInfo FindMethod(Type type, string name, int parameterCount, bool isStatic)
    {
        return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .FirstOrDefault(method => method.Name == name
                && method.IsStatic == isStatic
                && method.GetParameters().Length == parameterCount);
    }

    private static object InvokeNonPublic(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, methodName);
        return method.Invoke(target, arguments);
    }

    private static object GetProperty(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(property, Is.Not.Null, propertyName);
        return property.GetValue(target);
    }

    private static void SetProperty(object target, string propertyName, object value)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        MethodInfo setter = property?.GetSetMethod(true);
        Assert.That(setter, Is.Not.Null, propertyName);
        setter.Invoke(target, new[] { value });
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }

    private static object GetField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        return field.GetValue(target);
    }

    private static int Count(object collection)
    {
        return ((IEnumerable)collection).Cast<object>().Count();
    }

    private static void AssertCardsEqual(object expected, object actual)
    {
        string[] expectedCards = ((IEnumerable)expected).Cast<object>().Select(card => card.ToString()).ToArray();
        string[] actualCards = ((IEnumerable)actual).Cast<object>().Select(card => card.ToString()).ToArray();
        CollectionAssert.AreEqual(expectedCards, actualCards);
    }

    private static void Dispose(object battle)
    {
        if (battle != null)
            Invoke(battle, "Dispose");
    }
}
#endif