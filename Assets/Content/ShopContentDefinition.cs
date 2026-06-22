using UnityEngine;

public abstract class ShopContentDefinition : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [TextArea] [SerializeField] private string description;
    [Min(0)] [SerializeField] private int price = 10;

    public string Id => id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? id : displayName;
    public string Description => description;
    public int Price => Mathf.Max(0, price);

    public void Initialize(string contentId, string name, string contentDescription, int contentPrice)
    {
        id = contentId;
        displayName = name;
        description = contentDescription;
        price = Mathf.Max(0, contentPrice);
    }
}
