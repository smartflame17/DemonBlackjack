using UnityEngine;
using TMPro;
using DG.Tweening;
public class BattleResultPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text playerWonText;
    [SerializeField] private TMP_Text playerLostText;
    [SerializeField] private RunManager runManager;

    void Awake()
    {
        if (runManager == null)
            runManager = FindFirstObjectByType<RunManager>();
    }

    void OnEnable()
    {
        if (runManager == null)
            return;
        
        if (runManager.RunState.Money > 0)
        {
            playerWonText.gameObject.SetActive(true);
            playerLostText.gameObject.SetActive(false);

            playerWonText.DOFade(0f, 0f);
            playerWonText.DOFade(1f, 1f);
        }
        else
        {
            playerWonText.gameObject.SetActive(false);
            playerLostText.gameObject.SetActive(true);

            playerLostText.DOFade(0f, 0f);
            playerLostText.DOFade(1f, 1f);
        }
    }
}
