namespace Hazina.TaskRunner.Scheduling;

/// <summary>
/// Represents a named set of scheduled tasks (e.g., "Orchestration", "Maintenance")
/// </summary>
public class TaskSet
{
    /// <summary>
    /// Unique identifier for this task set
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name for this task set
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of this task set's purpose
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether this entire task set is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Tasks in this set
    /// </summary>
    public List<ScheduledTask> Tasks { get; set; } = new();

    /// <summary>
    /// Optional color for UI display (hex format, e.g., "#FF5733")
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Optional icon identifier for UI display
    /// </summary>
    public string? Icon { get; set; }
}
