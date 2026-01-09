using System;
using System.Collections.Generic;

namespace Hazina.AI.PromptManagement.Core.Models;

/// <summary>
/// Represents a prompt template with versioning support
/// </summary>
public class PromptTemplate
{
    /// <summary>
    /// Unique identifier for the prompt
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of what this prompt does
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Current active version (SHA-256 hash)
    /// </summary>
    public string CurrentVersion { get; set; } = string.Empty;

    /// <summary>
    /// Template engine to use: "Handlebars", "Liquid", "Scriban"
    /// </summary>
    public string TemplateEngine { get; set; } = "Handlebars";

    /// <summary>
    /// Category for organization: "agent", "rag", "cognitive_channel", "policy"
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// When this template was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When this template was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Additional flexible metadata
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Performance metrics for the current version (populated on demand)
    /// </summary>
    public PromptMetrics? Metrics { get; set; }
}

/// <summary>
/// Represents a specific version of a prompt template
/// </summary>
public class PromptVersion
{
    /// <summary>
    /// Version identifier (SHA-256 hash)
    /// </summary>
    public string VersionId { get; set; } = string.Empty;

    /// <summary>
    /// Parent prompt ID
    /// </summary>
    public string PromptId { get; set; } = string.Empty;

    /// <summary>
    /// Sequential version number (1, 2, 3, ...)
    /// </summary>
    public int VersionNumber { get; set; }

    /// <summary>
    /// The actual template content
    /// </summary>
    public string Template { get; set; } = string.Empty;

    /// <summary>
    /// Template variable definitions
    /// </summary>
    public Dictionary<string, object>? Variables { get; set; }

    /// <summary>
    /// When this version was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Who created this version: "human:user@email.com" or "system:rewriter"
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Why was this version created?
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Parent version ID (for tracking lineage)
    /// </summary>
    public string? ParentVersion { get; set; }

    /// <summary>
    /// Status: "active", "archived", "rolled_back", "sandbox"
    /// </summary>
    public string Status { get; set; } = "active";

    /// <summary>
    /// Additional version-specific metadata
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Aggregated performance metrics for a prompt version
/// </summary>
public class PromptMetrics
{
    /// <summary>
    /// Prompt ID
    /// </summary>
    public string PromptId { get; set; } = string.Empty;

    /// <summary>
    /// Version ID
    /// </summary>
    public string VersionId { get; set; } = string.Empty;

    /// <summary>
    /// Total number of times this prompt was used
    /// </summary>
    public int TotalUses { get; set; }

    /// <summary>
    /// Number of successful executions
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Number of failed executions
    /// </summary>
    public int FailureCount { get; set; }

    /// <summary>
    /// Success rate (0.0 to 1.0)
    /// </summary>
    public double SuccessRate => TotalUses > 0 ? (double)SuccessCount / TotalUses : 0;

    /// <summary>
    /// Average user rating (0.0 to 1.0)
    /// </summary>
    public double? AvgUserRating { get; set; }

    /// <summary>
    /// Average confidence score (0.0 to 1.0)
    /// </summary>
    public double? AvgConfidence { get; set; }

    /// <summary>
    /// Average latency in milliseconds
    /// </summary>
    public double? AvgLatencyMs { get; set; }

    /// <summary>
    /// Total tokens used (input + output)
    /// </summary>
    public long TotalTokensUsed { get; set; }

    /// <summary>
    /// Total cost in USD
    /// </summary>
    public decimal TotalCostUsd { get; set; }

    /// <summary>
    /// Evaluation metrics: MRR, NDCG, Precision@K, etc.
    /// </summary>
    public Dictionary<string, double>? EvalMetrics { get; set; }

    /// <summary>
    /// When these metrics were last updated
    /// </summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Request to create or update a prompt template
/// </summary>
public class PromptTemplateRequest
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Template { get; set; } = string.Empty;
    public string TemplateEngine { get; set; } = "Handlebars";
    public string? Category { get; set; }
    public Dictionary<string, object>? Variables { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = "system:migration";
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Rollback request
/// </summary>
public class RollbackRequest
{
    public string PromptId { get; set; } = string.Empty;
    public string TargetVersion { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string InitiatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Rollback history entry
/// </summary>
public class RollbackHistory
{
    public int RollbackId { get; set; }
    public string PromptId { get; set; } = string.Empty;
    public string FromVersion { get; set; } = string.Empty;
    public string ToVersion { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string InitiatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
