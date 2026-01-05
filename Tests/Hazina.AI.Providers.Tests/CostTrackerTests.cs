using FluentAssertions;
using Hazina.AI.Providers.Core;
using Xunit;

namespace Hazina.AI.Providers.Tests;

public class CostTrackerTests
{
    [Fact]
    public void TrackCost_ShouldIncrementTotalCost()
    {
        // Arrange
        var tracker = new CostTracker();
        var initialCost = tracker.GetTotalCost();

        // Act
        tracker.TrackCost("provider1", 0.05);
        tracker.TrackCost("provider1", 0.03);

        // Assert
        var totalCost = tracker.GetTotalCost();
        totalCost.Should().Be(0.08);
        totalCost.Should().BeGreaterThan(initialCost);
    }

    [Fact]
    public void GetProviderCost_ShouldReturnCorrectAmount()
    {
        // Arrange
        var tracker = new CostTracker();

        // Act
        tracker.TrackCost("provider1", 0.10);
        tracker.TrackCost("provider2", 0.05);
        tracker.TrackCost("provider1", 0.02);

        // Assert
        var provider1Cost = tracker.GetProviderCost("provider1");
        var provider2Cost = tracker.GetProviderCost("provider2");

        provider1Cost.Should().Be(0.12);
        provider2Cost.Should().Be(0.05);
    }

    [Fact]
    public void GetProviderCost_ForUnknownProvider_ShouldReturnZero()
    {
        // Arrange
        var tracker = new CostTracker();

        // Act
        var cost = tracker.GetProviderCost("unknown-provider");

        // Assert
        cost.Should().Be(0);
    }

    [Fact]
    public void TrackCost_WithMultipleProviders_ShouldTrackSeparately()
    {
        // Arrange
        var tracker = new CostTracker();

        // Act
        tracker.TrackCost("openai", 0.10);
        tracker.TrackCost("anthropic", 0.08);
        tracker.TrackCost("ollama", 0.00); // Free local model
        tracker.TrackCost("openai", 0.05);

        // Assert
        tracker.GetProviderCost("openai").Should().Be(0.15);
        tracker.GetProviderCost("anthropic").Should().Be(0.08);
        tracker.GetProviderCost("ollama").Should().Be(0.00);
        tracker.GetTotalCost().Should().Be(0.23);
    }

    [Fact]
    public void Reset_ShouldClearAllCosts()
    {
        // Arrange
        var tracker = new CostTracker();
        tracker.TrackCost("provider1", 0.50);
        tracker.TrackCost("provider2", 0.30);

        // Act
        tracker.Reset();

        // Assert
        tracker.GetTotalCost().Should().Be(0);
        tracker.GetProviderCost("provider1").Should().Be(0);
        tracker.GetProviderCost("provider2").Should().Be(0);
    }

    [Fact]
    public void TrackCost_WithNegativeAmount_ShouldThrow()
    {
        // Arrange
        var tracker = new CostTracker();

        // Act
        Action act = () => tracker.TrackCost("provider1", -0.05);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*negative*");
    }

    [Fact]
    public void GetCostBreakdown_ShouldReturnAllProviders()
    {
        // Arrange
        var tracker = new CostTracker();
        tracker.TrackCost("provider1", 0.10);
        tracker.TrackCost("provider2", 0.20);
        tracker.TrackCost("provider3", 0.30);

        // Act
        var breakdown = tracker.GetCostBreakdown();

        // Assert
        breakdown.Should().HaveCount(3);
        breakdown.Should().ContainKey("provider1");
        breakdown.Should().ContainKey("provider2");
        breakdown.Should().ContainKey("provider3");
        breakdown["provider1"].Should().Be(0.10);
        breakdown["provider2"].Should().Be(0.20);
        breakdown["provider3"].Should().Be(0.30);
    }

    [Fact]
    public void GetTotalCost_WithNoTracking_ShouldReturnZero()
    {
        // Arrange
        var tracker = new CostTracker();

        // Act
        var totalCost = tracker.GetTotalCost();

        // Assert
        totalCost.Should().Be(0);
    }

    [Theory]
    [InlineData(0.001, 0.002, 0.003)]
    [InlineData(0.1, 0.2, 0.3)]
    [InlineData(1.0, 2.0, 3.0)]
    [InlineData(10.5, 20.3, 30.8)]
    public void TrackCost_WithVariousAmounts_ShouldCalculateCorrectly(double cost1, double cost2, double expected)
    {
        // Arrange
        var tracker = new CostTracker();

        // Act
        tracker.TrackCost("provider", cost1);
        tracker.TrackCost("provider", cost2);

        // Assert
        var totalCost = tracker.GetProviderCost("provider");
        totalCost.Should().BeApproximately(expected, 0.0001);
    }
}
