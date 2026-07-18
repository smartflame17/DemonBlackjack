using System;
using PixelCrushers.DialogueSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class DialoguePersistenceAdapter : MonoBehaviour, IDataPersistence
{
    private IDialoguePersistenceBackend backend;
    private string cachedSaveData = string.Empty;
    private bool hasPendingLoad;

    private void OnEnable()
    {
        SetBackend(backend ?? new PixelCrushersDialoguePersistenceBackend());
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryApplyPendingLoad();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (backend != null)
            backend.InitializationCompleted -= OnDialogueSystemInitializationCompleted;
    }

    public void LoadData(GameData data)
    {
        cachedSaveData = data != null && data.dialogueSaveData != null
            ? data.dialogueSaveData
            : string.Empty;
        hasPendingLoad = true;
        TryApplyPendingLoad();
    }

    public void SaveData(GameData data)
    {
        if (data == null)
            return;

        backend?.Refresh();
        if (backend != null && backend.IsInitialized)
        {
            try
            {
                cachedSaveData = backend.GetSaveData();
            }
            catch (Exception exception)
            {
                Debug.LogError($"Failed to capture Dialogue System save data. Preserving the last loaded data.\n{exception}", this);
            }
        }

        data.dialogueSaveData = cachedSaveData;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryApplyPendingLoad();
    }

    private void OnDialogueSystemInitializationCompleted()
    {
        TryApplyPendingLoad();
    }

    private void TryApplyPendingLoad()
    {
        if (!hasPendingLoad || backend == null)
            return;

        backend.Refresh();
        if (!backend.IsInitialized)
            return;

        string saveDataToApply = cachedSaveData;
        hasPendingLoad = false;

        try
        {
            if (backend.IsConversationActive)
                backend.StopAllConversations();

            backend.ApplySaveData(saveDataToApply);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to apply Dialogue System save data. Resetting to database defaults.\n{exception}", this);
            cachedSaveData = string.Empty;
            TryResetToDefaults();
        }
    }

    private void TryResetToDefaults()
    {
        try
        {
            backend.ApplySaveData(string.Empty);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to reset Dialogue System data to database defaults.\n{exception}", this);
        }
    }

    internal void SetBackendForTests(IDialoguePersistenceBackend testBackend)
    {
        SetBackend(testBackend);
        TryApplyPendingLoad();
    }

    private void SetBackend(IDialoguePersistenceBackend newBackend)
    {
        if (backend != null)
            backend.InitializationCompleted -= OnDialogueSystemInitializationCompleted;

        backend = newBackend;

        if (backend != null)
            backend.InitializationCompleted += OnDialogueSystemInitializationCompleted;
    }
}

internal interface IDialoguePersistenceBackend
{
    event Action InitializationCompleted;
    bool IsInitialized { get; }
    bool IsConversationActive { get; }
    void Refresh();
    string GetSaveData();
    void StopAllConversations();
    void ApplySaveData(string saveData);
}

internal sealed class PixelCrushersDialoguePersistenceBackend : IDialoguePersistenceBackend
{
    private DialogueSystemController controller;

    public event Action InitializationCompleted;

    public bool IsInitialized => controller != null && controller.isInitialized;
    public bool IsConversationActive => DialogueManager.isConversationActive;

    public void Refresh()
    {
        DialogueSystemController currentController = DialogueManager.Instance;
        if (controller == currentController)
            return;

        if (controller != null)
            controller.initializationComplete -= OnInitializationCompleted;

        controller = currentController;

        if (controller != null && !controller.isInitialized)
            controller.initializationComplete += OnInitializationCompleted;
    }

    public string GetSaveData()
    {
        return PersistentDataManager.GetSaveData();
    }

    public void StopAllConversations()
    {
        DialogueManager.StopAllConversations();
    }

    public void ApplySaveData(string saveData)
    {
        PersistentDataManager.ApplySaveData(saveData, DatabaseResetOptions.KeepAllLoaded);
    }

    private void OnInitializationCompleted()
    {
        if (controller != null)
            controller.initializationComplete -= OnInitializationCompleted;

        InitializationCompleted?.Invoke();
    }
}
