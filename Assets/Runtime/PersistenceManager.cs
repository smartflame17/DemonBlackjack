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
    }

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
      if (data != null && data.timestamp > mostRecentTimestamp)
      {
        mostRecentTimestamp = data.timestamp;
        mostRecentProfileId = profileId;
      }
    }
    return mostRecentProfileId;   // Return the profile ID with the most recent timestamp
  }
}
