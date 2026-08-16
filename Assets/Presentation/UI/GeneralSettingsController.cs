using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class GeneralSettingsController : MonoBehaviour
{
    [SerializeField] private Button returnToMainMenuButton;

    private void Awake()
    {
        if (returnToMainMenuButton == null)
        {
            Debug.LogError("GeneralSettingsController requires a Return to Main Menu button.", this);
            return;
        }

        returnToMainMenuButton.onClick.AddListener(ReturnToMainMenu);
    }

    private void OnDestroy()
    {
        if (returnToMainMenuButton != null)
            returnToMainMenuButton.onClick.RemoveListener(ReturnToMainMenu);
    }

    private static void ReturnToMainMenu()
    {
        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.LoadScene("MenuScene");
        else
            SceneManager.LoadSceneAsync("MenuScene");
    }
}
