using System;
using System.Collections.Generic;

public sealed class RoundState
{
    private readonly List<Card> _playerHand = new();
    private readonly List<Card> _playerPlayedCards = new();
    private readonly List<Card> _opponentHand = new();
    private readonly List<Card> _opponentVisibleCards = new();
    private readonly List<Combatant> _opponentVisibleCardOwners = new();
    private readonly List<Card> _sharedVisibleCards = new();
    private readonly List<Card> _lockedCards = new();
    private readonly List<Card> _revealedFutureCards = new();
    private readonly List<string> _pendingEffectIds = new();
    private readonly List<Modifier> _scoringModifiers = new();
    private int _lastPlayerHitCardIndex = -1;

    public RoundState(int roundNumber, int targetScore, int playerBurstThreshold, int opponentBurstThreshold, int playerStake, int opponentStake, bool playerActsFirst)
    {
        RoundNumber = roundNumber;
        TargetScore = targetScore;
        PlayerBurstThreshold = playerBurstThreshold;
        OpponentBurstThreshold = opponentBurstThreshold;
        PlayerStake = playerStake;
        OpponentStake = opponentStake;
        PlayerActsFirst = playerActsFirst;
    }

    public int RoundNumber { get; }
    public int TargetScore { get; }
    public int PlayerBurstThreshold { get; private set; }
    public int OpponentBurstThreshold { get; private set; }
    public int BurstThreshold => PlayerBurstThreshold;
    public int Wager => Math.Min(PlayerStake, OpponentStake);
    public int PlayerStake { get; private set; }
    public int OpponentStake { get; private set; }
    public bool PlayerActsFirst { get; }
    public bool WagerCommitted { get; private set; }
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

    public void SetPlayerBurstThreshold(int threshold)
    {
        PlayerBurstThreshold = Math.Max(1, threshold);
    }

    public int GetBurstThreshold(Combatant combatant)
    {
        return combatant == Combatant.Player ? PlayerBurstThreshold : OpponentBurstThreshold;
    }

    public void AddToHand(Card card)
    {
        _playerHand.Add(card);
    }

    public void AddToOpponentHand(Card card)
    {
        _opponentHand.Add(card);
    }

    public bool TryCommitWager(int playerStake, int opponentStake)
    {
        if (WagerCommitted || playerStake <= 0 || opponentStake <= 0)
            return false;

        PlayerStake = playerStake;
        OpponentStake = opponentStake;
        WagerCommitted = true;
        return true;
    }

    public bool TryIncreaseWager(int playerStakeDelta, int opponentStakeDelta)
    {
        if (!WagerCommitted || playerStakeDelta <= 0 || opponentStakeDelta <= 0)
            return false;

        PlayerStake += playerStakeDelta;
        OpponentStake += opponentStakeDelta;
        return true;
    }

    public void AddSharedVisibleCard(Card card)
    {
        _sharedVisibleCards.Add(card);
    }

    public void TransformPlayerCards(Func<Card, Card> transform)
    {
        if (transform == null)
            return;

        TransformCards(_playerHand, transform);
        TransformCards(_playerPlayedCards, transform);
        TransformCards(_sharedVisibleCards, transform);

        for (int i = 0; i < _opponentVisibleCards.Count; i++)
        {
            if (_opponentVisibleCardOwners[i] == Combatant.Player)
                _opponentVisibleCards[i] = transform(_opponentVisibleCards[i]);
        }
    }

    public bool TryPlayCard(int handIndex, out Card card)
    {
        return TryPlayCard(handIndex, null, out card);
    }

    public bool TryPlayCard(int handIndex, Func<Card, Card> transform, out Card card)
    {
        if (handIndex < 0 || handIndex >= _playerHand.Count)
        {
            card = default;
            return false;
        }

        card = _playerHand[handIndex];
        _playerHand.RemoveAt(handIndex);
        if (transform != null)
            card = transform(card);
        _playerPlayedCards.Add(card);
        _lastPlayerHitCardIndex = _playerPlayedCards.Count - 1;
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
        AddOpponentVisibleCard(card, Combatant.Opponent);
        OpponentPlayedThisTurn = true;
        OpponentStood = false;
        return true;
    }

    public bool TryPlayHitCard(Card card)
    {
        _playerPlayedCards.Add(card);
        _lastPlayerHitCardIndex = _playerPlayedCards.Count - 1;
        PlayerPlayedThisTurn = true;
        PlayerStood = false;
        return true;
    }

    public bool CanRemoveLastPlayerHitCard => _lastPlayerHitCardIndex >= 0 && _lastPlayerHitCardIndex < _playerPlayedCards.Count;

    public bool TryRemoveLastPlayerHitCard(out Card card)
    {
        if (!CanRemoveLastPlayerHitCard)
        {
            card = default;
            return false;
        }

        card = _playerPlayedCards[_lastPlayerHitCardIndex];
        _playerPlayedCards.RemoveAt(_lastPlayerHitCardIndex);
        _lastPlayerHitCardIndex = -1;
        return true;
    }

    public void PlayOpponentHitCard(Card card)
    {
        AddOpponentVisibleCard(card, Combatant.Opponent);
        OpponentPlayedThisTurn = true;
        OpponentStood = false;
    }

    public bool TryGetPreviousPlayerPlayedCard(out Card card)
    {
        int previousIndex = _playerPlayedCards.Count - 2;
        if (previousIndex < 0)
        {
            card = default;
            return false;
        }

        card = _playerPlayedCards[previousIndex];
        return true;
    }

    public bool TryMovePreviousPlayerPlayedCardToOpponent(out Card card)
    {
        int previousIndex = _playerPlayedCards.Count - 2;
        if (previousIndex < 0)
        {
            card = default;
            return false;
        }

        card = _playerPlayedCards[previousIndex];
        _playerPlayedCards.RemoveAt(previousIndex);
        if (_lastPlayerHitCardIndex > previousIndex)
            _lastPlayerHitCardIndex--;
        else if (_lastPlayerHitCardIndex == previousIndex)
            _lastPlayerHitCardIndex = -1;

        AddOpponentVisibleCard(card, Combatant.Player);
        return true;
    }

    public void AddBattleOnlyPlayerHandCard(Card card)
    {
        _playerHand.Add(card);
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

    public IReadOnlyList<Card> TakeFieldCards(Combatant owner)
    {
        var cards = new List<Card>();

        if (owner == Combatant.Player)
        {
            cards.AddRange(_playerPlayedCards);
            cards.AddRange(_sharedVisibleCards);
            _playerPlayedCards.Clear();
            _sharedVisibleCards.Clear();
            _lastPlayerHitCardIndex = -1;
        }
        else
        {
            cards.AddRange(_opponentVisibleCards);
            _opponentVisibleCards.Clear();
            _opponentVisibleCardOwners.Clear();
        }

        return cards;
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
        _opponentVisibleCardOwners.Clear();
        _sharedVisibleCards.Clear();
        _lastPlayerHitCardIndex = -1;
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
        _lastPlayerHitCardIndex = -1;
        return cards;
    }

    public IReadOnlyList<Card> TakeOpponentCardsForCleanup()
    {
        var cards = new List<Card>();
        cards.AddRange(_opponentVisibleCards);
        _opponentVisibleCards.Clear();
        _opponentVisibleCardOwners.Clear();
        return cards;
    }

    public void TakeOpponentCardsForCleanup(ICollection<Card> playerOwnedCards, ICollection<Card> opponentOwnedCards)
    {
        for (int i = 0; i < _opponentVisibleCards.Count; i++)
        {
            if (_opponentVisibleCardOwners[i] == Combatant.Player)
                playerOwnedCards?.Add(_opponentVisibleCards[i]);
            else
                opponentOwnedCards?.Add(_opponentVisibleCards[i]);
        }

        _opponentVisibleCards.Clear();
        _opponentVisibleCardOwners.Clear();
    }

    public void ClearRoundOnlyState()
    {
        _lockedCards.Clear();
        _revealedFutureCards.Clear();
        _pendingEffectIds.Clear();
        _scoringModifiers.Clear();
        _lastPlayerHitCardIndex = -1;
    }

    private void AddOpponentVisibleCard(Card card, Combatant owner)
    {
        _opponentVisibleCards.Add(card);
        _opponentVisibleCardOwners.Add(owner);
    }

    private static void TransformCards(List<Card> cards, Func<Card, Card> transform)
    {
        for (int i = 0; i < cards.Count; i++)
            cards[i] = transform(cards[i]);
    }
}
