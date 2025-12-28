using Hazina.LLMs.GoogleADK.Events;
using Xunit;

namespace Hazina.LLMs.GoogleADK.Tests.Events;

public class StreamingEventBusTests
{
    [Fact]
    public void CreateStream_ShouldCreateSubscription()
    {
        // Arrange
        var eventBus = new StreamingEventBus();

        // Act
        var subscription = eventBus.CreateStream<AgentStartedEvent>("test-sub");

        // Assert
        Assert.NotNull(subscription);
        Assert.Equal("test-sub", subscription.SubscriptionId);
        Assert.Equal(typeof(AgentStartedEvent), subscription.EventType);

        eventBus.Clear();
    }

    [Fact]
    async Task GetEventStream_ShouldReceiveEmittedEvents()
    {
        // Arrange
        var eventBus = new StreamingEventBus();
        eventBus.CreateStream<AgentStartedEvent>("test-sub");

        var receivedEvents = new List<AgentEvent>();
        var cts = new CancellationTokenSource();

        // Start consuming events
        var consumeTask = Task.Run(async () =>
        {
            await foreach (var evt in eventBus.GetEventStream("test-sub", cts.Token))
            {
                receivedEvents.Add(evt);
                if (receivedEvents.Count >= 2)
                {
                    cts.Cancel();
                    break;
                }
            }
        });

        // Act
        await Task.Delay(100); // Let consumer start
        eventBus.Publish(new AgentStartedEvent { AgentName = "Agent1", AgentId = "agent-1" });
        eventBus.Publish(new AgentStartedEvent { AgentName = "Agent2", AgentId = "agent-2" });

        await Task.WhenAny(consumeTask, Task.Delay(1000));

        // Assert
        Assert.Equal(2, receivedEvents.Count);

        eventBus.Clear();
    }

    [Fact]
    public void CreateStream_WithFilter_ShouldFilterEvents()
    {
        // Arrange
        var eventBus = new StreamingEventBus();
        eventBus.CreateStream<AgentStartedEvent>(
            "filtered-sub",
            filter: evt => evt.AgentName == "Agent1"
        );

        var receivedEvents = new List<AgentEvent>();
        var cts = new CancellationTokenSource();

        // Start consuming
        var consumeTask = Task.Run(async () =>
        {
            await foreach (var evt in eventBus.GetEventStream("filtered-sub", cts.Token))
            {
                receivedEvents.Add(evt);
            }
        });

        // Act
        eventBus.Publish(new AgentStartedEvent { AgentName = "Agent1", AgentId = "agent-1" });
        eventBus.Publish(new AgentStartedEvent { AgentName = "Agent2", AgentId = "agent-2" });
        eventBus.Publish(new AgentStartedEvent { AgentName = "Agent1", AgentId = "agent-1" });

        Task.Delay(200).Wait();
        cts.Cancel();

        // Assert
        Assert.Equal(2, receivedEvents.Count);
        Assert.All(receivedEvents, e => Assert.Equal("Agent1", e.AgentName));

        eventBus.Clear();
    }

    [Fact]
    public void CloseStream_ShouldRemoveSubscription()
    {
        // Arrange
        var eventBus = new StreamingEventBus();
        eventBus.CreateStream<AgentStartedEvent>("test-sub");

        // Act
        eventBus.CloseStream("test-sub");
        var subscriptions = eventBus.GetActiveSubscriptions();

        // Assert
        Assert.Empty(subscriptions);

        eventBus.Clear();
    }

    [Fact]
    public void GetActiveSubscriptions_ShouldReturnAllSubscriptions()
    {
        // Arrange
        var eventBus = new StreamingEventBus();
        eventBus.CreateStream<AgentStartedEvent>("sub1");
        eventBus.CreateStream<AgentCompletedEvent>("sub2");
        eventBus.CreateStream<AgentErrorEvent>("sub3");

        // Act
        var subscriptions = eventBus.GetActiveSubscriptions();

        // Assert
        Assert.Equal(3, subscriptions.Count);

        eventBus.Clear();
    }
}
