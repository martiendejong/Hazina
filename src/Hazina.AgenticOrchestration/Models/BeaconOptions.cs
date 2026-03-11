namespace Hazina.AgenticOrchestration.Models;

/// <summary>
/// Options for building a structured startup beacon prompt
/// Inspired by Overstory's buildBeacon() function
/// </summary>
public class BeaconOptions
{
    public required string AgentName { get; set; }
    public required string Capability { get; set; }
    public required string TaskId { get; set; }
    public string? ParentAgent { get; set; }
    public int Depth { get; set; }

    /// <summary>
    /// Optional custom startup protocol (overrides default)
    /// </summary>
    public string? CustomProtocol { get; set; }
}

/// <summary>
/// Result of beacon injection
/// </summary>
public class BeaconResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
