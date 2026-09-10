using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hazina.AgenticOrchestration.Integration.EventBroker;
using Hazina.AgenticOrchestration.Integration.ServiceHub;
using Hazina.AgenticOrchestration.Services.Execution;

namespace Hazina.AgenticOrchestration.Integration.EventRouting;

/// <summary>
/// Event router - bridges EventBroker and ServiceHub
/// Routes events from EventBroker to appropriate agents via ServiceHub
/// </summary>
public class EventRouter
{
    private readonly EventBrokerAdapter _eventBroker;
    private readonly ServiceHubCoordinator _serviceHub;
    private readonly Dictionary<string, AgentType> _eventTypeMapping;

    public EventRouter(EventBrokerAdapter eventBroker, ServiceHubCoordinator serviceHub)
    {
        _eventBroker = eventBroker;
        _serviceHub = serviceHub;
        _eventTypeMapping = new Dictionary<string, AgentType>
        {
            ["clickup.task.new"] = AgentType.ClickUpTask,
            ["github.pr.new"] = AgentType.GitHubPR,
            ["github.pr.comment"] = AgentType.CodeReview,
            ["github.issue.new"] = AgentType.GitHubIssue
        };
    }

    /// <summary>
    /// Start event routing
    /// Subscribes to all event types and routes to agents
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("[EventRouter] Starting event routing...");

        // Subscribe to ClickUp task events
        await _eventBroker.SubscribeAsync<ClickUpTaskEvent>("clickup.task.new", async (evt) =>
        {
            await RouteToAgent(AgentType.ClickUpTask, "clickup.task.new", evt, cancellationToken);
        }, cancellationToken);

        // Subscribe to GitHub PR events
        await _eventBroker.SubscribeAsync<GitHubPREvent>("github.pr.new", async (evt) =>
        {
            await RouteToAgent(AgentType.GitHubPR, "github.pr.new", evt, cancellationToken);
        }, cancellationToken);

        // Subscribe to GitHub PR comment events (code review)
        await _eventBroker.SubscribeAsync<GitHubPRCommentEvent>("github.pr.comment", async (evt) =>
        {
            await RouteToAgent(AgentType.CodeReview, "github.pr.comment", evt, cancellationToken);
        }, cancellationToken);

        // Subscribe to GitHub issue events
        await _eventBroker.SubscribeAsync<GitHubIssueEvent>("github.issue.new", async (evt) =>
        {
            await RouteToAgent(AgentType.GitHubIssue, "github.issue.new", evt, cancellationToken);
        }, cancellationToken);

        // Start EventBroker processing
        await _eventBroker.StartProcessingAsync(cancellationToken);

        Console.WriteLine("[EventRouter] Event routing started");
    }

    /// <summary>
    /// Route event to appropriate agent
    /// Spawns new agent if capacity available, otherwise queues
    /// </summary>
    private async Task RouteToAgent(AgentType agentType, string eventType, object eventData, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[EventRouter] Routing {eventType} to {agentType} agent");

        // Check capacity
        var capacity = _serviceHub.GetAvailableCapacity();
        if (capacity == 0)
        {
            Console.WriteLine($"[EventRouter] No capacity available - event will be queued");
            // TODO: Queue for later processing
            return;
        }

        // Spawn agent
        var config = new AgentConfiguration
        {
            Name = $"{agentType}-{DateTime.UtcNow:HHmmss}",
            Parameters = new Dictionary<string, string>
            {
                ["eventType"] = eventType,
                ["eventData"] = System.Text.Json.JsonSerializer.Serialize(eventData)
            }
        };

        var agentId = await _serviceHub.SpawnAgentAsync(agentType, config, cancellationToken);

        // Assign work to agent
        await _serviceHub.AssignWorkAsync(agentId, eventType, eventData);

        Console.WriteLine($"[EventRouter] Spawned agent {agentId} for {eventType}");
    }

    /// <summary>
    /// Stop event routing
    /// </summary>
    public async Task StopAsync()
    {
        Console.WriteLine("[EventRouter] Stopping event routing...");

        await _eventBroker.StopProcessingAsync();
        await _serviceHub.ShutdownAsync();

        Console.WriteLine("[EventRouter] Event routing stopped");
    }
}

/// <summary>
/// ClickUp task event
/// </summary>
public class ClickUpTaskEvent
{
    public string TaskId { get; set; } = string.Empty;
    public string TaskName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Priority { get; set; }
}

/// <summary>
/// GitHub PR event
/// </summary>
public class GitHubPREvent
{
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
}

/// <summary>
/// GitHub PR comment event (triggers code review)
/// </summary>
public class GitHubPRCommentEvent
{
    public int PRNumber { get; set; }
    public string CommentBody { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// GitHub issue event
/// </summary>
public class GitHubIssueEvent
{
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
