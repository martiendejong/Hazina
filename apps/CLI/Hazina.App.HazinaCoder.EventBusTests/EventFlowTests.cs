using FluentAssertions;
using Hazina.App.HazinaCoder.Core.Events;

namespace Hazina.App.HazinaCoder.EventBusTests;

public class EventFlowTests
{
    [Fact]
    public void UserMessageReceived_Publish_DeliversToSubscriber()
    {
        using var bus = new EventBus();
        UserMessageReceivedEvent? received = null;
        bus.Subscribe<UserMessageReceivedEvent>(e => received = e);

        var published = new UserMessageReceivedEvent("session-1", "user-1", "hello");
        bus.Publish(published);

        received.Should().NotBeNull();
        received!.SessionId.Should().Be("session-1");
        received.UserId.Should().Be("user-1");
        received.Message.Should().Be("hello");
    }

    [Fact]
    public void IntentExtracted_Publish_DeliversToSubscriber()
    {
        using var bus = new EventBus();
        IntentExtractedEvent? received = null;
        bus.Subscribe<IntentExtractedEvent>(e => received = e);

        bus.Publish(new IntentExtractedEvent("session-1", "book_flight", 0.92));

        received.Should().NotBeNull();
        received!.Intent.Should().Be("book_flight");
        received.Confidence.Should().Be(0.92);
    }

    [Fact]
    public void PlanGenerated_Publish_DeliversToSubscriber()
    {
        using var bus = new EventBus();
        PlanGeneratedEvent? received = null;
        bus.Subscribe<PlanGeneratedEvent>(e => received = e);

        var steps = new[] { "search flights", "confirm dates", "book ticket" };
        bus.Publish(new PlanGeneratedEvent("session-1", "plan-1", steps));

        received.Should().NotBeNull();
        received!.PlanId.Should().Be("plan-1");
        received.Steps.Should().Equal(steps);
    }

    [Fact]
    public void UserProfileUpdated_Publish_DeliversToSubscriber()
    {
        using var bus = new EventBus();
        UserProfileUpdatedEvent? received = null;
        bus.Subscribe<UserProfileUpdatedEvent>(e => received = e);

        var fields = new Dictionary<string, object?> { ["preferredAirline"] = "KLM" };
        bus.Publish(new UserProfileUpdatedEvent("user-1", fields));

        received.Should().NotBeNull();
        received!.UserId.Should().Be("user-1");
        received.UpdatedFields.Should().ContainKey("preferredAirline").WhoseValue.Should().Be("KLM");
    }

    [Fact]
    public void FullPipeline_PublishesInOrder_EachSubscriberReceivesExactlyItsOwnEventType()
    {
        // Mirrors the real orchestration pipeline: a message comes in, intent is
        // extracted from it, a plan is generated, and the profile is updated.
        using var bus = new EventBus();
        var order = new List<string>();
        bus.Subscribe<UserMessageReceivedEvent>(_ => order.Add(nameof(UserMessageReceivedEvent)));
        bus.Subscribe<IntentExtractedEvent>(_ => order.Add(nameof(IntentExtractedEvent)));
        bus.Subscribe<PlanGeneratedEvent>(_ => order.Add(nameof(PlanGeneratedEvent)));
        bus.Subscribe<UserProfileUpdatedEvent>(_ => order.Add(nameof(UserProfileUpdatedEvent)));

        bus.Publish(new UserMessageReceivedEvent("s1", "u1", "book me a flight to Milan"));
        bus.Publish(new IntentExtractedEvent("s1", "book_flight", 0.92));
        bus.Publish(new PlanGeneratedEvent("s1", "p1", new[] { "search flights", "confirm dates" }));
        bus.Publish(new UserProfileUpdatedEvent("u1", new Dictionary<string, object?> { ["lastIntent"] = "book_flight" }));

        order.Should().Equal(
            nameof(UserMessageReceivedEvent),
            nameof(IntentExtractedEvent),
            nameof(PlanGeneratedEvent),
            nameof(UserProfileUpdatedEvent));
    }

    [Fact]
    public void Publish_DoesNotCrossDeliver_ToSubscribersOfOtherEventTypes()
    {
        using var bus = new EventBus();
        var intentHandlerInvoked = false;
        bus.Subscribe<IntentExtractedEvent>(_ => intentHandlerInvoked = true);

        bus.Publish(new UserMessageReceivedEvent("s1", "u1", "hi"));

        intentHandlerInvoked.Should().BeFalse();
    }

    [Fact]
    public void Publish_WithNoSubscribers_DoesNotThrow()
    {
        using var bus = new EventBus();

        var act = () => bus.Publish(new UserMessageReceivedEvent("s1", "u1", "hi"));

        act.Should().NotThrow();
    }
}
