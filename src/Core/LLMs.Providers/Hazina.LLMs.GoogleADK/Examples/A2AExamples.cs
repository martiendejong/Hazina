using Hazina.LLMs.GoogleADK.A2A.Models;
using Hazina.LLMs.GoogleADK.A2A.Registry;
using Hazina.LLMs.GoogleADK.A2A.Transport;
using Hazina.LLMs.GoogleADK.Agents;
using Hazina.LLMs.GoogleADK.Core;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Examples;

/// <summary>
/// Examples for A2A (Agent-to-Agent) Protocol (Step 7)
/// </summary>
public class A2AExamples
{
    /// <summary>
    /// Example 1: Basic agent registration and discovery
    /// </summary>
    public static async Task BasicRegistrationExample(ILLMClient llmClient, ILogger logger)
    {
        var directory = new InMemoryAgentDirectory(logger);
        var transport = new InProcessA2ATransport(logger);
        await transport.StartAsync();

        // Create and register agents
        var summarizer = new A2AEnabledAgent("Summarizer", llmClient, directory, transport)
            .AddCapability("summarize", "Summarizes text", new List<string> { "nlp", "text" });

        var translator = new A2AEnabledAgent("Translator", llmClient, directory, transport)
            .AddCapability("translate", "Translates text", new List<string> { "nlp", "language" });

        await summarizer.InitializeAsync();
        await translator.InitializeAsync();

        Console.WriteLine($"Registered agents: {summarizer.AgentId}, {translator.AgentId}");

        // Discover agents by capability
        var nlpAgents = await summarizer.DiscoverAgentsAsync("summarize");
        Console.WriteLine($"Found {nlpAgents.Count} agents with 'summarize' capability");

        foreach (var agent in nlpAgents)
        {
            Console.WriteLine($"  - {agent.Name} ({agent.AgentId})");
        }

        await summarizer.DisposeAsync();
        await translator.DisposeAsync();
        await transport.StopAsync();
        await directory.DisposeAsync();
    }

    /// <summary>
    /// Example 2: Agent-to-agent communication
    /// </summary>
    public static async Task AgentCommunicationExample(ILLMClient llmClient, ILogger logger)
    {
        var directory = new InMemoryAgentDirectory(logger);
        var transport = new InProcessA2ATransport(logger);
        await transport.StartAsync();

        // Create agents
        var coordinator = new A2AEnabledAgent("Coordinator", llmClient, directory, transport)
            .AddCapability("coordinate", "Coordinates tasks");

        var worker = new A2AEnabledAgent("Worker", llmClient, directory, transport)
            .AddCapability("process", "Processes data");

        await coordinator.InitializeAsync();
        await worker.InitializeAsync();

        // Coordinator sends request to worker
        var response = await coordinator.SendRequestToAgentAsync(
            worker.AgentId,
            "delegate_task",
            payload: "Process this data"
        );

        Console.WriteLine($"Request successful: {response.Success}");
        Console.WriteLine($"Response: {response.Result}");

        await coordinator.DisposeAsync();
        await worker.DisposeAsync();
        await transport.StopAsync();
        await directory.DisposeAsync();
    }

    /// <summary>
    /// Example 3: Task delegation by capability
    /// </summary>
    public static async Task TaskDelegationExample(ILLMClient llmClient, ILogger logger)
    {
        var directory = new InMemoryAgentDirectory(logger);
        var transport = new InProcessA2ATransport(logger);
        await transport.StartAsync();

        // Create specialized agents
        var manager = new A2AEnabledAgent("Manager", llmClient, directory, transport);

        var analyst = new A2AEnabledAgent("Analyst", llmClient, directory, transport)
            .AddCapability("analyze", "Analyzes data", new List<string> { "analytics" });

        var reporter = new A2AEnabledAgent("Reporter", llmClient, directory, transport)
            .AddCapability("report", "Generates reports", new List<string> { "reporting" });

        await manager.InitializeAsync();
        await analyst.InitializeAsync();
        await reporter.InitializeAsync();

        // Manager delegates analysis task by capability
        Console.WriteLine("Manager delegating analysis task...");
        var analysisResult = await manager.DelegateTaskAsync("analyze", "Sales data for Q1 2024");
        Console.WriteLine($"Analysis completed: {analysisResult.Success}");

        // Manager delegates reporting task
        Console.WriteLine("Manager delegating reporting task...");
        var reportResult = await manager.DelegateTaskAsync("report", "Generate quarterly report");
        Console.WriteLine($"Report completed: {reportResult.Success}");

        await manager.DisposeAsync();
        await analyst.DisposeAsync();
        await reporter.DisposeAsync();
        await transport.StopAsync();
        await directory.DisposeAsync();
    }

    /// <summary>
    /// Example 4: Agent notifications
    /// </summary>
    public static async Task NotificationExample(ILLMClient llmClient, ILogger logger)
    {
        var directory = new InMemoryAgentDirectory(logger);
        var transport = new InProcessA2ATransport(logger);
        await transport.StartAsync();

        // Create agents
        var broadcaster = new A2AEnabledAgent("Broadcaster", llmClient, directory, transport);
        var listener1 = new A2AEnabledAgent("Listener1", llmClient, directory, transport);
        var listener2 = new A2AEnabledAgent("Listener2", llmClient, directory, transport);

        await broadcaster.InitializeAsync();
        await listener1.InitializeAsync();
        await listener2.InitializeAsync();

        // Register notification handlers
        int listener1Notifications = 0;
        int listener2Notifications = 0;

        listener1.RegisterNotificationHandler("update", async notification =>
        {
            listener1Notifications++;
            Console.WriteLine($"Listener1 received notification: {notification.Payload}");
            await Task.CompletedTask;
        });

        listener2.RegisterNotificationHandler("update", async notification =>
        {
            listener2Notifications++;
            Console.WriteLine($"Listener2 received notification: {notification.Payload}");
            await Task.CompletedTask;
        });

        // Broadcaster sends notification to multiple agents
        await broadcaster.NotifyAgentsAsync(
            new List<string> { listener1.AgentId, listener2.AgentId },
            "update",
            "System update available"
        );

        await Task.Delay(500); // Give time for notifications to process

        Console.WriteLine($"Listener1 received {listener1Notifications} notifications");
        Console.WriteLine($"Listener2 received {listener2Notifications} notifications");

        await broadcaster.DisposeAsync();
        await listener1.DisposeAsync();
        await listener2.DisposeAsync();
        await transport.StopAsync();
        await directory.DisposeAsync();
    }

    /// <summary>
    /// Example 5: Custom request handlers
    /// </summary>
    public static async Task CustomHandlerExample(ILLMClient llmClient, ILogger logger)
    {
        var directory = new InMemoryAgentDirectory(logger);
        var transport = new InProcessA2ATransport(logger);
        await transport.StartAsync();

        var serviceAgent = new A2AEnabledAgent("ServiceAgent", llmClient, directory, transport);
        var clientAgent = new A2AEnabledAgent("ClientAgent", llmClient, directory, transport);

        await serviceAgent.InitializeAsync();
        await clientAgent.InitializeAsync();

        // Register custom handler for "calculate" requests
        serviceAgent.RegisterRequestHandler("calculate", async request =>
        {
            var input = request.Payload?.ToString() ?? "0";
            var numbers = input.Split(',').Select(int.Parse).ToArray();
            var sum = numbers.Sum();

            return new A2AResponse
            {
                RequestId = request.MessageId,
                SourceAgentId = serviceAgent.AgentId,
                TargetAgentId = request.SourceAgentId,
                Success = true,
                Result = sum
            };
        });

        // Client sends calculation request
        var response = await clientAgent.SendRequestToAgentAsync(
            serviceAgent.AgentId,
            "calculate",
            "10,20,30,40"
        );

        Console.WriteLine($"Calculation result: {response.Result}");

        await serviceAgent.DisposeAsync();
        await clientAgent.DisposeAsync();
        await transport.StopAsync();
        await directory.DisposeAsync();
    }

    /// <summary>
    /// Example 6: Multi-agent workflow
    /// </summary>
    public static async Task MultiAgentWorkflowExample(ILLMClient llmClient, ILogger logger)
    {
        var directory = new InMemoryAgentDirectory(logger);
        var transport = new InProcessA2ATransport(logger);
        await transport.StartAsync();

        // Create workflow agents
        var orchestrator = new A2AEnabledAgent("Orchestrator", llmClient, directory, transport);

        var dataCollector = new A2AEnabledAgent("DataCollector", llmClient, directory, transport)
            .AddCapability("collect", "Collects data");

        var dataProcessor = new A2AEnabledAgent("DataProcessor", llmClient, directory, transport)
            .AddCapability("process", "Processes data");

        var dataVisualizer = new A2AEnabledAgent("DataVisualizer", llmClient, directory, transport)
            .AddCapability("visualize", "Visualizes data");

        await orchestrator.InitializeAsync();
        await dataCollector.InitializeAsync();
        await dataProcessor.InitializeAsync();
        await dataVisualizer.InitializeAsync();

        Console.WriteLine("Starting multi-agent workflow...");

        // Step 1: Collect data
        Console.WriteLine("Step 1: Collecting data...");
        var collectResult = await orchestrator.DelegateTaskAsync("collect", "user_metrics");
        Console.WriteLine($"  Data collected: {collectResult.Success}");

        // Step 2: Process data
        Console.WriteLine("Step 2: Processing data...");
        var processResult = await orchestrator.DelegateTaskAsync("process", collectResult.Result);
        Console.WriteLine($"  Data processed: {processResult.Success}");

        // Step 3: Visualize data
        Console.WriteLine("Step 3: Visualizing data...");
        var visualizeResult = await orchestrator.DelegateTaskAsync("visualize", processResult.Result);
        Console.WriteLine($"  Data visualized: {visualizeResult.Success}");

        Console.WriteLine("Workflow completed successfully!");

        await orchestrator.DisposeAsync();
        await dataCollector.DisposeAsync();
        await dataProcessor.DisposeAsync();
        await dataVisualizer.DisposeAsync();
        await transport.StopAsync();
        await directory.DisposeAsync();
    }
}
