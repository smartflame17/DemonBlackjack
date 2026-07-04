using System;

public static class MoneyResolver
{
    public static RoundResolution ResolveRound(BattleState battle, RoundState round, bool applyBurstPenalty)
    {
        if (battle == null)
            throw new ArgumentNullException(nameof(battle));

        if (round == null)
            throw new ArgumentNullException(nameof(round));

        Combatant? winner = DetermineWinner(round.PlayerScore, round.OpponentScore);
        int opponentMoneyLost = 0;
        int playerMoneyLost = 0;

        int wager = Math.Max(0, round.EffectiveWager);
        if (wager > 0 && applyBurstPenalty)
            ResolveBurstTransfers(battle, round, wager, ref opponentMoneyLost, ref playerMoneyLost);

        ResolveBlackjackPot(battle, round, winner);

        if (battle.PlayerMoney > 0 && wager > 0)
            ResolvePlayerPokerPayout(battle, round, wager, ref opponentMoneyLost);

        return new RoundResolution(winner, opponentMoneyLost, playerMoneyLost);
    }

    public static Combatant? DetermineWinner(ScoreResult player, ScoreResult opponent)
    {
        if (player.IsBlackjack && !opponent.IsBlackjack)
            return Combatant.Player;

        if (opponent.IsBlackjack && !player.IsBlackjack)
            return Combatant.Opponent;

        if (player.IsBurst && !opponent.IsBurst)
            return Combatant.Opponent;

        if (opponent.IsBurst && !player.IsBurst)
            return Combatant.Player;
            
        if (player.FinalScore == opponent.FinalScore)
            return null;

        return player.FinalScore > opponent.FinalScore ? Combatant.Player : Combatant.Opponent;
    }

    private static void ResolveBurstTransfers(
        BattleState battle,
        RoundState round,
        int wager,
        ref int opponentMoneyLost,
        ref int playerMoneyLost)
    {
        int playerBurstOffset = Math.Max(0, round.PlayerScore.BlackjackScore - round.PlayerBurstThreshold);
        if (playerBurstOffset > 0)
            playerMoneyLost += TransferPlayerToOpponent(battle, playerBurstOffset * wager);

        int opponentBurstOffset = Math.Max(0, round.OpponentScore.BlackjackScore - round.OpponentBurstThreshold);
        if (opponentBurstOffset > 0)
            opponentMoneyLost += TransferOpponentToPlayer(battle, opponentBurstOffset * wager);
    }

    private static void ResolveBlackjackPot(BattleState battle, RoundState round, Combatant? winner)
    {
        if (round.Pot <= 0)
            return;

        if (winner == Combatant.Player)
            battle.AddPlayerMoney(round.Pot);
        else if (winner == Combatant.Opponent)
            battle.AddOpponentMoney(round.Pot);
        else
        {
            battle.AddPlayerMoney(round.PlayerStake);
            battle.AddOpponentMoney(round.OpponentStake);
        }
    }

    private static void ResolvePlayerPokerPayout(BattleState battle, RoundState round, int wager, ref int opponentMoneyLost)
    {
        PokerResult poker = ScoreResolver.ResolvePoker(round.GetPokerCardsForPlayerPayout());
        if (poker.Multiplier <= 0)
            return;

        opponentMoneyLost += TransferOpponentToPlayer(battle, wager * poker.Multiplier);
    }

    private static int TransferPlayerToOpponent(BattleState battle, int amount)
    {
        int lost = battle.LosePlayerMoney(amount);
        if (lost > 0)
            battle.AddOpponentMoney(lost);

        return lost;
    }

    private static int TransferOpponentToPlayer(BattleState battle, int amount)
    {
        int lost = battle.LoseOpponentMoney(amount);
        if (lost > 0)
            battle.AddPlayerMoney(lost);

        return lost;
    }
}

public readonly struct RoundResolution
{
    public RoundResolution(Combatant? winner, int opponentMoneyLost, int playerMoneyLost)
    {
        Winner = winner;
        OpponentMoneyLost = opponentMoneyLost;
        PlayerMoneyLost = playerMoneyLost;
    }

    public Combatant? Winner { get; }
    public int OpponentMoneyLost { get; }
    public int PlayerMoneyLost { get; }
}
