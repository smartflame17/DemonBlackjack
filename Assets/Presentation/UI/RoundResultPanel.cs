using UnityEngine;
using TMPro;
using DG.Tweening;

public class RoundResultPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text roundResultText;
    [SerializeField] private BattleController battleController;
    [SerializeField] private float fadeDuration = 0.5f;

    void Awake()
    {
        if (battleController == null)
            battleController = FindFirstObjectByType<BattleController>();
    }
    
    void OnEnable()
    {
        roundResultText.DOFade(1f, 0f); // Ensure the text is visible at the start
        if (battleController != null && battleController.BattleState != null)
        {
            roundResultText.DOFade(0f, fadeDuration).SetEase(Ease.InOutQuad);
        }
    }
}
