public readonly struct ScoreCalculatedEvent
{
    public ScoreCalculatedEvent(Combatant combatant, ScoreResult score)
    {
        Combatant = combatant;
        Score = score;
    }

    public Combatant Combatant { get; }
    public ScoreResult Score { get; }
}

public readonly struct PokerResolvedEvent
{
    public PokerResolvedEvent(Combatant combatant, PokerResult poker)
    {
        Combatant = combatant;
        Poker = poker;
    }

    public Combatant Combatant { get; }
    public PokerResult Poker { get; }
}

public readonly struct BurstAttemptedEvent
{
    public BurstAttemptedEvent(Combatant combatant, int score, int threshold, bool willBurst)
    {
        Combatant = combatant;
        Score = score;
        Threshold = threshold;
        WillBurst = willBurst;
    }

    public Combatant Combatant { get; }
    public int Score { get; }
    public int Threshold { get; }
    public bool WillBurst { get; }
}

public readonly struct BurstOccurredEvent
{
    public BurstOccurredEvent(Combatant combatant, int score)
    {
        Combatant = combatant;
        Score = score;
    }

    public Combatant Combatant { get; }
    public int Score { get; }
}

public readonly struct BurstThresholdChangedEvent
{
    public BurstThresholdChangedEvent(Combatant combatant, int threshold)
    {
        Combatant = combatant;
        Threshold = threshold;
    }

    public Combatant Combatant { get; }
    public int Threshold { get; }
}

public readonly struct BlackjackAchievedEvent
{
    public BlackjackAchievedEvent(Combatant combatant, int score)
    {
        Combatant = combatant;
        Score = score;
    }

    public Combatant Combatant { get; }
    public int Score { get; }
}
