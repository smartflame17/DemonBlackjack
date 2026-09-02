using System.Collections.Generic;

public static class ActiveItemResolver
{
    public const string RejectLastHit = "hit_to_played";
    public const string DrawThree = "draw_three";
    public const string DoubleWager = "double_wager";
    public const string LeverageTriple = "leverage_triple";
    public const string BurstThresholdPlusThree = "burst_threshold_plus_three";
    public const string ReturnPlayerFieldCardToHand = "return_player_field_card_to_hand";
    public const string DrawTwoHearts = "draw_two_hearts";
    public const string DrawTwoDiamonds = "draw_two_diamonds";
    public const string DrawTwoClubs = "draw_two_clubs";
    public const string DrawTwoSpades = "draw_two_spades";
    public const string BurstDeny = "burst_deny";
    public const string StockBuy = "stock_buy";
    public const string StockSell = "stock_sell";

    public static bool CanApply(string itemId, BattleState battle)
    {
        if (string.IsNullOrWhiteSpace(itemId) || battle == null || battle.CurrentRound == null)
            return false;

        return itemId switch
        {
            RejectLastHit => battle.CanTargetPlayerPlayedCard(),
            DrawThree => battle.CanDrawPlayerCards(),
            DoubleWager => battle.CanDoubleCurrentRoundWager(),
            LeverageTriple => battle.CanTripleCurrentRoundWager(),
            BurstThresholdPlusThree => battle.CanIncreasePlayerBurstThreshold(GameplayConstants.ActiveItemValue.BurstThresholdPlusThreeItemIncrease),
            BurstDeny => battle.CanSuppressPlayerBurstPenalty(),
            StockBuy => battle.PlayerMoney > 0,
            StockSell => battle.RunState.HasActiveItem(StockSell),
            ReturnPlayerFieldCardToHand => battle.CanTargetPlayerPlayedCard(),
            _ => false
        } || TryGetSuitDraw(itemId, out Suit suit) && battle.CanDrawPlayerCards(suit);
    }

    public static bool TryApply(string itemId, BattleState battle)
    {
        if (!CanApply(itemId, battle))
            return false;

        return itemId switch
        {
            RejectLastHit => false,
            DrawThree => battle.DrawCardsToPlayerHand(GameplayConstants.ActiveItemValue.DrawThreeItemDrawCount) > 0,
            DoubleWager => battle.DoubleCurrentRoundWager(),
            LeverageTriple => battle.TripleCurrentRoundWager(),
            BurstThresholdPlusThree => battle.IncreasePlayerBurstThreshold(GameplayConstants.ActiveItemValue.BurstThresholdPlusThreeItemIncrease),
            BurstDeny => battle.SuppressPlayerBurstPenalty(),
            ReturnPlayerFieldCardToHand => false,
            _ => false
        } || TryGetSuitDraw(itemId, out Suit suit) && battle.DrawCardsToPlayerHand(suit, GameplayConstants.ActiveItemValue.DrawTwoItemDrawCount) > 0;
    }

    public static bool RequiresCardSelection(string itemId)
    {
        return itemId == RejectLastHit || itemId == ReturnPlayerFieldCardToHand;
    }

    public static bool RequiresMoneyInput(string itemId)
    {
        return itemId == StockBuy;
    }

    public static bool CanApply(ActiveItemRuntimeState item, BattleState battle)
    {
        if (item == null || battle == null)
            return false;

        if (string.Equals(item.ItemId, StockSell, System.StringComparison.OrdinalIgnoreCase))
            return item.IsStockHolding && battle.CurrentRound != null;

        return item.IsStockHolding
            ? battle.CurrentRound != null
            : CanApply(item.ItemId, battle);
    }

    public static bool TryApplySelection(string itemId, BattleState battle, IReadOnlyList<int> selectedIndices)
    {
        if (!RequiresCardSelection(itemId)
            || battle == null
            || selectedIndices == null
            || selectedIndices.Count != 1)
        {
            return false;
        }

        int selectedIndex = selectedIndices[0];
        return itemId switch
        {
            RejectLastHit => battle.DiscardPlayerPlayedCard(selectedIndex),
            ReturnPlayerFieldCardToHand => battle.ReturnPlayerPlayedCardToHand(selectedIndex),
            _ => false
        };
    }

    private static bool TryGetSuitDraw(string itemId, out Suit suit)
    {
        switch (itemId)
        {
            case DrawTwoHearts:
                suit = Suit.Hearts;
                return true;
            case DrawTwoDiamonds:
                suit = Suit.Diamonds;
                return true;
            case DrawTwoClubs:
                suit = Suit.Clubs;
                return true;
            case DrawTwoSpades:
                suit = Suit.Spades;
                return true;
            default:
                suit = default;
                return false;
        }
    }
}
