public static class CardModifierResolver
{
    public const string RankToHearts = "rank_to_hearts";
    public const string RankToDiamonds = "rank_to_diamonds";
    public const string RankToClubs = "rank_to_clubs";
    public const string RankToSpades = "rank_to_spades";
    public const string NegativeRank = "negative_rank";
    public const string HitLower = "hit_lower";
    public const string DrawSuit = "draw_suit";
    public const string MoveJack = "move_jack";
    public const string CopyQueen = "copy_queen";
    public const string DuplicateKing = "duplicate_king";

    public static Card Apply(RunCard runCard, string modifierId)
    {
        if (string.IsNullOrWhiteSpace(modifierId))
            return runCard.ToBattleCard();

        return modifierId switch
        {
            RankToHearts => new Card(Suit.Hearts, runCard.Rank, modifierId),
            RankToDiamonds => new Card(Suit.Diamonds, runCard.Rank, modifierId),
            RankToClubs => new Card(Suit.Clubs, runCard.Rank, modifierId),
            RankToSpades => new Card(Suit.Spades, runCard.Rank, modifierId),
            _ => runCard.ToBattleCard(modifierId)
        };
    }

    public static Card Apply(Card card, string modifierId)
    {
        if (string.IsNullOrWhiteSpace(modifierId))
            return card;

        return modifierId switch
        {
            RankToHearts => new Card(Suit.Hearts, card.Rank, modifierId),
            RankToDiamonds => new Card(Suit.Diamonds, card.Rank, modifierId),
            RankToClubs => new Card(Suit.Clubs, card.Rank, modifierId),
            RankToSpades => new Card(Suit.Spades, card.Rank, modifierId),
            _ => new Card(card.Suit, card.Rank, modifierId)
        };
    }
}
