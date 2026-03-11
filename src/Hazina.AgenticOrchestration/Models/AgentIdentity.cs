namespace Hazina.AgenticOrchestration.Models;

/// <summary>
/// Agent Curriculum Vitae - persistent identity across sessions
/// Inspired by Overstory's agent identity system
/// </summary>
public class AgentIdentity
{
    /// <summary>
    /// Unique agent name (e.g., "scout-auth-01")
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Agent capability/role (scout, builder, reviewer, lead, merger, coordinator, supervisor)
    /// </summary>
    public required string Capability { get; set; }

    /// <summary>
    /// When this agent identity was first created
    /// </summary>
    public DateTime Created { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Number of sessions this agent has completed
    /// </summary>
    public int SessionsCompleted { get; set; }

    /// <summary>
    /// Domains where this agent has expertise (learned from mulch/experience)
    /// </summary>
    public List<string> ExpertiseDomains { get; set; } = new();

    /// <summary>
    /// Recent tasks completed (last 10)
    /// </summary>
    public List<AgentTaskRecord> RecentTasks { get; set; } = new();

    /// <summary>
    /// Last session completion timestamp
    /// </summary>
    public DateTime? LastSession { get; set; }
}

/// <summary>
/// Record of a task completed by an agent
/// </summary>
public class AgentTaskRecord
{
    public required string TaskId { get; set; }
    public required string Description { get; set; }
    public DateTime CompletedAt { get; set; }
    public string? Outcome { get; set; } // success, failure, escalated
    public int DurationMinutes { get; set; }
}
