public readonly struct ModifierAddedEvent
{
    public ModifierAddedEvent(Modifier modifier)
    {
        Modifier = modifier;
    }

    public Modifier Modifier { get; }
}

public readonly struct ModifierExpiredEvent
{
    public ModifierExpiredEvent(Modifier modifier)
    {
        Modifier = modifier;
    }

    public Modifier Modifier { get; }
}
