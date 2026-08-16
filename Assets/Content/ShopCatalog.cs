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
        catalog.activeItems.Add(Create<ActiveItemDefinition>(ActiveItemResolver.RejectLastHit, "결재 반려", "내 필드 카드 1장을 선택해 무덤으로 보냅니다.", 10));
        catalog.activeItems.Add(Create<ActiveItemDefinition>(ActiveItemResolver.DrawThree, "초과 근무 명령", "덱에서 카드 3장을 즉시 드로우합니다. 손패가 3장을 넘어갈 수 있습니다.", 10));
        catalog.activeItems.Add(Create<ActiveItemDefinition>(ActiveItemResolver.LeverageTriple, "레버리지", "이번 라운드의 판돈을 3배로 계산합니다. 획득과 손실 모두 적용됩니다.", 10));
        catalog.activeItems.Add(Create<ActiveItemDefinition>(ActiveItemResolver.BurstThresholdPlusThree, "분식 회계", "이번 라운드의 버스트 기준을 3 올립니다.", 10));
        catalog.activeItems.Add(Create<ActiveItemDefinition>(ActiveItemResolver.ReturnPlayerFieldCardToHand, "연차 소진", "내 필드 카드 1장을 손패로 회수합니다.", 10));
        catalog.activeItems.Add(Create<ActiveItemDefinition>(ActiveItemResolver.DrawTwoHearts, "내정자 면접: 하트", "덱에서 하트 카드 2장을 손패로 드로우합니다.", 10));
        catalog.activeItems.Add(Create<ActiveItemDefinition>(ActiveItemResolver.DrawTwoDiamonds, "내정자 면접: 다이아", "덱에서 다이아 카드 2장을 손패로 드로우합니다.", 10));
        catalog.activeItems.Add(Create<ActiveItemDefinition>(ActiveItemResolver.DrawTwoClubs, "내정자 면접: 클로버", "덱에서 클로버 카드 2장을 손패로 드로우합니다.", 10));
        catalog.activeItems.Add(Create<ActiveItemDefinition>(ActiveItemResolver.DrawTwoSpades, "내정자 면접: 스페이드", "덱에서 스페이드 카드 2장을 손패로 드로우합니다.", 10));
        catalog.relics.Add(Create<RelicDefinition>(RelicRuleResolver.BurstTwentyTwo, "Twenty-Two", "Raise your burst threshold to 22.", 50));
        catalog.cardUpgrades.Add(CreateCardUpgrade(CardModifierResolver.RankToHearts, "Heart Imprint", "Cards of the selected rank become Hearts when played.", 25, CardUpgradeAssignmentType.NumberedCards));
        catalog.cardUpgrades.Add(CreateCardUpgrade(CardModifierResolver.RankToDiamonds, "Diamond Imprint", "Cards of the selected rank become Diamonds when played.", 25, CardUpgradeAssignmentType.NumberedCards));
        catalog.cardUpgrades.Add(CreateCardUpgrade(CardModifierResolver.RankToClubs, "Club Imprint", "Cards of the selected rank become Clubs when played.", 25, CardUpgradeAssignmentType.NumberedCards));
        catalog.cardUpgrades.Add(CreateCardUpgrade(CardModifierResolver.RankToSpades, "Spade Imprint", "Cards of the selected rank become Spades when played.", 25, CardUpgradeAssignmentType.NumberedCards));
        catalog.cardUpgrades.Add(CreateCardUpgrade(CardModifierResolver.NegativeRank, "마이너스 적금", "선택 숫자를 음수로 계산합니다.", 25, CardUpgradeAssignmentType.NumberedCards));
        catalog.cardUpgrades.Add(CreateCardUpgrade(CardModifierResolver.HitHigher, "단축 근무", "Hit 시 덱에서 해당 숫자 이상의 카드를 찾아 즉시 Hit합니다.", 25, CardUpgradeAssignmentType.NumberedCards));
        catalog.cardUpgrades.Add(CreateCardUpgrade(CardModifierResolver.HitLower, "연장 근무", "Hit 시 덱에서 해당 숫자 이하의 카드를 찾아 즉시 Hit합니다.", 25, CardUpgradeAssignmentType.NumberedCards));
        catalog.cardUpgrades.Add(CreateCardUpgrade(CardModifierResolver.DrawRank, "Ctrl+F", "Hit 시 덱에서 같은 숫자 카드 1장을 손패로 드로우합니다.", 25, CardUpgradeAssignmentType.NumberedCards));
        catalog.cardUpgrades.Add(CreateCardUpgrade(CardModifierResolver.DrawSuit, "Ctrl+FF", "Hit 시 덱에서 같은 문양 카드 1장을 손패로 드로우합니다.", 25, CardUpgradeAssignmentType.NumberedCards));
        catalog.cardUpgrades.Add(CreateCardUpgrade(CardModifierResolver.DoubleCardValue, "가불", "Hit 시 이 카드 숫자를 2배로 계산합니다.", 25, CardUpgradeAssignmentType.NumberedCards));
        catalog.cardUpgrades.Add(CreateCardUpgrade(CardModifierResolver.HalfCardValue, "이월", "Hit 시 이 카드 숫자를 절반으로 계산합니다.", 25, CardUpgradeAssignmentType.NumberedCards));
        catalog.cardUpgrades.Add(CreateCardUpgrade(CardModifierResolver.TriggerPreviousEffect, "인수인계", "Hit 시 필드 마지막 카드 효과를 1회 복사해 발동합니다.", 25, CardUpgradeAssignmentType.NumberedCards));
        catalog.cardUpgrades.Add(CreateCardUpgrade(CardModifierResolver.DiscardLowestNumber, "정리 해고", "Hit 시 내 필드에서 가장 낮은 숫자 카드 1장을 무덤으로 보냅니다.", 25, CardUpgradeAssignmentType.NumberedCards));
        catalog.cardUpgrades.Add(CreateCardUpgrade(CardModifierResolver.JokerValue, "조커 카드", "Hit 시 1, 2, 12, 21 중 하나로 랜덤 판정합니다.", 25, CardUpgradeAssignmentType.NumberedCards));
        catalog.cardUpgrades.Add(CreateCardUpgrade(CardModifierResolver.ZeroThenForceHit, "인턴의 실수", "이 카드는 0으로 계산하고 덱에서 한 장을 강제로 Hit합니다.", 25, CardUpgradeAssignmentType.NumberedCards));
        catalog.cardUpgrades.Add(CreateCardUpgrade(CardModifierResolver.MoveJack, "복제기", "Hit 시 내 필드 마지막 카드가 적 필드로 이동합니다.", 25, CardUpgradeAssignmentType.JackCards));
        catalog.cardUpgrades.Add(CreateCardUpgrade(CardModifierResolver.MoveOpponentToPlayer, "헤드헌팅", "Hit 시 적 필드 마지막 카드가 내 필드로 이동합니다.", 25, CardUpgradeAssignmentType.JackCards));
        catalog.cardUpgrades.Add(CreateCardUpgrade(CardModifierResolver.DemoteOpponentCard, "좌천", "Hit 시 적 필드 마지막 카드 1장을 적 덱 맨 아래로 보냅니다.", 25, CardUpgradeAssignmentType.JackCards));
        catalog.cardUpgrades.Add(CreateCardUpgrade(CardModifierResolver.CopyQueen, "문양 복제", "Hit 시 덱에서 필드 마지막 카드와 같은 문양의 카드를 찾아 Hit합니다.", 25, CardUpgradeAssignmentType.QueenCards));
        catalog.cardUpgrades.Add(CreateCardUpgrade(CardModifierResolver.OutsourceOpponentCard, "외주화", "Hit 시 적 필드 마지막 카드를 내 필드로 가져옵니다.", 25, CardUpgradeAssignmentType.QueenCards));
        catalog.cardUpgrades.Add(CreateCardUpgrade(CardModifierResolver.DuplicateKing, "책임 전가", "Hit 시 내 필드 마지막 카드를 복사해서 즉시 Hit합니다.", 25, CardUpgradeAssignmentType.KingCards));
        catalog.cardUpgrades.Add(CreateCardUpgrade(CardModifierResolver.DuplicateOpponentCard, "에이스의 거래 방법", "Hit 시 적 필드 마지막 카드를 복사해서 즉시 Hit합니다.", 25, CardUpgradeAssignmentType.KingCards));
        catalog.cardUpgrades.Add(CreateCardUpgrade(CardModifierResolver.HitTopDeckCard, "낙하산", "Hit 시 덱 맨 위 카드를 즉시 Hit합니다.", 25, CardUpgradeAssignmentType.KingCards));
        catalog.cardUpgrades.Add(CreateCardUpgrade(CardModifierResolver.SplitPreviousCard, "작두", "Hit 시 숫자 카드 하나를 반으로 쪼개어 즉시 Hit합니다.", 25, CardUpgradeAssignmentType.KingCards));
        return catalog;
    }

    private static T Create<T>(string id, string displayName, string description, int price) where T : ShopContentDefinition
    {
        T definition = CreateInstance<T>();
        definition.hideFlags = HideFlags.DontSave;
        definition.Initialize(id, displayName, description, price);
        return definition;
    }

    private static CardUpgradeDefinition CreateCardUpgrade(string id, string displayName, string description, int price, CardUpgradeAssignmentType assignmentType)
    {
        CardUpgradeDefinition definition = CreateInstance<CardUpgradeDefinition>();
        definition.hideFlags = HideFlags.DontSave;
        definition.Initialize(id, displayName, description, price, assignmentType);
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
