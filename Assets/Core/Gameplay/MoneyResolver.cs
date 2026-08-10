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
        //if (wager > 0 && applyBurstPenalty)
        //    ResolveBurstTransfers(battle, round, wager);

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
        //if (battle.PlayerMoney > 0 && wager > 0)
        //    ResolvePlayerPokerPayout(battle, round, wager);

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
        int playerBurstAmount = CalculateBurstTransferAmount(round.PlayerScore, round.PlayerBurstThreshold);
        if (playerBurstAmount > 0)// && !round.PlayerBurstPenaltyResolved)
        {
            int lost = TransferPlayerToOpponent(battle, playerBurstAmount);
            round.RecordMoneyLost(Combatant.Player, lost);
            round.MarkBurstPenaltyResolved(Combatant.Player);
            EventBus.Publish(new MoneyTransferReasonEvent(MoneyTransferReason.BurstPenalty, playerBurstAmount));
        }

        int opponentBurstAmount = CalculateBurstTransferAmount(round.OpponentScore, round.OpponentBurstThreshold);
        if (opponentBurstAmount > 0)// && !round.OpponentBurstPenaltyResolved)
        {
            int lost = TransferOpponentToPlayer(battle, opponentBurstAmount);
            round.RecordMoneyLost(Combatant.Opponent, lost);
            round.MarkBurstPenaltyResolved(Combatant.Opponent);
            EventBus.Publish(new MoneyTransferReasonEvent(MoneyTransferReason.BurstPenalty, -opponentBurstAmount));
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
        EventBus.Publish(new MoneyTransferReasonEvent(MoneyTransferReason.PokerPayout, lost));
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
        long payout = (long)(poker.Multiplier * rankSum * PokerTransferValue.pokerPayoutAmount) / poker.CardIndices.Count;
        return payout >= int.MaxValue ? int.MaxValue : (int)payout;
    }

    public static MoneyDeltaPreview CalculatePlayerRealtimeMoneyPreview(
        IReadOnlyList<Card> pokerCards,
        ScoreResult playerScore,
        int playerBurstThreshold,
        int wager,
        int playerMoney,
        int opponentMoney)
    {
        if (wager <= 0)
            return default;

        PokerResult poker = ScoreResolver.ResolvePoker(pokerCards);
        int pokerPayout = CalculatePokerPayout(pokerCards, poker, wager);
        int pokerDelta = Math.Min(Math.Max(0, opponentMoney), pokerPayout);

        long playerMoneyAfterPoker = (long)Math.Max(0, playerMoney) + pokerDelta;
        int availablePlayerMoney = playerMoneyAfterPoker >= int.MaxValue
            ? int.MaxValue
            : (int)playerMoneyAfterPoker;
        int burstPenalty = CalculateBurstTransferAmount(playerScore, playerBurstThreshold);
        int burstDelta = -Math.Min(availablePlayerMoney, burstPenalty);

        return new MoneyDeltaPreview(pokerDelta, burstDelta);
    }

    private static int CalculateBurstTransferAmount(ScoreResult score, int burstThreshold)
    {
        int burstOffset = Math.Max(0, score.BlackjackScore - burstThreshold);
        long amount = (long)burstOffset * BurstTransferValue.burstPenaltyAmount;
        return amount >= int.MaxValue ? int.MaxValue : (int)amount;
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

public readonly struct MoneyDeltaPreview
{
    public MoneyDeltaPreview(int pokerDelta, int burstDelta)
    {
        PokerDelta = Math.Max(0, pokerDelta);
        BurstDelta = Math.Min(0, burstDelta);
    }

    public int PokerDelta { get; }
    public int BurstDelta { get; }
    public int FinalDelta => PokerDelta + BurstDelta;
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
