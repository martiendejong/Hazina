using FluentAssertions;
using Hazina.AI.Routing.Algorithms;
using Hazina.AI.Routing.Models;

namespace Hazina.AI.Routing.Tests;

public class TrustCalculatorTests
{
    [Fact]
    public void CalculateTrustScore_WithPerfectSuccessRate_Returns10()
    {
        // Arrange
        const double perfectRate = 1.0;

        // Act
        var trust = TrustCalculator.CalculateTrustScore(perfectRate);

        // Assert
        trust.Should().Be(10.0);
    }

    [Fact]
    public void CalculateTrustScore_WithHalfSuccessRate_Returns5()
    {
        // Arrange
        const double halfRate = 0.5;

        // Act
        var trust = TrustCalculator.CalculateTrustScore(halfRate);

        // Assert
        trust.Should().Be(5.0);
    }

    [Fact]
    public void CalculateTrustScore_WithRecentFailure_AppliesPenalty()
    {
        // Arrange
        const double successRate = 0.8; // Would normally be 8.0 trust

        // Act
        var trustWithoutFailure = TrustCalculator.CalculateTrustScore(successRate, recentFailure: false);
        var trustWithFailure = TrustCalculator.CalculateTrustScore(successRate, recentFailure: true);

        // Assert
        trustWithoutFailure.Should().Be(8.0);
        trustWithFailure.Should().Be(7.0); // 8.0 - 1.0 penalty
    }

    [Fact]
    public void CalculateTrustScore_WithRecentFailureAndLowRate_DoesNotGoBelowZero()
    {
        // Arrange
        const double lowRate = 0.05; // Would be 0.5 trust, minus 1.0 penalty = -0.5

        // Act
        var trust = TrustCalculator.CalculateTrustScore(lowRate, recentFailure: true);

        // Assert
        trust.Should().Be(0.0); // Math.Max(0, ...) ensures non-negative
    }

    [Fact]
    public void UpdateAverageTurns_WithZeroCurrentAverage_ReturnsNewValue()
    {
        // Arrange
        const double currentAvg = 0.0;
        const double newValue = 5.0;

        // Act
        var result = TrustCalculator.UpdateAverageTurns(currentAvg, newValue);

        // Assert
        result.Should().Be(5.0);
    }

    [Fact]
    public void UpdateAverageTurns_UsesExponentialMovingAverage()
    {
        // Arrange
        const double currentAvg = 10.0;
        const double newValue = 5.0;

        // Expected: 0.3 * 5.0 + 0.7 * 10.0 = 1.5 + 7.0 = 8.5
        const double expected = 8.5;

        // Act
        var result = TrustCalculator.UpdateAverageTurns(currentAvg, newValue);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(AgentType.Plan, TaskCategory.FeaturePlanning, 6.0)]
    [InlineData(AgentType.Plan, TaskCategory.ArchitectureDesign, 8.0)]
    [InlineData(AgentType.Explore, TaskCategory.CodeSearch, 2.0)]
    [InlineData(AgentType.Bash, TaskCategory.TerminalCommands, 1.0)]
    public void GetDefaultExecutionTime_ReturnsExpectedDefaults(
        AgentType agentType, TaskCategory category, double expectedTime)
    {
        // Act
        var time = TrustCalculator.GetDefaultExecutionTime(agentType, category);

        // Assert
        time.Should().Be(expectedTime);
    }

    [Fact]
    public void IsHighTrust_WithHighSuccessRate_ReturnsTrue()
    {
        // Arrange
        const double highRate = 0.90; // 90% success

        // Act
        var isHigh = TrustCalculator.IsHighTrust(highRate);

        // Assert
        isHigh.Should().BeTrue();
    }

    [Fact]
    public void IsHighTrust_WithLowSuccessRate_ReturnsFalse()
    {
        // Arrange
        const double lowRate = 0.80; // 80% success, below 85% threshold

        // Act
        var isHigh = TrustCalculator.IsHighTrust(lowRate);

        // Assert
        isHigh.Should().BeFalse();
    }

    [Fact]
    public void IsTrustScoreCalibrated_WithEnoughTasks_ReturnsTrue()
    {
        // Arrange
        const int enoughTasks = 5;

        // Act
        var isCalibrated = TrustCalculator.IsTrustScoreCalibrated(enoughTasks);

        // Assert
        isCalibrated.Should().BeTrue();
    }

    [Fact]
    public void IsTrustScoreCalibrated_WithTooFewTasks_ReturnsFalse()
    {
        // Arrange
        const int tooFewTasks = 2;

        // Act
        var isCalibrated = TrustCalculator.IsTrustScoreCalibrated(tooFewTasks);

        // Assert
        isCalibrated.Should().BeFalse();
    }

    [Fact]
    public void IsTrustScoreCalibrated_WithExactlyThreeTasks_ReturnsTrue()
    {
        // Arrange
        const int exactThreshold = 3;

        // Act
        var isCalibrated = TrustCalculator.IsTrustScoreCalibrated(exactThreshold);

        // Assert
        isCalibrated.Should().BeTrue();
    }
}
