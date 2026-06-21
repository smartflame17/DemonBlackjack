public readonly struct ShopOpenedEvent
{
    public ShopOpenedEvent(int encounterIndex, int roundNumber)
    {
        EncounterIndex = encounterIndex;
        RoundNumber = roundNumber;
    }

    public int EncounterIndex { get; }
    public int RoundNumber { get; }
}

public readonly struct ShopClosedEvent
{
    public ShopClosedEvent(int roundNumber) => RoundNumber = roundNumber;
    public int RoundNumber { get; }
}

public readonly struct ShopPurchaseSucceededEvent
{
    public ShopPurchaseSucceededEvent(ShopOfferType type, string contentId, Rank? rank, int price, int refund)
    {
        Type = type;
        ContentId = contentId;
        Rank = rank;
        Price = price;
        Refund = refund;
    }

    public ShopOfferType Type { get; }
    public string ContentId { get; }
    public Rank? Rank { get; }
    public int Price { get; }
    public int Refund { get; }
}

public readonly struct ShopPurchaseFailedEvent
{
    public ShopPurchaseFailedEvent(ShopOfferType type, string contentId, Rank? rank, ShopPurchaseFailure failure)
    {
        Type = type;
        ContentId = contentId;
        Rank = rank;
        Failure = failure;
    }

    public ShopOfferType Type { get; }
    public string ContentId { get; }
    public Rank? Rank { get; }
    public ShopPurchaseFailure Failure { get; }
}

public readonly struct RelicAddedEvent
{
    public RelicAddedEvent(string relicId) => RelicId = relicId;
    public string RelicId { get; }
}

public readonly struct ActiveItemAddedEvent
{
    public ActiveItemAddedEvent(string itemId, int count)
    {
        ItemId = itemId;
        Count = count;
    }
    public string ItemId { get; }
    public int Count { get; }
}

public readonly struct RankUpgradeChangedEvent
{
    public RankUpgradeChangedEvent(Rank rank, string previousUpgradeId, string upgradeId)
    {
        Rank = rank;
        PreviousUpgradeId = previousUpgradeId;
        UpgradeId = upgradeId;
    }
    public Rank Rank { get; }
    public string PreviousUpgradeId { get; }
    public string UpgradeId { get; }
}
