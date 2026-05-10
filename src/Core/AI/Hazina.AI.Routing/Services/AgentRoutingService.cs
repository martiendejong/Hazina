using Hazina.AI.Routing.Algorithms;
using Hazina.AI.Routing.Interfaces;
using Hazina.AI.Routing.Models;
using Microsoft.Extensions.Logging;

namespace Hazina.AI.Routing.Services;

/// <summary>
/// Implementation of intelligent AI agent routing service.
/// Provides cost-optimized delegation decisions based on Transaction Cost Economics.
/// </summary>
public class AgentRoutingService : IAgentRoutingService
{
    private readonly ILogger<AgentRoutingService> _logger;
    private readonly IAgentReputationStore _reputationStore;
    private readonly List<DelegationPattern> _patterns;
    private bool _initialized;

    public AgentRoutingService(ILogger<AgentRoutingService> logger, IAgentReputationStore reputationStore)
    {
        _logger = logger;
        _reputationStore = reputationStore;
        _patterns = new List<DelegationPattern>();
    }

    public async Task<DelegationDecision> CalculateDelegationCostAsync(
        string taskDescription,
        AgentType agentType,
        TaskCategory category,
        int criticality,
        int verifiability,
        double selfEstimateTurns)
    {
        await EnsureInitializedAsync();

        _logger.LogInformation(
            "Calculating delegation cost for {Task} with agent {Agent}",
            taskDescription, agentType);

        // Get agent reputation
        var reputation = await GetAgentReputationAsync(agentType, category);
        var trustScore = reputation?.TrustScore ?? TrustCalculator.BaselineTrust;
        var agentExecutionTurns = reputation?.AvgTurns > 0
            ? reputation.AvgTurns
            : TrustCalculator.GetDefaultExecutionTime(agentType, category);

        // Calculate costs
        var costs = CostCalculator.CalculateCosts(trustScore, criticality, agentExecutionTurns);
        costs.Execution.Self = selfEstimateTurns;

        var totalCostDelegate = costs.Transaction.Total + costs.Execution.Agent;
        var totalCostSelf = costs.Execution.Self;

        // Make decision
        var decision = CostCalculator.MakeDecision(totalCostDelegate, totalCostSelf);
        var savings = CostCalculator.CalculateSavings(totalCostDelegate, totalCostSelf);
        var roi = CostCalculator.CalculateROI(totalCostDelegate, totalCostSelf);
        var verificationLevel = CostCalculator.CalculateVerificationLevel(trustScore, criticality);

        return new DelegationDecision
        {
            Decision = decision,
            TotalCostDelegate = Math.Round(totalCostDelegate, 2),
            TotalCostSelf = Math.Round(totalCostSelf, 2),
            Savings = Math.Round(savings, 2),
            ROI = Math.Round(roi, 3),
            VerificationLevel = verificationLevel,
            TrustScore = trustScore,
            Costs = costs,
            TaskDescription = taskDescription,
            AgentType = agentType,
            Category = category
        };
    }

    public async Task UpdateAgentReputationAsync(
        AgentType agentType,
        TaskCategory category,
        DelegationOutcome outcome,
        double turnsUsed,
        string? failurePattern = null,
        string? notes = null)
    {
        await EnsureInitializedAsync();

        var reputation = await GetAgentReputationAsync(agentType, category)
            ?? CreateDefaultReputation(agentType, category);

        // Update statistics
        reputation.TotalTasks++;
        if (outcome == DelegationOutcome.Success)
        {
            reputation.Successes++;
        }
        else
        {
            reputation.Failures++;
            if (!string.IsNullOrEmpty(failurePattern) &&
                !reputation.FailurePatterns.Contains(failurePattern))
            {
                reputation.FailurePatterns.Add(failurePattern);
            }
        }

        // Recalculate success rate
        reputation.SuccessRate = reputation.TotalTasks > 0
            ? Math.Round((double)reputation.Successes / reputation.TotalTasks, 3)
            : 0.0;

        // Update average turns
        reputation.AvgTurns = TrustCalculator.UpdateAverageTurns(
            reputation.AvgTurns, turnsUsed);

        // Update trust score
        var recentFailure = outcome == DelegationOutcome.Failure;
        reputation.TrustScore = TrustCalculator.CalculateTrustScore(
            reputation.SuccessRate, recentFailure);

        reputation.LastUpdated = DateTime.UtcNow;
        if (!string.IsNullOrEmpty(notes))
        {
            reputation.Notes = notes;
        }

        await _reputationStore.SaveAsync(reputation);

        _logger.LogInformation(
            "Updated reputation for {Agent}/{Category}: Success rate {SuccessRate:P0}, Trust {Trust}",
            agentType, category, reputation.SuccessRate, reputation.TrustScore);
    }

    public async Task<AgentType> GetBestAgentAsync(TaskCategory category, int criticalityLevel)
    {
        await EnsureInitializedAsync();

        var agentTypes = Enum.GetValues<AgentType>()
            .Where(a => a != AgentType.Custom)
            .ToList();

        AgentType bestAgent = AgentType.GeneralPurpose;
        double highestTrust = 0;

        foreach (var agentType in agentTypes)
        {
            var reputation = await GetAgentReputationAsync(agentType, category);
            if (reputation != null && reputation.TrustScore > highestTrust)
            {
                highestTrust = reputation.TrustScore;
                bestAgent = agentType;
            }
        }

        _logger.LogInformation(
            "Best agent for {Category}: {Agent} (trust: {Trust})",
            category, bestAgent, highestTrust);

        return bestAgent;
    }

    public async Task<Dictionary<AgentType, CostEstimate>> EstimateTaskCostAsync(
        string taskDescription,
        TaskCategory category,
        int criticality,
        int verifiability,
        double selfEstimateTurns)
    {
        var estimates = new Dictionary<AgentType, CostEstimate>();

        var agentTypes = Enum.GetValues<AgentType>()
            .Where(a => a != AgentType.Custom)
            .ToList();

        foreach (var agentType in agentTypes)
        {
            var decision = await CalculateDelegationCostAsync(
                taskDescription, agentType, category, criticality, verifiability, selfEstimateTurns);

            estimates[agentType] = new CostEstimate
            {
                AgentType = agentType,
                TotalCost = decision.TotalCostDelegate,
                TransactionCost = decision.Costs.Transaction.Total,
                ExecutionCost = decision.Costs.Execution.Agent,
                ROI = decision.ROI,
                Recommended = decision.Decision == DecisionType.Delegate
            };
        }

        return estimates;
    }

    public async Task<DelegationPattern[]> GetLearnedPatternsAsync(
        AgentType? agentType = null,
        TaskCategory? category = null)
    {
        var query = _patterns.AsEnumerable();

        if (agentType.HasValue)
        {
            query = query.Where(p => p.AgentType == agentType.Value);
        }

        if (category.HasValue)
        {
            query = query.Where(p => p.Category == category.Value);
        }

        return await Task.FromResult(query.ToArray());
    }

    public async Task<AgentReputation?> GetAgentReputationAsync(
        AgentType agentType,
        TaskCategory category)
    {
        await EnsureInitializedAsync();
        return await _reputationStore.GetAsync(agentType, category);
    }

    /// <summary>
    /// Seeds baseline reputation for all agent/category combinations on first use.
    /// </summary>
    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        var existing = await _reputationStore.GetAllAsync();
        if (!existing.Any())
        {
            await InitializeDefaultReputationAsync();
        }

        _initialized = true;
    }

    private async Task InitializeDefaultReputationAsync()
    {
        foreach (var agentType in Enum.GetValues<AgentType>())
        {
            if (agentType == AgentType.Custom) continue;

            foreach (var category in Enum.GetValues<TaskCategory>())
            {
                if (category == TaskCategory.Other) continue;

                await _reputationStore.SaveAsync(CreateDefaultReputation(agentType, category));
            }
        }
    }

    private static AgentReputation CreateDefaultReputation(AgentType agentType, TaskCategory category)
    {
        return new AgentReputation
        {
            AgentType = agentType,
            Category = category,
            TotalTasks = 0,
            Successes = 0,
            Failures = 0,
            SuccessRate = 0.0,
            AvgTurns = 0.0,
            TrustScore = 5.0, // Neutral baseline
            Notes = "Baseline trust, no data yet"
        };
    }
}
