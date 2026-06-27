using UnityEngine;
using UnityEngine.EventSystems;

// For UI elements
[RequireComponent(typeof(RectTransform))]
public class EnlargeOnHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private float scaleFactor = 1.2f;
    [SerializeField] private float transitionDuration = 0.2f;

    private Vector3 originalScale;
    private Vector3 targetScale;
    private float transitionProgress;
    private bool isHovered;

    private void Awake()
    {
        originalScale = GetComponent<RectTransform>().localScale;
        targetScale = originalScale * scaleFactor;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        transitionProgress = 0f;
        StopAllCoroutines();
        StartCoroutine(ScaleToTarget(targetScale));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        transitionProgress = 0f;
        StopAllCoroutines();
        StartCoroutine(ScaleToTarget(originalScale));
    }

    private System.Collections.IEnumerator ScaleToTarget(Vector3 target)
    {
        while (transitionProgress < 1f)
        {
            transitionProgress += Time.deltaTime / transitionDuration;
            GetComponent<RectTransform>().localScale = Vector3.Lerp(GetComponent<RectTransform>().localScale, target, transitionProgress);
            yield return null;
        }
        GetComponent<RectTransform>().localScale = target; // Ensure final scale is set
    }
}
