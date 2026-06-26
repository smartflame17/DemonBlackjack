public static class ActiveItemResolver
{
    public const string RejectLastHit = "hit_to_played";
    public const string DrawThree = "draw_three";
    public const string DoubleWager = "double_wager";

    public static bool CanApply(string itemId, BattleState battle)
    {
        if (string.IsNullOrWhiteSpace(itemId) || battle == null || battle.CurrentRound == null)
            return false;

        return itemId switch
        {
            RejectLastHit => battle.CanDiscardLastPlayerHitCard(),
            DrawThree => battle.CanDrawPlayerCards(),
            DoubleWager => battle.CanDoubleCurrentRoundWager(),
            _ => false
        };
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
            _ => false
        };
    }
}
