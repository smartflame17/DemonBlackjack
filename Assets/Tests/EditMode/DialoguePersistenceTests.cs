#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using PixelCrushers.DialogueSystem;
using UnityEngine;

public sealed class DialoguePersistenceTests
{
    private GameObject adapterObject;
    private DialoguePersistenceAdapter adapter;
    private FakeDialoguePersistenceBackend backend;

    [SetUp]
    public void SetUp()
    {
        adapterObject = new GameObject("Dialogue Persistence Adapter Test");
        adapter = adapterObject.AddComponent<DialoguePersistenceAdapter>();
        backend = new FakeDialoguePersistenceBackend();
        adapter.SetBackendForTests(backend);
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(adapterObject);
    }

    [Test]
    public void GameData_RoundTripsDialogueSaveDataAsOpaqueText()
    {
        const string payload = "Variable={Affinity=42, Quoted=\"a; b \\\"value\\\"\"};\nActor[1]={Name=\"Devil\"};";
        var original = new GameData { dialogueSaveData = payload };

        string json = JsonUtility.ToJson(original);
        GameData restored = JsonUtility.FromJson<GameData>(json);

        Assert.That(restored.dialogueSaveData, Is.EqualTo(payload));
    }

    [Test]
    public void PixelCrushersBackend_RoundTripsDialogueVariable()
    {
        GameObject dialogueManagerObject = new("Dialogue Manager Test");
        DialogueDatabase database = ScriptableObject.CreateInstance<DialogueDatabase>();
        dialogueManagerObject.SetActive(false);

        bool previousLoggingState = Debug.unityLogger.logEnabled;
        try
        {
            Debug.unityLogger.logEnabled = false;
            DialogueSystemController controller = dialogueManagerObject.AddComponent<DialogueSystemController>();
            controller.initialDatabase = database;
            controller.preloadResources = false;
            controller.warmUpConversationController = DialogueSystemController.WarmUpMode.Off;
            controller.dontDestroyOnLoad = false;
            dialogueManagerObject.SetActive(true);
            controller.Start();

            const string variableName = "DialoguePersistenceIntegrationTest";
            DialogueLua.SetVariable(variableName, 17);
            var realBackend = new PixelCrushersDialoguePersistenceBackend();
            realBackend.Refresh();

            string saveData = realBackend.GetSaveData();
            DialogueLua.SetVariable(variableName, 99);
            realBackend.ApplySaveData(saveData);

            Assert.That(DialogueLua.GetVariable(variableName).asInt, Is.EqualTo(17));
        }
        finally
        {
            Debug.unityLogger.logEnabled = previousLoggingState;
            UnityEngine.Object.DestroyImmediate(dialogueManagerObject);
            UnityEngine.Object.DestroyImmediate(database);
        }
    }

    [Test]
    public void LegacyGameData_MissingDialogueFieldResetsToDefaults()
    {
        backend.IsInitialized = true;
        GameData legacyData = JsonUtility.FromJson<GameData>("{\"timestamp\":123}");

        adapter.LoadData(legacyData);

        CollectionAssert.AreEqual(new[] { string.Empty }, backend.AppliedSaveData);
    }

    [Test]
    public void SaveData_CapturesCompleteBackendPayload()
    {
        const string payload = "Variable={Affinity=7}; Item[1]={State=\"active\"};";
        backend.IsInitialized = true;
        backend.SaveData = payload;
        var gameData = new GameData();

        adapter.SaveData(gameData);

        Assert.That(gameData.dialogueSaveData, Is.EqualTo(payload));
        Assert.That(backend.GetSaveDataCallCount, Is.EqualTo(1));
    }

    [Test]
    public void SaveData_WhenBackendIsUnavailablePreservesLoadedPayload()
    {
        const string payload = "Variable={Affinity=11};";
        backend.IsInitialized = false;
        adapter.LoadData(new GameData { dialogueSaveData = payload });
        var destination = new GameData();

        adapter.SaveData(destination);

        Assert.That(destination.dialogueSaveData, Is.EqualTo(payload));
        Assert.That(backend.GetSaveDataCallCount, Is.Zero);
    }

    [Test]
    public void SaveData_WhenCaptureThrowsPreservesLoadedPayload()
    {
        const string payload = "Variable={Affinity=13};";
        backend.IsInitialized = false;
        adapter.LoadData(new GameData { dialogueSaveData = payload });
        backend.IsInitialized = true;
        backend.ThrowOnGetSaveData = true;
        var destination = new GameData();

        bool previousLoggingState = Debug.unityLogger.logEnabled;
        try
        {
            Debug.unityLogger.logEnabled = false;
            adapter.SaveData(destination);
        }
        finally
        {
            Debug.unityLogger.logEnabled = previousLoggingState;
        }

        Assert.That(destination.dialogueSaveData, Is.EqualTo(payload));
        Assert.That(backend.GetSaveDataCallCount, Is.EqualTo(1));
    }

    [Test]
    public void LoadData_DefersUntilInitializationAndAppliesOnlyLatestPayload()
    {
        backend.IsInitialized = false;
        adapter.LoadData(new GameData { dialogueSaveData = "Variable={Profile=1};" });
        adapter.LoadData(new GameData { dialogueSaveData = "Variable={Profile=2};" });

        Assert.That(backend.AppliedSaveData, Is.Empty);

        backend.CompleteInitialization();
        backend.CompleteInitialization();

        CollectionAssert.AreEqual(new[] { "Variable={Profile=2};" }, backend.AppliedSaveData);
    }

    [Test]
    public void LoadData_StopsActiveConversationBeforeApplyingPayload()
    {
        backend.IsInitialized = true;
        backend.IsConversationActive = true;

        adapter.LoadData(new GameData { dialogueSaveData = "Variable={Affinity=3};" });

        Assert.That(backend.StopAllConversationsCallCount, Is.EqualTo(1));
        CollectionAssert.AreEqual(new[] { "Variable={Affinity=3};" }, backend.AppliedSaveData);
    }

    [Test]
    public void LoadData_WhenApplicationThrowsFallsBackToDatabaseDefaults()
    {
        backend.IsInitialized = true;
        backend.ThrowOnNonEmptyApply = true;
        bool previousLoggingState = Debug.unityLogger.logEnabled;
        try
        {
            Debug.unityLogger.logEnabled = false;
            adapter.LoadData(new GameData { dialogueSaveData = "not valid lua" });
        }
        finally
        {
            Debug.unityLogger.logEnabled = previousLoggingState;
        }

        CollectionAssert.AreEqual(new[] { "not valid lua", string.Empty }, backend.AppliedSaveData);

        var destination = new GameData { dialogueSaveData = "stale" };
        adapter.SaveData(destination);
        Assert.That(destination.dialogueSaveData, Is.EqualTo(string.Empty));
    }

    private sealed class FakeDialoguePersistenceBackend : IDialoguePersistenceBackend
    {
        public event Action InitializationCompleted;

        public bool IsInitialized { get; set; }
        public bool IsConversationActive { get; set; }
        public bool ThrowOnGetSaveData { get; set; }
        public bool ThrowOnNonEmptyApply { get; set; }
        public string SaveData { get; set; } = string.Empty;
        public int GetSaveDataCallCount { get; private set; }
        public int StopAllConversationsCallCount { get; private set; }
        public List<string> AppliedSaveData { get; } = new();

        public void Refresh()
        {
        }

        public string GetSaveData()
        {
            GetSaveDataCallCount++;
            if (ThrowOnGetSaveData)
                throw new InvalidOperationException("Dialogue capture failed.");
            return SaveData;
        }

        public void StopAllConversations()
        {
            StopAllConversationsCallCount++;
            IsConversationActive = false;
        }

        public void ApplySaveData(string saveData)
        {
            AppliedSaveData.Add(saveData);
            if (ThrowOnNonEmptyApply && !string.IsNullOrEmpty(saveData))
                throw new InvalidOperationException("Invalid dialogue payload.");
        }

        public void CompleteInitialization()
        {
            IsInitialized = true;
            InitializationCompleted?.Invoke();
        }
    }
}
#endif
