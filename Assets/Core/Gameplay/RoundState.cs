using System.Collections.Generic;

public sealed class RoundState
{
    private readonly List<Card> _playerHand = new();
    private readonly List<Card> _playerPlayedCards = new();
    private readonly List<Card> _opponentVisibleCards = new();
    private readonly List<Card> _sharedVisibleCards = new();
    private readonly List<Card> _lockedCards = new();
    private readonly List<Card> _revealedFutureCards = new();
    private readonly List<string> _pendingEffectIds = new();
    private readonly List<Modifier> _scoringModifiers = new();

    public RoundState(int roundNumber, int targetScore, int burstThreshold, int wager)
    {
        RoundNumber = roundNumber;
        TargetScore = targetScore;
        BurstThreshold = burstThreshold;
        Wager = wager;
    }

    public int RoundNumber { get; }
    public int TargetScore { get; }
    public int BurstThreshold { get; }
    public int Wager { get; }
    public ScoreResult PlayerScore { get; private set; }
    public ScoreResult OpponentScore { get; private set; }
    public IReadOnlyList<Card> PlayerHand => _playerHand;
    public IReadOnlyList<Card> PlayerPlayedCards => _playerPlayedCards;
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
        return true;
    }

    public void SetOpponentCards(IEnumerable<Card> cards)
    {
        _opponentVisibleCards.Clear();

        if (cards != null)
            _opponentVisibleCards.AddRange(cards);
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

    public IReadOnlyList<Card> TakeCardsForCleanup()
    {
        var cards = new List<Card>();
        cards.AddRange(_playerHand);
        cards.AddRange(_playerPlayedCards);
        cards.AddRange(_opponentVisibleCards);
        cards.AddRange(_sharedVisibleCards);

        _playerHand.Clear();
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
