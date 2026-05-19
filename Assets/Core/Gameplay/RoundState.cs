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

    public RoundState(int roundNumber, int targetScore, int burstThreshold, int wager, bool playerActsFirst)
    {
        RoundNumber = roundNumber;
        TargetScore = targetScore;
        BurstThreshold = burstThreshold;
        Wager = wager;
        PlayerActsFirst = playerActsFirst;
    }

    public int RoundNumber { get; }
    public int TargetScore { get; }
    public int BurstThreshold { get; }
    public int Wager { get; }
    public bool PlayerActsFirst { get; }
    public int Reward => Wager * 2;
    public bool PlayerHasPlayed { get; private set; }
    public bool OpponentHasPlayed { get; private set; }
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
        if (PlayerHasPlayed || handIndex < 0 || handIndex >= _playerHand.Count)
        {
            card = default;
            return false;
        }

        card = _playerHand[handIndex];
        _playerHand.RemoveAt(handIndex);
        PlayDrawnCard(card);
        return true;
    }

    public bool TryPlayOpponentCard(int handIndex, out Card card)
    {
        if (OpponentHasPlayed || handIndex < 0 || handIndex >= _opponentHand.Count)
        {
            card = default;
            return false;
        }

        card = _opponentHand[handIndex];
        _opponentHand.RemoveAt(handIndex);
        _opponentVisibleCards.Add(card);
        OpponentHasPlayed = true;
        return true;
    }

    public bool TryPlayHitCard(Card card)
    {
        if (PlayerHasPlayed)
            return false;

        PlayDrawnCard(card);
        return true;
    }

    private void PlayDrawnCard(Card card)
    {
        _playerPlayedCards.Add(card);
        PlayerHasPlayed = true;
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
}
