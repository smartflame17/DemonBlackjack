using System;

public static class MoneyResolver
{
    public static RoundResolution ResolveRound(BattleState battle, RoundState round)
    {
        if (battle == null)
            throw new ArgumentNullException(nameof(battle));

        if (round == null)
            throw new ArgumentNullException(nameof(round));

        return ResolveRound(battle, round, true);
    }

    public static RoundResolution ResolveRound(BattleState battle, RoundState round, bool applyBurstPenalty)
    {
        if (battle == null)
            throw new ArgumentNullException(nameof(battle));

        if (round == null)
            throw new ArgumentNullException(nameof(round));

        Combatant? winner = DetermineWinner(round.PlayerScore, round.OpponentScore);
        int opponentMoneyLost = 0;
        int playerMoneyLost = 0;

        if (applyBurstPenalty)
        {
            if (round.PlayerScore.IsBurst)
                playerMoneyLost = battle.LosePlayerMoney(CalculateHalfMoneyLoss(battle.PlayerMoney));

            if (round.OpponentScore.IsBurst)
                opponentMoneyLost = battle.LoseOpponentMoney(CalculateHalfMoneyLoss(battle.OpponentMoney));
        }

        return new RoundResolution(winner, opponentMoneyLost, playerMoneyLost);
    }

    public static Combatant? DetermineWinner(ScoreResult player, ScoreResult opponent)
    {
        if (player.IsBurst && opponent.IsBurst)
            return null;

        if (player.IsBurst)
            return Combatant.Opponent;

        if (opponent.IsBurst)
            return Combatant.Player;

        if (player.IsBlackjack && !opponent.IsBlackjack)
            return Combatant.Player;

        if (opponent.IsBlackjack && !player.IsBlackjack)
            return Combatant.Opponent;

        if (player.FinalScore == opponent.FinalScore)
            return null;

        return player.FinalScore > opponent.FinalScore ? Combatant.Player : Combatant.Opponent;
    }

    private static int CalculateHalfMoneyLoss(int currentMoney)
    {
        if (currentMoney <= 0)
            return 0;

        return Math.Max(1, currentMoney / 2);
    }
}

public static class HealthResolver
{
    public static RoundResolution ResolveRound(BattleState battle, RoundState round)
    {
        return MoneyResolver.ResolveRound(battle, round);
    }
}

public readonly struct RoundResolution
{
    public RoundResolution(Combatant? winner, int opponentDamage, int playerDamage)
    {
        Winner = winner;
        OpponentDamage = opponentDamage;
        PlayerDamage = playerDamage;
        OpponentMoneyLost = opponentDamage;
        PlayerMoneyLost = playerDamage;
    }

    public Combatant? Winner { get; }
    public int OpponentDamage { get; }
    public int PlayerDamage { get; }
    public int OpponentMoneyLost { get; }
    public int PlayerMoneyLost { get; }
}
