using Hazina.AI.Agents.Coordination;
using Xunit;

namespace Hazina.AI.Agents.Tests.Coordination;

public class AgentDelegationServiceTests : IDisposable
{
    private readonly string _testReputationFile;
    private readonly AgentDelegationService _service;

    public AgentDelegationServiceTests()
    {
        _testReputationFile = Path.Combine(Path.GetTempPath(), $"test-reputation-{Guid.NewGuid()}.json");
        _service = new AgentDelegationService(_testReputationFile);
    }

    public void Dispose()
    {
        if (File.Exists(_testReputationFile))
        {
            File.Delete(_testReputationFile);
        }
    }

    [Fact]
    public async Task CalculateDelegationCost_NewAgent_ReturnsNeutralTrust()
    {
        // Arrange
        var request = new DelegationRequest(
            TaskDescription: "Search for IActionService implementations",
            AgentType: "Explore",
            TaskCategory: "code_search",
            Criticality: 5,
            Verifiability: 8,
            SelfEstimateTurns: 4.0
        );

        // Act
        var decision = await _service.CalculateDelegationCostAsync(request);

        // Assert
        Assert.Equal(5.0, decision.TrustScore); // Neutral starting trust
        Assert.True(decision.ExecutionCost > 0);
        Assert.True(decision.TransactionCost > 0);
    }

    [Fact]
    public async Task CalculateDelegationCost_DelegationCheaperThanSelf_RecommendsDelegation()
    {
        // Arrange
        var request = new DelegationRequest(
            TaskDescription: "Find all controller files",
            AgentType: "Explore",
            TaskCategory: "file_discovery",
            Criticality: 3,
            Verifiability: 9,
            SelfEstimateTurns: 10.0 // Would take me 10 turns
        );

        // Act
        var decision = await _service.CalculateDelegationCostAsync(request);

        // Assert
        Assert.True(decision.ShouldDelegate, "Should delegate when total cost < self cost");
        Assert.True(decision.ROI > 0, "ROI should be positive when delegation saves turns");
        Assert.Contains("DELEGATE", decision.Recommendation);
    }

    [Fact]
    public async Task CalculateDelegationCost_SelfExecutionCheaper_RecommendsSelf()
    {
        // Arrange
        var request = new DelegationRequest(
            TaskDescription: "Quick file check",
            AgentType: "Explore",
            TaskCategory: "file_discovery",
            Criticality: 2,
            Verifiability: 10,
            SelfEstimateTurns: 1.0 // Only takes me 1 turn
        );

        // Act
        var decision = await _service.CalculateDelegationCostAsync(request);

        // Assert
        Assert.False(decision.ShouldDelegate, "Should NOT delegate when self execution is faster");
        Assert.True(decision.ROI < 0, "ROI should be negative when delegation wastes turns");
        Assert.Contains("DO_MYSELF", decision.Recommendation);
    }

    [Fact]
    public async Task UpdateAgentReputation_SuccessfulTask_IncreasesTrust()
    {
        // Arrange
        var update1 = new AgentReputationUpdate(
            AgentType: "Explore",
            TaskCategory: "code_search",
            Success: true,
            TurnsUsed: 2.5
        );

        var update2 = new AgentReputationUpdate(
            AgentType: "Explore",
            TaskCategory: "code_search",
            Success: true,
            TurnsUsed: 2.3
        );

        // Act
        await _service.UpdateAgentReputationAsync(update1);
        await _service.UpdateAgentReputationAsync(update2);

        var trustAfter = await _service.GetTrustScoreAsync("Explore", "code_search");

        // Assert
        Assert.True(trustAfter > 5.0, "Trust should increase above neutral after successful tasks");
    }

    [Fact]
    public async Task UpdateAgentReputation_FailedTask_DecreasesTrust()
    {
        // Arrange
        var successUpdate = new AgentReputationUpdate(
            AgentType: "Explore",
            TaskCategory: "code_search",
            Success: true,
            TurnsUsed: 2.0
        );

        var failureUpdate = new AgentReputationUpdate(
            AgentType: "Explore",
            TaskCategory: "code_search",
            Success: false,
            TurnsUsed: 5.0
        );

        // Act
        await _service.UpdateAgentReputationAsync(successUpdate);
        var trustAfterSuccess = await _service.GetTrustScoreAsync("Explore", "code_search");

        await _service.UpdateAgentReputationAsync(failureUpdate);
        var trustAfterFailure = await _service.GetTrustScoreAsync("Explore", "code_search");

        // Assert
        Assert.True(trustAfterFailure < trustAfterSuccess, "Trust should decrease after failure");
    }

    [Fact]
    public async Task GetBestAgent_WithHistoricalData_ReturnsHighestScoringAgent()
    {
        // Arrange - Build reputation for two agents
        await _service.UpdateAgentReputationAsync(new AgentReputationUpdate(
            "Explore", "code_search", Success: true, TurnsUsed: 2.0));
        await _service.UpdateAgentReputationAsync(new AgentReputationUpdate(
            "Explore", "code_search", Success: true, TurnsUsed: 2.5));
        await _service.UpdateAgentReputationAsync(new AgentReputationUpdate(
            "Explore", "code_search", Success: true, TurnsUsed: 2.2));

        await _service.UpdateAgentReputationAsync(new AgentReputationUpdate(
            "Plan", "code_search", Success: true, TurnsUsed: 5.0));
        await _service.UpdateAgentReputationAsync(new AgentReputationUpdate(
            "Plan", "code_search", Success: false, TurnsUsed: 7.0));

        // Act
        var recommendation = await _service.GetBestAgentAsync("code_search");

        // Assert
        Assert.Equal("Explore", recommendation.AgentType);
        Assert.True(recommendation.ConfidenceScore > 0);
        Assert.Contains("tasks", recommendation.Rationale.ToLower());
    }

    [Fact]
    public async Task GetBestAgent_NoHistoricalData_ReturnsGeneralPurpose()
    {
        // Act
        var recommendation = await _service.GetBestAgentAsync("unknown_category");

        // Assert
        Assert.Equal("general-purpose", recommendation.AgentType);
        Assert.True(recommendation.ConfidenceScore < 0.5, "Low confidence for unknown category");
        Assert.Contains("No historical data", recommendation.Rationale);
    }

    [Fact]
    public async Task VerificationStrategy_HighTrustLowCriticality_ReturnsNone()
    {
        // Arrange - Build high trust
        for (int i = 0; i < 10; i++)
        {
            await _service.UpdateAgentReputationAsync(new AgentReputationUpdate(
                "Explore", "code_search", Success: true, TurnsUsed: 2.0));
        }

        var request = new DelegationRequest(
            TaskDescription: "Simple search",
            AgentType: "Explore",
            TaskCategory: "code_search",
            Criticality: 2,
            Verifiability: 10,
            SelfEstimateTurns: 5.0
        );

        // Act
        var decision = await _service.CalculateDelegationCostAsync(request);

        // Assert
        Assert.Equal(DelegationVerificationStrategy.None, decision.VerificationStrategy);
        Assert.True(decision.VerificationLevel < 0.3, "Low verification needed for high trust + low criticality");
    }

    [Fact]
    public async Task VerificationStrategy_LowTrustHighCriticality_ReturnsComprehensive()
    {
        // Arrange - Build low trust
        await _service.UpdateAgentReputationAsync(new AgentReputationUpdate(
            "Explore", "code_search", Success: false, TurnsUsed: 10.0));
        await _service.UpdateAgentReputationAsync(new AgentReputationUpdate(
            "Explore", "code_search", Success: false, TurnsUsed: 12.0));

        var request = new DelegationRequest(
            TaskDescription: "Critical search",
            AgentType: "Explore",
            TaskCategory: "code_search",
            Criticality: 9,
            Verifiability: 5,
            SelfEstimateTurns: 20.0
        );

        // Act
        var decision = await _service.CalculateDelegationCostAsync(request);

        // Assert
        Assert.Equal(DelegationVerificationStrategy.Comprehensive, decision.VerificationStrategy);
        Assert.True(decision.VerificationLevel > 0.5, "High verification needed for low trust + high criticality");
    }

    [Fact]
    public async Task GetStatistics_AfterMultipleTasks_ReturnsAccurateMetrics()
    {
        // Arrange
        await _service.UpdateAgentReputationAsync(new AgentReputationUpdate(
            "Explore", "code_search", Success: true, TurnsUsed: 2.0));
        await _service.UpdateAgentReputationAsync(new AgentReputationUpdate(
            "Explore", "code_search", Success: true, TurnsUsed: 3.0));
        await _service.UpdateAgentReputationAsync(new AgentReputationUpdate(
            "Explore", "code_search", Success: false, TurnsUsed: 5.0));
        await _service.UpdateAgentReputationAsync(new AgentReputationUpdate(
            "Plan", "architecture_analysis", Success: true, TurnsUsed: 4.0));

        // Act
        var stats = await _service.GetStatisticsAsync();

        // Assert
        Assert.Equal(4, stats.TotalDelegations);
        Assert.Equal(3, stats.SuccessfulDelegations);
        Assert.Equal(0.75, stats.SuccessRate);
        Assert.Equal(2, stats.StatsByAgent.Count);

        var exploreStats = stats.StatsByAgent["Explore_code_search"];
        Assert.Equal(3, exploreStats.TaskCount);
        Assert.Equal(2, exploreStats.Successes);
        Assert.Equal(2.0 / 3.0, exploreStats.SuccessRate);
    }

    [Fact]
    public async Task TrustDiscount_ReducesTransactionCost()
    {
        // Arrange - Build high trust
        for (int i = 0; i < 10; i++)
        {
            await _service.UpdateAgentReputationAsync(new AgentReputationUpdate(
                "Explore", "code_search", Success: true, TurnsUsed: 2.0));
        }

        var requestHighTrust = new DelegationRequest(
            "Search task", "Explore", "code_search",
            Criticality: 5, Verifiability: 8, SelfEstimateTurns: 10.0);

        var requestLowTrust = new DelegationRequest(
            "Search task", "Plan", "code_search",
            Criticality: 5, Verifiability: 8, SelfEstimateTurns: 10.0);

        // Act
        var highTrustDecision = await _service.CalculateDelegationCostAsync(requestHighTrust);
        var lowTrustDecision = await _service.CalculateDelegationCostAsync(requestLowTrust);

        // Assert
        Assert.True(highTrustDecision.TransactionCost < lowTrustDecision.TransactionCost,
            "Higher trust should result in lower transaction cost (trust discount)");
    }
}
