using System.Collections.Concurrent;
using Hazina.LLMs.GoogleADK.A2A.Models;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.A2A.Registry;

/// <summary>
/// In-memory implementation of agent directory
/// </summary>
public class InMemoryAgentDirectory : IAgentDirectory, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, AgentDescriptor> _agents = new();
    private readonly ILogger? _logger;
    private readonly Timer? _cleanupTimer;
    private readonly TimeSpan _heartbeatTimeout = TimeSpan.FromMinutes(5);

    public InMemoryAgentDirectory(ILogger? logger = null)
    {
        _logger = logger;

        // Cleanup stale agents every minute
        _cleanupTimer = new Timer(
            CleanupStaleAgents,
            null,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1)
        );
    }

    public Task<AgentDescriptor> RegisterAgentAsync(
        AgentDescriptor agent,
        CancellationToken cancellationToken = default)
    {
        agent.RegisteredAt = DateTime.UtcNow;
        agent.LastHeartbeat = DateTime.UtcNow;
        agent.Status = A2AAgentStatus.Available;

        _agents[agent.AgentId] = agent;
        _logger?.LogInformation("Registered agent: {AgentId} ({Name})", agent.AgentId, agent.Name);

        return Task.FromResult(agent);
    }

    public Task<bool> UnregisterAgentAsync(
        string agentId,
        CancellationToken cancellationToken = default)
    {
        var removed = _agents.TryRemove(agentId, out _);
        if (removed)
        {
            _logger?.LogInformation("Unregistered agent: {AgentId}", agentId);
        }
        return Task.FromResult(removed);
    }

    public Task<AgentDescriptor?> GetAgentAsync(
        string agentId,
        CancellationToken cancellationToken = default)
    {
        _agents.TryGetValue(agentId, out var agent);
        return Task.FromResult(agent);
    }

    public Task<AgentDiscoveryResult> FindAgentsByCapabilityAsync(
        string capabilityName,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var matches = _agents.Values
            .Where(a => a.Status == A2AAgentStatus.Available)
            .Where(a => a.Capabilities.Any(c =>
                c.Name.Equals(capabilityName, StringComparison.OrdinalIgnoreCase)))
            .Take(limit)
            .ToList();

        return Task.FromResult(new AgentDiscoveryResult
        {
            Agents = matches,
            TotalCount = matches.Count
        });
    }

    public Task<AgentDiscoveryResult> FindAgentsByTagsAsync(
        List<string> tags,
        bool matchAll = false,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var matches = _agents.Values
            .Where(a => a.Status == A2AAgentStatus.Available)
            .Where(a =>
            {
                var agentTags = a.Capabilities.SelectMany(c => c.Tags).Distinct().ToList();
                return matchAll
                    ? tags.All(t => agentTags.Contains(t, StringComparer.OrdinalIgnoreCase))
                    : tags.Any(t => agentTags.Contains(t, StringComparer.OrdinalIgnoreCase));
            })
            .Take(limit)
            .ToList();

        return Task.FromResult(new AgentDiscoveryResult
        {
            Agents = matches,
            TotalCount = matches.Count
        });
    }

    public Task<AgentDiscoveryResult> GetAvailableAgentsAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var available = _agents.Values
            .Where(a => a.Status == A2AAgentStatus.Available)
            .Take(limit)
            .ToList();

        return Task.FromResult(new AgentDiscoveryResult
        {
            Agents = available,
            TotalCount = available.Count
        });
    }

    public Task<bool> UpdateAgentStatusAsync(
        string agentId,
        A2AAgentStatus status,
        CancellationToken cancellationToken = default)
    {
        if (_agents.TryGetValue(agentId, out var agent))
        {
            agent.Status = status;
            _logger?.LogDebug("Updated agent {AgentId} status to {Status}", agentId, status);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<bool> HeartbeatAsync(
        string agentId,
        CancellationToken cancellationToken = default)
    {
        if (_agents.TryGetValue(agentId, out var agent))
        {
            agent.LastHeartbeat = DateTime.UtcNow;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    private void CleanupStaleAgents(object? state)
    {
        var threshold = DateTime.UtcNow - _heartbeatTimeout;
        var staleAgents = _agents.Values
            .Where(a => a.LastHeartbeat < threshold)
            .Select(a => a.AgentId)
            .ToList();

        foreach (var agentId in staleAgents)
        {
            if (_agents.TryRemove(agentId, out var agent))
            {
                _logger?.LogWarning("Removed stale agent: {AgentId} (last heartbeat: {LastHeartbeat})",
                    agentId, agent.LastHeartbeat);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_cleanupTimer != null)
        {
            await _cleanupTimer.DisposeAsync();
        }
        _agents.Clear();
    }
}
