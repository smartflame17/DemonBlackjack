using System;
using System.Collections.Generic;

internal enum PlayedPilePlacementResult
{
    Unavailable,
    Placed,
    Overflowed
}

public sealed class RoundState
{
    private readonly List<Card> _playerHand = new();
    private readonly List<Card> _playerPlayedCards = new();
    private readonly List<Card> _opponentHand = new();
    private readonly List<Card> _opponentVisibleCards = new();
    private readonly List<Card> _opponentOriginalVisibleCards = new();
    private readonly List<Combatant> _opponentVisibleCardOwners = new();
    private readonly List<Card> _sharedVisibleCards = new();
    private readonly List<Card> _lockedCards = new();
    private readonly List<Card> _revealedFutureCards = new();
    private readonly List<string> _pendingEffectIds = new();
    private readonly List<Modifier> _scoringModifiers = new();
    private readonly HashSet<PokerHandRank> _achievedPlayerPokerHandRanks = new();
    private readonly int _maxPlayedPileCardCount;

    public RoundState(int roundNumber, int playerBurstThreshold, int opponentBurstThreshold, int baseWager, bool playerActsFirst)
        : this(
            roundNumber,
            playerBurstThreshold,
            opponentBurstThreshold,
            baseWager,
            playerActsFirst,
            GameplayConstants.GameSettingConfig.MaxPlayedPileCardCount)
    {
    }

    public RoundState(
        int roundNumber,
        int playerBurstThreshold,
        int opponentBurstThreshold,
        int baseWager,
        bool playerActsFirst,
        int maxPlayedPileCardCount)
    {
        RoundNumber = roundNumber;
        PlayerBurstThreshold = playerBurstThreshold;
        OpponentBurstThreshold = opponentBurstThreshold;
        BaseWager = Math.Max(0, baseWager);
        PlayerActsFirst = playerActsFirst;
        _maxPlayedPileCardCount = maxPlayedPileCardCount;
    }

    public int RoundNumber { get; }
    public int PlayerBurstThreshold { get; private set; }
    public int OpponentBurstThreshold { get; private set; }
    public int BurstThreshold => PlayerBurstThreshold;
    public int BaseWager { get; private set; }
    public int WagerMultiplier { get; private set; } = 1;
    public int EffectiveWager => WagerCommitted ? BaseWager * WagerMultiplier : 0;
    public int Wager => EffectiveWager;
    public int PlayerStake { get; private set; }
    public int OpponentStake { get; private set; }
    public bool PlayerActsFirst { get; }
    public bool WagerCommitted { get; private set; }
    public int Pot => PlayerStake + OpponentStake;
    public bool PlayerBurstPenaltyResolved { get; private set; }
    public bool OpponentBurstPenaltyResolved { get; private set; }
    public int OpponentMoneyLost { get; private set; }
    public int PlayerMoneyLost { get; private set; }
    public int PlayerPokerEarnings { get; private set; }
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
    public IReadOnlyCollection<PokerHandRank> AchievedPlayerPokerHandRanks => _achievedPlayerPokerHandRanks;
    public int MaxPlayedPileCardCount => _maxPlayedPileCardCount;

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
        if (WagerCommitted)
            return false;

        if (playerStake <= 0 || opponentStake <= 0)
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
        WagerMultiplier++;
        return true;
    }

    public bool TrySetWagerMultiplier(int wagerMultiplier, int playerStakeDelta, int opponentStakeDelta)
    {
        if (!WagerCommitted || wagerMultiplier <= WagerMultiplier || playerStakeDelta <= 0 || opponentStakeDelta <= 0)
            return false;

        PlayerStake += playerStakeDelta;
        OpponentStake += opponentStakeDelta;
        WagerMultiplier = wagerMultiplier;
        return true;
    }

    public void AddSharedVisibleCard(Card card)
    {
        _sharedVisibleCards.Add(card);
    }

    public bool CanPlacePlayedCard(Combatant combatant)
    {
        int currentCount = combatant == Combatant.Player
            ? _playerPlayedCards.Count
            : _opponentVisibleCards.Count;
        return _maxPlayedPileCardCount <= 0 || currentCount < _maxPlayedPileCardCount;
    }

    public void RefreshOpponentVisibleCards(Func<Card, Combatant, Card> transform)
    {
        for (int i = 0; i < _opponentOriginalVisibleCards.Count; i++)
        {
            Card originalCard = _opponentOriginalVisibleCards[i];
            _opponentVisibleCards[i] = transform != null
                ? transform(originalCard, _opponentVisibleCardOwners[i])
                : originalCard;
        }
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
            {
                _opponentOriginalVisibleCards[i] = transform(_opponentOriginalVisibleCards[i]);
                _opponentVisibleCards[i] = transform(_opponentVisibleCards[i]);
            }
        }
    }

    public bool TryPlayCard(int handIndex, out Card card)
    {
        return TryPlayCard(handIndex, null, out card);
    }

    public bool TryPlayCard(int handIndex, Func<Card, Card> transform, out Card card)
    {
        if (!CanPlacePlayedCard(Combatant.Player)
            || handIndex < 0
            || handIndex >= _playerHand.Count)
        {
            card = default;
            return false;
        }

        card = _playerHand[handIndex];
        _playerHand.RemoveAt(handIndex);
        if (transform != null)
            card = transform(card);
        _playerPlayedCards.Add(card);
        PlayerPlayedThisTurn = true;
        PlayerStood = false;
        return true;
    }

    public bool TryPlayOpponentCard(int handIndex, out Card card)
    {
        if (!CanPlacePlayedCard(Combatant.Opponent)
            || handIndex < 0
            || handIndex >= _opponentHand.Count)
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
        if (!CanPlacePlayedCard(Combatant.Player))
            return false;

        _playerPlayedCards.Add(card);
        PlayerPlayedThisTurn = true;
        PlayerStood = false;
        return true;
    }

    public bool TryRemovePlayerPlayedCard(int index, out Card card)
    {
        if (index < 0 || index >= _playerPlayedCards.Count)
        {
            card = default;
            return false;
        }

        card = _playerPlayedCards[index];
        _playerPlayedCards.RemoveAt(index);
        return true;
    }

    public bool TryReplaceLastPlayerPlayedCard(Card card)
    {
        int index = _playerPlayedCards.Count - 1;
        if (index < 0)
            return false;

        _playerPlayedCards[index] = card;
        return true;
    }

    public bool TryPlayOpponentHitCard(Card card)
    {
        if (!CanPlacePlayedCard(Combatant.Opponent))
            return false;

        AddOpponentVisibleCard(card, Combatant.Opponent);
        OpponentPlayedThisTurn = true;
        OpponentStood = false;
        return true;
    }

    internal PlayedPilePlacementResult TryPlayPlayerCardForEffect(
        int handIndex,
        Func<Card, Card> transform,
        out Card card)
    {
        if (handIndex < 0 || handIndex >= _playerHand.Count)
        {
            card = default;
            return PlayedPilePlacementResult.Unavailable;
        }

        card = _playerHand[handIndex];
        _playerHand.RemoveAt(handIndex);
        if (transform != null)
            card = transform(card);

        return TryPlaceEffectCard(Combatant.Player, card, Combatant.Player);
    }

    internal PlayedPilePlacementResult TryPlaceEffectCard(
        Combatant combatant,
        Card card,
        Combatant opponentPileOwner)
    {
        if (!CanPlacePlayedCard(combatant))
            return PlayedPilePlacementResult.Overflowed;

        if (combatant == Combatant.Player)
        {
            _playerPlayedCards.Add(card);
            PlayerPlayedThisTurn = true;
            PlayerStood = false;
        }
        else
        {
            AddOpponentVisibleCard(card, opponentPileOwner);
            OpponentPlayedThisTurn = true;
            OpponentStood = false;
        }

        return PlayedPilePlacementResult.Placed;
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

    public bool TryGetLastPlayerPlayedCard(out Card card)
    {
        int index = _playerPlayedCards.Count - 1;
        if (index < 0)
        {
            card = default;
            return false;
        }

        card = _playerPlayedCards[index];
        return true;
    }

    public bool TryGetPreviousOpponentVisibleCard(out Card card)
    {
        int index = _opponentVisibleCards.Count - 1;
        if (index < 0)
        {
            card = default;
            return false;
        }

        card = _opponentVisibleCards[index];
        return true;
    }

    internal PlayedPilePlacementResult TryMovePreviousPlayerPlayedCardToOpponentForEffect(
        out Card card,
        out Combatant discardOwner)
    {
        discardOwner = Combatant.Player;
        int previousIndex = _playerPlayedCards.Count - 2;
        if (previousIndex < 0)
        {
            card = default;
            return PlayedPilePlacementResult.Unavailable;
        }

        card = _playerPlayedCards[previousIndex];
        _playerPlayedCards.RemoveAt(previousIndex);

        if (!CanPlacePlayedCard(Combatant.Opponent))
            return PlayedPilePlacementResult.Overflowed;

        AddOpponentVisibleCard(card, Combatant.Player);
        return PlayedPilePlacementResult.Placed;
    }

    internal PlayedPilePlacementResult TryMovePreviousOpponentVisibleCardToPlayerForEffect(
        out Card card,
        out Combatant discardOwner)
    {
        discardOwner = Combatant.Player;
        if (!TryRemovePreviousOpponentVisibleCard(out card, out _))
            return PlayedPilePlacementResult.Unavailable;

        return TryPlaceEffectCard(Combatant.Player, card, Combatant.Player);
    }

    public bool TryRemovePreviousOpponentVisibleCard(out Card card, out Combatant owner)
    {
        int index = _opponentVisibleCards.Count - 1;
        if (index < 0)
        {
            card = default;
            owner = default;
            return false;
        }

        card = _opponentVisibleCards[index];
        owner = _opponentVisibleCardOwners[index];
        _opponentVisibleCards.RemoveAt(index);
        _opponentOriginalVisibleCards.RemoveAt(index);
        _opponentVisibleCardOwners.RemoveAt(index);
        return true;
    }

    public bool TryRemovePreviousPlayerPlayedNumberCard(out Card card)
    {
        int previousIndex = _playerPlayedCards.Count - 2;
        if (previousIndex < 0 || !IsSplittableNumberRank(_playerPlayedCards[previousIndex].Rank))
        {
            card = default;
            return false;
        }

        card = _playerPlayedCards[previousIndex];
        _playerPlayedCards.RemoveAt(previousIndex);

        return true;
    }

    public bool TryRemoveLowestPlayerNumberCard(out Card card)
    {
        int selectedIndex = -1;
        int selectedRankValue = int.MaxValue;
        for (int i = 0; i < _playerPlayedCards.Count; i++)
        {
            Rank rank = _playerPlayedCards[i].Rank;
            if (!IsNumberRank(rank))
                continue;

            int value = (int)rank;
            if (value >= selectedRankValue)
                continue;

            selectedIndex = i;
            selectedRankValue = value;
        }

        if (selectedIndex < 0)
        {
            card = default;
            return false;
        }

        card = _playerPlayedCards[selectedIndex];
        _playerPlayedCards.RemoveAt(selectedIndex);

        return true;
    }

    public bool TryReturnPlayerPlayedCardToHand(int index, out Card card)
    {
        if (index < 0 || index >= _playerPlayedCards.Count)
        {
            card = default;
            return false;
        }

        card = _playerPlayedCards[index];
        _playerPlayedCards.RemoveAt(index);
        _playerHand.Add(card);

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

    public bool ConsumePlayedThisTurn(Combatant combatant)
    {
        if (combatant == Combatant.Player)
        {
            bool played = PlayerPlayedThisTurn;
            PlayerPlayedThisTurn = false;
            return played;
        }

        bool opponentPlayed = OpponentPlayedThisTurn;
        OpponentPlayedThisTurn = false;
        return opponentPlayed;
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
        }
        else
        {
            cards.AddRange(_opponentOriginalVisibleCards);
            _opponentVisibleCards.Clear();
            _opponentOriginalVisibleCards.Clear();
            _opponentVisibleCardOwners.Clear();
        }

        return cards;
    }

    public void SetScores(ScoreResult playerScore, ScoreResult opponentScore)
    {
        PlayerScore = playerScore;
        OpponentScore = opponentScore;
    }

    public void MarkBurstPenaltyResolved(Combatant combatant)
    {
        if (combatant == Combatant.Player)
            PlayerBurstPenaltyResolved = true;
        else
            OpponentBurstPenaltyResolved = true;
    }

    public void RecordMoneyLost(Combatant combatant, int amount)
    {
        int finalAmount = Math.Max(0, amount);
        if (finalAmount <= 0)
            return;

        if (combatant == Combatant.Player)
            PlayerMoneyLost += finalAmount;
        else
            OpponentMoneyLost += finalAmount;
    }

    public void RecordPlayerPokerEarnings(int amount)
    {
        int finalAmount = Math.Max(0, amount);
        if (finalAmount <= 0)
            return;

        long total = (long)PlayerPokerEarnings + finalAmount;
        PlayerPokerEarnings = total >= int.MaxValue ? int.MaxValue : (int)total;
    }

    public bool HasAchievedPlayerPokerHandRank(PokerHandRank rank)
    {
        return _achievedPlayerPokerHandRanks.Contains(rank);
    }

    public bool TryRecordAchievedPlayerPokerHandRank(PokerHandRank rank)
    {
        return _achievedPlayerPokerHandRanks.Add(rank);
    }

    public IReadOnlyList<Card> GetPokerCardsForPlayerPayout()
    {
        var cards = new List<Card>();
        cards.AddRange(_playerPlayedCards);
        cards.AddRange(_sharedVisibleCards);
        return cards;
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
        cards.AddRange(_opponentOriginalVisibleCards);
        cards.AddRange(_sharedVisibleCards);

        _playerPlayedCards.Clear();
        _opponentVisibleCards.Clear();
        _opponentOriginalVisibleCards.Clear();
        _opponentVisibleCardOwners.Clear();
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
        var cards = new List<Card>(_opponentOriginalVisibleCards);
        _opponentVisibleCards.Clear();
        _opponentOriginalVisibleCards.Clear();
        _opponentVisibleCardOwners.Clear();
        return cards;
    }

    public void TakeOpponentCardsForCleanup(ICollection<Card> playerOwnedCards, ICollection<Card> opponentOwnedCards)
    {
        for (int i = 0; i < _opponentOriginalVisibleCards.Count; i++)
        {
            if (_opponentVisibleCardOwners[i] == Combatant.Player)
                playerOwnedCards?.Add(_opponentOriginalVisibleCards[i]);
            else
                opponentOwnedCards?.Add(_opponentOriginalVisibleCards[i]);
        }

        _opponentVisibleCards.Clear();
        _opponentOriginalVisibleCards.Clear();
        _opponentVisibleCardOwners.Clear();
    }

    public void ClearRoundOnlyState()
    {
        _lockedCards.Clear();
        _revealedFutureCards.Clear();
        _pendingEffectIds.Clear();
        _scoringModifiers.Clear();
    }

    public RoundStateData ToData()
    {
        var data = new RoundStateData
        {
            roundNumber = RoundNumber,
            playerBurstThreshold = PlayerBurstThreshold,
            opponentBurstThreshold = OpponentBurstThreshold,
            baseWager = BaseWager,
            wagerMultiplier = WagerMultiplier,
            playerStake = PlayerStake,
            opponentStake = OpponentStake,
            playerActsFirst = PlayerActsFirst,
            wagerCommitted = WagerCommitted,
            playerBurstPenaltyResolved = PlayerBurstPenaltyResolved,
            opponentBurstPenaltyResolved = OpponentBurstPenaltyResolved,
            opponentMoneyLost = OpponentMoneyLost,
            playerMoneyLost = PlayerMoneyLost,
            playerPokerEarnings = PlayerPokerEarnings,
            playerStood = PlayerStood,
            opponentStood = OpponentStood,
            playerPlayedThisTurn = PlayerPlayedThisTurn,
            opponentPlayedThisTurn = OpponentPlayedThisTurn,
            maxPlayedPileCardCount = _maxPlayedPileCardCount,
            playerScore = ScoreResultData.FromScoreResult(PlayerScore),
            opponentScore = ScoreResultData.FromScoreResult(OpponentScore)
        };

        AddCards(data.playerHand, _playerHand);
        AddCards(data.playerPlayedCards, _playerPlayedCards);
        AddCards(data.opponentHand, _opponentHand);
        AddCards(data.opponentVisibleCards, _opponentVisibleCards);
        AddCards(data.opponentOriginalVisibleCards, _opponentOriginalVisibleCards);
        data.opponentVisibleCardOwners.AddRange(_opponentVisibleCardOwners);
        AddCards(data.sharedVisibleCards, _sharedVisibleCards);
        AddCards(data.lockedCards, _lockedCards);
        AddCards(data.revealedFutureCards, _revealedFutureCards);
        data.pendingEffectIds.AddRange(_pendingEffectIds);
        for (int i = 0; i < _scoringModifiers.Count; i++)
            data.scoringModifiers.Add(ModifierData.FromModifier(_scoringModifiers[i]));
        data.achievedPlayerPokerHandRanks.AddRange(_achievedPlayerPokerHandRanks);
        return data;
    }

    public static RoundState FromData(RoundStateData data)
    {
        if (data == null
            || data.roundNumber <= 0
            || data.playerScore == null
            || data.opponentScore == null
            || data.opponentVisibleCards == null
            || data.opponentOriginalVisibleCards == null
            || data.opponentVisibleCardOwners == null
            || data.opponentVisibleCards.Count != data.opponentOriginalVisibleCards.Count
            || data.opponentVisibleCards.Count != data.opponentVisibleCardOwners.Count)
        {
            return null;
        }

        var state = new RoundState(
            data.roundNumber,
            Math.Max(1, data.playerBurstThreshold),
            Math.Max(1, data.opponentBurstThreshold),
            Math.Max(0, data.baseWager),
            data.playerActsFirst,
            data.maxPlayedPileCardCount);

        state.WagerMultiplier = Math.Max(1, data.wagerMultiplier);
        state.PlayerStake = Math.Max(0, data.playerStake);
        state.OpponentStake = Math.Max(0, data.opponentStake);
        state.WagerCommitted = data.wagerCommitted;
        state.PlayerBurstPenaltyResolved = data.playerBurstPenaltyResolved;
        state.OpponentBurstPenaltyResolved = data.opponentBurstPenaltyResolved;
        state.OpponentMoneyLost = Math.Max(0, data.opponentMoneyLost);
        state.PlayerMoneyLost = Math.Max(0, data.playerMoneyLost);
        state.PlayerPokerEarnings = Math.Max(0, data.playerPokerEarnings);
        state.PlayerStood = data.playerStood;
        state.OpponentStood = data.opponentStood;
        state.PlayerPlayedThisTurn = data.playerPlayedThisTurn;
        state.OpponentPlayedThisTurn = data.opponentPlayedThisTurn;
        state.PlayerScore = data.playerScore.ToScoreResult();
        state.OpponentScore = data.opponentScore.ToScoreResult();

        AddCards(state._playerHand, data.playerHand);
        AddCards(state._playerPlayedCards, data.playerPlayedCards);
        AddCards(state._opponentHand, data.opponentHand);
        AddCards(state._opponentVisibleCards, data.opponentVisibleCards);
        AddCards(state._opponentOriginalVisibleCards, data.opponentOriginalVisibleCards);
        state._opponentVisibleCardOwners.AddRange(data.opponentVisibleCardOwners);
        if (state._opponentVisibleCards.Count != state._opponentOriginalVisibleCards.Count
            || state._opponentVisibleCards.Count != state._opponentVisibleCardOwners.Count)
        {
            return null;
        }
        AddCards(state._sharedVisibleCards, data.sharedVisibleCards);
        AddCards(state._lockedCards, data.lockedCards);
        AddCards(state._revealedFutureCards, data.revealedFutureCards);
        if (data.pendingEffectIds != null)
            state._pendingEffectIds.AddRange(data.pendingEffectIds);
        if (data.scoringModifiers != null)
        {
            for (int i = 0; i < data.scoringModifiers.Count; i++)
            {
                if (data.scoringModifiers[i] != null)
                    state._scoringModifiers.Add(data.scoringModifiers[i].ToModifier());
            }
        }
        if (data.achievedPlayerPokerHandRanks != null)
            state._achievedPlayerPokerHandRanks.UnionWith(data.achievedPlayerPokerHandRanks);
        return state;
    }

    private static void AddCards(List<CardData> target, IReadOnlyList<Card> source)
    {
        for (int i = 0; i < source.Count; i++)
            target.Add(CardData.FromCard(source[i]));
    }

    private static void AddCards(List<Card> target, List<CardData> source)
    {
        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] != null)
                target.Add(source[i].ToCard());
        }
    }

    private void AddOpponentVisibleCard(Card card, Combatant owner)
    {
        _opponentOriginalVisibleCards.Add(card);
        _opponentVisibleCards.Add(card);
        _opponentVisibleCardOwners.Add(owner);
    }

    private static bool IsNumberRank(Rank rank)
    {
        return rank >= Rank.Ace && rank <= Rank.Ten;
    }

    private static bool IsSplittableNumberRank(Rank rank)
    {
        return rank >= Rank.Two && rank <= Rank.Ten;
    }

    private static void TransformCards(List<Card> cards, Func<Card, Card> transform)
    {
        for (int i = 0; i < cards.Count; i++)
            cards[i] = transform(cards[i]);
    }
}
