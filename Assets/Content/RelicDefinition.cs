using UnityEngine;

[CreateAssetMenu(fileName = "Relic", menuName = "Demon Blackjack/Shop/Relic")]
public sealed class RelicDefinition : ShopContentDefinition
{
    [SerializeField] private bool hasCounter;

    public bool HasCounter => hasCounter;
}
