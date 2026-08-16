using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using System;

public class PersistenceManager : MonoBehaviour
{
    [Header("File Settings")]
    [SerializeField] private string fileName;
    [SerializeField] private bool useEncryption = false; // Toggle for encryption
    [SerializeField] private string selectedProfileId = "slot1"; // Default profile ID

    private GameData gameData;
    private FileDataHandler fileDataHandler;
    private List<IDataPersistence> dataPersistenceObjects;
    public int PlayerSetSeed { get; set; }
    public static PersistenceManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        PlayerSetSeed = -1; // Initialize PlayerSetSeed to -1 (indicating no seed set)
    }

    private void Start()
  {
    InitializeFileDataHandler();
    this.dataPersistenceObjects = FindAllDataPersistenceObjects();
    LoadGame();
  }

  private void InitializeFileDataHandler()
  {
    string fileWithProfileId = selectedProfileId + "_" + fileName;
    this.fileDataHandler = new FileDataHandler(Application.persistentDataPath, fileWithProfileId, useEncryption);
  }

  public void ChangeSelectedProfileId(string newProfileId)
  {
    this.selectedProfileId = newProfileId;
    InitializeFileDataHandler();
  }

  public void NewGame()
  {
    gameData = new GameData(); // Initialize a new GameData instance
    ApplyGameData();
    Debug.Log($"New game started in slot {selectedProfileId}.");
  }

  public void LoadGame()
  {
    if (fileDataHandler == null)
      InitializeFileDataHandler();

    this.gameData = fileDataHandler.Load(); // Load game data from file
    if (this.gameData == null)
    {
      Debug.Log("No game data found, starting a new game.");
      NewGame(); // If no game data exists, create a new one
      return;
    }

    ApplyGameData();
  }

  private void ApplyGameData()
  {
    this.dataPersistenceObjects = FindAllDataPersistenceObjects(); // Refresh the list of IDataPersistence objects
    foreach (IDataPersistence dataPersistenceObj in this.dataPersistenceObjects)
    {
      dataPersistenceObj.LoadData(gameData); // Load data into each IDataPersistence object
    }
  }

  public void SaveGame()
  {
    if (fileDataHandler == null)
      InitializeFileDataHandler();

    if (gameData == null)
      NewGame();

    this.dataPersistenceObjects = FindAllDataPersistenceObjects();

    //Pass the gameData to other scripts to update it
    foreach (IDataPersistence dataPersistenceObj in this.dataPersistenceObjects)
    {
      dataPersistenceObj.SaveData(gameData); // Save data from each IDataPersistence object
    }

    gameData.schemaVersion = GameData.CurrentSchemaVersion;
    gameData.timestamp = DateTime.Now.ToFileTime(); // Update timestamp

    // Save gameData to a file or PlayerPrefs
    fileDataHandler.Save(gameData); // Save the game data to file
    Debug.Log($"Game data saved in slot {selectedProfileId}.");
  }

  public bool TryLoadRunState(out RunState runState)
  {
    if (gameData == null)
    {
      if (fileDataHandler == null)
        InitializeFileDataHandler();

      gameData = fileDataHandler.Load();
    }

    runState = gameData != null ? RunState.FromData(gameData.runState) : null;
    return runState != null;
  }

  public void SaveRunState(RunState runState)
  {
    if (runState == null)
      return;

    if (gameData == null)
      NewGame();

    gameData.runState = runState.ToData();
    SaveGame();
  }

  public void LoadCurrentProfile()
  {
    LoadGame();
  }

  public void SaveCurrentProfile()
  {
    SaveGame();
  }

  private List<IDataPersistence> FindAllDataPersistenceObjects()
  {
    // Find all objects in the scene that implement IDataPersistence
    return FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<IDataPersistence>().ToList();
  }

  public List<String> GetAllProfileIds()
  {
    return FileDataHandler.GetAllProfileIds(Application.persistentDataPath, fileName);
  }

  public GameData GetGameData()
  {
    return this.gameData;
  }

  public void LoadScene(string sceneName)
  {
    if (SceneTransitionManager.Instance != null)
      SceneTransitionManager.Instance.LoadScene(sceneName);
    else
      UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName);
  }

  public void QuitGame()
  {
    Debug.Log("Quitting game...");
    #if UNITY_EDITOR
      UnityEditor.EditorApplication.isPlaying = false;
    #else
    Application.Quit();
    #endif
  }

  public string GetMostRecentProfileId()
  {
    List<string> profileIds = GetAllProfileIds();
    if (profileIds.Count == 0) return null;

    string mostRecentProfileId = profileIds[0];
    long mostRecentTimestamp = 0;

    foreach (string profileId in profileIds)
    {
      var handler = new FileDataHandler(Application.persistentDataPath, profileId + "_" + fileName, useEncryption);
      GameData data = handler.Load(); // Load game data from file
      if (IsValidSave(data) && data.timestamp > mostRecentTimestamp)
      {
        mostRecentTimestamp = data.timestamp;
        mostRecentProfileId = profileId;
      }
    }
    return mostRecentTimestamp > 0 ? mostRecentProfileId : null;
  }

  public bool HasAnyValidSave()
  {
    return GetMostRecentProfileId() != null;
  }

  public bool TryLoadMostRecentGame()
  {
    string profileId = GetMostRecentProfileId();
    if (string.IsNullOrWhiteSpace(profileId))
      return false;

    var handler = new FileDataHandler(Application.persistentDataPath, profileId + "_" + fileName, useEncryption);
    GameData loadedData = handler.Load();
    if (!IsValidSave(loadedData))
      return false;

    selectedProfileId = profileId;
    fileDataHandler = handler;
    gameData = loadedData;
    ApplyGameData();
    return true;
  }

  private static bool IsValidSave(GameData data)
  {
    if (data?.runState == null || data.timestamp <= 0)
      return false;

    try
    {
      return RunState.FromData(data.runState) != null;
    }
    catch (Exception exception)
    {
      Debug.LogWarning($"Ignoring invalid save data: {exception.Message}");
      return false;
    }
  }
}
