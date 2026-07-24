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
        int opponentMoneyLost = round.OpponentMoneyLost;
        int playerMoneyLost = round.PlayerMoneyLost;
        int opponentWinBonus = winner == Combatant.Opponent
            ? Math.Max(0, battle.GetOpponentWinBonus(round))
            : 0;
        //WARNING: now that burst transfer is realtime, we dont need round end burst transfer?
        int wager = Math.Max(0, round.EffectiveWager);
        if (wager > 0 && applyBurstPenalty)
            ResolveBurstTransfers(battle, round, wager, ref opponentMoneyLost, ref playerMoneyLost);

        ResolveBlackjackPot(battle, round, winner);

        // Devil ability payout at end
        if (opponentWinBonus > 0)
        {
            int lost = TransferPlayerToOpponent(battle, opponentWinBonus);
            if (lost > 0)
            {
                playerMoneyLost += lost;
                round.RecordMoneyLost(Combatant.Player, lost);
                EventBus.Publish(new MoneyTransferReasonEvent(MoneyTransferReason.DevilAbilityPayout, -lost));
            }
        }

        // WARNING: Like burst transfer, we dont need round end poker transfer?
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
        if (playerBurstOffset > 0 && !round.PlayerBurstPenaltyResolved)
        {
            int amount = playerBurstOffset * wager;
            int lost = TransferPlayerToOpponent(battle, amount);
            playerMoneyLost += lost;
            round.RecordMoneyLost(Combatant.Player, lost);
            round.MarkBurstPenaltyResolved(Combatant.Player);
            EventBus.Publish(new MoneyTransferReasonEvent(MoneyTransferReason.BurstPenalty, amount));
        }

        int opponentBurstOffset = Math.Max(0, round.OpponentScore.BlackjackScore - round.OpponentBurstThreshold);
        if (opponentBurstOffset > 0 && !round.OpponentBurstPenaltyResolved)
        {
            int amount = opponentBurstOffset * wager;
            int lost = TransferOpponentToPlayer(battle, amount);
            opponentMoneyLost += lost;
            round.RecordMoneyLost(Combatant.Opponent, lost);
            round.MarkBurstPenaltyResolved(Combatant.Opponent);
            EventBus.Publish(new MoneyTransferReasonEvent(MoneyTransferReason.BurstPenalty, -amount));
        }
    }

    private static void ResolveBlackjackPot(BattleState battle, RoundState round, Combatant? winner)
    {
        if (round.Pot <= 0)
            return;

        if (winner == Combatant.Player)
        {
            battle.AddPlayerMoney(round.Pot);
            EventBus.Publish(new MoneyTransferReasonEvent(MoneyTransferReason.BlackjackPayout, round.Pot));
        }
            
        else if (winner == Combatant.Opponent)
        {
            battle.AddOpponentMoney(round.Pot);
            EventBus.Publish(new MoneyTransferReasonEvent(MoneyTransferReason.BlackjackPayout, -round.Pot));
        }
        else
        {
            battle.AddPlayerMoney(round.PlayerStake);
            battle.AddOpponentMoney(round.OpponentStake);
            EventBus.Publish(new MoneyTransferReasonEvent(MoneyTransferReason.BlackjackPayout, 0));
        }
    }

    private static void ResolvePlayerPokerPayout(BattleState battle, RoundState round, int wager, ref int opponentMoneyLost)
    {
        PokerResult poker = ScoreResolver.ResolvePoker(round.GetPokerCardsForPlayerPayout());
        if (poker.Multiplier <= 0)
            return;

        opponentMoneyLost += TransferOpponentToPlayer(battle, wager * poker.Multiplier);
        EventBus.Publish(new MoneyTransferReasonEvent(MoneyTransferReason.PokerPayout, wager * poker.Multiplier));
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
