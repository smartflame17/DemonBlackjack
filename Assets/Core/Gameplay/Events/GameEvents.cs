// Global event bus: battle lifetime events are published once by runtime orchestration.
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

// Global event bus: player changes originate from RunState and opponent changes from BattleState.
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
// Global event bus. For informing UI why money has changed; publish once per transfer manually.
// TODO: Consider adding multipliers for better ui representation later.
public readonly struct MoneyTransferReasonEvent
{
    public MoneyTransferReasonEvent(Combatant receiver, MoneyTransferReason reason, int delta)
    {
        Receiver = receiver;
        Reason = reason;
        Delta = delta;
    }
    public Combatant Receiver { get; }
    public MoneyTransferReason Reason { get; }
    public int Delta { get; }
}

// Battle-scoped local event bus.
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

// Global event bus: published after an active item is applied and consumed successfully.
public readonly struct ItemUsedEvent
{
    public ItemUsedEvent(string itemId)
    {
        ItemId = itemId;
    }

    public string ItemId { get; }
}

// Battle-scoped local event bus.
public readonly struct VisualsResolvedEvent
{
    public VisualsResolvedEvent(VisualCommandType commandType)
    {
        CommandType = commandType;
    }

    public VisualCommandType CommandType { get; }
}

// Global event bus.
public readonly struct RunPhaseChangedEvent
{
    public RunPhaseChangedEvent(RunPhase phase)
    {
        Phase = phase;
    }

    public RunPhase Phase { get; }
}

// Battle-scoped local event bus.
public readonly struct BattlePhaseChangedEvent
{
    public BattlePhaseChangedEvent(BattlePhase phase)
    {
        Phase = phase;
    }

    public BattlePhase Phase { get; }
}

// Battle-scoped local event bus.
public readonly struct DevilTurnChoiceEvent
{
    public DevilTurnChoiceEvent(DevilTurnChoice choice)
    {
        Choice = choice;
    }

    public DevilTurnChoice Choice { get; }
}
