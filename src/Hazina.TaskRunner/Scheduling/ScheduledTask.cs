namespace Hazina.TaskRunner.Scheduling;

/// <summary>
/// Represents a scheduled PowerShell script task
/// </summary>
public class ScheduledTask
{
    /// <summary>
    /// Unique identifier for the task
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name for the task
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Full path to the PowerShell script to execute
    /// </summary>
    public string ScriptPath { get; set; } = string.Empty;

    /// <summary>
    /// Cron expression for scheduling (e.g., "0 6 * * *" for daily at 6am)
    /// </summary>
    public string CronExpression { get; set; } = string.Empty;

    /// <summary>
    /// Whether this task is currently enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether to run with elevated (administrator) privileges
    /// </summary>
    public bool RunElevated { get; set; } = false;

    /// <summary>
    /// Maximum execution time in seconds before timeout (default: 300 = 5 minutes)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Parameters to pass to the script
    /// </summary>
    public Dictionary<string, object>? Parameters { get; set; }

    /// <summary>
    /// Timestamp of last execution (UTC)
    /// </summary>
    public DateTime? LastRun { get; set; }

    /// <summary>
    /// Timestamp of next scheduled execution (UTC)
    /// </summary>
    public DateTime? NextRun { get; set; }

    /// <summary>
    /// Working directory for script execution
    /// </summary>
    public string? WorkingDirectory { get; set; }
}
