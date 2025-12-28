using Hazina.LLMs.GoogleADK.Agents;
using Hazina.LLMs.GoogleADK.Core;
using Hazina.LLMs.GoogleADK.Events;
using Hazina.LLMs.GoogleADK.Memory;
using Hazina.LLMs.GoogleADK.Memory.Models;
using Hazina.LLMs.GoogleADK.Memory.Storage;
using Hazina.LLMs.GoogleADK.Sessions;
using Hazina.LLMs.GoogleADK.Sessions.Storage;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Examples;

/// <summary>
/// Examples for Memory Bank and Enhanced Events (Steps 5 & 6)
/// </summary>
public class MemoryAndEventsExamples
{
    /// <summary>
    /// Example 1: Basic memory usage
    /// </summary>
    public static async Task BasicMemoryExample(ILLMClient llmClient, ILogger logger)
    {
        // Create memory bank
        var memoryStorage = new InMemoryMemoryStorage(logger);
        var memoryBank = new MemoryBank(memoryStorage, logger);

        // Store different types of memories
        await memoryBank.StoreMemoryAsync(
            content: "User prefers dark mode",
            type: MemoryType.Semantic,
            importance: 0.8,
            tags: new List<string> { "preferences", "ui" }
        );

        await memoryBank.StoreMemoryAsync(
            content: "User asked about Python on 2024-01-15",
            type: MemoryType.Episodic,
            importance: 0.5,
            tags: new List<string> { "conversation", "python" }
        );

        await memoryBank.StoreMemoryAsync(
            content: "To create a git commit: use 'git commit -m message'",
            type: MemoryType.Procedural,
            importance: 0.7,
            tags: new List<string> { "git", "how-to" }
        );

        // Search memories
        var results = await memoryBank.SearchByTextAsync("git", limit: 5);
        Console.WriteLine($"Found {results.Count} memories about git");

        foreach (var result in results)
        {
            Console.WriteLine($"- [{result.Memory.Type}] {result.Memory.Content}");
        }

        await memoryBank.DisposeAsync();
    }

    /// <summary>
    /// Example 2: Memory-enabled agent
    /// </summary>
    public static async Task MemoryEnabledAgentExample(ILLMClient llmClient, ILogger logger)
    {
        // Setup
        var memoryStorage = new InMemoryMemoryStorage(logger);
        var memoryBank = new MemoryBank(memoryStorage, logger);
        var sessionStorage = new InMemorySessionStorage(logger);
        var sessionManager = new SessionManager(sessionStorage, logger);

        // Create memory-enabled agent
        var agent = new MemoryEnabledAgent(
            name: "SmartAssistant",
            llmClient: llmClient,
            sessionManager: sessionManager,
            memoryBank: memoryBank,
            memoryConfig: new MemoryConfiguration
            {
                MaxRetrievedMemories = 5,
                StoreUserInputs = true,
                StoreAgentResponses = false
            },
            context: new AgentContext(new AgentState(), new EventBus(), logger)
        );

        await agent.InitializeAsync();

        // Store some knowledge
        await agent.StoreSemanticMemoryAsync(
            content: "The capital of France is Paris",
            importance: 0.9,
            tags: new List<string> { "geography", "facts" }
        );

        // First interaction - will store in memory
        var result1 = await agent.ExecuteWithMemoryAsync("What's the capital of France?");
        Console.WriteLine($"Response 1: {result1.Output}");

        // Later interaction - will retrieve from memory
        var result2 = await agent.ExecuteWithMemoryAsync("Tell me about France's capital");
        Console.WriteLine($"Response 2: {result2.Output}");

        // Search agent's memories
        var memories = await agent.SearchMemoriesAsync("France", limit: 10);
        Console.WriteLine($"\nAgent has {memories.Count} memories about France");

        await agent.CompleteSessionAsync();
        await agent.DisposeAsync();
        await sessionManager.DisposeAsync();
        await memoryBank.DisposeAsync();
    }

    /// <summary>
    /// Example 3: Streaming events
    /// </summary>
    public static async Task StreamingEventsExample(ILLMClient llmClient, ILogger logger)
    {
        var eventBus = new StreamingEventBus(logger);

        // Create streaming subscription
        var subscription = eventBus.CreateStream<AgentEvent>(
            subscriptionId: "all-events",
            filter: null,
            bufferSize: 100
        );

        Console.WriteLine($"Created subscription: {subscription.SubscriptionId}");

        // Start consuming events in background
        var cts = new CancellationTokenSource();
        var consumeTask = Task.Run(async () =>
        {
            await foreach (var evt in eventBus.GetEventStream("all-events", cts.Token))
            {
                Console.WriteLine($"[{evt.Timestamp:HH:mm:ss}] {evt.EventType} - Agent: {evt.AgentId}");
            }
        });

        // Create agent with streaming event bus
        var memoryStorage = new InMemoryMemoryStorage();
        var memoryBank = new MemoryBank(memoryStorage);
        var sessionStorage = new InMemorySessionStorage();
        var sessionManager = new SessionManager(sessionStorage);

        var agent = new MemoryEnabledAgent(
            name: "StreamingAgent",
            llmClient: llmClient,
            sessionManager: sessionManager,
            memoryBank: memoryBank,
            context: new AgentContext(new AgentState(), eventBus, logger)
        );

        await agent.InitializeAsync();

        // Execute - events will be streamed
        await agent.ExecuteWithMemoryAsync("Hello, how are you?");

        // Give time for events to process
        await Task.Delay(500);

        // Stop consuming
        cts.Cancel();
        eventBus.CloseStream("all-events");

        await agent.DisposeAsync();
        await sessionManager.DisposeAsync();
        await memoryBank.DisposeAsync();
    }

    /// <summary>
    /// Example 4: Event filtering
    /// </summary>
    public static async Task EventFilteringExample(ILLMClient llmClient, ILogger logger)
    {
        var eventBus = new StreamingEventBus(logger);

        // Create filtered subscriptions
        eventBus.CreateStream<AgentCompletedEvent>(
            subscriptionId: "completed-only",
            filter: evt => evt.Success == true
        );

        eventBus.CreateStream<AgentErrorEvent>(
            subscriptionId: "errors-only"
        );

        // Monitor specific events
        var cts = new CancellationTokenSource();

        var completedTask = Task.Run(async () =>
        {
            await foreach (var evt in eventBus.GetEventStream("completed-only", cts.Token))
            {
                var completedEvt = (AgentCompletedEvent)evt;
                Console.WriteLine($"✓ Completed: Agent {evt.AgentId} - Success: {completedEvt.Success}");
            }
        });

        var errorTask = Task.Run(async () =>
        {
            await foreach (var evt in eventBus.GetEventStream("errors-only", cts.Token))
            {
                var errorEvt = (AgentErrorEvent)evt;
                Console.WriteLine($"✗ Error: {errorEvt.ErrorMessage}");
            }
        });

        // Simulate some events
        eventBus.Publish(new AgentCompletedEvent { AgentId = "Agent1", Success = true });
        eventBus.Publish(new AgentErrorEvent { AgentId = "Agent2", ErrorMessage = "Test error" });
        eventBus.Publish(new AgentCompletedEvent { AgentId = "Agent3", Success = true });

        await Task.Delay(500);
        cts.Cancel();

        eventBus.Clear();
    }

    /// <summary>
    /// Example 5: Server-Sent Events (SSE)
    /// </summary>
    public static async Task ServerSentEventsExample(ILLMClient llmClient, ILogger logger)
    {
        var eventBus = new StreamingEventBus(logger);

        // Create SSE stream
        eventBus.CreateStream<AgentEvent>("sse-stream");
        var sseStream = new ServerSentEventStream(eventBus, "sse-stream", logger);

        // Simulate SSE client
        var cts = new CancellationTokenSource();
        var clientTask = Task.Run(async () =>
        {
            await foreach (var sseMessage in sseStream.StreamEventsAsync(cts.Token))
            {
                Console.WriteLine("SSE Message:");
                Console.WriteLine(sseMessage);
            }
        });

        // Publish some events
        await Task.Delay(100);
        eventBus.Publish(new AgentStartedEvent { AgentName = "TestAgent", AgentId = "test-1" });
        await Task.Delay(100);
        eventBus.Publish(new AgentCompletedEvent { AgentId = "test-1", Success = true });

        await Task.Delay(500);
        cts.Cancel();

        await sseStream.DisposeAsync();
        eventBus.Clear();
    }

    /// <summary>
    /// Example 6: Event replay for debugging
    /// </summary>
    public static async Task EventReplayExample(ILLMClient llmClient, ILogger logger)
    {
        var eventBus = new EventBus();
        var eventReplay = new EventReplay(maxHistorySize: 100, logger);

        // Record events as they happen
        eventBus.Subscribe<AgentEvent>(evt => eventReplay.RecordEvent(evt));

        // Create agent and do some work
        var memoryStorage = new InMemoryMemoryStorage();
        var memoryBank = new MemoryBank(memoryStorage);
        var sessionStorage = new InMemorySessionStorage();
        var sessionManager = new SessionManager(sessionStorage);

        var agent = new MemoryEnabledAgent(
            name: "ReplayAgent",
            llmClient: llmClient,
            sessionManager: sessionManager,
            memoryBank: memoryBank,
            context: new AgentContext(new AgentState(), eventBus, logger)
        );

        await agent.InitializeAsync();
        await agent.ExecuteWithMemoryAsync("Test message 1");
        await agent.ExecuteWithMemoryAsync("Test message 2");

        // Get event history
        var stats = eventReplay.GetStatistics();
        Console.WriteLine($"Recorded {stats.TotalEvents} events");
        Console.WriteLine("Events by type:");
        foreach (var kvp in stats.EventsByType)
        {
            Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
        }

        // Export events to JSON
        var json = eventReplay.ExportToJson();
        Console.WriteLine("\nEvent history JSON:");
        Console.WriteLine(json);

        // Replay events to a new bus
        var newBus = new EventBus();
        newBus.Subscribe<AgentEvent>(evt =>
        {
            Console.WriteLine($"Replayed: {evt.EventType} at {evt.Timestamp}");
        });

        await eventReplay.ReplayEventsAsync(newBus, replaySpeed: TimeSpan.FromSeconds(0.1));

        await agent.DisposeAsync();
        await sessionManager.DisposeAsync();
        await memoryBank.DisposeAsync();
    }

    /// <summary>
    /// Example 7: Memory consolidation
    /// </summary>
    public static async Task MemoryConsolidationExample(ILogger logger)
    {
        var memoryStorage = new InMemoryMemoryStorage(logger);
        var memoryBank = new MemoryBank(memoryStorage, logger);

        // Store many memories with varying importance
        for (int i = 0; i < 20; i++)
        {
            await memoryBank.StoreMemoryAsync(
                content: $"Memory {i}",
                type: MemoryType.Working,
                importance: i * 0.05 // Importance from 0.0 to 0.95
            );
        }

        Console.WriteLine($"Created 20 memories");

        var statsBefore = await memoryBank.GetStatisticsAsync();
        Console.WriteLine($"Before consolidation: {statsBefore.TotalMemories} memories");
        Console.WriteLine($"Average strength: {statsBefore.AverageStrength:F2}");

        // Consolidate - remove weak memories
        var removed = await memoryBank.ConsolidateMemoriesAsync(strengthThreshold: 0.3);

        var statsAfter = await memoryBank.GetStatisticsAsync();
        Console.WriteLine($"\nAfter consolidation: {statsAfter.TotalMemories} memories");
        Console.WriteLine($"Removed: {removed} weak memories");
        Console.WriteLine($"Average strength: {statsAfter.AverageStrength:F2}");

        await memoryBank.DisposeAsync();
    }
}
