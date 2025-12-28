using Hazina.LLMs.GoogleADK.A2A.Models;

namespace Hazina.LLMs.GoogleADK.A2A.Registry;

/// <summary>
/// Directory for discovering and managing agents in the A2A network
/// </summary>
public interface IAgentDirectory
{
    /// <summary>
    /// Register an agent in the directory
    /// </summary>
    Task<AgentDescriptor> RegisterAgentAsync(
        AgentDescriptor agent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unregister an agent from the directory
    /// </summary>
    Task<bool> UnregisterAgentAsync(
        string agentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get agent by ID
    /// </summary>
    Task<AgentDescriptor?> GetAgentAsync(
        string agentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find agents by capability
    /// </summary>
    Task<AgentDiscoveryResult> FindAgentsByCapabilityAsync(
        string capabilityName,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find agents by tags
    /// </summary>
    Task<AgentDiscoveryResult> FindAgentsByTagsAsync(
        List<string> tags,
        bool matchAll = false,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all available agents
    /// </summary>
    Task<AgentDiscoveryResult> GetAvailableAgentsAsync(
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update agent status
    /// </summary>
    Task<bool> UpdateAgentStatusAsync(
        string agentId,
        A2AAgentStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send heartbeat to keep agent alive
    /// </summary>
    Task<bool> HeartbeatAsync(
        string agentId,
        CancellationToken cancellationToken = default);
}
