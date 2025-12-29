using Hazina.LLMs.GoogleADK.Agents;
using Hazina.LLMs.GoogleADK.Core;
using Hazina.LLMs.GoogleADK.DeveloperUI;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Examples;

/// <summary>
/// Examples for Developer UI and Monitoring (Step 10)
/// </summary>
public class DeveloperUIExamples
{
    /// <summary>
    /// Example 1: Basic agent monitoring
    /// </summary>
    public static async Task BasicMonitoringExample(ILLMClient llmClient, ILogger logger)
    {
        var monitor = new AgentMonitor(logger: logger);

        // Create and register agents
        var agent1 = new LlmAgent("Assistant", llmClient);
        var agent2 = new LlmAgent("Analyzer", llmClient);

        await agent1.InitializeAsync();
        await agent2.InitializeAsync();

        monitor.RegisterAgent(agent1);
        monitor.RegisterAgent(agent2);

        Console.WriteLine("Registered Agents:");
        foreach (var agentInfo in monitor.GetAgents())
        {
            Console.WriteLine($"  - {agentInfo.Name} ({agentInfo.AgentId})");
            Console.WriteLine($"    Status: {agentInfo.Status}");
            Console.WriteLine($"    Events: {agentInfo.EventCount}");
        }

        // Execute agents
        await agent1.ExecuteAsync("Hello!");
        await agent2.ExecuteAsync("Analyze this");

        // Check statistics
        var stats = monitor.GetStatistics();
        Console.WriteLine($"\nStatistics:");
        Console.WriteLine($"  Total Agents: {stats.TotalAgents}");
        Console.WriteLine($"  Total Events: {stats.TotalEvents}");
        Console.WriteLine($"  Events by Type:");
        foreach (var kvp in stats.EventsByType)
        {
            Console.WriteLine($"    {kvp.Key}: {kvp.Value}");
        }

        await agent1.DisposeAsync();
        await agent2.DisposeAsync();
        await monitor.DisposeAsync();
    }

    /// <summary>
    /// Example 2: Agent debugging
    /// </summary>
    public static async Task DebuggingExample(ILLMClient llmClient, ILogger logger)
    {
        var controller = new AgentController(logger);

        var agent = new LlmAgent("DebugAgent", llmClient);
        await agent.InitializeAsync();

        controller.RegisterAgent(agent);

        // Execute with debugging
        var debugInfo = await controller.ExecuteWithDebuggingAsync(
            agent.AgentId,
            "Test input for debugging"
        );

        Console.WriteLine("Debug Information:");
        Console.WriteLine($"  Execution ID: {debugInfo.ExecutionId}");
        Console.WriteLine($"  Status: {debugInfo.Status}");
        Console.WriteLine($"  Duration: {debugInfo.Duration?.TotalMilliseconds:F2}ms");
        Console.WriteLine($"  Input: {debugInfo.Input}");
        Console.WriteLine($"  Output: {debugInfo.Output}");

        if (debugInfo.Errors.Any())
        {
            Console.WriteLine($"  Errors: {string.Join(", ", debugInfo.Errors)}");
        }

        await agent.DisposeAsync();
    }

    /// <summary>
    /// Example 3: Performance profiling
    /// </summary>
    public static async Task PerformanceProfilingExample(ILLMClient llmClient, ILogger logger)
    {
        var controller = new AgentController(logger);

        var agent = new LlmAgent("ProfiledAgent", llmClient);
        await agent.InitializeAsync();

        controller.RegisterAgent(agent);

        // Execute multiple times
        Console.WriteLine("Running performance tests...");
        for (int i = 0; i < 5; i++)
        {
            await controller.ExecuteWithDebuggingAsync(agent.AgentId, $"Test {i + 1}");
        }

        // Get performance profile
        var profile = controller.GetPerformanceProfile(agent.AgentId);

        if (profile != null)
        {
            Console.WriteLine("\nPerformance Profile:");
            Console.WriteLine($"  Total Executions: {profile.TotalExecutions}");
            Console.WriteLine($"  Average Time: {profile.AverageExecutionTime.TotalMilliseconds:F2}ms");
            Console.WriteLine($"  Min Time: {profile.MinExecutionTime.TotalMilliseconds:F2}ms");
            Console.WriteLine($"  Max Time: {profile.MaxExecutionTime.TotalMilliseconds:F2}ms");
            Console.WriteLine($"  Success Rate: {profile.SuccessRate:P}");
        }

        await agent.DisposeAsync();
    }

    /// <summary>
    /// Example 4: Agent control
    /// </summary>
    public static async Task AgentControlExample(ILLMClient llmClient, ILogger logger)
    {
        var controller = new AgentController(logger);

        var agent = new LlmAgent("ControlledAgent", llmClient);
        await agent.InitializeAsync();

        controller.RegisterAgent(agent);

        // Get agent state
        var state = controller.GetAgentState(agent.AgentId);
        Console.WriteLine($"Agent Status: {state?.Status}");

        // Update configuration
        controller.UpdateAgentConfiguration(agent.AgentId, new Dictionary<string, object>
        {
            ["maxRetries"] = 3,
            ["timeout"] = 30000
        });

        var config = controller.GetAgentConfiguration(agent.AgentId);
        Console.WriteLine("\nAgent Configuration:");
        foreach (var kvp in config!)
        {
            Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
        }

        // Pause and resume
        controller.PauseAgent(agent.AgentId);
        Console.WriteLine("\nAgent paused");

        controller.ResumeAgent(agent.AgentId);
        Console.WriteLine("Agent resumed");

        await agent.DisposeAsync();
    }

    /// <summary>
    /// Example 5: Execution history
    /// </summary>
    public static async Task ExecutionHistoryExample(ILLMClient llmClient, ILogger logger)
    {
        var controller = new AgentController(logger);

        var agent = new LlmAgent("HistoryAgent", llmClient);
        await agent.InitializeAsync();

        controller.RegisterAgent(agent);

        // Execute several times
        await controller.ExecuteWithDebuggingAsync(agent.AgentId, "First execution");
        await controller.ExecuteWithDebuggingAsync(agent.AgentId, "Second execution");
        await controller.ExecuteWithDebuggingAsync(agent.AgentId, "Third execution");

        // Get execution history
        var history = controller.GetExecutionHistory(agent.AgentId);

        Console.WriteLine("Execution History:");
        foreach (var execution in history)
        {
            Console.WriteLine($"\n  Execution: {execution.ExecutionId}");
            Console.WriteLine($"    Time: {execution.StartTime:HH:mm:ss}");
            Console.WriteLine($"    Duration: {execution.Duration?.TotalMilliseconds:F0}ms");
            Console.WriteLine($"    Status: {execution.Status}");
            Console.WriteLine($"    Input: {execution.Input}");
        }

        await agent.DisposeAsync();
    }

    /// <summary>
    /// Example 6: Event tracking
    /// </summary>
    public static async Task EventTrackingExample(ILLMClient llmClient, ILogger logger)
    {
        var monitor = new AgentMonitor(logger: logger);

        var agent = new LlmAgent("EventAgent", llmClient);
        await agent.InitializeAsync();

        monitor.RegisterAgent(agent);

        // Execute agent
        await agent.ExecuteAsync("Generate some events");

        // Get event history
        var events = monitor.GetAgentEvents(agent.AgentId);

        Console.WriteLine($"Events for {agent.Name}:");
        foreach (var evt in events)
        {
            Console.WriteLine($"  [{evt.Timestamp:HH:mm:ss}] {evt.EventType}");
        }

        await agent.DisposeAsync();
        await monitor.DisposeAsync();
    }
}
