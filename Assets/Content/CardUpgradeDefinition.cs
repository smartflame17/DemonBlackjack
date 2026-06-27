using System;
using UnityEngine;

public enum CardUpgradeAssignmentType
{
    NumberedCards,
    FaceCards,
    JackCards,
    QueenCards,
    KingCards
}

[CreateAssetMenu(fileName = "CardUpgrade", menuName = "Demon Blackjack/Shop/Card Upgrade")]
public sealed class CardUpgradeDefinition : ShopContentDefinition
{
    [SerializeField] private CardUpgradeAssignmentType assignmentType = CardUpgradeAssignmentType.NumberedCards;

    public CardUpgradeAssignmentType AssignmentType => assignmentType;

    public void Initialize(string contentId, string name, string contentDescription, int contentPrice, CardUpgradeAssignmentType contentAssignmentType)
    {
        Initialize(contentId, name, contentDescription, contentPrice);
        assignmentType = contentAssignmentType;
    }

    public bool CanApplyToRank(Rank rank)
    {
        if (!Enum.IsDefined(typeof(Rank), rank))
            return false;

        return assignmentType switch
        {
            CardUpgradeAssignmentType.NumberedCards => (int)rank >= (int)Rank.Ace && (int)rank <= (int)Rank.Ten,
            CardUpgradeAssignmentType.FaceCards => (int)rank >= (int)Rank.Jack && (int)rank <= (int)Rank.King,
            CardUpgradeAssignmentType.JackCards => rank == Rank.Jack,
            CardUpgradeAssignmentType.QueenCards => rank == Rank.Queen,
            CardUpgradeAssignmentType.KingCards => rank == Rank.King,
            _ => false
        };
    }
}
