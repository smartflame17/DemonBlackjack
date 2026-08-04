using UnityEngine;

public abstract class TooltipContentDefinition : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [TextArea] [SerializeField] private string description;

    public string Id => id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? id : displayName;
    public string Description => description;

    public void Initialize(string contentId, string name, string contentDescription)
    {
        id = contentId;
        displayName = name;
        description = contentDescription;
    }
}
