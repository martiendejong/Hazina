using System.Text.Json;

namespace Hazina.AI.Agents.Coordination;

/// <summary>
/// Implementation of intelligent agent delegation using Transaction Cost Economics.
///
/// Core algorithm:
/// - Execution cost = estimated turns for agent to complete task
/// - Transaction cost = setup + verification + communication overhead
/// - Transaction cost influenced by trust (higher trust = lower verification)
/// - Delegate if: (execution_cost + transaction_cost) < self_execution_cost
///
/// Learning mechanism:
/// - Track success/failure per agent per category
/// - Update trust scores based on outcomes
/// - Optimize routing over time
/// </summary>
public class AgentDelegationService : IAgentDelegationService
{
    private readonly string _reputationFilePath;
    private AgentReputation _reputation;
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Transaction cost components
    private const double SEARCH_COST = 0.5;       // Finding/briefing agent
    private const double VERIFICATION_BASE = 0.3;  // Base verification cost
    private const double COMMUNICATION_OVERHEAD = 0.2; // Per-turn overhead

    public AgentDelegationService(string? reputationFilePath = null)
    {
        _reputationFilePath = reputationFilePath ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".hazina", "agent-reputation.json");

        _reputation = LoadReputation();
    }

    public async Task<DelegationDecision> CalculateDelegationCostAsync(DelegationRequest request)
    {
        await _lock.WaitAsync();
        try
        {
            var categoryData = GetOrCreateCategoryData(request.AgentType, request.TaskCategory);
            var trustScore = categoryData.TrustScore;
            var avgAgentTurns = categoryData.AvgTurns > 0 ? categoryData.AvgTurns : 3.0;

            // Calculate transaction costs (influenced by trust)
            var trustDiscount = (trustScore / 10.0) * 0.5; // Max 50% discount at trust=10
            var verificationCost = VERIFICATION_BASE * (1.0 - trustDiscount);
            var criticalityPenalty = (request.Criticality / 10.0) * 0.5; // Higher criticality = more verification
            verificationCost += criticalityPenalty;

            var transactionCost = SEARCH_COST + verificationCost +
                (avgAgentTurns * COMMUNICATION_OVERHEAD);

            // Calculate execution cost
            var executionCost = avgAgentTurns;

            // Calculate self cost
            var selfCost = request.SelfEstimateTurns;

            // Total delegation cost
            var totalCost = executionCost + transactionCost;

            // ROI: positive = delegation saves turns
            var roi = selfCost - totalCost;

            // Verification strategy based on trust and criticality
            var verificationStrategy = DetermineVerificationStrategy(trustScore, request.Criticality);
            var verificationLevel = CalculateVerificationLevel(trustScore, request.Criticality, request.Verifiability);

            // Decision
            var shouldDelegate = totalCost < selfCost && roi > 0;

            var recommendation = GenerateRecommendation(
                shouldDelegate, roi, trustScore, request.Criticality, request.Verifiability);

            return new DelegationDecision(
                ShouldDelegate: shouldDelegate,
                ExecutionCost: Math.Round(executionCost, 2),
                TransactionCost: Math.Round(transactionCost, 2),
                TotalCost: Math.Round(totalCost, 2),
                SelfCost: Math.Round(selfCost, 2),
                ROI: Math.Round(roi, 2),
                TrustScore: trustScore,
                VerificationLevel: verificationLevel,
                Recommendation: recommendation,
                VerificationStrategy: verificationStrategy
            );
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateAgentReputationAsync(AgentReputationUpdate update)
    {
        await _lock.WaitAsync();
        try
        {
            var categoryData = GetOrCreateCategoryData(update.AgentType, update.TaskCategory);

            // Update statistics
            categoryData.TaskCount++;
            if (update.Success)
            {
                categoryData.Successes++;
            }

            // Update average turns (moving average)
            if (categoryData.TaskCount == 1)
            {
                categoryData.AvgTurns = update.TurnsUsed;
            }
            else
            {
                categoryData.AvgTurns = (categoryData.AvgTurns * (categoryData.TaskCount - 1) + update.TurnsUsed)
                    / categoryData.TaskCount;
            }

            // Update success rate
            categoryData.SuccessRate = (double)categoryData.Successes / categoryData.TaskCount;

            // Update trust score (0-10 scale, based on success rate and consistency)
            var successRateTrust = categoryData.SuccessRate * 10.0;
            var consistencyFactor = Math.Max(0, 1.0 - (categoryData.AvgTurns / 10.0)); // Penalty for long tasks
            categoryData.TrustScore = Math.Round(successRateTrust * (0.8 + consistencyFactor * 0.2), 1);

            categoryData.LastUpdated = DateTime.UtcNow;

            await SaveReputationAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<AgentRecommendation> GetBestAgentAsync(string taskCategory, TaskRequirements? requirements = null)
    {
        await _lock.WaitAsync();
        try
        {
            var candidates = new List<(string agentType, AgentCategoryData data)>();

            foreach (var (agentType, agentData) in _reputation.Agents)
            {
                if (agentData.TaskCategories.TryGetValue(taskCategory, out var categoryData))
                {
                    candidates.Add((agentType, categoryData));
                }
            }

            if (!candidates.Any())
            {
                return new AgentRecommendation(
                    AgentType: "general-purpose",
                    ConfidenceScore: 0.3,
                    EstimatedTurns: 5.0,
                    TrustScore: 5.0,
                    Rationale: "No historical data for this category, defaulting to general-purpose agent"
                );
            }

            // Score candidates: trust * success_rate * (1 / avg_turns)
            var scored = candidates
                .Select(c => new
                {
                    c.agentType,
                    c.data,
                    score = (c.data.TrustScore / 10.0) * c.data.SuccessRate * (1.0 / Math.Max(c.data.AvgTurns, 1.0))
                })
                .OrderByDescending(x => x.score)
                .First();

            var confidence = Math.Min(1.0, scored.score * scored.data.TaskCount / 10.0); // Confidence grows with data

            return new AgentRecommendation(
                AgentType: scored.agentType,
                ConfidenceScore: Math.Round(confidence, 2),
                EstimatedTurns: Math.Round(scored.data.AvgTurns, 1),
                TrustScore: scored.data.TrustScore,
                Rationale: $"Based on {scored.data.TaskCount} tasks: {scored.data.SuccessRate:P0} success rate, avg {scored.data.AvgTurns:F1} turns"
            );
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task<double> GetTrustScoreAsync(string agentType, string taskCategory)
    {
        var categoryData = GetOrCreateCategoryData(agentType, taskCategory);
        return Task.FromResult(categoryData.TrustScore);
    }

    public async Task<DelegationStatistics> GetStatisticsAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var totalDelegations = 0;
            var successfulDelegations = 0;
            var statsByAgent = new Dictionary<string, AgentCategoryStats>();

            foreach (var (agentType, agentData) in _reputation.Agents)
            {
                foreach (var (category, categoryData) in agentData.TaskCategories)
                {
                    totalDelegations += categoryData.TaskCount;
                    successfulDelegations += categoryData.Successes;

                    var key = $"{agentType}_{category}";
                    statsByAgent[key] = new AgentCategoryStats(
                        AgentType: agentType,
                        Category: category,
                        TaskCount: categoryData.TaskCount,
                        Successes: categoryData.Successes,
                        SuccessRate: categoryData.SuccessRate,
                        AverageTurns: categoryData.AvgTurns,
                        TrustScore: categoryData.TrustScore
                    );
                }
            }

            var successRate = totalDelegations > 0 ? (double)successfulDelegations / totalDelegations : 0.0;
            var avgCostSavings = 0.0; // TODO: Track actual cost savings

            return new DelegationStatistics(
                TotalDelegations: totalDelegations,
                SuccessfulDelegations: successfulDelegations,
                SuccessRate: successRate,
                AverageCostSavings: avgCostSavings,
                StatsByAgent: statsByAgent
            );
        }
        finally
        {
            _lock.Release();
        }
    }

    // Private helper methods

    private AgentCategoryData GetOrCreateCategoryData(string agentType, string taskCategory)
    {
        if (!_reputation.Agents.TryGetValue(agentType, out var agentData))
        {
            agentData = new AgentData { TaskCategories = new Dictionary<string, AgentCategoryData>() };
            _reputation.Agents[agentType] = agentData;
        }

        if (!agentData.TaskCategories.TryGetValue(taskCategory, out var categoryData))
        {
            categoryData = new AgentCategoryData
            {
                TrustScore = 5.0, // Neutral starting trust
                AvgTurns = 3.0,   // Conservative estimate
                TaskCount = 0,
                Successes = 0,
                SuccessRate = 0.0,
                LastUpdated = DateTime.UtcNow
            };
            agentData.TaskCategories[taskCategory] = categoryData;
        }

        return categoryData;
    }

    private static DelegationVerificationStrategy DetermineVerificationStrategy(double trust, int criticality)
    {
        if (criticality >= 10) return DelegationVerificationStrategy.Complete;
        if (trust >= 9 && criticality < 3) return DelegationVerificationStrategy.None;
        if (trust >= 7 && criticality < 5) return DelegationVerificationStrategy.Light;
        if (trust < 5 || criticality > 7) return DelegationVerificationStrategy.Comprehensive;
        return DelegationVerificationStrategy.Standard;
    }

    private static double CalculateVerificationLevel(double trust, int criticality, int verifiability)
    {
        // Base verification on inverse of trust
        var trustComponent = 1.0 - (trust / 10.0);

        // Increase for criticality
        var criticalityComponent = criticality / 10.0;

        // Decrease if highly verifiable (easier to check)
        var verifiabilityDiscount = verifiability / 20.0; // Max 50% discount

        var level = (trustComponent * 0.4 + criticalityComponent * 0.6) * (1.0 - verifiabilityDiscount);

        return Math.Round(Math.Clamp(level, 0.0, 1.0), 2);
    }

    private static string GenerateRecommendation(bool shouldDelegate, double roi, double trust, int criticality, int verifiability)
    {
        if (!shouldDelegate)
        {
            if (roi < -2.0)
                return $"DO_MYSELF: Delegation would waste {Math.Abs(roi):F1} turns. Much faster to do directly.";
            return $"DO_MYSELF: Marginal difference (ROI: {roi:F1}). Direct execution more efficient.";
        }

        var trustWarning = trust < 5.0 ? " (Low trust: verify thoroughly)" : "";
        var criticalityWarning = criticality >= 8 ? " (High criticality: comprehensive checks)" : "";

        return $"DELEGATE: Saves {roi:F1} turns. Verification: {(verifiability >= 7 ? "easy" : "moderate")}.{trustWarning}{criticalityWarning}";
    }

    private AgentReputation LoadReputation()
    {
        if (!File.Exists(_reputationFilePath))
        {
            return new AgentReputation { Agents = new Dictionary<string, AgentData>() };
        }

        try
        {
            var json = File.ReadAllText(_reputationFilePath);
            return JsonSerializer.Deserialize<AgentReputation>(json) ??
                new AgentReputation { Agents = new Dictionary<string, AgentData>() };
        }
        catch
        {
            return new AgentReputation { Agents = new Dictionary<string, AgentData>() };
        }
    }

    private async Task SaveReputationAsync()
    {
        var directory = Path.GetDirectoryName(_reputationFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(_reputation, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_reputationFilePath, json);
    }

    // Internal data models for JSON persistence

    private class AgentReputation
    {
        public Dictionary<string, AgentData> Agents { get; set; } = new();
    }

    private class AgentData
    {
        public Dictionary<string, AgentCategoryData> TaskCategories { get; set; } = new();
    }

    private class AgentCategoryData
    {
        public double TrustScore { get; set; }
        public double AvgTurns { get; set; }
        public int TaskCount { get; set; }
        public int Successes { get; set; }
        public double SuccessRate { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
