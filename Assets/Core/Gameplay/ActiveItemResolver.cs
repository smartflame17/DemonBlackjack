public static class ActiveItemResolver
{
    public const string ClearPlayerField = "item_clear_player_field";
    public const string ClearOpponentField = "item_clear_opponent_field";
    public const string ClearAllFields = "item_clear_all_fields";

    public static bool CanApply(string itemId, BattleState battle)
    {
        if (string.IsNullOrWhiteSpace(itemId) || battle == null || battle.CurrentRound == null)
            return false;

        RoundState round = battle.CurrentRound;
        bool playerHasCards = round.PlayerPlayedCards.Count > 0 || round.SharedVisibleCards.Count > 0;
        bool opponentHasCards = round.OpponentVisibleCards.Count > 0;

        return itemId switch
        {
            ClearPlayerField => playerHasCards,
            ClearOpponentField => opponentHasCards,
            ClearAllFields => playerHasCards || opponentHasCards,
            _ => false
        };
    }

    public static bool TryApply(string itemId, BattleState battle)
    {
        if (!CanApply(itemId, battle))
            return false;

        return itemId switch
        {
            ClearPlayerField => battle.ClearField(Combatant.Player),
            ClearOpponentField => battle.ClearField(Combatant.Opponent),
            ClearAllFields => battle.ClearAllFields(),
            _ => false
        };
    }
}
