using System;

[Serializable]
public readonly struct Modifier
{
    public Modifier(string id, ModifierOperation operation, int value, int remainingRounds = -1)
    {
        Id = id;
        Operation = operation;
        Value = value;
        RemainingRounds = remainingRounds;
    }

    public string Id { get; }
    public ModifierOperation Operation { get; }
    public int Value { get; }
    public int RemainingRounds { get; }

    public bool IsTemporary => RemainingRounds >= 0;
}

public enum ModifierOperation
{
    Add,
    Multiply,
    Override
}
