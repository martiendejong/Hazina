namespace Hazina.LLMs.GoogleADK.A2A.Models;

/// <summary>
/// Describes an agent in the A2A network
/// </summary>
public class AgentDescriptor
{
    public string AgentId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public A2AAgentStatus Status { get; set; } = A2AAgentStatus.Unknown;
    public List<AgentCapability> Capabilities { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
    public string? Endpoint { get; set; }
}

/// <summary>
/// Agent availability status in the A2A network
/// </summary>
public enum A2AAgentStatus
{
    Unknown,
    Available,
    Busy,
    Offline,
    Error
}

/// <summary>
/// Result of agent discovery query
/// </summary>
public class AgentDiscoveryResult
{
    public List<AgentDescriptor> Agents { get; set; } = new();
    public int TotalCount { get; set; }
    public string? ContinuationToken { get; set; }
}
