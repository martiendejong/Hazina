using FluentAssertions;
using Hazina.AI.Providers.Cost;
using Xunit;

namespace Hazina.AI.Providers.Tests;

public class CostTrackerTests
{
    [Fact]
    public void RecordUsage_ShouldIncrementTotalCost()
    {
        // Arrange
        var tracker = new CostTracker();
        var usage1 = new TokenUsageInfo(1000, 500, 0.05m, 0.03m, "gpt-4o-mini");

        // Act
        tracker.RecordUsage("provider1", usage1);
        var totalCost = tracker.GetTotalCost();

        // Assert
        totalCost.Should().Be(0.08m); // 0.05 + 0.03
    }

    [Fact]
    public void RecordUsage_MultipleEntries_ShouldAggregate()
    {
        // Arrange
        var tracker = new CostTracker();
        var usage1 = new TokenUsageInfo(1000, 500, 0.05m, 0.03m, "gpt-4o-mini");
        var usage2 = new TokenUsageInfo(2000, 1000, 0.10m, 0.06m, "gpt-4o-mini");

        // Act
        tracker.RecordUsage("provider1", usage1);
        tracker.RecordUsage("provider1", usage2);

        // Assert
        var providerCost = tracker.GetTotalCost("provider1");
        providerCost.Should().Be(0.24m); // (0.05+0.03) + (0.10+0.06)
    }

    [Fact]
    public void GetTotalCost_ForSpecificProvider_ShouldReturnCorrectAmount()
    {
        // Arrange
        var tracker = new CostTracker();
        var usage1 = new TokenUsageInfo(1000, 500, 0.10m, 0.05m, "gpt-4o");
        var usage2 = new TokenUsageInfo(500, 250, 0.05m, 0.02m, "gpt-4o-mini");

        // Act
        tracker.RecordUsage("openai", usage1);
        tracker.RecordUsage("anthropic", usage2);

        // Assert
        var openaiCost = tracker.GetTotalCost("openai");
        var anthropicCost = tracker.GetTotalCost("anthropic");

        openaiCost.Should().Be(0.15m);
        anthropicCost.Should().Be(0.07m);
    }

    [Fact]
    public void GetTotalCost_ForUnknownProvider_ShouldReturnZero()
    {
        // Arrange
        var tracker = new CostTracker();

        // Act
        var cost = tracker.GetTotalCost("unknown-provider");

        // Assert
        cost.Should().Be(0m);
    }

    [Fact]
    public void GetTotalCost_AcrossAllProviders_ShouldSumCorrectly()
    {
        // Arrange
        var tracker = new CostTracker();
        var usage1 = new TokenUsageInfo(1000, 500, 0.10m, 0.05m, "gpt-4o");
        var usage2 = new TokenUsageInfo(500, 250, 0.05m, 0.02m, "gpt-4o-mini");
        var usage3 = new TokenUsageInfo(0, 0, 0.00m, 0.00m, "llama-local");

        // Act
        tracker.RecordUsage("openai", usage1);
        tracker.RecordUsage("anthropic", usage2);
        tracker.RecordUsage("ollama", usage3);

        // Assert
        var totalCost = tracker.GetTotalCost();
        totalCost.Should().Be(0.22m); // 0.15 + 0.07 + 0.00
    }

    [Fact]
    public void Reset_ShouldClearAllCosts()
    {
        // Arrange
        var tracker = new CostTracker();
        var usage = new TokenUsageInfo(1000, 500, 0.50m, 0.30m, "gpt-4o");
        tracker.RecordUsage("provider1", usage);
        tracker.RecordUsage("provider2", usage);

        // Act
        tracker.Reset();

        // Assert
        tracker.GetTotalCost().Should().Be(0m);
        tracker.GetTotalCost("provider1").Should().Be(0m);
        tracker.GetTotalCost("provider2").Should().Be(0m);
    }

    [Fact]
    public void Reset_ForSpecificProvider_ShouldOnlyClearThatProvider()
    {
        // Arrange
        var tracker = new CostTracker();
        var usage = new TokenUsageInfo(1000, 500, 0.10m, 0.05m, "gpt-4o");
        tracker.RecordUsage("provider1", usage);
        tracker.RecordUsage("provider2", usage);

        // Act
        tracker.Reset("provider1");

        // Assert
        tracker.GetTotalCost("provider1").Should().Be(0m);
        tracker.GetTotalCost("provider2").Should().Be(0.15m);
        tracker.GetTotalCost().Should().Be(0.15m);
    }

    [Fact]
    public void RecordUsage_WithNullUsage_ShouldThrow()
    {
        // Arrange
        var tracker = new CostTracker();

        // Act
        Action act = () => tracker.RecordUsage("provider1", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("usage");
    }

    [Fact]
    public void RecordUsage_WithEmptyProviderName_ShouldThrow()
    {
        // Arrange
        var tracker = new CostTracker();
        var usage = new TokenUsageInfo(1000, 500, 0.10m, 0.05m, "gpt-4o");

        // Act
        Action act = () => tracker.RecordUsage("", usage);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("providerName");
    }

    [Fact]
    public void GetCostByProvider_ShouldReturnAllProviders()
    {
        // Arrange
        var tracker = new CostTracker();
        var usage1 = new TokenUsageInfo(1000, 500, 0.10m, 0.00m, "gpt-4o");
        var usage2 = new TokenUsageInfo(500, 250, 0.20m, 0.00m, "claude");
        var usage3 = new TokenUsageInfo(2000, 1000, 0.30m, 0.00m, "llama");

        // Act
        tracker.RecordUsage("provider1", usage1);
        tracker.RecordUsage("provider2", usage2);
        tracker.RecordUsage("provider3", usage3);

        var breakdown = tracker.GetCostByProvider();

        // Assert
        breakdown.Should().HaveCount(3);
        breakdown.Should().ContainKey("provider1").WhoseValue.Should().Be(0.10m);
        breakdown.Should().ContainKey("provider2").WhoseValue.Should().Be(0.20m);
        breakdown.Should().ContainKey("provider3").WhoseValue.Should().Be(0.30m);
    }

    [Fact]
    public void GetUsage_ShouldReturnTokenUsageInfo()
    {
        // Arrange
        var tracker = new CostTracker();
        var usage = new TokenUsageInfo(1000, 500, 0.10m, 0.05m, "gpt-4o");

        // Act
        tracker.RecordUsage("provider1", usage);
        var retrieved = tracker.GetUsage("provider1");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved.InputTokens.Should().Be(1000);
        retrieved.OutputTokens.Should().Be(500);
        retrieved.TotalTokens.Should().Be(1500);
        retrieved.TotalCost.Should().Be(0.15m);
        retrieved.ModelName.Should().Be("gpt-4o");
    }

    [Fact]
    public void GetTotalUsage_ShouldAggregateAcrossProviders()
    {
        // Arrange
        var tracker = new CostTracker();
        var usage1 = new TokenUsageInfo(1000, 500, 0.10m, 0.05m, "gpt-4o");
        var usage2 = new TokenUsageInfo(2000, 1000, 0.20m, 0.10m, "claude");

        // Act
        tracker.RecordUsage("provider1", usage1);
        tracker.RecordUsage("provider2", usage2);

        var total = tracker.GetTotalUsage();

        // Assert
        total.InputTokens.Should().Be(3000);
        total.OutputTokens.Should().Be(1500);
        total.TotalTokens.Should().Be(4500);
        total.TotalCost.Should().Be(0.45m);
    }
}
