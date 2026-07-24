using System;

public static class CardModifierResolver
{
    public const string RankToHearts = "rank_to_hearts";
    public const string RankToDiamonds = "rank_to_diamonds";
    public const string RankToClubs = "rank_to_clubs";
    public const string RankToSpades = "rank_to_spades";
    public const string NegativeRank = "negative_rank";
    public const string HitLower = "hit_lower";
    public const string HitHigher = "hit_higher";
    public const string DrawSuit = "draw_suit";
    public const string DrawRank = "draw_rank";
    public const string MoveJack = "move_jack";
    public const string MoveOpponentToPlayer = "move_opponent_to_player";
    public const string DemoteOpponentCard = "demote_opponent_card";
    public const string CopyQueen = "copy_queen";
    public const string OutsourceOpponentCard = "outsource_opponent_card";
    public const string DuplicateKing = "duplicate_king";
    public const string DuplicateOpponentCard = "duplicate_opponent_card";
    public const string HitTopDeckCard = "hit_top_deck_card";
    public const string SplitPreviousCard = "split_previous_card";
    public const string DoubleCardValue = "double_card_value";
    public const string HalfCardValue = "half_card_value";
    public const string TriggerPreviousEffect = "trigger_previous_effect";
    public const string DiscardLowestNumber = "discard_lowest_number";
    public const string JokerValue = "joker_value";
    public const string JokerValuePrefix = "joker_value_";
    public const string ZeroThenForceHit = "zero_then_force_hit";

    public static Card Apply(RunCard runCard, string modifierId)
    {
        if (string.IsNullOrWhiteSpace(modifierId))
            return runCard.ToBattleCard();

        return modifierId switch
        {
            RankToHearts => new Card(Suit.Hearts, runCard.Rank, modifierId),
            RankToDiamonds => new Card(Suit.Diamonds, runCard.Rank, modifierId),
            RankToClubs => new Card(Suit.Clubs, runCard.Rank, modifierId),
            RankToSpades => new Card(Suit.Spades, runCard.Rank, modifierId),
            _ => runCard.ToBattleCard(modifierId)
        };
    }

    public static Card Apply(Card card, string modifierId)
    {
        if (string.IsNullOrWhiteSpace(modifierId))
            return card;

        return modifierId switch
        {
            RankToHearts => new Card(Suit.Hearts, card.Rank, modifierId),
            RankToDiamonds => new Card(Suit.Diamonds, card.Rank, modifierId),
            RankToClubs => new Card(Suit.Clubs, card.Rank, modifierId),
            RankToSpades => new Card(Suit.Spades, card.Rank, modifierId),
            _ => new Card(card.Suit, card.Rank, modifierId)
        };
    }

    public static string CreateResolvedJokerValue(int value)
    {
        return $"{JokerValuePrefix}{value}";
    }

    public static bool TryGetResolvedJokerValue(string modifierId, out int value)
    {
        if (!string.IsNullOrWhiteSpace(modifierId)
            && modifierId.StartsWith(JokerValuePrefix, StringComparison.Ordinal)
            && int.TryParse(modifierId.Substring(JokerValuePrefix.Length), out value))
        {
            if (value == 1 || value == 2 || value == 12 || value == 21)
                return true;
        }

        value = 0;
        return false;
    }
}
