using System;
using System.Collections.Generic;

public static class EventBus
{
    private static readonly ScopedEventBus _global = new();

    public static IEventBus Global => _global;

    public static void Subscribe<T>(Action<T> callback)
    {
        _global.Subscribe(callback);
    }

    public static void Unsubscribe<T>(Action<T> callback)
    {
        _global.Unsubscribe(callback);
    }

    public static void Publish<T>(T eventData)
    {
        _global.Publish(eventData);
    }

    public static void Clear()
    {
        _global.Clear();
    }
}

public interface IEventBus
{
    void Subscribe<T>(Action<T> callback);
    void Unsubscribe<T>(Action<T> callback);

    /// <summary>
    /// Publishes synchronously. Events published by a subscriber are queued until every
    /// subscriber of the current event has run, so nested events are delivered in FIFO order.
    /// A nested call can therefore return before that nested event has been delivered.
    /// </summary>
    void Publish<T>(T eventData);
}

public sealed class ScopedEventBus : IEventBus
{
    private readonly Dictionary<Type, Delegate> _subscribers = new();
    private readonly Queue<Action> _pendingPublishes = new();
    private bool _isDraining;

    public void Subscribe<T>(Action<T> callback)
    {
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));

        Type type = typeof(T);

        if (_subscribers.TryGetValue(type, out var existing))
        {
            _subscribers[type] = Delegate.Combine(existing, callback);
        }
        else
        {
            _subscribers[type] = callback;
        }
    }

    public void Unsubscribe<T>(Action<T> callback)
    {
        if (callback == null)
            return;

        Type type = typeof(T);

        if (!_subscribers.TryGetValue(type, out var existing))
            return;

        var current = Delegate.Remove(existing, callback);

        if (current == null)
            _subscribers.Remove(type);
        else
            _subscribers[type] = current;
    }

    public void Publish<T>(T eventData)
    {
        Type type = typeof(T);
        if (!_subscribers.TryGetValue(type, out var callback))
            return;

        // Capture the invocation list at publication time. Subscription changes made before
        // a queued event is delivered must not change who receives that already-published event.
        var capturedCallback = (Action<T>)callback;
        _pendingPublishes.Enqueue(() => capturedCallback.Invoke(eventData));

        if (_isDraining)
            return;

        _isDraining = true;
        try
        {
            while (_pendingPublishes.Count > 0)
                _pendingPublishes.Dequeue().Invoke();
        }
        catch
        {
            _pendingPublishes.Clear();
            throw;
        }
        finally
        {
            _isDraining = false;
        }
    }

    public void Clear()
    {
        _subscribers.Clear();
        _pendingPublishes.Clear();
    }
}
