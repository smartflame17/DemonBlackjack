using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class SettingMenuController : MonoBehaviour
{
    [SerializeField] private Button returnToMainMenuButton;
    [SerializeField] private Button closeButton;

    [Header("Tween Settings")]
    [SerializeField] private float menuPanelAnimationDuration = 1.0f;
    [SerializeField] private Ease menuPanelAnimationEase = Ease.InBack;
    private RectTransform rectTransform => transform as RectTransform;
    private void Awake()
    {
        if (returnToMainMenuButton == null)
            Debug.LogError("Missing Main Menu Button Reference!");
        if (closeButton == null)
            Debug.LogError("Missing Close Button Reference!");
        
        if (returnToMainMenuButton != null)
            returnToMainMenuButton.onClick.AddListener(ReturnToMainMenu);
        if (closeButton != null)
            closeButton.onClick.AddListener(CloseMenuPanel);
    }

    private void OnEnable()
    {
        rectTransform.DOAnchorPos(new Vector2(35, 0), menuPanelAnimationDuration).SetEase(menuPanelAnimationEase);
    }

    private void CloseMenuPanel()
    {
        rectTransform.DOAnchorPos(new Vector2(35, -800), menuPanelAnimationDuration).SetEase(menuPanelAnimationEase).OnComplete(()=>gameObject.SetActive(false));
    }

    private void ReturnToMainMenu()
    {
        PersistenceManager.Instance?.SaveGame();

        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.LoadScene("MenuScene");
        else
            UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("MenuScene");
    }

}
