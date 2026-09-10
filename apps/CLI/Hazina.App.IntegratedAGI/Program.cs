using System;
using System.Threading;
using System.Threading.Tasks;
using Hazina.AgenticOrchestration.Integration.EventBroker;
using Hazina.AgenticOrchestration.Integration.EventRouting;
using Hazina.AgenticOrchestration.Integration.ServiceHub;
using Hazina.AgenticOrchestration.Integration.StateSync;
using Spectre.Console;

namespace Hazina.App.IntegratedAGI;

/// <summary>
/// Integrated AGI System - Phases 1+2+3 combined
/// Event-driven autonomous execution with multi-agent coordination
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        AnsiConsole.Write(new FigletText("Jengo AGI").Color(Color.Cyan));
        AnsiConsole.MarkupLine("[cyan]Integrated Autonomous System (Phases 1-3)[/]");
        AnsiConsole.WriteLine();

        // Initialize Phase 3 components
        var eventBroker = new InMemoryEventBroker(); // Simple in-memory implementation
        var eventBrokerAdapter = new EventBrokerAdapter(eventBroker);
        var serviceHub = new ServiceHubCoordinator(maxConcurrentAgents: 3);
        var stateSynchronizer = new AgentStateSynchronizer();
        var eventRouter = new EventRouter(eventBrokerAdapter, serviceHub);

        using var cts = new CancellationTokenSource();

        // Handle Ctrl+C
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            AnsiConsole.MarkupLine("[yellow]Stopping integrated AGI system...[/]");
            cts.Cancel();
        };

        // Start event routing
        _ = Task.Run(async () =>
        {
            await eventRouter.StartAsync(cts.Token);
        }, cts.Token);

        // Start status dashboard
        _ = Task.Run(() => DisplayDashboard(serviceHub, stateSynchronizer, cts.Token), cts.Token);

        AnsiConsole.MarkupLine("[green]✅ Integrated AGI system running[/]");
        AnsiConsole.MarkupLine("[dim]Components:[/]");
        AnsiConsole.MarkupLine("[dim]  • EventBroker: Event-driven messaging[/]");
        AnsiConsole.MarkupLine("[dim]  • ServiceHub: Multi-agent coordination (max 3 concurrent)[/]");
        AnsiConsole.MarkupLine("[dim]  • StateSynchronizer: Work locking & state sharing[/]");
        AnsiConsole.MarkupLine("[dim]  • EventRouter: Routes events to agents[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Press Ctrl+C to stop[/]");
        AnsiConsole.WriteLine();

        // Simulate events for demo
        _ = Task.Run(async () => await SimulateEvents(eventBrokerAdapter, cts.Token), cts.Token);

        // Wait for cancellation
        await Task.Delay(Timeout.Infinite, cts.Token);
    }

    /// <summary>
    /// Display real-time dashboard
    /// </summary>
    static async Task DisplayDashboard(ServiceHubCoordinator serviceHub, AgentStateSynchronizer stateSync, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

                var agents = serviceHub.GetAllAgents();
                var syncStats = stateSync.GetStatistics();
                var capacity = serviceHub.GetAvailableCapacity();

                AnsiConsole.MarkupLine($"[dim]━━━ Status Update [{DateTime.Now:HH:mm:ss}] ━━━[/]");
                AnsiConsole.MarkupLine($"[dim]Agents: {agents.Count} active | Capacity: {capacity}/3 available[/]");
                AnsiConsole.MarkupLine($"[dim]Sync: {syncStats.ActiveAgents} active | {syncStats.ActiveLocks} locks[/]");

                foreach (var agent in agents)
                {
                    var statusColor = agent.State switch
                    {
                        ServiceHub.AgentState.Running => "green",
                        ServiceHub.AgentState.Completed => "blue",
                        ServiceHub.AgentState.Failed => "red",
                        _ => "yellow"
                    };

                    var workInfo = agent.CurrentWork != null ? $"Working on: {agent.CurrentWork.WorkItemId}" : "Idle";
                    AnsiConsole.MarkupLine($"[dim]  • [{statusColor}]{agent.AgentId}[/] ({agent.Type}) - {workInfo}[/]");
                }

                AnsiConsole.WriteLine();
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Simulate events for demo
    /// In production, these would come from ClickUp/GitHub monitors
    /// </summary>
    static async Task SimulateEvents(EventBrokerAdapter eventBroker, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);

        AnsiConsole.MarkupLine("[cyan]📨 Simulating ClickUp task event...[/]");
        await eventBroker.PublishAsync("clickup.task.new", new ClickUpTaskEvent
        {
            TaskId = "abc123",
            TaskName = "Implement user authentication",
            Description = "Add JWT authentication to API",
            Priority = 2
        }, cancellationToken);

        await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);

        AnsiConsole.MarkupLine("[cyan]📨 Simulating GitHub PR event...[/]");
        await eventBroker.PublishAsync("github.pr.new", new GitHubPREvent
        {
            Number = 42,
            Title = "Add logging middleware",
            Url = "https://github.com/user/repo/pull/42",
            Author = "developer"
        }, cancellationToken);

        await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken);

        AnsiConsole.MarkupLine("[cyan]📨 Simulating GitHub issue event...[/]");
        await eventBroker.PublishAsync("github.issue.new", new GitHubIssueEvent
        {
            Number = 15,
            Title = "Bug: Login fails on mobile",
            Body = "Steps to reproduce...",
            Url = "https://github.com/user/repo/issues/15"
        }, cancellationToken);
    }
}

/// <summary>
/// Simple in-memory EventBroker for demo
/// In production, this would be replaced with real DataDrivenAI EventBroker
/// </summary>
public class InMemoryEventBroker : IEventBroker
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Func<object, Task>> _handlers = new();

    public Task PublishAsync<T>(string eventType, T data, CancellationToken cancellationToken = default)
    {
        if (_handlers.TryGetValue(eventType, out var handler))
        {
            return handler(data!);
        }
        return Task.CompletedTask;
    }

    public Task SubscribeAsync(string eventType, Func<object, Task> handler, CancellationToken cancellationToken = default)
    {
        _handlers[eventType] = handler;
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }
}
