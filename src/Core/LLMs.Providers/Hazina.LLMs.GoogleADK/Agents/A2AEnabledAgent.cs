using Hazina.LLMs.GoogleADK.A2A;
using Hazina.LLMs.GoogleADK.A2A.Models;
using Hazina.LLMs.GoogleADK.A2A.Registry;
using Hazina.LLMs.GoogleADK.A2A.Transport;
using Hazina.LLMs.GoogleADK.Core;
using Hazina.LLMs.GoogleADK.Events;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Agents;

/// <summary>
/// Agent capable of A2A (Agent-to-Agent) communication
/// </summary>
public class A2AEnabledAgent : LlmAgent
{
    private readonly IAgentDirectory _directory;
    private readonly IA2ATransport _transport;
    private readonly A2AClient _a2aClient;
    private readonly List<AgentCapability> _capabilities = new();
    private readonly Timer? _heartbeatTimer;

    public A2AEnabledAgent(
        string name,
        ILLMClient llmClient,
        IAgentDirectory directory,
        IA2ATransport transport,
        AgentContext? context = null)
        : base(name, llmClient, context)
    {
        _directory = directory;
        _transport = transport;
        _a2aClient = new A2AClient(AgentId, transport, directory, context?.Logger);

        // Start heartbeat timer (every 2 minutes)
        _heartbeatTimer = new Timer(
            SendHeartbeat,
            null,
            TimeSpan.FromMinutes(2),
            TimeSpan.FromMinutes(2)
        );
    }

    /// <summary>
    /// Add a capability to this agent
    /// </summary>
    public A2AEnabledAgent AddCapability(
        string name,
        string description,
        List<string>? tags = null)
    {
        _capabilities.Add(new AgentCapability
        {
            Name = name,
            Description = description,
            Tags = tags ?? new List<string>()
        });
        return this;
    }

    /// <summary>
    /// Initialize and register with directory
    /// </summary>
    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await base.InitializeAsync(cancellationToken);

        // Register with directory
        var descriptor = new AgentDescriptor
        {
            AgentId = AgentId,
            Name = Name,
            Description = SystemInstructions,
            Capabilities = _capabilities,
            Status = A2AAgentStatus.Available
        };

        await _directory.RegisterAgentAsync(descriptor, cancellationToken);
        Context.Logger?.LogInformation("Agent {AgentId} registered in A2A directory", AgentId);

        // Start transport
        await _transport.StartAsync(cancellationToken);

        // Register default request handlers
        RegisterDefaultHandlers();
    }

    /// <summary>
    /// Send a request to another agent
    /// </summary>
    public async Task<A2AResponse> SendRequestToAgentAsync(
        string targetAgentId,
        string requestType,
        object? payload = null,
        CancellationToken cancellationToken = default)
    {
        await _directory.UpdateAgentStatusAsync(AgentId, A2AAgentStatus.Busy, cancellationToken);

        try
        {
            return await _a2aClient.SendRequestAsync(
                targetAgentId,
                requestType,
                payload,
                cancellationToken: cancellationToken
            );
        }
        finally
        {
            await _directory.UpdateAgentStatusAsync(AgentId, A2AAgentStatus.Available, cancellationToken);
        }
    }

    /// <summary>
    /// Delegate a task to another agent by capability
    /// </summary>
    public async Task<A2AResponse> DelegateTaskAsync(
        string capabilityName,
        object? payload = null,
        CancellationToken cancellationToken = default)
    {
        await _directory.UpdateAgentStatusAsync(AgentId, A2AAgentStatus.Busy, cancellationToken);

        try
        {
            return await _a2aClient.DelegateTaskAsync(capabilityName, payload, cancellationToken);
        }
        finally
        {
            await _directory.UpdateAgentStatusAsync(AgentId, A2AAgentStatus.Available, cancellationToken);
        }
    }

    /// <summary>
    /// Send notification to other agents
    /// </summary>
    public async Task NotifyAgentsAsync(
        List<string> targetAgentIds,
        string notificationType,
        object? payload = null,
        CancellationToken cancellationToken = default)
    {
        await _a2aClient.SendNotificationAsync(targetAgentIds, notificationType, payload, cancellationToken);
    }

    /// <summary>
    /// Discover agents by capability
    /// </summary>
    public async Task<List<AgentDescriptor>> DiscoverAgentsAsync(
        string capabilityName,
        CancellationToken cancellationToken = default)
    {
        return await _a2aClient.DiscoverAgentsByCapabilityAsync(capabilityName, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Register a custom request handler
    /// </summary>
    public void RegisterRequestHandler(
        string requestType,
        Func<A2ARequest, Task<A2AResponse>> handler)
    {
        _transport.RegisterRequestHandler(requestType, handler);
    }

    /// <summary>
    /// Register a custom notification handler
    /// </summary>
    public void RegisterNotificationHandler(
        string notificationType,
        Func<A2ANotification, Task> handler)
    {
        _transport.RegisterNotificationHandler(notificationType, handler);
    }

    private void RegisterDefaultHandlers()
    {
        // Handle "delegate_task" requests
        _transport.RegisterRequestHandler("delegate_task", async request =>
        {
            try
            {
                // Execute the delegated task using the agent's execute method
                var input = request.Payload?.ToString() ?? string.Empty;
                var result = await ExecuteAsync(input);

                return new A2AResponse
                {
                    RequestId = request.MessageId,
                    SourceAgentId = AgentId,
                    TargetAgentId = request.SourceAgentId,
                    Success = true,
                    Result = result.Output
                };
            }
            catch (Exception ex)
            {
                return new A2AResponse
                {
                    RequestId = request.MessageId,
                    SourceAgentId = AgentId,
                    TargetAgentId = request.SourceAgentId,
                    Success = false,
                    Error = ex.Message
                };
            }
        });

        // Handle "ping" requests
        _transport.RegisterRequestHandler("ping", async request =>
        {
            return new A2AResponse
            {
                RequestId = request.MessageId,
                SourceAgentId = AgentId,
                TargetAgentId = request.SourceAgentId,
                Success = true,
                Result = "pong"
            };
        });
    }

    private void SendHeartbeat(object? state)
    {
        _ = _directory.HeartbeatAsync(AgentId);
    }

    public override async Task DisposeAsync()
    {
        if (_heartbeatTimer != null)
        {
            await _heartbeatTimer.DisposeAsync();
        }

        await _directory.UnregisterAgentAsync(AgentId);
        await _transport.StopAsync();
        await _a2aClient.DisposeAsync();

        await base.DisposeAsync();
    }
}
