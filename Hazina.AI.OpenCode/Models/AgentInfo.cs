namespace Hazina.AI.OpenCode.Models;

/// <summary>
/// Information about an OpenCode agent
/// </summary>
public class AgentInfo
{
    /// <summary>
    /// Agent name
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Agent description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Port the agent is listening on
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Base URL for the agent
    /// </summary>
    public required string BaseUrl { get; set; }

    /// <summary>
    /// Whether the agent is currently responding
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Projects this agent has access to
    /// </summary>
    public List<string> AllowedProjects { get; set; } = new();

    /// <summary>
    /// Last health check time
    /// </summary>
    public DateTime? LastHealthCheck { get; set; }
}
