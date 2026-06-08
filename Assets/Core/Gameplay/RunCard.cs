using System;

[Serializable]
public readonly struct RunCard
{
    public RunCard(string instanceId, Suit suit, Rank rank, string modifierId = null)
    {
        InstanceId = string.IsNullOrWhiteSpace(instanceId) ? CreateInstanceId(suit, rank) : instanceId;
        Suit = suit;
        Rank = rank;
        ModifierId = modifierId;
    }

    public string InstanceId { get; }
    public Suit Suit { get; }
    public Rank Rank { get; }
    public string ModifierId { get; }
    public bool HasModifier => !string.IsNullOrWhiteSpace(ModifierId);

    public Card ToBattleCard(string modifierId = null)
    {
        return new Card(Suit, Rank, modifierId ?? ModifierId);
    }

    public static string CreateInstanceId(Suit suit, Rank rank)
    {
        return $"{suit}_{rank}";
    }
}
