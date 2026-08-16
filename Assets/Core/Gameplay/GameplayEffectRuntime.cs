using System;

public interface IBattleEffectHandler : IDisposable
{
}

public sealed class BattleEffectRuntime : IDisposable
{
    private static readonly int[] JokerValues = { 1, 2, 12, 21 };

    private readonly BattleState _battle;
    private int _effectDepth;
    private bool _disposed;

    public BattleEffectRuntime(BattleState battle)
    {
        _battle = battle ?? throw new ArgumentNullException(nameof(battle));
        _battle.EventBus.Subscribe<CardPlayedEvent>(OnCardPlayed);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _battle.EventBus.Unsubscribe<CardPlayedEvent>(OnCardPlayed);
        _disposed = true;
    }

    private void OnCardPlayed(CardPlayedEvent eventData)
    {
        if (eventData.Owner != Combatant.Player)
            return;

        if (_effectDepth > 1)
            return;

        _effectDepth++;
        try
        {
            ResolveCardEffect(eventData.Card, true);
        }
        finally
        {
            _effectDepth--;
        }
    }

    private bool ResolveCardEffect(Card card, bool canTriggerPreviousEffect)
    {
        switch (card.ModifierId)
        {
            case CardModifierResolver.JokerValue:
                int jokerValue = JokerValues[_battle.NextRandomInclusive(0, JokerValues.Length - 1)];
                return _battle.TryReplaceLastPlayerPlayedCardForEffect(new Card(
                    card.Suit,
                    card.Rank,
                    CardModifierResolver.CreateResolvedJokerValue(jokerValue)));
            case CardModifierResolver.HitHigher:
                return _battle.TryPlayRandomPlayerDeckCardAtOrAboveRank(card.Rank);
            case CardModifierResolver.HitLower:
                return _battle.TryPlayRandomPlayerDeckCardAtOrBelowRank(card.Rank);
            case CardModifierResolver.DrawRank:
                return _battle.TryDrawRandomPlayerCardOfRankToHand(card.Rank);
            case CardModifierResolver.DrawSuit:
                return _battle.TryDrawRandomPlayerCardOfSuitToHand(card.Suit);
            case CardModifierResolver.MoveJack:
                return _battle.TryMovePreviousPlayerPlayedCardToOpponent(out _);
            case CardModifierResolver.MoveOpponentToPlayer:
                return _battle.TryMovePreviousOpponentVisibleCardToPlayer(out _);
            case CardModifierResolver.DemoteOpponentCard:
                return _battle.TryMovePreviousOpponentVisibleCardToOpponentDeckBottom(out _);
            case CardModifierResolver.CopyQueen:
                if (_battle.TryGetPreviousPlayerPlayedCard(out Card queenPrevious))
                    return _battle.TryPlayRandomPlayerDeckCardOfSuit(queenPrevious.Suit);

                return false;
            case CardModifierResolver.OutsourceOpponentCard:
                return _battle.TryMovePreviousOpponentVisibleCardToPlayer(out _);
            case CardModifierResolver.DuplicateKing:
                if (_battle.TryGetPreviousPlayerPlayedCard(out Card kingPrevious))
                    return _battle.AddBattleOnlyCardToPlayerField(new Card(kingPrevious.Suit, kingPrevious.Rank, kingPrevious.ModifierId));

                return false;
            case CardModifierResolver.DuplicateOpponentCard:
                if (_battle.TryGetPreviousOpponentVisibleCard(out Card opponentPrevious))
                    return _battle.AddBattleOnlyCardToPlayerField(new Card(opponentPrevious.Suit, opponentPrevious.Rank, opponentPrevious.ModifierId));

                return false;
            case CardModifierResolver.HitTopDeckCard:
                return _battle.TryPlayTopPlayerDeckCardForEffect();
            case CardModifierResolver.SplitPreviousCard:
                return _battle.TrySplitPreviousPlayerNumberCard();
            case CardModifierResolver.TriggerPreviousEffect:
                if (!canTriggerPreviousEffect)
                    return false;

                return _battle.TryGetPreviousPlayerPlayedCard(out Card previous)
                    && ResolveCardEffect(previous, false);
            case CardModifierResolver.DiscardLowestNumber:
                return _battle.TryDiscardLowestPlayerNumberCard();
            case CardModifierResolver.ZeroThenForceHit:
                return _battle.TryPlayTopPlayerDeckCardForEffect();
            default:
                return false;
        }
    }
}
