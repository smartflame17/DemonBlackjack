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
