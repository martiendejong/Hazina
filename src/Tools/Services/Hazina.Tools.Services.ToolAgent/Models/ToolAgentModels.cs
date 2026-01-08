namespace Hazina.Tools.Services.ToolAgent.Models;

/// <summary>
/// Request to the tool agent to execute an action.
/// </summary>
public record ToolAgentRequest
{
    /// <summary>
    /// The action to execute (e.g., "update_brand_profile", "generate_logo").
    /// </summary>
    public required string Action { get; init; }

    /// <summary>
    /// Optional context hint to help the tool agent understand the situation.
    /// Keep this minimal - the tool agent doesn't need full conversation history.
    /// </summary>
    public string? ContextHint { get; init; }

    /// <summary>
    /// Project ID for which to execute the action.
    /// </summary>
    public required string ProjectId { get; init; }

    /// <summary>
    /// Chat ID where the action was requested.
    /// </summary>
    public required string ChatId { get; init; }

    /// <summary>
    /// Optional user ID.
    /// </summary>
    public string? UserId { get; init; }
}

/// <summary>
/// Result from the tool agent after executing an action.
/// </summary>
public record ToolAgentResult
{
    /// <summary>
    /// Whether the action executed successfully.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Summary of what was done.
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// List of tools that were executed.
    /// </summary>
    public IReadOnlyList<string> ExecutedTools { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Optional error message if execution failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Optional data returned from the execution.
    /// </summary>
    public object? Data { get; init; }
}

/// <summary>
/// Describes an available action that the tool agent can execute.
/// </summary>
public record ToolAgentAction
{
    /// <summary>
    /// Action identifier (e.g., "update_brand_profile").
    /// </summary>
    public required string Action { get; init; }

    /// <summary>
    /// Human-readable description of what this action does.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Whether this action is typically async (fire-and-forget).
    /// </summary>
    public bool IsAsync { get; init; } = true;

    /// <summary>
    /// Estimated execution time in seconds.
    /// </summary>
    public int EstimatedDurationSeconds { get; init; }
}
