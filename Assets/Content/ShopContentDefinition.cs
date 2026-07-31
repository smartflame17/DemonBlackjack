using UnityEngine;

public abstract class ShopContentDefinition : TooltipContentDefinition
{
    [Min(0)] [SerializeField] private int price = 10;

    public int Price => Mathf.Max(0, price);

    public void Initialize(string contentId, string name, string contentDescription, int contentPrice)
    {
        base.Initialize(contentId, name, contentDescription);
        price = Mathf.Max(0, contentPrice);
    }
}
