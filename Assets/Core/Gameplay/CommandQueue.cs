using System.Collections.Generic;

public sealed class CommandQueue
{
    private readonly Queue<VisualCommand> _commands = new();

    public int Count => _commands.Count;
    public bool HasPendingCommands => _commands.Count > 0;

    public void Enqueue(VisualCommand command)
    {
        _commands.Enqueue(command);
    }

    public bool TryDequeue(out VisualCommand command)
    {
        if (_commands.Count == 0)
        {
            command = default;
            return false;
        }

        command = _commands.Dequeue();
        return true;
    }

    public void Clear()
    {
        _commands.Clear();
    }
}

public readonly struct VisualCommand
{
    public VisualCommand(VisualCommandType type, string payload = null)
    {
        Type = type;
        Payload = payload;
    }

    public VisualCommandType Type { get; }
    public string Payload { get; }
}
