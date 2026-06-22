using UnityEngine;
using UnityEngine.EventSystems;

public sealed class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private string contentId;
    [SerializeField] private TooltipPanel tooltipPanel;

    public string ContentId => contentId;

    public void Bind(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            Clear();
            return;
        }

        contentId = id;
        ResolvePanel();
    }

    public void Clear()
    {
        tooltipPanel?.Hide(this);
        contentId = null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (string.IsNullOrWhiteSpace(contentId))
            return;

        ResolvePanel();
        tooltipPanel?.Show(contentId, this, eventData.position);
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
