using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SceneTransitionManager : MonoBehaviour
{
    private const int OverlaySortingOrder = short.MaxValue;

    [SerializeField, Min(0f)] private float fadeDuration = 0.5f;
    [SerializeField] private Image fadeImage;

    public static SceneTransitionManager Instance { get; private set; }

    public bool IsTransitioning { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<SceneTransitionManager>(FindObjectsInactive.Include) != null)
            return;

        GameObject transitionObject = new GameObject("Scene Transition Manager");
        transitionObject.SetActive(false);

        Canvas canvas = transitionObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = OverlaySortingOrder;
        transitionObject.AddComponent<CanvasScaler>();
        transitionObject.AddComponent<GraphicRaycaster>();

        GameObject imageObject = new GameObject("Fade Image", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(transitionObject.transform, false);

        RectTransform imageTransform = imageObject.GetComponent<RectTransform>();
        imageTransform.anchorMin = Vector2.zero;
        imageTransform.anchorMax = Vector2.one;
        imageTransform.offsetMin = Vector2.zero;
        imageTransform.offsetMax = Vector2.zero;

        Image image = imageObject.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = true;
        image.enabled = false;

        SceneTransitionManager manager = transitionObject.AddComponent<SceneTransitionManager>();
        manager.fadeImage = image;
        transitionObject.SetActive(true);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (fadeImage == null)
            fadeImage = GetComponentInChildren<Image>(true);

        if (fadeImage == null)
        {
            Debug.LogError("SceneTransitionManager requires a fullscreen fade Image.", this);
            return;
        }
        fadeImage.enabled = true;
        fadeImage.color = Color.black;
        SetFadeAlpha(1f);
        
        if (fadeImage.enabled)
            Debug.Log("SceneTransitionManager initialized. Starting fade-out.", this);

        StartCoroutine(FadeOut());
    }

    public void LoadScene(string sceneName)
    {
        if (IsTransitioning)
            return;

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("Cannot transition to a scene with an empty name.", this);
            return;
        }
        if (SoundManager.Instance != null && SoundManager.Instance.CurrentBGM != EBgm.NONE)
            SoundManager.Instance?.FadeOutBGM(1.0f);
        StartCoroutine(TransitionToScene(sceneName));
    }

    private IEnumerator TransitionToScene(string sceneName)
    {
        IsTransitioning = true;
        yield return Fade(0f, 1f);

        AsyncOperation loadOperation = null;
        Exception loadException = null;
        try
        {
            loadOperation = SceneManager.LoadSceneAsync(sceneName);
        }
        catch (Exception exception)
        {
            loadException = exception;
        }

        if (loadException != null)
        {
            Debug.LogException(loadException, this);
            yield return FadeOut();
            IsTransitioning = false;
            yield break;
        }

        if (loadOperation == null)
        {
            Debug.LogError($"Unity could not start loading scene '{sceneName}'.", this);
            yield return FadeOut();
            IsTransitioning = false;
            yield break;
        }

        yield return loadOperation;
        yield return FadeOut();
        IsTransitioning = false;
    }

    private IEnumerator FadeOut()
    {
        yield return Fade(1f, 0f);
        fadeImage.enabled = false;
    }

    private IEnumerator Fade(float startAlpha, float targetAlpha)
    {
        if (fadeImage == null)
            yield break;

        fadeImage.enabled = true;
        SetFadeAlpha(startAlpha);

        if (fadeDuration <= 0f)
        {
            SetFadeAlpha(targetAlpha);
            yield break;
        }
        Debug.Log($"Starting fade from {startAlpha} to {targetAlpha} over {fadeDuration} seconds.", this);
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetFadeAlpha(Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration));
            yield return null;
        }
        Debug.Log($"Fade completed. Final alpha: {targetAlpha}", this);
        SetFadeAlpha(targetAlpha);
    }

    private void SetFadeAlpha(float alpha)
    {
        Color color = fadeImage.color;
        color.a = alpha;
        fadeImage.color = color;
    }
}
