using System;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private string contentId;
    [SerializeField] private string fallbackContentId;
    [SerializeField] private TooltipPanel tooltipPanel;
    private Func<string, string> _descriptionFormatter;

    public string ContentId => contentId;
    public string FallbackContentId => fallbackContentId;

    public void Bind(string id)
    {
        Bind(id, null);
    }

    public void Bind(string id, string fallbackId)
    {
        Bind(id, fallbackId, null);
    }

    public void Bind(string id, string fallbackId, Func<string, string> descriptionFormatter)
    {
        if (string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(fallbackId))
        {
            Clear();
            return;
        }

        contentId = string.IsNullOrWhiteSpace(id) ? null : id;
        fallbackContentId = string.IsNullOrWhiteSpace(fallbackId) ? null : fallbackId;
        _descriptionFormatter = descriptionFormatter;
        ResolvePanel();
        tooltipPanel?.Refresh(this);
    }

    public void Clear()
    {
        tooltipPanel?.Hide(this);
        contentId = null;
        fallbackContentId = null;
        _descriptionFormatter = null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (string.IsNullOrWhiteSpace(contentId) && string.IsNullOrWhiteSpace(fallbackContentId))
            return;

        ResolvePanel();
        tooltipPanel?.Show(contentId, fallbackContentId, this, eventData.position);
    }

    public string FormatDescription(string description)
    {
        return _descriptionFormatter != null ? _descriptionFormatter(description) : description;
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
