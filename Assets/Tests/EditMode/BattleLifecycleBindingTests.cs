#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class BattleLifecycleBindingTests
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
    public void DestroyedEventLogger_DoesNotReceiveNextScenesBattleStartedEvent()
    {
        GameObject firstControllerObject = null;
        GameObject loggerObject = null;
        GameObject replacementControllerObject = null;

        try
        {
            firstControllerObject = new GameObject("First BattleController");
            BattleController firstController = firstControllerObject.AddComponent<BattleController>();
            firstController.InitializeBattle(new RunState(101), new BattleConfig("first", 100));

            loggerObject = new GameObject("EventLogger");
            loggerObject.SetActive(false);
            EventLogger logger = loggerObject.AddComponent<EventLogger>();
            SetField(logger, "battleController", firstController);
            Invoke(logger, "OnEnable");

            PublishBattleStarted(firstController);
            Invoke(logger, "OnDisable");

            Object.DestroyImmediate(loggerObject);
            loggerObject = null;
            Object.DestroyImmediate(firstControllerObject);
            firstControllerObject = null;

            replacementControllerObject = new GameObject("Replacement BattleController");
            BattleController replacementController = replacementControllerObject.AddComponent<BattleController>();
            replacementController.InitializeBattle(new RunState(202), new BattleConfig("replacement", 100));

            Assert.DoesNotThrow(() => PublishBattleStarted(replacementController));
        }
        finally
        {
            if (loggerObject != null)
                Object.DestroyImmediate(loggerObject);
            if (firstControllerObject != null)
                Object.DestroyImmediate(firstControllerObject);
            if (replacementControllerObject != null)
                Object.DestroyImmediate(replacementControllerObject);
        }
    }

    [Test]
    public void DevilDialogueController_OnEnableBindsExistingActiveBattle()
    {
        var controllerObject = new GameObject("BattleController");
        var dialogueObject = new GameObject("DevilDialogueController");
        dialogueObject.SetActive(false);

        try
        {
            BattleController controller = controllerObject.AddComponent<BattleController>();
            controller.InitializeBattle(new RunState(303), new BattleConfig("active_devil", 100));

            DevilDialogueController dialogueController = dialogueObject.AddComponent<DevilDialogueController>();
            SetField(dialogueController, "battleController", controller);
            Invoke(dialogueController, "OnEnable");

            Assert.That(
                GetField<ScopedEventBus>(dialogueController, "battleBus"),
                Is.SameAs(controller.BattleState.EventBus));
            Assert.That(
                GetField<string>(dialogueController, "_currentDevilId"),
                Is.EqualTo("active_devil"));

            Invoke(dialogueController, "OnDisable");
            Assert.That(GetField<ScopedEventBus>(dialogueController, "battleBus"), Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(dialogueObject);
            Object.DestroyImmediate(controllerObject);
        }
    }

    [Test]
    public void RunManager_StartBattlePublishesOneGlobalEventWithCreatedBattleState()
    {
        var controllerObject = new GameObject("BattleController");
        var managerObject = new GameObject("RunManager");
        managerObject.SetActive(false);

        try
        {
            BattleController controller = controllerObject.AddComponent<BattleController>();
            RunManager manager = managerObject.AddComponent<RunManager>();
            SetField(manager, "battleController", controller);
            manager.StartRun(404);

            int globalEvents = 0;
            BattleStartedEvent received = default;
            EventBus.Subscribe<BattleStartedEvent>(eventData =>
            {
                globalEvents++;
                received = eventData;
            });

            manager.StartBattle(new BattleConfig("global_start", 275));

            BattleState battle = controller.BattleState;
            Assert.That(globalEvents, Is.EqualTo(1));
            Assert.That(received.EncounterId, Is.EqualTo(battle.Config.EncounterId));
            Assert.That(received.Seed, Is.EqualTo(battle.BattleSeed));
            Assert.That(received.PlayerMoney, Is.EqualTo(battle.PlayerMoney));
            Assert.That(received.OpponentMoney, Is.EqualTo(battle.OpponentMoney));

            int scopedEvents = 0;
            var standaloneBattle = new BattleState(new RunState(405), new BattleConfig("standalone", 100));
            standaloneBattle.EventBus.Subscribe<BattleStartedEvent>(_ => scopedEvents++);
            standaloneBattle.Initialize();
            Assert.That(scopedEvents, Is.Zero);
            standaloneBattle.Dispose();
        }
        finally
        {
            Object.DestroyImmediate(managerObject);
            Object.DestroyImmediate(controllerObject);
        }
    }

    [Test]
    public void BattleEndPublishesGloballyOnlyAfterVisualTransitionAndThenCleansUpRun()
    {
        var controllerObject = new GameObject("BattleController");
        var managerObject = new GameObject("RunManager");
        managerObject.SetActive(false);

        try
        {
            BattleController controller = controllerObject.AddComponent<BattleController>();
            RunManager manager = managerObject.AddComponent<RunManager>();
            SetField(manager, "battleController", controller);
            Invoke(manager, "OnEnable");
            manager.StartRun(505);
            manager.StartBattle(new BattleConfig("global_end", 10, baseWager: 10));

            BattleState battle = controller.BattleState;
            int globalEvents = 0;
            int scopedEvents = 0;
            BattleEndedEvent received = default;
            EventBus.Subscribe<BattleEndedEvent>(eventData =>
            {
                globalEvents++;
                received = eventData;
            });
            battle.EventBus.Subscribe<BattleEndedEvent>(_ => scopedEvents++);

            Assert.That(controller.StartNextRound(10), Is.True);
            Assert.That(controller.TryPlayCard(0), Is.True);
            battle.CurrentRound.MarkOpponentStood();
            Assert.That(controller.TryStand(), Is.True);

            Assert.That(battle.Phase, Is.EqualTo(BattlePhase.BattleEnd));
            Assert.That(controller.IsWaitingForVisuals, Is.True);
            Assert.That(globalEvents, Is.Zero);
            Assert.That(scopedEvents, Is.Zero);
            Assert.That(manager.RunState.BattleHistory, Is.Empty);
            Assert.That(manager.RunState.Phase, Is.EqualTo(RunPhase.Battle));

            controller.CompletePendingVisualTransition();

            Assert.That(globalEvents, Is.EqualTo(1));
            Assert.That(scopedEvents, Is.Zero);
            Assert.That(received.Result.PlayerWon, Is.True);
            Assert.That(manager.RunState.BattleHistory.Count, Is.EqualTo(1));
            Assert.That(manager.RunState.Phase, Is.EqualTo(RunPhase.Rewards));
            Assert.That(controller.BattleState, Is.Null);

            controller.CompletePendingVisualTransition();
            Assert.That(globalEvents, Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(managerObject);
            Object.DestroyImmediate(controllerObject);
        }
    }

    private static void PublishBattleStarted(BattleController controller)
    {
        BattleState battle = controller.BattleState;
        EventBus.Publish(new BattleStartedEvent(
            battle.Config.EncounterId,
            battle.BattleSeed,
            battle.PlayerMoney,
            battle.OpponentMoney));
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

    private static void Invoke(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, methodName);
        method.Invoke(target, null);
    }
}
#endif
