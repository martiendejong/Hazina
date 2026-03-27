namespace Hazina.TaskRunner.PowerShell;

/// <summary>
/// Configuration options for PowerShell script execution
/// </summary>
public class ExecutionOptions
{
    /// <summary>
    /// Execute without creating visible console windows (default: true)
    /// </summary>
    public bool Silent { get; set; } = true;

    /// <summary>
    /// Run with administrator privileges (default: false)
    /// </summary>
    public bool Elevated { get; set; } = false;

    /// <summary>
    /// Maximum execution time before termination (default: 5 minutes)
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Working directory for script execution (default: script's directory)
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Parameters to pass to the PowerShell script
    /// </summary>
    public Dictionary<string, object>? Parameters { get; set; }

    /// <summary>
    /// Log execution details to file (default: true)
    /// </summary>
    public bool LogExecution { get; set; } = true;

    /// <summary>
    /// Path to log file. Defaults to task-execution-log.jsonl relative to the app base directory.
    /// Override via configuration or environment variable HAZINA_TASK_LOG_PATH.
    /// </summary>
    public string LogFilePath { get; set; } =
        Environment.GetEnvironmentVariable("HAZINA_TASK_LOG_PATH")
        ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "task-execution-log.jsonl");
}
