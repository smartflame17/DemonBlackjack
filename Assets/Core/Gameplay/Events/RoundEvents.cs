// All events here should be published to the battle-scoped local event bus
public readonly struct RoundStartedEvent
{
    public RoundStartedEvent(int roundNumber)
    {
        RoundNumber = roundNumber;
    }

    public int RoundNumber { get; }
}

public readonly struct RoundEndedEvent
{
    public RoundEndedEvent(int roundNumber)
    {
        RoundNumber = roundNumber;
    }

    public int RoundNumber { get; }
}

public readonly struct PlayerTurnStartedEvent
{
}

public readonly struct PlayerTurnEndedEvent
{
}


public readonly struct WagerCommittedEvent
{
    // If isPlayerWager is true, Response indicates the devil's response to the player's wager offer.
    public WagerCommittedEvent(int wagerAmount, bool isPlayerWager, WagerResponse response)
    {
        WagerAmount = wagerAmount;
        IsPlayerWager = isPlayerWager;
        Response = response;
    }
    
    public int WagerAmount { get; }
    public bool IsPlayerWager { get; }
    public WagerResponse Response { get; }
}
