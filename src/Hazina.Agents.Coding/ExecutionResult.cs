namespace Hazina.Agents.Coding;

/// <summary>
/// Result of tool execution outside the GLM model.
/// Captures success/failure and all output for observation.
/// </summary>
public class ExecutionResult
{
    /// <summary>
    /// Whether the tool execution succeeded
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Standard output from the tool
    /// </summary>
    public required string Output { get; init; }

    /// <summary>
    /// Error output or exception message
    /// </summary>
    public required string Error { get; init; }

    /// <summary>
    /// Tool that was executed
    /// </summary>
    public string? Tool { get; init; }

    /// <summary>
    /// Create a successful result
    /// </summary>
    public static ExecutionResult SuccessResult(string output, string? tool = null)
    {
        return new ExecutionResult
        {
            Success = true,
            Output = output,
            Error = string.Empty,
            Tool = tool
        };
    }

    /// <summary>
    /// Create a failure result
    /// </summary>
    public static ExecutionResult FailureResult(string error, string? tool = null, string? partialOutput = null)
    {
        return new ExecutionResult
        {
            Success = false,
            Output = partialOutput ?? string.Empty,
            Error = error,
            Tool = tool
        };
    }
}

/// <summary>
/// GLM planner output - mandatory JSON schema
/// </summary>
public class PlanResult
{
    /// <summary>
    /// Short internal reasoning (required)
    /// </summary>
    public required string Thought { get; init; }

    /// <summary>
    /// High-level plan steps (required)
    /// </summary>
    public required List<string> Plan { get; init; }

    /// <summary>
    /// Actions to execute (required)
    /// </summary>
    public required List<ToolAction> Actions { get; init; }
}

/// <summary>
/// Individual tool action from GLM planner
/// </summary>
public class ToolAction
{
    /// <summary>
    /// Tool name: read_file | apply_diff | run | git_status | git_diff
    /// </summary>
    public required string Tool { get; init; }

    /// <summary>
    /// Optional file path for read_file, apply_diff
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Optional unified diff for apply_diff
    /// </summary>
    public string? Diff { get; init; }

    /// <summary>
    /// Optional PowerShell command for run
    /// </summary>
    public string? Command { get; init; }
}

/// <summary>
/// Agent context passed to GLM planner
/// </summary>
public class AgentContext
{
    /// <summary>
    /// Current coding task description
    /// </summary>
    public required string Task { get; init; }

    /// <summary>
    /// Current working directory
    /// </summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>
    /// Memory summary from previous iterations
    /// </summary>
    public string? MemorySummary { get; init; }

    /// <summary>
    /// Results from previous iteration's actions
    /// </summary>
    public List<ExecutionResult>? PreviousResults { get; init; }

    /// <summary>
    /// Current iteration number
    /// </summary>
    public int Iteration { get; init; }
}

/// <summary>
/// Coding task specification
/// </summary>
public class CodingTask
{
    /// <summary>
    /// Task description or instruction
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Working directory for the task
    /// </summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>
    /// Maximum iterations allowed (default: 5)
    /// </summary>
    public int MaxIterations { get; init; } = 5;

    /// <summary>
    /// Optional test command to verify success
    /// </summary>
    public string? TestCommand { get; init; }
}

/// <summary>
/// Final result of agent run
/// </summary>
public class AgentRunResult
{
    /// <summary>
    /// Whether the task completed successfully
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Number of iterations executed
    /// </summary>
    public required int Iterations { get; init; }

    /// <summary>
    /// Summary of what was done
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// Final memory summary
    /// </summary>
    public string? MemorySummary { get; init; }

    /// <summary>
    /// Error message if failed
    /// </summary>
    public string? Error { get; init; }
}
