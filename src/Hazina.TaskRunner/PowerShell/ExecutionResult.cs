namespace Hazina.TaskRunner.PowerShell;

/// <summary>
/// Result of PowerShell script execution
/// </summary>
public class ExecutionResult
{
    /// <summary>
    /// Whether the script executed successfully without errors
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Combined output from all streams (Output, Verbose, Debug, Information)
    /// </summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>
    /// Error messages if execution failed
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Warning messages from the script
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Execution duration
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Exit code (0 = success, non-zero = error)
    /// </summary>
    public int ExitCode { get; set; }

    /// <summary>
    /// Script path that was executed
    /// </summary>
    public string ScriptPath { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when execution started
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Timestamp when execution completed
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Whether execution was terminated due to timeout
    /// </summary>
    public bool TimedOut { get; set; }

    /// <summary>
    /// Detailed exception information if an error occurred
    /// </summary>
    public string? ExceptionDetails { get; set; }
}
