public readonly struct BattleStartedEvent
{
    public BattleStartedEvent(string encounterId, int seed, int playerMoney, int opponentMoney)
    {
        EncounterId = encounterId;
        Seed = seed;
        PlayerMoney = playerMoney;
        OpponentMoney = opponentMoney;
    }

    public string EncounterId { get; }
    public int Seed { get; }
    public int PlayerMoney { get; }
    public int OpponentMoney { get; }
}

public readonly struct BattleEndedEvent
{
    public BattleEndedEvent(BattleResult result)
    {
        Result = result;
    }

    public BattleResult Result { get; }
}

public readonly struct MoneyChangedEvent
{
    public MoneyChangedEvent(Combatant owner, int currentMoney, int delta)
    {
        Owner = owner;
        CurrentMoney = currentMoney;
        Delta = delta;
    }

    public Combatant Owner { get; }
    public int CurrentMoney { get; }
    public int Delta { get; }
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

public readonly struct ItemUsedEvent
{
    public ItemUsedEvent(string itemId)
    {
        ItemId = itemId;
    }

    public string ItemId { get; }
}

public readonly struct VisualsResolvedEvent
{
    public VisualsResolvedEvent(VisualCommandType commandType)
    {
        CommandType = commandType;
    }

    public VisualCommandType CommandType { get; }
}

public readonly struct RunPhaseChangedEvent
{
    public RunPhaseChangedEvent(RunPhase phase)
    {
        Phase = phase;
    }

    public RunPhase Phase { get; }
}

public readonly struct BattlePhaseChangedEvent
{
    public BattlePhaseChangedEvent(BattlePhase phase)
    {
        Phase = phase;
    }

    public BattlePhase Phase { get; }
}
