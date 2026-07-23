using FluentAssertions;
using Hazina.App.HazinaCoder.Core.Events;

namespace Hazina.App.HazinaCoder.EventBusTests;

public class HandlerOrderTests
{
    [Fact]
    public void Subscribe_MultipleHandlers_InvokedInSubscriptionOrder()
    {
        using var bus = new EventBus();
        var invocationOrder = new List<string>();

        bus.Subscribe<IntentExtractedEvent>(_ => invocationOrder.Add("first"));
        bus.Subscribe<IntentExtractedEvent>(_ => invocationOrder.Add("second"));
        bus.Subscribe<IntentExtractedEvent>(_ => invocationOrder.Add("third"));

        bus.Publish(new IntentExtractedEvent("s1", "book_flight", 0.9));

        invocationOrder.Should().Equal("first", "second", "third");
    }

    [Fact]
    public void Subscribe_MultipleHandlers_OrderIsStable_AcrossMultiplePublishes()
    {
        using var bus = new EventBus();
        var invocationOrder = new List<string>();

        bus.Subscribe<IntentExtractedEvent>(_ => invocationOrder.Add("first"));
        bus.Subscribe<IntentExtractedEvent>(_ => invocationOrder.Add("second"));
        bus.Subscribe<IntentExtractedEvent>(_ => invocationOrder.Add("third"));

        bus.Publish(new IntentExtractedEvent("s1", "book_flight", 0.9));
        bus.Publish(new IntentExtractedEvent("s1", "cancel_flight", 0.8));

        invocationOrder.Should().Equal("first", "second", "third", "first", "second", "third");
    }

    [Fact]
    public void Unsubscribe_RemovesHandlerFromOrder_WithoutDisturbingOthers()
    {
        using var bus = new EventBus();
        var invocationOrder = new List<string>();

        bus.Subscribe<IntentExtractedEvent>(_ => invocationOrder.Add("first"));
        var secondSubscription = bus.Subscribe<IntentExtractedEvent>(_ => invocationOrder.Add("second"));
        bus.Subscribe<IntentExtractedEvent>(_ => invocationOrder.Add("third"));

        secondSubscription.Dispose();
        bus.Publish(new IntentExtractedEvent("s1", "book_flight", 0.9));

        invocationOrder.Should().Equal("first", "third");
    }

    [Fact]
    public void Subscribe_LateSubscriber_JoinsAtEndOfOrder()
    {
        using var bus = new EventBus();
        var invocationOrder = new List<string>();

        bus.Subscribe<IntentExtractedEvent>(_ => invocationOrder.Add("first"));
        bus.Publish(new IntentExtractedEvent("s1", "book_flight", 0.9));

        bus.Subscribe<IntentExtractedEvent>(_ => invocationOrder.Add("second"));
        bus.Publish(new IntentExtractedEvent("s1", "cancel_flight", 0.8));

        invocationOrder.Should().Equal("first", "first", "second");
    }
}
