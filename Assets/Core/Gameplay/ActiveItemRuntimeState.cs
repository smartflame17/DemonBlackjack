using System;

public sealed class ActiveItemRuntimeState
{
    private ActiveItemRuntimeState(string itemId, int stockOriginalAmount, int stockPricePercent)
    {
        ItemId = itemId;
        StockOriginalAmount = Math.Max(0, stockOriginalAmount);
        StockPricePercent = StockValueResolver.ClampPricePercent(stockPricePercent);
    }

    public string ItemId { get; }
    public int StockOriginalAmount { get; }
    public int StockPricePercent { get; }
    public int StockCurrentAmount => StockValueResolver.CalculateCurrentAmount(StockOriginalAmount, StockPricePercent);
    public bool IsStockHolding => string.Equals(ItemId, ActiveItemResolver.StockSell, StringComparison.OrdinalIgnoreCase)
        && StockOriginalAmount > 0;

    public static ActiveItemRuntimeState Create(string itemId)
    {
        return string.IsNullOrWhiteSpace(itemId)
            ? null
            : new ActiveItemRuntimeState(itemId, 0, StockValueResolver.InitialPricePercent);
    }

    public static ActiveItemRuntimeState CreateStockHolding(int originalAmount, int pricePercent = StockValueResolver.InitialPricePercent)
    {
        if (originalAmount <= 0)
            return null;

        return new ActiveItemRuntimeState(ActiveItemResolver.StockSell, originalAmount, pricePercent);
    }

    public ActiveItemRuntimeState WithStockPricePercent(int pricePercent)
    {
        return IsStockHolding
            ? new ActiveItemRuntimeState(ItemId, StockOriginalAmount, pricePercent)
            : this;
    }
}

public static class StockValueResolver
{
    public const int MinimumPricePercent = 10;
    public const int MaximumPricePercent = 200;
    public const int InitialPricePercent = 100;
    public const int ChangeStepPercent = 10;
    public const int MinimumChangeStep = -4;
    public const int MaximumChangeStep = 4;

    public static int ApplyChangeStep(int currentPricePercent, int changeStep)
    {
        int normalizedStep = Math.Clamp(changeStep, MinimumChangeStep, MaximumChangeStep);
        return ClampPricePercent(currentPricePercent + normalizedStep * ChangeStepPercent);
    }

    public static int ClampPricePercent(int pricePercent)
    {
        return Math.Clamp(pricePercent, MinimumPricePercent, MaximumPricePercent);
    }

    public static int CalculateCurrentAmount(int originalAmount, int pricePercent)
    {
        if (originalAmount <= 0)
            return 0;

        long value = (long)originalAmount * ClampPricePercent(pricePercent) / 100L;
        return (int)Math.Min(int.MaxValue, Math.Max(1L, value));
    }
}

public static class ActiveItemTooltipFormatter
{
    public const string StockOriginalAmountToken = "{stock_original_amount}";
    public const string StockCurrentAmountToken = "{stock_current_amount}";

    public static string Format(string description, ActiveItemRuntimeState item)
    {
        if (string.IsNullOrEmpty(description) || item == null || !item.IsStockHolding)
            return description;

        return description
            .Replace(StockOriginalAmountToken, item.StockOriginalAmount.ToString("N0"))
            .Replace(StockCurrentAmountToken, item.StockCurrentAmount.ToString("N0"));
    }
}
