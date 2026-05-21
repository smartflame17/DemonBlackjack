using System;

public static class HealthResolver
{
    public static RoundResolution ResolveRound(BattleState battle, RoundState round)
    {
        if (battle == null)
            throw new ArgumentNullException(nameof(battle));

        if (round == null)
            throw new ArgumentNullException(nameof(round));

        Combatant? winner = DetermineWinner(round.PlayerScore, round.OpponentScore);

        if (winner == Combatant.Player)
        {
            int damage = CalculateDamage(round.PlayerScore, round.Wager);
            battle.DamageOpponent(damage);
            return new RoundResolution(winner, damage, 0);
        }

        if (winner == Combatant.Opponent)
        {
            int damage = CalculateDamage(round.OpponentScore, round.Wager);
            battle.DamagePlayer(damage);
            return new RoundResolution(winner, 0, damage);
        }

        return new RoundResolution(null, 0, 0);
    }

    private static Combatant? DetermineWinner(ScoreResult player, ScoreResult opponent)
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

    // TODO: damage calculation needs to account for burst and blackjack conditions, as well as potential future modifiers
    // Burst deals half of current hp damage
    private static int CalculateDamage(ScoreResult score, int wager)
    {
        int blackjackBonus = score.IsBlackjack ? 2 : 0;
        return Math.Max(1, wager + score.PokerMultiplier + blackjackBonus);
    }
}

public readonly struct RoundResolution
{
    public RoundResolution(Combatant? winner, int opponentDamage, int playerDamage)
    {
        Winner = winner;
        OpponentDamage = opponentDamage;
        PlayerDamage = playerDamage;
    }

    public Combatant? Winner { get; }
    public int OpponentDamage { get; }
    public int PlayerDamage { get; }
}
