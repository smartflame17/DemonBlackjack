using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button loadGameButton;
    [SerializeField] private Button quitGameButton;

    private void Awake()
    {
        newGameButton.onClick.AddListener(StartNewGame);
        loadGameButton.onClick.AddListener(LoadGame);
        quitGameButton.onClick.AddListener(QuitGame);
    }
    private void StartNewGame()
    {
        PersistenceManager.Instance.NewGame();
        PersistenceManager.Instance.LoadScene("TutorialScene");
    }
    private void LoadGame()
    {
        PersistenceManager.Instance.LoadGame();
        PersistenceManager.Instance.LoadScene("MainScene");
    }
    private void QuitGame()
    {
        PersistenceManager.Instance.QuitGame();
    }
}
