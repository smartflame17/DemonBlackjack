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
