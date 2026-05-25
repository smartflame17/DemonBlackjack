using System;
using System.Collections.Generic;

public sealed class RoundState
{
    private readonly List<Card> _playerHand = new();
    private readonly List<Card> _playerPlayedCards = new();
    private readonly List<Card> _opponentHand = new();
    private readonly List<Card> _opponentVisibleCards = new();
    private readonly List<Card> _sharedVisibleCards = new();
    private readonly List<Card> _lockedCards = new();
    private readonly List<Card> _revealedFutureCards = new();
    private readonly List<string> _pendingEffectIds = new();
    private readonly List<Modifier> _scoringModifiers = new();

    public RoundState(int roundNumber, int targetScore, int burstThreshold, int playerStake, int opponentStake, bool playerActsFirst)
    {
        RoundNumber = roundNumber;
        TargetScore = targetScore;
        BurstThreshold = burstThreshold;
        PlayerStake = playerStake;
        OpponentStake = opponentStake;
        PlayerActsFirst = playerActsFirst;
    }

    public int RoundNumber { get; }
    public int TargetScore { get; }
    public int BurstThreshold { get; }
    public int Wager => Math.Min(PlayerStake, OpponentStake);
    public int PlayerStake { get; }
    public int OpponentStake { get; }
    public bool PlayerActsFirst { get; }
    public int Pot => PlayerStake + OpponentStake;
    public int Reward => Pot;
    public bool PlayerHasPlayed => _playerPlayedCards.Count > 0;
    public bool OpponentHasPlayed => _opponentVisibleCards.Count > 0;
    public bool PlayerStood { get; private set; }
    public bool OpponentStood { get; private set; }
    public bool PlayerPlayedThisTurn { get; private set; }
    public bool OpponentPlayedThisTurn { get; private set; }
    public ScoreResult PlayerScore { get; private set; }
    public ScoreResult OpponentScore { get; private set; }
    public IReadOnlyList<Card> PlayerHand => _playerHand;
    public IReadOnlyList<Card> PlayerPlayedCards => _playerPlayedCards;
    public IReadOnlyList<Card> OpponentHand => _opponentHand;
    public IReadOnlyList<Card> OpponentVisibleCards => _opponentVisibleCards;
    public IReadOnlyList<Card> SharedVisibleCards => _sharedVisibleCards;
    public IReadOnlyList<Card> LockedCards => _lockedCards;
    public IReadOnlyList<Card> RevealedFutureCards => _revealedFutureCards;
    public IReadOnlyList<string> PendingEffectIds => _pendingEffectIds;
    public IReadOnlyList<Modifier> ScoringModifiers => _scoringModifiers;

    public void AddToHand(Card card)
    {
        _playerHand.Add(card);
    }

    public void AddToOpponentHand(Card card)
    {
        _opponentHand.Add(card);
    }

    public void AddSharedVisibleCard(Card card)
    {
        _sharedVisibleCards.Add(card);
    }

    public bool TryPlayCard(int handIndex, out Card card)
    {
        if (handIndex < 0 || handIndex >= _playerHand.Count)
        {
            card = default;
            return false;
        }

        card = _playerHand[handIndex];
        _playerHand.RemoveAt(handIndex);
        _playerPlayedCards.Add(card);
        PlayerPlayedThisTurn = true;
        PlayerStood = false;
        return true;
    }

    public bool TryPlayOpponentCard(int handIndex, out Card card)
    {
        if (handIndex < 0 || handIndex >= _opponentHand.Count)
        {
            card = default;
            return false;
        }

        card = _opponentHand[handIndex];
        _opponentHand.RemoveAt(handIndex);
        _opponentVisibleCards.Add(card);
        OpponentPlayedThisTurn = true;
        OpponentStood = false;
        return true;
    }

    public bool TryPlayHitCard(Card card)
    {
        _playerPlayedCards.Add(card);
        PlayerPlayedThisTurn = true;
        PlayerStood = false;
        return true;
    }

    public void PlayOpponentHitCard(Card card)
    {
        _opponentVisibleCards.Add(card);
        OpponentPlayedThisTurn = true;
        OpponentStood = false;
    }

    public void BeginPlayerTurn()
    {
        PlayerPlayedThisTurn = false;
    }

    public void BeginOpponentTurn()
    {
        OpponentPlayedThisTurn = false;
    }

    public void MarkPlayerStood()
    {
        PlayerStood = true;
    }

    public void MarkOpponentStood()
    {
        OpponentStood = true;
    }

    public void AddScoringModifier(Modifier modifier)
    {
        _scoringModifiers.Add(modifier);
    }

    public void SetScores(ScoreResult playerScore, ScoreResult opponentScore)
    {
        PlayerScore = playerScore;
        OpponentScore = opponentScore;
    }

    public void MoveHandsTo(ICollection<Card> playerHand, ICollection<Card> opponentHand)
    {
        if (playerHand != null)
        {
            for (int i = 0; i < _playerHand.Count; i++)
                playerHand.Add(_playerHand[i]);
        }

        if (opponentHand != null)
        {
            for (int i = 0; i < _opponentHand.Count; i++)
                opponentHand.Add(_opponentHand[i]);
        }

        _playerHand.Clear();
        _opponentHand.Clear();
    }

    public IReadOnlyList<Card> TakeCardsForCleanup()
    {
        var cards = new List<Card>();
        cards.AddRange(_playerPlayedCards);
        cards.AddRange(_opponentVisibleCards);
        cards.AddRange(_sharedVisibleCards);

        _playerPlayedCards.Clear();
        _opponentVisibleCards.Clear();
        _sharedVisibleCards.Clear();
        _lockedCards.Clear();
        _revealedFutureCards.Clear();
        _pendingEffectIds.Clear();
        _scoringModifiers.Clear();

        return cards;
    }

    public IReadOnlyList<Card> TakePlayerCardsForCleanup()
    {
        var cards = new List<Card>();
        cards.AddRange(_playerPlayedCards);
        cards.AddRange(_sharedVisibleCards);
        _playerPlayedCards.Clear();
        _sharedVisibleCards.Clear();
        return cards;
    }

    public IReadOnlyList<Card> TakeOpponentCardsForCleanup()
    {
        var cards = new List<Card>();
        cards.AddRange(_opponentVisibleCards);
        _opponentVisibleCards.Clear();
        return cards;
    }

    public void ClearRoundOnlyState()
    {
        _lockedCards.Clear();
        _revealedFutureCards.Clear();
        _pendingEffectIds.Clear();
        _scoringModifiers.Clear();
    }
}
