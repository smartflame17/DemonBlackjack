public static class CardModifierResolver
{
    public const string RankToHearts = "rank_to_hearts";
    public const string RankToDiamonds = "rank_to_diamonds";
    public const string RankToClubs = "rank_to_clubs";
    public const string RankToSpades = "rank_to_spades";

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
}
