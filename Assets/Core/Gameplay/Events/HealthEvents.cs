public readonly struct DamageTakenEvent
{
    public DamageTakenEvent(Combatant target, int amount, int remainingHp)
    {
        Target = target;
        Amount = amount;
        RemainingHp = remainingHp;
    }

    public Combatant Target { get; }
    public int Amount { get; }
    public int RemainingHp { get; }
}

public readonly struct HealingReceivedEvent
{
    public HealingReceivedEvent(Combatant target, int amount, int currentHp)
    {
        Target = target;
        Amount = amount;
        CurrentHp = currentHp;
    }

    public Combatant Target { get; }
    public int Amount { get; }
    public int CurrentHp { get; }
}

public readonly struct DeathPreventedEvent
{
    public DeathPreventedEvent(Combatant target, bool wasPrevented)
    {
        Target = target;
        WasPrevented = wasPrevented;
    }

    public Combatant Target { get; }
    public bool WasPrevented { get; }
}
