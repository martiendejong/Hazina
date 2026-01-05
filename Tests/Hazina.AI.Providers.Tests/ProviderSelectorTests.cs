using FluentAssertions;
using Hazina.AI.Providers.Core;
using Hazina.AI.Providers.Selection;
using Xunit;

namespace Hazina.AI.Providers.Tests;

public class ProviderSelectorTests
{
    [Fact]
    public void SelectProvider_WithPriorityStrategy_ShouldSelectHighestPriority()
    {
        // Arrange
        var providers = new List<ProviderInfo>
        {
            new ProviderInfo
            {
                Name = "provider-low",
                Priority = 2,
                HealthStatus = ProviderHealth.Healthy
            },
            new ProviderInfo
            {
                Name = "provider-high",
                Priority = 1,
                HealthStatus = ProviderHealth.Healthy
            }
        };

        var selector = new ProviderSelector(SelectionStrategy.Priority);

        // Act
        var selected = selector.SelectProvider(providers, null);

        // Assert
        selected.Should().NotBeNull();
        selected!.Name.Should().Be("provider-high");
        selected.Priority.Should().Be(1);
    }

    [Fact]
    public void SelectProvider_WithLeastCostStrategy_ShouldSelectCheapestProvider()
    {
        // Arrange
        var providers = new List<ProviderInfo>
        {
            new ProviderInfo
            {
                Name = "provider-expensive",
                EstimatedCost = 0.01,
                HealthStatus = ProviderHealth.Healthy
            },
            new ProviderInfo
            {
                Name = "provider-cheap",
                EstimatedCost = 0.001,
                HealthStatus = ProviderHealth.Healthy
            }
        };

        var selector = new ProviderSelector(SelectionStrategy.LeastCost);

        // Act
        var selected = selector.SelectProvider(providers, null);

        // Assert
        selected.Should().NotBeNull();
        selected!.Name.Should().Be("provider-cheap");
        selected.EstimatedCost.Should().Be(0.001);
    }

    [Fact]
    public void SelectProvider_WithFastestResponseStrategy_ShouldSelectFastestProvider()
    {
        // Arrange
        var providers = new List<ProviderInfo>
        {
            new ProviderInfo
            {
                Name = "provider-slow",
                AverageResponseTime = TimeSpan.FromSeconds(5),
                HealthStatus = ProviderHealth.Healthy
            },
            new ProviderInfo
            {
                Name = "provider-fast",
                AverageResponseTime = TimeSpan.FromSeconds(1),
                HealthStatus = ProviderHealth.Healthy
            }
        };

        var selector = new ProviderSelector(SelectionStrategy.FastestResponse);

        // Act
        var selected = selector.SelectProvider(providers, null);

        // Assert
        selected.Should().NotBeNull();
        selected!.Name.Should().Be("provider-fast");
        selected.AverageResponseTime.Should().Be(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void SelectProvider_WithUnhealthyProviders_ShouldSkipUnhealthy()
    {
        // Arrange
        var providers = new List<ProviderInfo>
        {
            new ProviderInfo
            {
                Name = "provider-unhealthy",
                Priority = 1,
                HealthStatus = ProviderHealth.Unhealthy
            },
            new ProviderInfo
            {
                Name = "provider-healthy",
                Priority = 2,
                HealthStatus = ProviderHealth.Healthy
            }
        };

        var selector = new ProviderSelector(SelectionStrategy.Priority);

        // Act
        var selected = selector.SelectProvider(providers, null);

        // Assert
        selected.Should().NotBeNull();
        selected!.Name.Should().Be("provider-healthy");
        selected.HealthStatus.Should().Be(ProviderHealth.Healthy);
    }

    [Fact]
    public void SelectProvider_WithRoundRobinStrategy_ShouldRotateProviders()
    {
        // Arrange
        var providers = new List<ProviderInfo>
        {
            new ProviderInfo { Name = "provider1", HealthStatus = ProviderHealth.Healthy },
            new ProviderInfo { Name = "provider2", HealthStatus = ProviderHealth.Healthy },
            new ProviderInfo { Name = "provider3", HealthStatus = ProviderHealth.Healthy }
        };

        var selector = new ProviderSelector(SelectionStrategy.RoundRobin);

        // Act
        var first = selector.SelectProvider(providers, null);
        var second = selector.SelectProvider(providers, null);
        var third = selector.SelectProvider(providers, null);
        var fourth = selector.SelectProvider(providers, null);

        // Assert
        var selected = new[] { first!.Name, second!.Name, third!.Name, fourth!.Name };
        selected.Distinct().Count().Should().BeGreaterOrEqualTo(2, "should rotate between providers");
    }

    [Fact]
    public void SelectProvider_WithNoProviders_ShouldReturnNull()
    {
        // Arrange
        var providers = new List<ProviderInfo>();
        var selector = new ProviderSelector(SelectionStrategy.Priority);

        // Act
        var selected = selector.SelectProvider(providers, null);

        // Assert
        selected.Should().BeNull();
    }

    [Fact]
    public void SelectProvider_WithAllUnhealthyProviders_ShouldReturnNull()
    {
        // Arrange
        var providers = new List<ProviderInfo>
        {
            new ProviderInfo { Name = "provider1", HealthStatus = ProviderHealth.Unhealthy },
            new ProviderInfo { Name = "provider2", HealthStatus = ProviderHealth.Unhealthy }
        };

        var selector = new ProviderSelector(SelectionStrategy.Priority);

        // Act
        var selected = selector.SelectProvider(providers, null);

        // Assert
        selected.Should().BeNull();
    }
}
