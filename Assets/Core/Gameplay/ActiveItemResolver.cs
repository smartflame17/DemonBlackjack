public static class ActiveItemResolver
{
    public const string ClearPlayerField = "item_clear_player_field";
    public const string ClearOpponentField = "item_clear_opponent_field";
    public const string ClearAllFields = "item_clear_all_fields";

    public static bool TryApply(string itemId, BattleState battle)
    {
        if (string.IsNullOrWhiteSpace(itemId) || battle == null)
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
