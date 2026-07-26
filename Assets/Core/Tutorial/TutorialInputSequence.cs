using System.Collections.Generic;

public sealed class TutorialInputSequence
{
    private TutorialRoundSpec round;
    private int actionIndex;
    private bool ignoreNextCardPlayedAfterHit;

    public int ActionIndex => actionIndex;
    public bool HasActions => round?.PlayerActions != null && round.PlayerActions.Count > 0;
    public bool IsComplete => !HasActions || actionIndex >= round.PlayerActions.Count;

    public void BeginRound(TutorialRoundSpec roundSpec)
    {
        round = roundSpec;
        actionIndex = 0;
        ignoreNextCardPlayedAfterHit = false;
    }

    public bool CanStartRound()
    {
        return true;
    }

    public bool CanPlayCard(IReadOnlyList<Card> hand, int handIndex)
    {
        if (!HasActions)
            return true;

        if (!TryGetExpected(out TutorialPlayerActionSpec expected) || expected.ActionType != TutorialPlayerActionType.PlayRank)
            return false;

        return hand != null
            && handIndex >= 0
            && handIndex < hand.Count
            && hand[handIndex].Rank == expected.Rank;
    }

    public bool CanHit(IReadOnlyList<Card> drawPile)
    {
        if (!HasActions)
            return true;

        if (!TryGetExpected(out TutorialPlayerActionSpec expected) || expected.ActionType != TutorialPlayerActionType.HitRank)
            return false;

        return drawPile != null
            && drawPile.Count > 0
            && drawPile[drawPile.Count - 1].Rank == expected.Rank;
    }

    public bool CanStand()
    {
        if (!HasActions)
            return true;

        return TryGetExpected(out TutorialPlayerActionSpec expected)
            && expected.ActionType == TutorialPlayerActionType.Stand;
    }

    public bool CanUseActiveItem()
    {
        return !HasActions;
    }

    public void NotifyCardPlayed(Card card)
    {
        if (ignoreNextCardPlayedAfterHit)
        {
            ignoreNextCardPlayedAfterHit = false;
            return;
        }

        TryAdvance(TutorialPlayerActionType.PlayRank, card.Rank);
    }

    public void NotifyPlayerHitUsed(Card card)
    {
        if (TryAdvance(TutorialPlayerActionType.HitRank, card.Rank))
            ignoreNextCardPlayedAfterHit = true;
    }

    public void NotifyStandAccepted()
    {
        TryAdvance(TutorialPlayerActionType.Stand, null);
    }

    public bool TryGetExpectedAction(out TutorialPlayerActionSpec expected)
    {
        return TryGetExpected(out expected);
    }

    private bool TryGetExpected(out TutorialPlayerActionSpec expected)
    {
        if (HasActions && actionIndex >= 0 && actionIndex < round.PlayerActions.Count)
        {
            expected = round.PlayerActions[actionIndex];
            return true;
        }

        expected = default;
        return false;
    }

    private bool TryAdvance(TutorialPlayerActionType actionType, Rank? rank)
    {
        if (!TryGetExpected(out TutorialPlayerActionSpec expected))
            return false;

        if (expected.ActionType != actionType)
            return false;

        if (expected.RequiresRank && (!rank.HasValue || expected.Rank != rank.Value))
            return false;

        actionIndex++;
        return true;
    }
}
