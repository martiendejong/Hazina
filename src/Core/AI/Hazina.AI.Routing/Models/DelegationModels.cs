namespace Hazina.AI.Routing.Models;

/// <summary>
/// Result of delegation cost calculation.
/// Includes recommendation (DELEGATE or DO_MYSELF) with full cost breakdown.
/// </summary>
public class DelegationDecision
{
    public required DecisionType Decision { get; set; }
    public required double TotalCostDelegate { get; set; }
    public required double TotalCostSelf { get; set; }
    public required double Savings { get; set; }
    public required double ROI { get; set; }
    public required VerificationLevel VerificationLevel { get; set; }
    public required double TrustScore { get; set; }
    public string? SuccessCriteria { get; set; }
    public required CostBreakdown Costs { get; set; }
    public required string TaskDescription { get; set; }
    public required AgentType AgentType { get; set; }
    public required TaskCategory Category { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Breakdown of delegation costs.
/// Transaction costs + Execution costs.
/// </summary>
public class CostBreakdown
{
    public required TransactionCost Transaction { get; set; }
    public required ExecutionCost Execution { get; set; }
}

/// <summary>
/// Transaction costs for delegation.
/// Based on Transaction Cost Economics theory.
/// </summary>
public class TransactionCost
{
    /// <summary>Search cost: finding and briefing the agent</summary>
    public double Search { get; set; } = 0.5;

    /// <summary>Negotiation cost: defining success criteria</summary>
    public double Negotiation { get; set; } = 0.5;

    /// <summary>Enforcement cost: monitoring and verification</summary>
    /// <remarks>Calculated as: (10 - trust) × criticality / 10</remarks>
    public required double Enforcement { get; set; }

    public double Total => Search + Negotiation + Enforcement;
}

/// <summary>
/// Execution costs for task completion.
/// </summary>
public class ExecutionCost
{
    /// <summary>Estimated turns if agent executes</summary>
    public required double Agent { get; set; }

    /// <summary>Estimated turns if doing it yourself</summary>
    public required double Self { get; set; }
}

/// <summary>
/// Cost estimate for a specific agent type.
/// </summary>
public class CostEstimate
{
    public required AgentType AgentType { get; set; }
    public required double TotalCost { get; set; }
    public required double TransactionCost { get; set; }
    public required double ExecutionCost { get; set; }
    public required double ROI { get; set; }
    public required bool Recommended { get; set; }
}

/// <summary>
/// Agent reputation data.
/// Tracks performance history and trust scores.
/// </summary>
public class AgentReputation
{
    public required AgentType AgentType { get; set; }
    public required TaskCategory Category { get; set; }
    public int TotalTasks { get; set; }
    public int Successes { get; set; }
    public int Failures { get; set; }
    public double SuccessRate { get; set; }
    public double AvgTurns { get; set; }
    public double TrustScore { get; set; } = 5.0; // Baseline neutral trust
    public List<string> FailurePatterns { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
}

/// <summary>
/// Learned delegation pattern.
/// Represents recurring successful (or unsuccessful) delegation scenarios.
/// </summary>
public class DelegationPattern
{
    public required string PatternId { get; set; }
    public required AgentType AgentType { get; set; }
    public required TaskCategory Category { get; set; }
    public required DecisionType TypicalDecision { get; set; }
    public int Occurrences { get; set; }
    public double SuccessRate { get; set; }
    public required string Description { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
}

/// <summary>
/// Types of AI agents available for delegation.
/// </summary>
public enum AgentType
{
    /// <summary>Fast codebase exploration agent</summary>
    Explore,

    /// <summary>Software architect agent for planning</summary>
    Plan,

    /// <summary>General-purpose agent for complex tasks</summary>
    GeneralPurpose,

    /// <summary>Command execution specialist</summary>
    Bash,

    /// <summary>Custom agent type</summary>
    Custom
}

/// <summary>
/// Categories of tasks for delegation.
/// </summary>
public enum TaskCategory
{
    CodeSearch,
    ArchitectureAnalysis,
    FileDiscovery,
    FeaturePlanning,
    ArchitectureDesign,
    Research,
    Debugging,
    ComplexAnalysis,
    GitOperations,
    TerminalCommands,
    Other
}

/// <summary>
/// Verification level required for task results.
/// Determines how many turns to spend verifying agent work.
/// </summary>
public enum VerificationLevel
{
    /// <summary>0-1 turns: Quick spot check</summary>
    SpotCheck,

    /// <summary>1-2 turns: Moderate verification</summary>
    Moderate,

    /// <summary>2-4 turns: Thorough review</summary>
    Thorough,

    /// <summary>4+ turns: Deep audit</summary>
    DeepAudit
}

/// <summary>
/// Decision type for delegation.
/// </summary>
public enum DecisionType
{
    /// <summary>Delegate task to agent</summary>
    Delegate,

    /// <summary>Execute task yourself</summary>
    DoMyself
}

/// <summary>
/// Outcome of delegated task.
/// </summary>
public enum DelegationOutcome
{
    Success,
    Failure
}
