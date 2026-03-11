namespace Hazina.AI.Agents.Coordination;

/// <summary>
/// Intelligent agent delegation service based on Transaction Cost Economics.
/// Determines when to delegate tasks to sub-agents vs. executing directly,
/// tracks agent performance, and provides optimal routing decisions.
///
/// Builder Protocol Implementation: Abstraction from personal tools
/// (calculate-delegation-cost.ps1) into reusable framework service.
/// </summary>
public interface IAgentDelegationService
{
    /// <summary>
    /// Calculates whether delegation is cost-effective for a given task.
    /// </summary>
    /// <param name="request">Delegation decision request parameters</param>
    /// <returns>Decision with cost breakdown and recommendation</returns>
    Task<DelegationDecision> CalculateDelegationCostAsync(DelegationRequest request);

    /// <summary>
    /// Updates agent reputation based on task outcome.
    /// Enables learning and improves future routing decisions.
    /// </summary>
    /// <param name="update">Performance update for an agent</param>
    Task UpdateAgentReputationAsync(AgentReputationUpdate update);

    /// <summary>
    /// Gets the best agent for a specific task category based on historical performance.
    /// </summary>
    /// <param name="taskCategory">Category of task (e.g., code_search, architecture_analysis)</param>
    /// <param name="requirements">Optional additional requirements (criticality, verifiability)</param>
    /// <returns>Recommended agent type with confidence score</returns>
    Task<AgentRecommendation> GetBestAgentAsync(string taskCategory, TaskRequirements? requirements = null);

    /// <summary>
    /// Gets current trust score for an agent in a specific category.
    /// Trust affects transaction cost (higher trust = lower verification overhead).
    /// </summary>
    /// <param name="agentType">Type of agent</param>
    /// <param name="taskCategory">Task category</param>
    /// <returns>Trust score (0.0 to 10.0)</returns>
    Task<double> GetTrustScoreAsync(string agentType, string taskCategory);

    /// <summary>
    /// Gets delegation statistics for monitoring and optimization.
    /// </summary>
    /// <returns>Aggregated delegation metrics</returns>
    Task<DelegationStatistics> GetStatisticsAsync();
}

/// <summary>
/// Request parameters for delegation decision.
/// </summary>
public record DelegationRequest(
    string TaskDescription,
    string AgentType,
    string TaskCategory,
    int Criticality,        // 0-10: how bad is failure?
    int Verifiability,      // 0-10: can I check results?
    double SelfEstimateTurns // How long would it take me?
);

/// <summary>
/// Delegation decision with cost breakdown.
/// </summary>
public record DelegationDecision(
    bool ShouldDelegate,
    double ExecutionCost,
    double TransactionCost,
    double TotalCost,
    double SelfCost,
    double ROI,                  // Return on investment
    double TrustScore,
    double VerificationLevel,    // How much verification needed (0.0-1.0)
    string Recommendation,       // Human-readable explanation
    DelegationVerificationStrategy VerificationStrategy
);

/// <summary>
/// How to verify delegated work based on trust and criticality.
/// </summary>
public enum DelegationVerificationStrategy
{
    None,           // Trust = 9+, criticality < 3: spot check only
    Light,          // Trust = 7-9, criticality < 5: sample verification
    Standard,       // Trust = 5-7 or criticality 5-7: thorough check
    Comprehensive,  // Trust < 5 or criticality > 7: detailed verification
    Complete        // Criticality = 10: verify everything
}

/// <summary>
/// Agent performance update after task completion.
/// </summary>
public record AgentReputationUpdate(
    string AgentType,
    string TaskCategory,
    bool Success,
    double TurnsUsed,
    string? Notes = null
);

/// <summary>
/// Recommendation for best agent to use.
/// </summary>
public record AgentRecommendation(
    string AgentType,
    double ConfidenceScore,     // 0.0-1.0
    double EstimatedTurns,
    double TrustScore,
    string Rationale
);

/// <summary>
/// Optional task requirements for agent selection.
/// </summary>
public record TaskRequirements(
    int? Criticality = null,
    int? Verifiability = null,
    double? MaxCost = null
);

/// <summary>
/// Aggregated delegation statistics.
/// </summary>
public record DelegationStatistics(
    int TotalDelegations,
    int SuccessfulDelegations,
    double SuccessRate,
    double AverageCostSavings,
    Dictionary<string, AgentCategoryStats> StatsByAgent
);

/// <summary>
/// Per-agent, per-category statistics.
/// </summary>
public record AgentCategoryStats(
    string AgentType,
    string Category,
    int TaskCount,
    int Successes,
    double SuccessRate,
    double AverageTurns,
    double TrustScore
);
