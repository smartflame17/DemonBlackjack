// This file contains globally used enums for state representation throughout the game

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
    Opponent,
    System      // For system events that are not specific to either player or opponent
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
    HighCard,
    Pair,
    LowStraight,       // Four-card straight (e.g., 2-3-4-5)
    TwoPair,
    LowFlush,          // Four-card flush (e.g., 2-4-6-8 of Hearts)
    ThreeOfAKind,
    Straight,
    Flush,
    FullHouse,
    FourOfAKind,
    StraightFlush,
    RoyalFlush
}

public enum VisualCommandType
{
    BattleStarted,
    RoundStarted,
    CardsDrawn,
    CardsPlayed,
    ScoresResolved,
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
    Decline
}


public enum DevilHandLevel
{
    VeryLow = 1,        // hand 10 or below
    Low = 2,            // hand between 11 and 14
    Medium = 3,         // hand between 15 and 17
    High = 4,           // hand between 18 and 19
    VeryHigh = 5        // hand 20 or above
}

// For logging money changes on panel
public enum MoneyTransferReason
{
    Other,
    InitialWager,
    PokerPayout,
    BurstPenalty,
    BlackjackPayout,
    DevilAbilityPayout
}
