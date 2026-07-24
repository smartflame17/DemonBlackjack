public static class ActiveItemResolver
{
    public const string RejectLastHit = "hit_to_played";
    public const string DrawThree = "draw_three";
    public const string DoubleWager = "double_wager";
    public const string LeverageTriple = "leverage_triple";
    public const string BurstThresholdPlusThree = "burst_threshold_plus_three";
    public const string ReturnPlayerFieldCardToHand = "return_player_field_card_to_hand";
    public const string DrawTwoHearts = "draw_two_hearts";
    public const string DrawTwoDiamonds = "draw_two_diamonds";
    public const string DrawTwoClubs = "draw_two_clubs";
    public const string DrawTwoSpades = "draw_two_spades";

    public static bool CanApply(string itemId, BattleState battle)
    {
        if (string.IsNullOrWhiteSpace(itemId) || battle == null || battle.CurrentRound == null)
            return false;

        return itemId switch
        {
            RejectLastHit => battle.CanDiscardLastPlayerHitCard(),
            DrawThree => battle.CanDrawPlayerCards(),
            DoubleWager => battle.CanDoubleCurrentRoundWager(),
            LeverageTriple => battle.CanTripleCurrentRoundWager(),
            BurstThresholdPlusThree => battle.CanIncreasePlayerBurstThreshold(3),
            ReturnPlayerFieldCardToHand => battle.CanReturnPlayerFieldCardToHand(),
            _ => false
        } || TryGetSuitDraw(itemId, out Suit suit) && battle.CanDrawPlayerCards(suit);
    }

    public static bool TryApply(string itemId, BattleState battle)
    {
        if (!CanApply(itemId, battle))
            return false;

        return itemId switch
        {
            RejectLastHit => battle.DiscardLastPlayerHitCard(),
            DrawThree => battle.DrawCardsToPlayerHand(3) > 0,
            DoubleWager => battle.DoubleCurrentRoundWager(),
            LeverageTriple => battle.TripleCurrentRoundWager(),
            BurstThresholdPlusThree => battle.IncreasePlayerBurstThreshold(3),
            ReturnPlayerFieldCardToHand => battle.ReturnPlayerFieldCardToHand(),
            _ => false
        } || TryGetSuitDraw(itemId, out Suit suit) && battle.DrawCardsToPlayerHand(suit, 2) > 0;
    }

    private static bool TryGetSuitDraw(string itemId, out Suit suit)
    {
        switch (itemId)
        {
            case DrawTwoHearts:
                suit = Suit.Hearts;
                return true;
            case DrawTwoDiamonds:
                suit = Suit.Diamonds;
                return true;
            case DrawTwoClubs:
                suit = Suit.Clubs;
                return true;
            case DrawTwoSpades:
                suit = Suit.Spades;
                return true;
            default:
                suit = default;
                return false;
        }
    }
}
