public enum RunPhase
{
    Inactive,
    Init,
    Map,
    Encounter,
    Battle,
    Rewards,
    Shop,
    Event,
    Complete
}

public enum BattlePhase
{
    Inactive,
    Init,
    PreRound,
    PlayerPhase,
    OpponentPhase,
    PostRound,
    Cleanup,
    BattleEnd
}

public enum Combatant
{
    Player,
    Opponent
}

public enum Suit
{
    Hearts,
    Diamonds,
    Clubs,
    Spades
}

public enum Rank
{
    Ace = 1,
    Two = 2,
    Three = 3,
    Four = 4,
    Five = 5,
    Six = 6,
    Seven = 7,
    Eight = 8,
    Nine = 9,
    Ten = 10,
    Jack = 11,
    Queen = 12,
    King = 13
}

public enum PokerHandRank
{
    HighCard = 1,
    Pair = 2,
    TwoPair = 3,
    ThreeOfAKind = 4,
    Straight = 5,
    Flush = 6,
    FullHouse = 7,
    FourOfAKind = 8,
    StraightFlush = 9,
    RoyalFlush = 10
}

public enum VisualCommandType
{
    BattleStarted,
    RoundStarted,
    CardsDrawn,
    CardsPlayed,
    ScoresResolved,
    HealthChanged,
    MoneyChanged,
    RoundEnded,
    BattleEnded
}

public enum DevilTurnChoice
{
    Stand,
    Hit,
    Play
}

public enum WagerResponse
{
    Accept,
    Reduce
}
