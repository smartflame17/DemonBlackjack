using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public struct PokerRankTextBinding
{
    public TMP_Text text;
    public PokerHandRank rank;
}

public class PokerRankViewPanel : MonoBehaviour
{
    [SerializeField] private GameplayAssetRegistry assetRegistry;
    [SerializeField] private Button closeButton;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private List<PokerRankTextBinding> rankMultiplierTexts;
    
    void Awake()
    {
        closeButton.onClick.AddListener(Hide);

        for (int i = 0; i < rankMultiplierTexts.Count; i++)
        {
            var binding = rankMultiplierTexts[i];
            int multiplier = ScoreResolver.GetPokerMultiplier(binding.rank);
            binding.text.text = $"x{multiplier}";
        }
    }
    
    public void Show()
    {
        gameObject.SetActive(true);
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        StartCoroutine(FadeInCoroutine());
    }

    public void Hide()
    {
        StartCoroutine(FadeOutCoroutine());
    }

    private IEnumerator FadeInCoroutine()
    {
        float elapsedTime = 0f;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / fadeDuration);
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
            yield return null;
        }

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    private IEnumerator FadeOutCoroutine()
    {
        float elapsedTime = 0f;
        float startAlpha = canvasGroup.alpha;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / fadeDuration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        gameObject.SetActive(false);
    }
}
