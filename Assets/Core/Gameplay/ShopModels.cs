using System;

public enum ShopOfferType
{
    ActiveItem,
    Relic,
    CardUpgrade
}

public enum ShopPurchaseFailure
{
    None,
    InvalidOffer,
    AlreadyPurchased,
    AlreadyOwned,
    InsufficientFunds
}

public readonly struct ShopPurchaseResult
{
    public ShopPurchaseResult(bool succeeded, ShopPurchaseFailure failure, int price, int refund)
    {
        Succeeded = succeeded;
        Failure = failure;
        Price = price;
        Refund = refund;
    }

    public bool Succeeded { get; }
    public ShopPurchaseFailure Failure { get; }
    public int Price { get; }
    public int Refund { get; }
    public int NetCost => Math.Max(0, Price - Refund);

    public static ShopPurchaseResult Success(int price, int refund = 0) => new(true, ShopPurchaseFailure.None, price, refund);
    public static ShopPurchaseResult Failed(ShopPurchaseFailure failure, int price = 0, int refund = 0) => new(false, failure, price, refund);
}

[Serializable]
public readonly struct OwnedRankUpgrade
{
    public OwnedRankUpgrade(Rank rank, string upgradeId, int paidPrice)
    {
        Rank = rank;
        UpgradeId = upgradeId;
        PaidPrice = Math.Max(0, paidPrice);
    }

    public Rank Rank { get; }
    public string UpgradeId { get; }
    public int PaidPrice { get; }
}
