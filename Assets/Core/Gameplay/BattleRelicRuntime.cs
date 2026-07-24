using System;
using System.Collections.Generic;

public abstract class BattleRelicRuntime : IDisposable
{
    private readonly List<Action> _unsubscribeActions = new();
    private bool _disposed;

    protected BattleRelicRuntime(string relicId, BattleState battle)
    {
        if (string.IsNullOrWhiteSpace(relicId))
            throw new ArgumentException("Relic id cannot be blank.", nameof(relicId));

        RelicId = relicId;
        Battle = battle ?? throw new ArgumentNullException(nameof(battle));
    }

    public string RelicId { get; }
    protected BattleState Battle { get; }
    protected RunState RunState => Battle.RunState;
    protected BattleConfig Config => Battle.Config;
    public virtual int OpponentBlackjackBonus => 0;

    public virtual Card TransformPlayerPlayedCard(Card card)
    {
        return card;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        for (int i = _unsubscribeActions.Count - 1; i >= 0; i--)
            _unsubscribeActions[i]();

        _unsubscribeActions.Clear();
        _disposed = true;
    }

    protected void Subscribe<T>(Action<T> callback)
    {
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));

        Battle.EventBus.Subscribe(callback);
        _unsubscribeActions.Add(() => Battle.EventBus.Unsubscribe(callback));
    }

    protected void PublishCounter(int value)
    {
        EventBus.Publish(new RelicCounterChangedEvent(RelicId, Math.Max(0, value)));
    }

    protected void PublishActivation()
    {
        EventBus.Publish(new RelicActivatedEvent(RelicId));
    }

    protected bool RefreshCurrentRoundPlayerBurstThreshold()
    {
        RoundState round = Battle.CurrentRound;
        if (round == null)
            return false;

        int threshold = RelicRuleResolver.ResolvePlayerBurstThreshold(RunState, Config.BurstThreshold);
        int previousThreshold = round.PlayerBurstThreshold;
        round.SetPlayerBurstThreshold(threshold);

        if (round.PlayerBurstThreshold == previousThreshold)
            return false;

        EventBus.Publish(new BurstThresholdChangedEvent(Combatant.Player, round.PlayerBurstThreshold));
        return true;
    }

    protected static bool IsFaceCard(Rank rank)
    {
        return rank == Rank.Jack || rank == Rank.Queen || rank == Rank.King;
    }
}

public static class BattleRelicRuntimeFactory
{
    public static List<BattleRelicRuntime> CreateAll(IEnumerable<string> relicIds, BattleState battle)
    {
        var runtimes = new List<BattleRelicRuntime>();
        var createdIds = new HashSet<string>();
        if (relicIds == null)
            return runtimes;

        foreach (string relicId in relicIds)
        {
            if (string.IsNullOrWhiteSpace(relicId) || !createdIds.Add(relicId))
                continue;

            BattleRelicRuntime runtime = Create(relicId, battle);
            if (runtime != null)
                runtimes.Add(runtime);
        }

        return runtimes;
    }

    public static BattleRelicRuntime Create(string relicId, BattleState battle)
    {
        if (string.IsNullOrWhiteSpace(relicId))
            return null;

        return relicId switch
        {
            RelicRuleResolver.AddJqk => new AddJqkRelicRuntime(battle),
            RelicRuleResolver.BurstExtend => new BurstExtendRelicRuntime(battle),
            RelicRuleResolver.SuitOverride => new SuitOverrideRelicRuntime(battle),
            _ => null
        };
    }
}

public sealed class AddJqkRelicRuntime : BattleRelicRuntime
{
    private int _bonus;

    public AddJqkRelicRuntime(BattleState battle)
        : base(RelicRuleResolver.AddJqk, battle)
    {
        Subscribe<RoundStartedEvent>(OnRoundStarted);
        Subscribe<RoundEndedEvent>(OnRoundEnded);
        Subscribe<CardPlayedEvent>(OnCardPlayed);
    }

    public override int OpponentBlackjackBonus => _bonus;

    private void OnRoundStarted(RoundStartedEvent eventData)
    {
        ResetRoundState();
    }

    private void OnRoundEnded(RoundEndedEvent eventData)
    {
        ResetRoundState();
    }

    private void OnCardPlayed(CardPlayedEvent eventData)
    {
        if (eventData.Owner != Combatant.Opponent || !IsFaceCard(eventData.Card.Rank))
            return;

        _bonus++;
        PublishCounter(_bonus);
        PublishActivation();
    }

    private void ResetRoundState()
    {
        _bonus = 0;
        PublishCounter(0);
    }
}

public sealed class BurstExtendRelicRuntime : BattleRelicRuntime
{
    private const int HitTarget = 5;

    private int _hitCount;

    public BurstExtendRelicRuntime(BattleState battle)
        : base(RelicRuleResolver.BurstExtend, battle)
    {
        Subscribe<PlayerHitUsedEvent>(OnPlayerHitUsed);
        Subscribe<BattleEndedEvent>(OnBattleEnded);
    }

    private void OnPlayerHitUsed(PlayerHitUsedEvent eventData)
    {
        _hitCount++;
        if (_hitCount < HitTarget)
        {
            PublishCounter(_hitCount);
            return;
        }

        RunState.IncreasePlayerBurstThreshold(1);
        RefreshCurrentRoundPlayerBurstThreshold();

        _hitCount = 0;
        PublishCounter(0);
        PublishActivation();
    }

    private void OnBattleEnded(BattleEndedEvent eventData)
    {
        _hitCount = 0;
        PublishCounter(0);
    }
}

public sealed class SuitOverrideRelicRuntime : BattleRelicRuntime
{
    private bool _hasAnchor;
    private Rank _anchorRank;
    private Suit _anchorSuit;

    public SuitOverrideRelicRuntime(BattleState battle)
        : base(RelicRuleResolver.SuitOverride, battle)
    {
        Subscribe<RoundStartedEvent>(OnRoundStarted);
        Subscribe<RoundEndedEvent>(OnRoundEnded);
    }

    public override Card TransformPlayerPlayedCard(Card card)
    {
        if (!_hasAnchor)
        {
            _anchorRank = card.Rank;
            _anchorSuit = card.Suit;
            _hasAnchor = true;
            return card;
        }

        return card.Rank == _anchorRank
            ? new Card(_anchorSuit, card.Rank, card.ModifierId)
            : card;
    }

    private void OnRoundStarted(RoundStartedEvent eventData)
    {
        ResetRoundState();
    }

    private void OnRoundEnded(RoundEndedEvent eventData)
    {
        ResetRoundState();
    }

    private void ResetRoundState()
    {
        _hasAnchor = false;
    }
}
