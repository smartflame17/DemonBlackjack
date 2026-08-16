// All events here should be published to the battle-scoped local event bus
public readonly struct CardDrawnEvent
{
    public CardDrawnEvent(Combatant owner, Card card, int remainingDeckCount)
    {
        Owner = owner;
        Card = card;
        RemainingDeckCount = remainingDeckCount;
    }

    public Combatant Owner { get; }
    public Card Card { get; }
    public int RemainingDeckCount { get; }
}

public readonly struct HandRefilledEvent
{
    public HandRefilledEvent(int handCount)
    {
        HandCount = handCount;
    }

    public int HandCount { get; }
}

public readonly struct CardPlayedEvent
{
    public CardPlayedEvent(Combatant owner, Card card)
    {
        Owner = owner;
        Card = card;
    }

    public Combatant Owner { get; }
    public Card Card { get; }
}

public readonly struct PlayerHitUsedEvent
{
    public PlayerHitUsedEvent(int roundNumber, Card card)
    {
        RoundNumber = roundNumber;
        Card = card;
    }

    public int RoundNumber { get; }
    public Card Card { get; }
}

public readonly struct CardDiscardedEvent
{
    public CardDiscardedEvent(Combatant owner, Card card)
    {
        Owner = owner;
        Card = card;
    }

    public Combatant Owner { get; }
    public Card Card { get; }
}

public readonly struct DeckShuffledEvent
{
    public DeckShuffledEvent(Combatant owner, int reshuffleCount, int deckCount)
    {
        Owner = owner;
        ReshuffleCount = reshuffleCount;
        DeckCount = deckCount;
    }

    public Combatant Owner { get; }
    public int ReshuffleCount { get; }
    public int DeckCount { get; }
}
