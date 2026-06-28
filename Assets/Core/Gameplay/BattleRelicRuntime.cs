using System;

public sealed class BattleRelicRuntime : IDisposable
{
    private const int BurstExtendHitTarget = 5;

    private readonly BattleState _battle;
    private int _addJqkBonus;
    private int _burstExtendHitCount;
    private bool _hasSuitOverrideAnchor;
    private Rank _suitOverrideRank;
    private Suit _suitOverrideSuit;
    private bool _disposed;

    public BattleRelicRuntime(BattleState battle)
    {
        _battle = battle ?? throw new ArgumentNullException(nameof(battle));
        _battle.EventBus.Subscribe<RoundStartedEvent>(OnRoundStarted);
        _battle.EventBus.Subscribe<RoundEndedEvent>(OnRoundEnded);
        _battle.EventBus.Subscribe<CardPlayedEvent>(OnCardPlayed);
        _battle.EventBus.Subscribe<PlayerHitUsedEvent>(OnPlayerHitUsed);
        _battle.EventBus.Subscribe<BattleEndedEvent>(OnBattleEnded);
    }

    public int OpponentBlackjackBonus => Owns(RelicRuleResolver.AddJqk) ? _addJqkBonus : 0;

    public Card TransformPlayerPlayedCard(Card card)
    {
        if (!Owns(RelicRuleResolver.SuitOverride))
            return card;

        if (!_hasSuitOverrideAnchor)
        {
            _suitOverrideRank = card.Rank;
            _suitOverrideSuit = card.Suit;
            _hasSuitOverrideAnchor = true;
            return card;
        }

        return card.Rank == _suitOverrideRank
            ? new Card(_suitOverrideSuit, card.Rank, card.ModifierId)
            : card;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _battle.EventBus.Unsubscribe<RoundStartedEvent>(OnRoundStarted);
        _battle.EventBus.Unsubscribe<RoundEndedEvent>(OnRoundEnded);
        _battle.EventBus.Unsubscribe<CardPlayedEvent>(OnCardPlayed);
        _battle.EventBus.Unsubscribe<PlayerHitUsedEvent>(OnPlayerHitUsed);
        _battle.EventBus.Unsubscribe<BattleEndedEvent>(OnBattleEnded);
        _disposed = true;
    }

    private void OnRoundStarted(RoundStartedEvent eventData)
    {
        ResetRoundState();
    }

    private void OnRoundEnded(RoundEndedEvent eventData)
    {
        ResetRoundState();
    }

    private void OnBattleEnded(BattleEndedEvent eventData)
    {
        if (Owns(RelicRuleResolver.BurstExtend))
        {
            _burstExtendHitCount = 0;
            PublishCounter(RelicRuleResolver.BurstExtend, 0);
        }
    }

    private void OnCardPlayed(CardPlayedEvent eventData)
    {
        if (!Owns(RelicRuleResolver.AddJqk)
            || eventData.Owner != Combatant.Opponent
            || !IsFaceCard(eventData.Card.Rank))
        {
            return;
        }

        _addJqkBonus++;
        PublishCounter(RelicRuleResolver.AddJqk, _addJqkBonus);
    }

    private void OnPlayerHitUsed(PlayerHitUsedEvent eventData)
    {
        if (!Owns(RelicRuleResolver.BurstExtend))
            return;

        _burstExtendHitCount++;
        if (_burstExtendHitCount < BurstExtendHitTarget)
        {
            PublishCounter(RelicRuleResolver.BurstExtend, _burstExtendHitCount);
            return;
        }

        _battle.RunState.IncreasePlayerBurstThreshold(1);
        _battle.CurrentRound?.SetPlayerBurstThreshold(RelicRuleResolver.ResolvePlayerBurstThreshold(_battle.RunState, _battle.Config.BurstThreshold));
        _burstExtendHitCount = 0;
        PublishCounter(RelicRuleResolver.BurstExtend, 0);
    }

    private void ResetRoundState()
    {
        _hasSuitOverrideAnchor = false;

        if (!Owns(RelicRuleResolver.AddJqk))
            return;

        _addJqkBonus = 0;
        PublishCounter(RelicRuleResolver.AddJqk, 0);
    }

    private bool Owns(string relicId)
    {
        return _battle.RunState != null && _battle.RunState.HasRelic(relicId);
    }

    private static bool IsFaceCard(Rank rank)
    {
        return rank == Rank.Jack || rank == Rank.Queen || rank == Rank.King;
    }

    private static void PublishCounter(string relicId, int value)
    {
        EventBus.Publish(new RelicCounterChangedEvent(relicId, Math.Max(0, value)));
    }
}
