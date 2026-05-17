using System;

[Serializable]
public readonly struct Card
{
    public Card(Suit suit, Rank rank, string modifierId = null)
    {
        Suit = suit;
        Rank = rank;
        ModifierId = modifierId;
    }

    public Suit Suit { get; }
    public Rank Rank { get; }
    public string ModifierId { get; }
    public bool HasModifier => !string.IsNullOrWhiteSpace(ModifierId);

    public int BlackjackValue
    {
        get
        {
            if (Rank == Rank.Ace)
                return 11;

            return (int)Rank >= (int)Rank.Jack ? 10 : (int)Rank;
        }
    }

    public override string ToString()
    {
        return HasModifier ? $"{Rank} of {Suit} [{ModifierId}]" : $"{Rank} of {Suit}";
    }
}
