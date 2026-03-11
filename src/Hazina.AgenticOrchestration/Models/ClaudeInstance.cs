namespace Hazina.AgenticOrchestration.Models;

/// <summary>
/// Represents an active Claude Code CLI instance
/// </summary>
public class ClaudeInstance
{
    public string SessionId { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime LastHeartbeat { get; set; }
    public string Status { get; set; } = string.Empty; // active, idle, waiting, completed
    public string? CurrentTask { get; set; }
    public string? WorktreeSeat { get; set; }
    public int TasksCompleted { get; set; }
    public int TasksFailed { get; set; }

    // Computed properties
    public TimeSpan Runtime => DateTime.UtcNow - StartTime;
    public bool IsActive => (DateTime.UtcNow - LastHeartbeat).TotalMinutes < 1;
}
