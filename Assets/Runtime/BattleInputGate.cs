public interface IBattleInputGate
{
    bool CanStartRound(BattleState battle, int wager);
    bool CanPlayCard(BattleState battle, int handIndex);
    bool CanHit(BattleState battle);
    bool CanStand(BattleState battle);
    bool CanUseActiveItem(BattleState battle, string itemId);
    void NotifyStandAccepted(BattleState battle);
}
