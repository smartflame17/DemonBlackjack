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

    public bool TryGetDefinition(string id, out ShopContentDefinition definition)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            if (TryFind(activeItems, id, out ActiveItemDefinition activeItem))
            {
                definition = activeItem;
                return true;
            }

            if (TryFind(relics, id, out RelicDefinition relic))
            {
                definition = relic;
                return true;
            }

            if (TryFind(cardUpgrades, id, out CardUpgradeDefinition upgrade))
            {
                definition = upgrade;
                return true;
            }
        }

        definition = null;
        return false;
    }

    public static ShopCatalog CreateRuntimeDefault()
    {
        ShopCatalog catalog = CreateInstance<ShopCatalog>();
        catalog.hideFlags = HideFlags.DontSave;
        catalog.activeItems.Add(Create<ActiveItemDefinition>(ActiveItemResolver.RejectLastHit, "결재 반려", "이번 턴 마지막 Hit 카드를 무덤으로 보내 점수와 족보 계산에서 제외합니다.", 10));
        catalog.activeItems.Add(Create<ActiveItemDefinition>(ActiveItemResolver.DrawThree, "특근 지시서", "덱에서 카드 3장을 즉시 드로우합니다. 손패가 3장을 넘어갈 수 있습니다.", 10));
        catalog.activeItems.Add(Create<ActiveItemDefinition>(ActiveItemResolver.DoubleWager, "레버리지", "이번 라운드의 판돈을 2배로 계산합니다. 획득과 손실 모두 적용됩니다.", 10));
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

    private static bool TryFind<T>(IReadOnlyList<T> definitions, string id, out T definition) where T : ShopContentDefinition
    {
        for (int i = 0; i < definitions.Count; i++)
        {
            T candidate = definitions[i];
            if (candidate != null && string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                definition = candidate;
                return true;
            }
        }

        definition = null;
        return false;
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
