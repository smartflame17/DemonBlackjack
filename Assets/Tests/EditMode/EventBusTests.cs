#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;

public sealed class EventBusTests
{
    [Test]
    public void Publish_NestedEventRunsAfterEveryParentSubscriber()
    {
        var bus = new ScopedEventBus();
        var received = new List<string>();

        bus.Subscribe<ParentEvent>(_ =>
        {
            received.Add("parent:first:start");
            bus.Publish(new ChildEvent(1));
            received.Add("parent:first:end");
        });
        bus.Subscribe<ParentEvent>(_ => received.Add("parent:second"));
        bus.Subscribe<ChildEvent>(_ => received.Add("child"));

        bus.Publish(new ParentEvent());

        CollectionAssert.AreEqual(
            new[] { "parent:first:start", "parent:first:end", "parent:second", "child" },
            received);
    }

    [Test]
    public void Publish_NestedEventsAndGrandchildrenUseFifoOrder()
    {
        var bus = new ScopedEventBus();
        var received = new List<string>();

        bus.Subscribe<ParentEvent>(_ =>
        {
            received.Add("parent");
            bus.Publish(new ChildEvent(1));
            bus.Publish(new ChildEvent(2));
        });
        bus.Subscribe<ChildEvent>(eventData =>
        {
            received.Add($"child:{eventData.Id}");
            if (eventData.Id == 1)
                bus.Publish(new GrandchildEvent());
        });
        bus.Subscribe<GrandchildEvent>(_ => received.Add("grandchild"));

        bus.Publish(new ParentEvent());

        CollectionAssert.AreEqual(
            new[] { "parent", "child:1", "child:2", "grandchild" },
            received);
    }

    [Test]
    public void Publish_CapturesSubscribersWhenEventIsPublished()
    {
        var bus = new ScopedEventBus();
        var received = new List<string>();
        Action<ChildEvent> original = _ => received.Add("original");
        Action<ChildEvent> late = _ => received.Add("late");
        bus.Subscribe(original);
        bus.Subscribe<ParentEvent>(_ =>
        {
            bus.Publish(new ChildEvent(1));
            bus.Unsubscribe(original);
            bus.Subscribe(late);
        });

        bus.Publish(new ParentEvent());

        CollectionAssert.AreEqual(new[] { "original" }, received);
    }

    [Test]
    public void Publish_ExceptionClearsPendingEventsAndBusCanPublishAgain()
    {
        var bus = new ScopedEventBus();
        bool childReceived = false;
        bool recoveryReceived = false;
        bus.Subscribe<ParentEvent>(_ => bus.Publish(new ChildEvent(1)));
        bus.Subscribe<ParentEvent>(_ => throw new InvalidOperationException("expected"));
        bus.Subscribe<ChildEvent>(_ => childReceived = true);
        bus.Subscribe<RecoveryEvent>(_ => recoveryReceived = true);

        Assert.Throws<InvalidOperationException>(() => bus.Publish(new ParentEvent()));
        Assert.That(childReceived, Is.False);

        bus.Publish(new RecoveryEvent());

        Assert.That(recoveryReceived, Is.True);
    }

    [Test]
    public void Clear_CancelsQueuedEventsButCurrentInvocationListCompletes()
    {
        var bus = new ScopedEventBus();
        var received = new List<string>();
        bus.Subscribe<ParentEvent>(_ =>
        {
            received.Add("parent:first");
            bus.Publish(new ChildEvent(1));
            bus.Clear();
        });
        bus.Subscribe<ParentEvent>(_ => received.Add("parent:second"));
        bus.Subscribe<ChildEvent>(_ => received.Add("child"));

        bus.Publish(new ParentEvent());

        CollectionAssert.AreEqual(new[] { "parent:first", "parent:second" }, received);
    }

    private readonly struct ParentEvent
    {
    }

    private readonly struct ChildEvent
    {
        public ChildEvent(int id)
        {
            Id = id;
        }

        public int Id { get; }
    }

    private readonly struct GrandchildEvent
    {
    }

    private readonly struct RecoveryEvent
    {
    }
}
#endif
