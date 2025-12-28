using Hazina.LLMs.GoogleADK.A2A.Models;
using Hazina.LLMs.GoogleADK.A2A.Registry;
using Hazina.LLMs.GoogleADK.A2A.Transport;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.A2A;

/// <summary>
/// Client for interacting with other agents via A2A protocol
/// </summary>
public class A2AClient : IAsyncDisposable
{
    private readonly string _sourceAgentId;
    private readonly IA2ATransport _transport;
    private readonly IAgentDirectory _directory;
    private readonly ILogger? _logger;

    public A2AClient(
        string sourceAgentId,
        IA2ATransport transport,
        IAgentDirectory directory,
        ILogger? logger = null)
    {
        _sourceAgentId = sourceAgentId;
        _transport = transport;
        _directory = directory;
        _logger = logger;
    }

    /// <summary>
    /// Send a request to another agent
    /// </summary>
    public async Task<A2AResponse> SendRequestAsync(
        string targetAgentId,
        string requestType,
        object? payload = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        // Check if target agent is available
        var targetAgent = await _directory.GetAgentAsync(targetAgentId, cancellationToken);
        if (targetAgent == null)
        {
            throw new InvalidOperationException($"Target agent {targetAgentId} not found in directory");
        }

        if (targetAgent.Status != A2AAgentStatus.Available)
        {
            throw new InvalidOperationException($"Target agent {targetAgentId} is not available (status: {targetAgent.Status})");
        }

        var request = new A2ARequest
        {
            SourceAgentId = _sourceAgentId,
            TargetAgentId = targetAgentId,
            RequestType = requestType,
            Payload = payload,
            Timeout = timeout ?? TimeSpan.FromSeconds(30)
        };

        _logger?.LogDebug("Sending request {RequestId} to agent {TargetAgentId}", request.MessageId, targetAgentId);

        var response = await _transport.SendRequestAsync(request, cancellationToken);

        _logger?.LogDebug("Received response for request {RequestId}: Success={Success}",
            request.MessageId, response.Success);

        return response;
    }

    /// <summary>
    /// Send a notification to one or more agents
    /// </summary>
    public async Task SendNotificationAsync(
        List<string> targetAgentIds,
        string notificationType,
        object? payload = null,
        CancellationToken cancellationToken = default)
    {
        var notification = new A2ANotification
        {
            SourceAgentId = _sourceAgentId,
            TargetAgentIds = targetAgentIds,
            NotificationType = notificationType,
            Payload = payload
        };

        _logger?.LogDebug("Sending notification {NotificationId} to {Count} agents",
            notification.MessageId, targetAgentIds.Count);

        await _transport.SendNotificationAsync(notification, cancellationToken);
    }

    /// <summary>
    /// Discover agents by capability
    /// </summary>
    public async Task<List<AgentDescriptor>> DiscoverAgentsByCapabilityAsync(
        string capabilityName,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _directory.FindAgentsByCapabilityAsync(capabilityName, limit, cancellationToken);
        return result.Agents;
    }

    /// <summary>
    /// Discover agents by tags
    /// </summary>
    public async Task<List<AgentDescriptor>> DiscoverAgentsByTagsAsync(
        List<string> tags,
        bool matchAll = false,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _directory.FindAgentsByTagsAsync(tags, matchAll, limit, cancellationToken);
        return result.Agents;
    }

    /// <summary>
    /// Delegate a task to another agent by capability
    /// </summary>
    public async Task<A2AResponse> DelegateTaskAsync(
        string capabilityName,
        object? payload = null,
        CancellationToken cancellationToken = default)
    {
        // Find an agent with the required capability
        var agents = await DiscoverAgentsByCapabilityAsync(capabilityName, 1, cancellationToken);

        if (agents.Count == 0)
        {
            throw new InvalidOperationException($"No agent found with capability: {capabilityName}");
        }

        var targetAgent = agents[0];

        _logger?.LogInformation("Delegating task (capability: {Capability}) to agent {AgentId}",
            capabilityName, targetAgent.AgentId);

        return await SendRequestAsync(
            targetAgent.AgentId,
            "delegate_task",
            payload,
            cancellationToken: cancellationToken
        );
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
