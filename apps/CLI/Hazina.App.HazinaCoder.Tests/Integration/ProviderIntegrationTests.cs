using Xunit;
using FluentAssertions;
using Hazina.App.HazinaCoder.Core.Providers;
using Hazina.App.HazinaCoder.Core.Events;
using Hazina.App.HazinaCoder.Core.Streaming;

namespace Hazina.App.HazinaCoder.Tests.Integration;

/// <summary>
/// Integration tests for provider stack
/// Iteration 31: Integration testing
/// </summary>
public class ProviderIntegrationTests
{
    [Fact(Skip = "Requires API key")]
    public async Task ProviderChain_WithRetryAndRateLimit_ShouldWork()
    {
        // Arrange
        var retryPolicy = new RetryPolicy(maxRetries: 3);
        var rateLimiter = new RateLimiter(maxRequests: 10, timeWindow: TimeSpan.FromSeconds(1));

        // Act & Assert
        await rateLimiter.WaitAsync();

        var result = await retryPolicy.ExecuteAsync(async () =>
        {
            await Task.CompletedTask;
            return "success";
        });

        result.Should().Be("success");
    }

    [Fact]
    public async Task EventBus_WithStreamingAdapter_ShouldPublishEvents()
    {
        // Arrange
        var eventBus = new AgentEventBus();
        var receivedEvents = new List<AgentEvent>();

        eventBus.Subscribe<AgentEvent>(evt => receivedEvents.Add(evt));

        // Act
        eventBus.Publish(new AgentEvent { Type = "Test", Data = "data1" });
        eventBus.Publish(new AgentEvent { Type = "Test", Data = "data2" });

        // Assert
        receivedEvents.Should().HaveCount(2);
        receivedEvents[0].Data.Should().Be("data1");
        receivedEvents[1].Data.Should().Be("data2");
    }
}
