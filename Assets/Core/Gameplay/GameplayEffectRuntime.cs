using System;

public interface IBattleEffectHandler : IDisposable
{
}

public sealed class BattleEffectRuntime : IDisposable
{
    private readonly BattleState _battle;
    private bool _disposed;

    public BattleEffectRuntime(BattleState battle)
    {
        _battle = battle ?? throw new ArgumentNullException(nameof(battle));
        _battle.EventBus.Subscribe<CardPlayedEvent>(OnCardPlayed);
        _battle.EventBus.Subscribe<ItemUsedEvent>(OnItemUsed);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _battle.EventBus.Unsubscribe<CardPlayedEvent>(OnCardPlayed);
        _battle.EventBus.Unsubscribe<ItemUsedEvent>(OnItemUsed);
        _disposed = true;
    }

    // Central extension points for future event-driven relic and upgrade effects.
    private void OnCardPlayed(CardPlayedEvent eventData)
    {
    }

    private void OnItemUsed(ItemUsedEvent eventData)
    {
    }
}
