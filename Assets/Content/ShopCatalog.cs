using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ShopCatalog", menuName = "Demon Blackjack/Shop/Catalog")]
public sealed class ShopCatalog : ScriptableObject
{
    [SerializeField] private List<ActiveItemDefinition> activeItems = new();
    [SerializeField] private List<RelicDefinition> relics = new();
    [SerializeField] private List<CardUpgradeDefinition> cardUpgrades = new();

    public IReadOnlyList<ActiveItemDefinition> ActiveItems => activeItems;
    public IReadOnlyList<RelicDefinition> Relics => relics;
    public IReadOnlyList<CardUpgradeDefinition> CardUpgrades => cardUpgrades;

    public static ShopCatalog CreateRuntimeDefault()
    {
        ShopCatalog catalog = CreateInstance<ShopCatalog>();
        catalog.hideFlags = HideFlags.DontSave;
        catalog.activeItems.Add(Create<ActiveItemDefinition>(ActiveItemResolver.ClearPlayerField, "Clear Your Field", "Discard all cards on your field.", 20));
        catalog.activeItems.Add(Create<ActiveItemDefinition>(ActiveItemResolver.ClearOpponentField, "Clear Enemy Field", "Discard all cards on the opponent field.", 30));
        catalog.activeItems.Add(Create<ActiveItemDefinition>(ActiveItemResolver.ClearAllFields, "Clear All Fields", "Discard every card currently in play.", 40));
        catalog.relics.Add(Create<RelicDefinition>(RelicRuleResolver.BurstTwentyTwo, "Twenty-Two", "Raise your burst threshold to 22.", 50));
        catalog.cardUpgrades.Add(Create<CardUpgradeDefinition>(CardModifierResolver.RankToHearts, "Heart Imprint", "Cards of the selected rank become Hearts when played.", 25));
        catalog.cardUpgrades.Add(Create<CardUpgradeDefinition>(CardModifierResolver.RankToDiamonds, "Diamond Imprint", "Cards of the selected rank become Diamonds when played.", 25));
        catalog.cardUpgrades.Add(Create<CardUpgradeDefinition>(CardModifierResolver.RankToClubs, "Club Imprint", "Cards of the selected rank become Clubs when played.", 25));
        catalog.cardUpgrades.Add(Create<CardUpgradeDefinition>(CardModifierResolver.RankToSpades, "Spade Imprint", "Cards of the selected rank become Spades when played.", 25));
        return catalog;
    }

    private static T Create<T>(string id, string displayName, string description, int price) where T : ShopContentDefinition
    {
        T definition = CreateInstance<T>();
        definition.hideFlags = HideFlags.DontSave;
        definition.Initialize(id, displayName, description, price);
        return definition;
    }
}

public readonly struct CardUpgradeOffer : IEquatable<CardUpgradeOffer>
{
    public CardUpgradeOffer(CardUpgradeDefinition definition, Rank rank)
    {
        Definition = definition;
        Rank = rank;
    }

    public CardUpgradeDefinition Definition { get; }
    public Rank Rank { get; }
    public bool Equals(CardUpgradeOffer other) => Definition == other.Definition && Rank == other.Rank;
    public override bool Equals(object obj) => obj is CardUpgradeOffer other && Equals(other);
    public override int GetHashCode() => ((Definition != null ? Definition.GetHashCode() : 0) * 397) ^ (int)Rank;
}
