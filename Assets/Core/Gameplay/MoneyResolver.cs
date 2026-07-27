using System;
using System.Collections.Generic;
using GameplayConstants;

public static class MoneyResolver
{
    public static RoundResolution ResolveRound(BattleState battle, RoundState round, bool applyBurstPenalty)
    {
        if (battle == null)
            throw new ArgumentNullException(nameof(battle));

        if (round == null)
            throw new ArgumentNullException(nameof(round));

        Combatant? winner = DetermineWinner(round.PlayerScore, round.OpponentScore);
        int opponentWinBonus = winner == Combatant.Opponent
            ? Math.Max(0, battle.GetOpponentWinBonus(round))
            : 0;
        //WARNING: now that burst transfer is realtime, we dont need round end burst transfer?
        int wager = Math.Max(0, round.EffectiveWager);
        if (wager > 0 && applyBurstPenalty)
            ResolveBurstTransfers(battle, round, wager);

        ResolveBlackjackPot(battle, round, winner);

        // Devil ability payout at end
        if (opponentWinBonus > 0)
        {
            int lost = TransferPlayerToOpponent(battle, opponentWinBonus);
            if (lost > 0)
            {
                round.RecordMoneyLost(Combatant.Player, lost);
                EventBus.Publish(new MoneyTransferReasonEvent(MoneyTransferReason.DevilAbilityPayout, lost));
            }
        }

        // WARNING: Like burst transfer, we dont need round end poker transfer?
        if (battle.PlayerMoney > 0 && wager > 0)
            ResolvePlayerPokerPayout(battle, round, wager);

        return new RoundResolution(winner, round.OpponentMoneyLost, round.PlayerMoneyLost);
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

    public static void ResolveBurstTransfers(
        BattleState battle,
        RoundState round,
        int wager)
    {
        int playerBurstOffset = Math.Max(0, round.PlayerScore.BlackjackScore - round.PlayerBurstThreshold);
        if (playerBurstOffset > 0 && !round.PlayerBurstPenaltyResolved)
        {
            // Version 1: Transfer based on wager
            //int amount = playerBurstOffset * wager;

            // Version 2: Fixed amount
            int amount = playerBurstOffset * BurstTransferValue.burstPenaltyAmount;
            int lost = TransferPlayerToOpponent(battle, amount);
            round.RecordMoneyLost(Combatant.Player, lost);
            round.MarkBurstPenaltyResolved(Combatant.Player);
            EventBus.Publish(new MoneyTransferReasonEvent(MoneyTransferReason.BurstPenalty, amount));
        }

        int opponentBurstOffset = Math.Max(0, round.OpponentScore.BlackjackScore - round.OpponentBurstThreshold);
        if (opponentBurstOffset > 0 && !round.OpponentBurstPenaltyResolved)
        {
            // Version 1: Transfer based on wager
            //int amount = opponentBurstOffset * wager;

            // Version 2: Fixed amount
            int amount = opponentBurstOffset * BurstTransferValue.burstPenaltyAmount;
            int lost = TransferOpponentToPlayer(battle, amount);
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

    public static void ResolvePlayerPokerPayout(BattleState battle, RoundState round, int wager)
    {
        IReadOnlyList<Card> cards = round.GetPokerCardsForPlayerPayout();
        PokerResult poker = ScoreResolver.ResolvePoker(cards);
        int payout = CalculatePokerPayout(cards, poker, wager);
        if (payout <= 0)
            return;

        int lost = TransferOpponentToPlayer(battle, payout);
        round.RecordMoneyLost(Combatant.Opponent, lost);
        EventBus.Publish(new MoneyTransferReasonEvent(MoneyTransferReason.PokerPayout, payout));
    }

    public static int CalculatePokerPayout(IReadOnlyList<Card> cards, PokerResult poker, int wager)
    {
        if (cards == null || wager <= 0 || poker.Multiplier <= 0 || poker.CardIndices == null || poker.CardIndices.Count == 0)
            return 0;

        int rankSum = 0;
        int highestRank = 0;
        for (int i = 0; i < poker.CardIndices.Count; i++)
        {
            int cardIndex = poker.CardIndices[i];
            if (cardIndex < 0 || cardIndex >= cards.Count)
                return 0;

            int rank = RankToPokerValue(cards[cardIndex].Rank);     // Don't really need to convert to poker value, but this is more consistent, and open for future extensions on scoring
            rankSum += rank;
            highestRank = Math.Max(highestRank, rank);
        }
        /*
        // Version 1: Payout based per rank
        long payout = poker.Rank switch
        {
            PokerHandRank.Pair
                or PokerHandRank.TwoPair
                or PokerHandRank.ThreeOfAKind
                or PokerHandRank.FourOfAKind
                or PokerHandRank.FullHouse
                => (long)poker.Multiplier * rankSum * wager,
            PokerHandRank.Straight
                or PokerHandRank.StraightFlush
                => (long)poker.Multiplier * poker.CardIndices.Count * highestRank * wager,
            PokerHandRank.Flush
                => (long)poker.Multiplier * rankSum * wager / poker.CardIndices.Count,
            PokerHandRank.RoyalFlush
                => (long)poker.Multiplier * wager,
            _ => 0
        };
        */
        // Version 2: Fixed Payout
        long payout = (long)poker.Multiplier * rankSum / poker.CardIndices.Count * PokerTransferValue.pokerPayoutAmount;

        return payout >= int.MaxValue ? int.MaxValue : (int)payout;
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

    public static int RankToPokerValue(Rank rank)
    {
        return rank switch
        {
            Rank.Two => 2,
            Rank.Three => 3,
            Rank.Four => 4,
            Rank.Five => 5,
            Rank.Six => 6,
            Rank.Seven => 7,
            Rank.Eight => 8,
            Rank.Nine => 9,
            Rank.Ten => 10,
            Rank.Jack => 11,
            Rank.Queen => 12,
            Rank.King => 13,
            Rank.Ace => 14,
            _ => throw new ArgumentOutOfRangeException(nameof(rank), $"Invalid rank: {rank}")
        };
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
