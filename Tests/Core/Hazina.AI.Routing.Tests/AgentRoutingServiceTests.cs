using FluentAssertions;
using Hazina.AI.Routing.Interfaces;
using Hazina.AI.Routing.Models;
using Hazina.AI.Routing.Persistence;
using Hazina.AI.Routing.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace Hazina.AI.Routing.Tests;

public class AgentRoutingServiceTests
{
    private readonly IAgentRoutingService _service;

    public AgentRoutingServiceTests()
    {
        var mockLogger = new Mock<ILogger<AgentRoutingService>>();
        var store = new InMemoryAgentReputationStore();
        _service = new AgentRoutingService(mockLogger.Object, store);
    }

    [Fact]
    public async Task CalculateDelegationCostAsync_WithSimpleTask_ReturnsDoMyself()
    {
        // Arrange - simple tasks (≤2 turns) should be done yourself
        const string taskDescription = "Read a file";
        const AgentType agentType = AgentType.Bash;
        const TaskCategory category = TaskCategory.FileDiscovery;
        const int lowCriticality = 2;
        const int highVerifiability = 9;
        const double selfTurns = 1.0;

        // Act
        var decision = await _service.CalculateDelegationCostAsync(
            taskDescription, agentType, category, lowCriticality, highVerifiability, selfTurns);

        // Assert
        decision.Decision.Should().Be(DecisionType.DoMyself);
        decision.TotalCostSelf.Should().BeLessThan(decision.TotalCostDelegate);
    }

    [Fact]
    public async Task CalculateDelegationCostAsync_WithComplexTask_RecommendsDelegateIfCheaper()
    {
        // Arrange - complex task with high self-cost
        const string taskDescription = "Analyze complex codebase architecture";
        const AgentType agentType = AgentType.Explore;
        const TaskCategory category = TaskCategory.ComplexAnalysis;
        const int medCriticality = 5;
        const int medVerifiability = 6;
        const double highSelfTurns = 10.0; // High self-cost

        // Act
        var decision = await _service.CalculateDelegationCostAsync(
            taskDescription, agentType, category, medCriticality, medVerifiability, highSelfTurns);

        // Assert
        if (decision.TotalCostDelegate < decision.TotalCostSelf)
        {
            decision.Decision.Should().Be(DecisionType.Delegate);
            decision.ROI.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task CalculateDelegationCostAsync_WithHighCriticality_IncludesHighEnforcementCost()
    {
        // Arrange - critical task requires deep verification
        const string taskDescription = "Deploy to production";
        const AgentType agentType = AgentType.Bash;
        const TaskCategory category = TaskCategory.Other;
        const int highCriticality = 9;
        const int medVerifiability = 5;
        const double medSelfTurns = 3.0;

        // Act
        var decision = await _service.CalculateDelegationCostAsync(
            taskDescription, agentType, category, highCriticality, medVerifiability, medSelfTurns);

        // Assert
        decision.Costs.Transaction.Enforcement.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public async Task UpdateAgentReputationAsync_WithSuccess_UpdatesReputation()
    {
        // Arrange
        const AgentType agentType = AgentType.Plan;
        const TaskCategory category = TaskCategory.FeaturePlanning;

        // Act - successful task
        await _service.UpdateAgentReputationAsync(
            agentType, category, DelegationOutcome.Success, turnsUsed: 3);

        // Get updated reputation
        var reputation = await _service.GetAgentReputationAsync(agentType, category);

        // Assert
        reputation.Should().NotBeNull();
        reputation!.TotalTasks.Should().Be(1);
        reputation.Successes.Should().Be(1);
        reputation.Failures.Should().Be(0);
    }

    [Fact]
    public async Task UpdateAgentReputationAsync_WithFailure_DecreasesTrust()
    {
        // Arrange - establish baseline with success
        const AgentType agentType = AgentType.Explore;
        const TaskCategory category = TaskCategory.Research;

        // First success to establish trust
        await _service.UpdateAgentReputationAsync(
            agentType, category, DelegationOutcome.Success, turnsUsed: 4);

        var initialReputation = await _service.GetAgentReputationAsync(agentType, category);
        var initialTrust = initialReputation!.TrustScore;

        // Act - failure
        await _service.UpdateAgentReputationAsync(
            agentType, category, DelegationOutcome.Failure, turnsUsed: 10,
            failurePattern: "Timeout exceeded");

        // Assert
        var updatedReputation = await _service.GetAgentReputationAsync(agentType, category);
        updatedReputation!.TrustScore.Should().BeLessThan(initialTrust);
        updatedReputation.Failures.Should().Be(1);
        updatedReputation.TotalTasks.Should().Be(2);
    }

    [Fact]
    public async Task GetBestAgentAsync_ReturnsAgentType()
    {
        // Arrange
        const TaskCategory category = TaskCategory.CodeSearch;
        const int criticality = 5;

        // Act
        var bestAgent = await _service.GetBestAgentAsync(category, criticality);

        // Assert
        bestAgent.Should().BeOneOf(Enum.GetValues<AgentType>());
    }

    [Fact]
    public async Task GetBestAgentAsync_WithBuiltReputation_PrefersHighTrustAgent()
    {
        // Arrange - build reputation for different agents
        const TaskCategory category = TaskCategory.Other;

        // Plan agent: high success rate
        await _service.UpdateAgentReputationAsync(
            AgentType.Plan, category, DelegationOutcome.Success, turnsUsed: 2);
        await _service.UpdateAgentReputationAsync(
            AgentType.Plan, category, DelegationOutcome.Success, turnsUsed: 2);

        // Explore agent: mixed results
        await _service.UpdateAgentReputationAsync(
            AgentType.Explore, category, DelegationOutcome.Success, turnsUsed: 5);
        await _service.UpdateAgentReputationAsync(
            AgentType.Explore, category, DelegationOutcome.Failure, turnsUsed: 10);

        // Act
        var bestAgent = await _service.GetBestAgentAsync(category, criticalityLevel: 5);

        // Assert
        bestAgent.Should().Be(AgentType.Plan); // Should prefer Plan due to better track record
    }

    [Fact]
    public async Task EstimateTaskCostAsync_ReturnsEstimatesForAllAgents()
    {
        // Arrange
        const string taskDescription = "Search for API usage patterns";
        const TaskCategory category = TaskCategory.ComplexAnalysis;
        const int criticality = 6;
        const int verifiability = 7;
        const double selfTurns = 5.0;

        // Act
        var estimates = await _service.EstimateTaskCostAsync(
            taskDescription, category, criticality, verifiability, selfTurns);

        // Assert
        estimates.Should().NotBeEmpty();
        estimates.Should().ContainKeys(
            AgentType.Plan,
            AgentType.Explore,
            AgentType.Bash,
            AgentType.GeneralPurpose);

        foreach (var estimate in estimates.Values)
        {
            estimate.TotalCost.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task GetLearnedPatternsAsync_ReturnsPatterns()
    {
        // Arrange - create some history
        await _service.UpdateAgentReputationAsync(
            AgentType.Bash, TaskCategory.GitOperations, DelegationOutcome.Success, turnsUsed: 1.5);

        // Act
        var patterns = await _service.GetLearnedPatternsAsync();

        // Assert
        patterns.Should().NotBeNull();
    }

    [Fact]
    public async Task GetLearnedPatternsAsync_WithFilters_ReturnsFilteredPatterns()
    {
        // Arrange
        await _service.UpdateAgentReputationAsync(
            AgentType.Plan, TaskCategory.FeaturePlanning, DelegationOutcome.Success, turnsUsed: 6);
        await _service.UpdateAgentReputationAsync(
            AgentType.Bash, TaskCategory.GitOperations, DelegationOutcome.Success, turnsUsed: 1.5);

        // Act
        var planPatterns = await _service.GetLearnedPatternsAsync(agentType: AgentType.Plan);
        var bashPatterns = await _service.GetLearnedPatternsAsync(agentType: AgentType.Bash);

        // Assert
        planPatterns.Should().NotBeNull();
        bashPatterns.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAgentReputationAsync_WithNoHistory_ReturnsBaselineReputation()
    {
        // Arrange - brand new agent-category combination
        const AgentType agentType = AgentType.Plan;
        const TaskCategory category = TaskCategory.Debugging;

        // Act
        var reputation = await _service.GetAgentReputationAsync(agentType, category);

        // Assert
        reputation.Should().NotBeNull(); // Service creates baseline reputation
        reputation!.TotalTasks.Should().Be(0);
        reputation.TrustScore.Should().Be(5.0); // Baseline neutral trust
        reputation.Successes.Should().Be(0);
        reputation.Failures.Should().Be(0);
    }

    [Theory]
    [InlineData(AgentType.Plan, TaskCategory.FeaturePlanning)]
    [InlineData(AgentType.Explore, TaskCategory.Research)]
    [InlineData(AgentType.Bash, TaskCategory.FileDiscovery)]
    public async Task FullWorkflow_CalculateDecision_Delegate_UpdateReputation_Works(
        AgentType agentType, TaskCategory category)
    {
        // Arrange
        const string taskDescription = "Test task";
        const int criticality = 5;
        const int verifiability = 7;
        const double selfTurns = 5.0;

        // Act 1: Calculate decision
        var decision = await _service.CalculateDelegationCostAsync(
            taskDescription, agentType, category, criticality, verifiability, selfTurns);

        decision.Should().NotBeNull();

        // Act 2: Simulate delegation (if recommended)
        if (decision.Decision == DecisionType.Delegate)
        {
            await _service.UpdateAgentReputationAsync(
                agentType, category, DelegationOutcome.Success, turnsUsed: 3);
        }

        // Act 3: Get reputation
        var reputation = await _service.GetAgentReputationAsync(agentType, category);

        // Assert - if delegated, reputation should exist
        if (decision.Decision == DecisionType.Delegate)
        {
            reputation.Should().NotBeNull();
            reputation!.TotalTasks.Should().BeGreaterThan(0);
        }
    }
}
