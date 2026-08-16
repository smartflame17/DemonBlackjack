using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using DG.Tweening;
using EasyTextEffects.Editor.MyBoxCopy.Extensions;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private CanvasGroup mainMenuCanvasGroup;
    [SerializeField] private Image mainMenuBackground;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueGameButton;
    [SerializeField] private Button loadGameButton;
    [SerializeField] private Button quitGameButton;

    [Header("New Game Settings")]
    [SerializeField] private GameObject newGameMenuPanel;
    [SerializeField] private Toggle enableTutorialToggle;
    [SerializeField] private TMP_InputField seedInputField;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button returnButton;
    
    [Header("Animation Settings")]
    [SerializeField] private float backgroundStartAnchorPositionX = 110f;
    [SerializeField] private float backgroundEndAnchorPositionX = 1700f;
    [SerializeField] private float fadeAnimationSpeed = 0.5f;
    [SerializeField] private float slideAnimationSpeed = 0.7f;
    [SerializeField] private float newGameMenuScrollDistanceX = 200f;

    private void Awake()
    {
        newGameButton.onClick.AddListener(OpenNewGameMenu);
        if (continueGameButton != null)
            continueGameButton.onClick.AddListener(ContinueGame);
        quitGameButton.onClick.AddListener(QuitGame);
        startGameButton.onClick.AddListener(StartNewGame);
        returnButton.onClick.AddListener(ReturnToMainMenu);
    }

    private void Start()
    {
        RefreshContinueButton();
    }

    private void OnDestroy()
    {
        newGameButton?.onClick.RemoveListener(OpenNewGameMenu);
        continueGameButton?.onClick.RemoveListener(ContinueGame);
        quitGameButton?.onClick.RemoveListener(QuitGame);
        startGameButton?.onClick.RemoveListener(StartNewGame);
        returnButton?.onClick.RemoveListener(ReturnToMainMenu);
    }

    private void OpenNewGameMenu()
    {
        PersistenceManager.Instance.PlayerSetSeed = -1; // Reset PlayerSetSeed to -1 when opening the new game menu
        mainMenuCanvasGroup.DOFade(0f, fadeAnimationSpeed).OnComplete(() =>
        {
            mainMenuCanvasGroup.interactable = false;
            mainMenuCanvasGroup.blocksRaycasts = false;
            mainMenuBackground.transform.As<RectTransform>().DOAnchorPosX(backgroundEndAnchorPositionX, slideAnimationSpeed).SetEase(Ease.InOutSine).OnComplete(() =>
            {
                newGameMenuPanel.transform.As<RectTransform>().DOAnchorPosX(-newGameMenuScrollDistanceX, slideAnimationSpeed).SetEase(Ease.InOutSine);
            });
        });
    }

    private void ReturnToMainMenu()
    {
        newGameMenuPanel.transform.As<RectTransform>().DOAnchorPosX(newGameMenuScrollDistanceX, slideAnimationSpeed).SetEase(Ease.InOutSine).OnComplete(() =>
        {
            mainMenuBackground.transform.As<RectTransform>().DOAnchorPosX(backgroundStartAnchorPositionX, slideAnimationSpeed).SetEase(Ease.InOutSine).OnComplete(() =>
            {
                mainMenuCanvasGroup.DOFade(1f, fadeAnimationSpeed);
                mainMenuCanvasGroup.interactable = true;
                mainMenuCanvasGroup.blocksRaycasts = true;
            });
        });
    }

    private void StartNewGame()
    {
        if (!string.IsNullOrEmpty(seedInputField.text) && !string.IsNullOrWhiteSpace(seedInputField.text))
        {
            PersistenceManager.Instance.PlayerSetSeed = ParseSeed(seedInputField.text);
        }
        PersistenceManager.Instance.NewGame();
        Debug.Log($"Starting new game with seed: {PersistenceManager.Instance.PlayerSetSeed}");

        if (enableTutorialToggle.isOn)
        {
            PersistenceManager.Instance.LoadScene("TutorialScene");
        }
        else
        {
            PersistenceManager.Instance.LoadScene("MainScene");
        }
    }
    private void ContinueGame()
    {
        if (PersistenceManager.Instance != null && PersistenceManager.Instance.TryLoadMostRecentGame())
        {
            PersistenceManager.Instance.LoadScene("MainScene");
            return;
        }

        RefreshContinueButton();
    }

    private void RefreshContinueButton()
    {
        if (continueGameButton == null)
            return;

        continueGameButton.gameObject.SetActive(true);
        continueGameButton.interactable = PersistenceManager.Instance != null
            && PersistenceManager.Instance.HasAnyValidSave();
    }
    private void QuitGame()
    {
        PersistenceManager.Instance.QuitGame();
    }

    public static int ParseSeed(string input)
{
    if (string.IsNullOrWhiteSpace(input))
        return 0;

    // Preserve ordinary numeric seeds exactly.
    if (int.TryParse(input, out int numericSeed))
        return numericSeed;

    // FNV-1a: deterministic string hash
    unchecked
    {
        uint hash = 2166136261;

        foreach (char c in input)
        {
            hash ^= c;
            hash *= 16777619;
        }

        return (int)hash;
    }
}
}
