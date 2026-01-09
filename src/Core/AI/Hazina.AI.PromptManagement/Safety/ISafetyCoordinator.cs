using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hazina.AI.PromptManagement.Rewriter;

namespace Hazina.AI.PromptManagement.Safety;

/// <summary>
/// Safety coordinator that validates proposals before deployment
/// </summary>
public interface ISafetyCoordinator
{
    /// <summary>
    /// Validate a proposal against all safety checks
    /// </summary>
    /// <param name="proposal">Proposal to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result</returns>
    Task<SafetyValidationResult> ValidateProposalAsync(
        PromptProposal proposal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Run sandbox test for a proposal
    /// </summary>
    /// <param name="proposal">Proposal to test</param>
    /// <param name="testSetId">Test set to use</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Sandbox test result</returns>
    Task<SandboxTestResult> RunSandboxTestAsync(
        PromptProposal proposal,
        string testSetId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if prompt is in cooldown period (too soon since last change)
    /// </summary>
    /// <param name="promptId">Prompt ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cooldown status</returns>
    Task<CooldownStatus> CheckCooldownAsync(
        string promptId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get safety configuration for a prompt
    /// </summary>
    /// <param name="promptId">Prompt ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Safety configuration</returns>
    Task<SafetyConfig> GetSafetyConfigAsync(
        string promptId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update safety configuration for a prompt
    /// </summary>
    /// <param name="config">Safety configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveSafetyConfigAsync(
        SafetyConfig config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Trigger emergency stop (block all changes to a prompt)
    /// </summary>
    /// <param name="promptId">Prompt ID</param>
    /// <param name="reason">Reason for emergency stop</param>
    /// <param name="initiatedBy">Who triggered the stop</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task EmergencyStopAsync(
        string promptId,
        string reason,
        string initiatedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Release emergency stop
    /// </summary>
    /// <param name="promptId">Prompt ID</param>
    /// <param name="releasedBy">Who released the stop</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ReleaseEmergencyStopAsync(
        string promptId,
        string releasedBy,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of safety validation
/// </summary>
public class SafetyValidationResult
{
    public bool Passed { get; set; }
    public List<SafetyViolation> Violations { get; set; } = new();
    public List<SafetyWarning> Warnings { get; set; } = new();
    public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;

    // Individual check results
    public bool CooldownPassed { get; set; }
    public bool SemanticSimilarityPassed { get; set; }
    public bool PerformanceThresholdPassed { get; set; }
    public bool SandboxTestPassed { get; set; }
    public bool EmergencyStopActive { get; set; }

    // Details
    public Dictionary<string, object> Details { get; set; } = new();
}

/// <summary>
/// Safety violation (blocking issue)
/// </summary>
public class SafetyViolation
{
    public string ViolationType { get; set; } = string.Empty;  // "cooldown" | "drift" | "performance" | "emergency_stop"
    public string Message { get; set; } = string.Empty;
    public double Severity { get; set; }  // 0.0 to 1.0
    public Dictionary<string, object> Details { get; set; } = new();
}

/// <summary>
/// Safety warning (non-blocking concern)
/// </summary>
public class SafetyWarning
{
    public string WarningType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}

/// <summary>
/// Sandbox test result
/// </summary>
public class SandboxTestResult
{
    public string TestId { get; set; } = Guid.NewGuid().ToString();
    public string ProposalId { get; set; } = string.Empty;
    public string TestSetId { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // Baseline (current version) performance
    public Dictionary<string, double> BaselineMetrics { get; set; } = new();

    // Proposed version performance
    public Dictionary<string, double> ProposedMetrics { get; set; } = new();

    // Comparison
    public Dictionary<string, double> ImprovementPercent { get; set; } = new();  // Per metric
    public double OverallImprovement { get; set; }
    public bool MeetsThreshold { get; set; }  // Proposed >= 95% of baseline

    // Errors
    public string? Errors { get; set; }

    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Cooldown status
/// </summary>
public class CooldownStatus
{
    public bool InCooldown { get; set; }
    public DateTime? LastChangeAt { get; set; }
    public TimeSpan CooldownPeriod { get; set; }
    public DateTime? CooldownEndsAt { get; set; }
    public TimeSpan? TimeRemaining { get; set; }
}

/// <summary>
/// Safety configuration for a prompt
/// </summary>
public class SafetyConfig
{
    public string PromptId { get; set; } = string.Empty;

    // Cooldown settings
    public TimeSpan CooldownPeriod { get; set; } = TimeSpan.FromHours(24);
    public bool EnforceCooldown { get; set; } = true;

    // Performance threshold settings
    public double MinPerformanceRatio { get; set; } = 0.95;  // New must be >= 95% of baseline
    public bool EnforcePerformanceThreshold { get; set; } = true;

    // Semantic similarity settings
    public double MinSemanticSimilarity { get; set; } = 0.85;
    public bool EnforceSemanticSimilarity { get; set; } = true;

    // Sandbox testing
    public bool RequireSandboxTest { get; set; } = true;
    public string? DefaultTestSetId { get; set; }

    // Emergency stop
    public bool EmergencyStopActive { get; set; } = false;
    public string? EmergencyStopReason { get; set; }
    public DateTime? EmergencyStopActivatedAt { get; set; }
    public string? EmergencyStopActivatedBy { get; set; }

    // Metadata
    public Dictionary<string, object> Metadata { get; set; } = new();
}
