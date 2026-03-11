using FluentAssertions;
using Hazina.AI.Routing.Algorithms;
using Hazina.AI.Routing.Models;

namespace Hazina.AI.Routing.Tests;

public class CostCalculatorTests
{
    [Fact]
    public void CalculateCosts_WithHighTrustLowCriticality_ReturnsLowEnforcementCost()
    {
        // Arrange
        const double highTrust = 9.0;
        const int lowCriticality = 2;
        const double agentTurns = 3.0;

        // Act
        var result = CostCalculator.CalculateCosts(highTrust, lowCriticality, agentTurns);

        // Assert
        result.Transaction.Search.Should().Be(0.5);
        result.Transaction.Negotiation.Should().Be(0.5);
        result.Transaction.Enforcement.Should().BeLessThan(0.3); // (10-9)*2/10 = 0.2
        result.Execution.Agent.Should().Be(3.0);
    }

    [Fact]
    public void CalculateCosts_WithLowTrustHighCriticality_ReturnsHighEnforcementCost()
    {
        // Arrange
        const double lowTrust = 2.0;
        const int highCriticality = 9;
        const double agentTurns = 5.0;

        // Act
        var result = CostCalculator.CalculateCosts(lowTrust, highCriticality, agentTurns);

        // Assert
        result.Transaction.Enforcement.Should().BeGreaterThan(7.0); // (10-2)*9/10 = 7.2
    }

    [Fact]
    public void CalculateVerificationLevel_WithHighTrustLowCriticality_ReturnsSpotCheck()
    {
        // Arrange
        const double highTrust = 9.0;
        const int lowCriticality = 2;

        // Act
        var level = CostCalculator.CalculateVerificationLevel(highTrust, lowCriticality);

        // Assert
        level.Should().Be(VerificationLevel.SpotCheck); // (10-9)*2/10 = 0.2 < 1.0
    }

    [Fact]
    public void CalculateVerificationLevel_WithLowTrustHighCriticality_ReturnsDeepAudit()
    {
        // Arrange
        const double lowTrust = 2.0;
        const int highCriticality = 9;

        // Act
        var level = CostCalculator.CalculateVerificationLevel(lowTrust, highCriticality);

        // Assert
        level.Should().Be(VerificationLevel.DeepAudit); // (10-2)*9/10 = 7.2 >= 4.0
    }

    [Fact]
    public void CalculateROI_WithCheaperDelegation_ReturnsPositiveROI()
    {
        // Arrange
        const double costDelegate = 5.0;
        const double costSelf = 10.0;

        // Act
        var roi = CostCalculator.CalculateROI(costDelegate, costSelf);

        // Assert
        roi.Should().Be(0.5); // (10-5)/10 = 0.5
    }

    [Fact]
    public void CalculateROI_WithExpensiveDelegation_ReturnsNegativeROI()
    {
        // Arrange
        const double costDelegate = 10.0;
        const double costSelf = 5.0;

        // Act
        var roi = CostCalculator.CalculateROI(costDelegate, costSelf);

        // Assert
        roi.Should().Be(-1.0); // (5-10)/5 = -1.0
    }

    [Fact]
    public void MakeDecision_WhenDelegateIsCheaper_ReturnsDelegate()
    {
        // Arrange
        const double costDelegate = 5.0;
        const double costSelf = 10.0;

        // Act
        var decision = CostCalculator.MakeDecision(costDelegate, costSelf);

        // Assert
        decision.Should().Be(DecisionType.Delegate);
    }

    [Fact]
    public void MakeDecision_WhenSelfIsCheaper_ReturnsDoMyself()
    {
        // Arrange
        const double costDelegate = 10.0;
        const double costSelf = 5.0;

        // Act
        var decision = CostCalculator.MakeDecision(costDelegate, costSelf);

        // Assert
        decision.Should().Be(DecisionType.DoMyself);
    }

    [Fact]
    public void CalculateSavings_ReturnsAbsoluteDifference()
    {
        // Arrange
        const double cost1 = 10.0;
        const double cost2 = 7.0;

        // Act
        var savings1 = CostCalculator.CalculateSavings(cost1, cost2);
        var savings2 = CostCalculator.CalculateSavings(cost2, cost1);

        // Assert
        savings1.Should().Be(3.0);
        savings2.Should().Be(3.0); // Absolute value, always positive
    }
}
