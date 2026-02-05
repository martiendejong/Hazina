using Xunit;
using FluentAssertions;
using Hazina.App.HazinaCoder.Core.Events;
using Hazina.App.HazinaCoder.Core.Streaming;

namespace Hazina.App.HazinaCoder.Tests.Core.Events;

public class EventBusTests
{
    [Fact]
    public void Subscribe_ShouldReceive_PublishedEvents()
    {
        // Arrange
        var eventBus = new AgentEventBus();
        AgentEvent? receivedEvent = null;

        eventBus.Subscribe<AgentEvent>(evt => receivedEvent = evt);

        var testEvent = new AgentEvent
        {
            Type = "TestEvent",
            Data = "test data"
        };

        // Act
        eventBus.Publish(testEvent);

        // Assert
        receivedEvent.Should().NotBeNull();
        receivedEvent!.Type.Should().Be("TestEvent");
        receivedEvent.Data.Should().Be("test data");
    }

    [Fact]
    public void Publish_WithNoSubscribers_ShouldNotThrow()
    {
        // Arrange
        var eventBus = new AgentEventBus();
        var testEvent = new AgentEvent { Type = "TestEvent" };

        // Act & Assert
        var act = () => eventBus.Publish(testEvent);
        act.Should().NotThrow();
    }

    [Fact]
    public void Subscribe_MultipleHandlers_ShouldAllReceiveEvent()
    {
        // Arrange
        var eventBus = new AgentEventBus();
        var receivedCount = 0;

        eventBus.Subscribe<AgentEvent>(_ => receivedCount++);
        eventBus.Subscribe<AgentEvent>(_ => receivedCount++);
        eventBus.Subscribe<AgentEvent>(_ => receivedCount++);

        // Act
        eventBus.Publish(new AgentEvent { Type = "TestEvent" });

        // Assert
        receivedCount.Should().Be(3);
    }

    [Fact]
    public void Unsubscribe_ShouldStopReceiving_Events()
    {
        // Arrange
        var eventBus = new AgentEventBus();
        var receivedCount = 0;

        var handler = new Action<AgentEvent>(_ => receivedCount++);
        eventBus.Subscribe(handler);

        eventBus.Publish(new AgentEvent { Type = "Test1" });

        // Act
        eventBus.Unsubscribe(handler);
        eventBus.Publish(new AgentEvent { Type = "Test2" });

        // Assert
        receivedCount.Should().Be(1); // Only first event received
    }

    [Fact]
    public void Subscribe_WithFilter_ShouldOnlyReceive_FilteredEvents()
    {
        // Arrange
        var eventBus = new AgentEventBus();
        var receivedTypes = new List<string>();

        eventBus.Subscribe<AgentEvent>(evt =>
        {
            if (evt.Type?.StartsWith("Important") == true)
                receivedTypes.Add(evt.Type);
        });

        // Act
        eventBus.Publish(new AgentEvent { Type = "ImportantEvent" });
        eventBus.Publish(new AgentEvent { Type = "UnimportantEvent" });
        eventBus.Publish(new AgentEvent { Type = "ImportantUpdate" });

        // Assert
        receivedTypes.Should().HaveCount(2);
        receivedTypes.Should().Contain("ImportantEvent");
        receivedTypes.Should().Contain("ImportantUpdate");
    }
}
