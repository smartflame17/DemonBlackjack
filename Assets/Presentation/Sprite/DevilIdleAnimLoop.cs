using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(RectTransform))]
public class DevilIdleAnimLoop : MonoBehaviour
{
    [SerializeField] private RectTransform devilTransform;
    [SerializeField] private float scaleAmount = 1.1f;
    [SerializeField] private float duration = 2f;

    private float originalScaleY;

    void Awake()
    {
        if (devilTransform == null)
            devilTransform = GetComponent<RectTransform>();

        originalScaleY = devilTransform.localScale.y;
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        devilTransform.DOScaleY(originalScaleY * scaleAmount, duration).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
    }
}
