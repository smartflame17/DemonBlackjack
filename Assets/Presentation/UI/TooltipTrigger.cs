using UnityEngine;
using UnityEngine.EventSystems;

public sealed class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private string contentId;
    [SerializeField] private string fallbackContentId;
    [SerializeField] private TooltipPanel tooltipPanel;

    public string ContentId => contentId;
    public string FallbackContentId => fallbackContentId;

    public void Bind(string id)
    {
        Bind(id, null);
    }

    public void Bind(string id, string fallbackId)
    {
        if (string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(fallbackId))
        {
            Clear();
            return;
        }

        contentId = string.IsNullOrWhiteSpace(id) ? null : id;
        fallbackContentId = string.IsNullOrWhiteSpace(fallbackId) ? null : fallbackId;
        ResolvePanel();
    }

    public void Clear()
    {
        tooltipPanel?.Hide(this);
        contentId = null;
        fallbackContentId = null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (string.IsNullOrWhiteSpace(contentId) && string.IsNullOrWhiteSpace(fallbackContentId))
            return;

        ResolvePanel();
        tooltipPanel?.Show(contentId, fallbackContentId, this, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        tooltipPanel?.Hide(this);
    }

    private void OnDisable()
    {
        tooltipPanel?.Hide(this);
    }

    private void ResolvePanel()
    {
        if (tooltipPanel != null)
            return;

        TooltipPanel[] panels = Resources.FindObjectsOfTypeAll<TooltipPanel>();
        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i].gameObject.scene.IsValid())
            {
                tooltipPanel = panels[i];
                return;
            }
        }
    }
}
