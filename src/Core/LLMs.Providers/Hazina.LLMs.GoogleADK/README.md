# Hazina.LLMs.GoogleADK

Google Agent Development Kit (ADK) implementation for Hazina. Provides a robust agent architecture aligned with Google ADK patterns for building sophisticated AI agents.

## Overview

This library implements the core components of Google's Agent Development Kit (ADK) architecture in C#/.NET, providing:

- **BaseAgent**: Abstract base class for all agents with lifecycle management
- **LlmAgent**: LLM-powered agents using Gemini or other language models
- **AgentContext**: Execution context with state, events, and logging
- **AgentState**: State management for agent configuration and runtime data
- **EventBus**: Pub/sub event system for agent coordination
- **AgentRuntime**: Factory and orchestration for multiple agents

## Key Features

### Agent Architecture

- **Lifecycle Management**: Initialize, Execute, Pause, Resume, Dispose
- **Event-Driven**: Built-in event system for monitoring agent actions
- **State Management**: Thread-safe state storage and snapshots
- **Context Propagation**: Parent-child context for nested agent execution
- **Cancellation Support**: Graceful cancellation handling

### Event System

The library includes a comprehensive event system with events for:

- Agent lifecycle: `AgentStartedEvent`, `AgentCompletedEvent`, `AgentErrorEvent`
- Tool execution: `ToolCalledEvent`, `ToolResultEvent`
- Messages: `MessageEvent`
- State changes: `StateChangedEvent`
- Streaming: `StreamChunkEvent`

### LLM Integration

`LlmAgent` provides:
- Conversation history management
- System instructions support
- Streaming and non-streaming responses
- Token usage tracking
- Image input support
- Tool/function calling

## Installation

Add a project reference:

```xml
<ProjectReference Include="path/to/Hazina.LLMs.GoogleADK/Hazina.LLMs.GoogleADK.csproj" />
```

## Configuration

Create an `appsettings.json`:

```json
{
  "GoogleADK": {
    "ApiKey": "your-gemini-api-key",
    "Endpoint": "https://generativelanguage.googleapis.com/v1beta",
    "DefaultModel": "gemini-1.5-pro",
    "MaxHistorySize": 50,
    "EnableStreaming": false,
    "DefaultSystemInstructions": "You are a helpful AI assistant.",
    "SessionTimeoutMinutes": 30,
    "EnableDetailedLogging": false
  }
}
```

## Usage Examples

### Basic LLM Agent

```csharp
using Hazina.LLMs.GoogleADK.Agents;
using Hazina.LLMs.GoogleADK.Core;
using Hazina.LLMs.Gemini;

// Load configuration
var config = AdkConfiguration.Load();

// Create Gemini client
var geminiConfig = new GeminiConfig
{
    ApiKey = config.ApiKey,
    Model = config.DefaultModel,
    Endpoint = config.Endpoint
};
var llmClient = new GeminiClientWrapper(geminiConfig);

// Create agent
var agent = new LlmAgent("MyAssistant", llmClient)
{
    SystemInstructions = "You are a helpful coding assistant.",
    EnableStreaming = false
};

// Initialize agent
await agent.InitializeAsync();

// Execute agent
var result = await agent.ExecuteAsync("Write a hello world in C#");
Console.WriteLine(result.Output);

// Check token usage
if (result.Metadata.TryGetValue("tokenUsage", out var usage))
{
    Console.WriteLine($"Tokens used: {usage}");
}
```

### Agent with Event Handling

```csharp
// Create agent
var agent = new LlmAgent("EventAgent", llmClient);

// Subscribe to events
agent.SubscribeToEvent<AgentStartedEvent>(evt =>
{
    Console.WriteLine($"Agent {evt.AgentName} started");
});

agent.SubscribeToEvent<MessageEvent>(evt =>
{
    Console.WriteLine($"{evt.Role}: {evt.Content}");
});

agent.SubscribeToEvent<AgentCompletedEvent>(evt =>
{
    Console.WriteLine($"Agent completed in {evt.Duration.TotalSeconds}s");
});

// Initialize and execute
await agent.InitializeAsync();
await agent.ExecuteAsync("What is the capital of France?");
```

### Streaming Responses

```csharp
var agent = new LlmAgent("StreamingAgent", llmClient)
{
    EnableStreaming = true
};

// Subscribe to stream chunks
agent.SubscribeToEvent<StreamChunkEvent>(evt =>
{
    Console.Write(evt.Chunk);
});

await agent.InitializeAsync();
await agent.ExecuteAsync("Tell me a story about a robot");
```

### Agent Runtime with Multiple Agents

```csharp
using Hazina.LLMs.GoogleADK.Core;

// Create runtime
var runtime = new AgentRuntime();

// Create and register agents
var codeAgent = new LlmAgent("CodeAssistant", llmClient)
{
    SystemInstructions = "You are an expert programmer."
};

var writerAgent = new LlmAgent("WriterAssistant", llmClient)
{
    SystemInstructions = "You are a creative writer."
};

runtime.RegisterAgent(codeAgent);
runtime.RegisterAgent(writerAgent);

// Execute agents sequentially
var results = await runtime.ExecuteSequentialAsync(new List<(string, string)>
{
    (codeAgent.AgentId, "Write a function to sort an array"),
    (writerAgent.AgentId, "Write a poem about coding")
});

foreach (var result in results)
{
    Console.WriteLine(result.Output);
    Console.WriteLine("---");
}

// Get statistics
var stats = runtime.GetStatistics();
Console.WriteLine($"Total agents: {stats.TotalAgents}");
Console.WriteLine($"Completed: {stats.CompletedAgents}");
```

### Custom Agent Implementation

```csharp
using Hazina.LLMs.GoogleADK.Core;

public class CustomAgent : BaseAgent
{
    public CustomAgent(string name) : base(name) { }

    protected override async Task<AgentResult> OnExecuteAsync(
        string input,
        CancellationToken cancellationToken)
    {
        // Your custom logic here
        Context.Log(LogLevel.Information, "Processing: {Input}", input);

        // Emit custom events
        Context.EmitEvent(new MessageEvent
        {
            Role = "system",
            Content = "Processing request..."
        });

        // Simulate work
        await Task.Delay(1000, cancellationToken);

        var output = $"Processed: {input}";
        return AgentResult.CreateSuccess(output);
    }
}

// Usage
var customAgent = new CustomAgent("MyCustomAgent");
await customAgent.InitializeAsync();
var result = await customAgent.ExecuteAsync("test input");
```

### Conversation Management

```csharp
var agent = new LlmAgent("ConversationAgent", llmClient);
await agent.InitializeAsync();

// First message
await agent.ExecuteAsync("My name is Alice");

// Second message - agent remembers context
await agent.ExecuteAsync("What's my name?");
// Output: "Your name is Alice"

// View conversation history
foreach (var msg in agent.ConversationHistory)
{
    Console.WriteLine($"{msg.Role.Role}: {msg.Text}");
}

// Clear history if needed
agent.ClearHistory();
```

### Agent State Management

```csharp
var agent = new LlmAgent("StateAgent", llmClient);

// Set state
agent.Context.State.Set("userId", "12345");
agent.Context.State.Set("preferences", new { theme = "dark", language = "en" });

// Get state
var userId = agent.Context.State.Get<string>("userId");
var prefs = agent.Context.State.Get<object>("preferences");

// Take snapshot
var snapshot = agent.GetStateSnapshot();

// Restore later
agent.Context.State.RestoreSnapshot(snapshot);
```

## Architecture

### Class Hierarchy

```
BaseAgent (abstract)
├── LlmAgent
└── [Custom agents extend BaseAgent]
```

### Core Components

- **BaseAgent**: Foundation for all agents
  - Lifecycle: Initialize → Execute → Dispose
  - Events: Emit and subscribe to agent events
  - State: Manage configuration and runtime data

- **AgentContext**: Execution context
  - State management
  - Event bus access
  - Logging integration
  - Cancellation handling

- **AgentState**: State storage
  - Thread-safe get/set operations
  - Snapshots and restoration
  - Status tracking

- **EventBus**: Event system
  - Pub/sub pattern
  - Type-safe event handling
  - Async event publishing

- **AgentRuntime**: Orchestration
  - Agent registration
  - Sequential execution
  - Parallel execution
  - Global event monitoring

## Event Types

| Event | Description |
|-------|-------------|
| `AgentStartedEvent` | Agent initialization completed |
| `AgentCompletedEvent` | Agent execution finished |
| `AgentErrorEvent` | Agent encountered an error |
| `ToolCalledEvent` | Agent called a tool/function |
| `ToolResultEvent` | Tool returned a result |
| `MessageEvent` | Message sent or received |
| `StateChangedEvent` | Agent state was modified |
| `StreamChunkEvent` | Streaming chunk received |

## Agent Status States

| Status | Description |
|--------|-------------|
| `Idle` | Agent ready for execution |
| `Initializing` | Agent is initializing |
| `Running` | Agent actively executing |
| `Waiting` | Agent waiting for input |
| `Paused` | Execution paused |
| `Completed` | Execution finished |
| `Error` | Error occurred |
| `Cancelled` | Execution cancelled |

## Dependencies

- **Hazina.LLMs.Client**: ILLMClient interface
- **Hazina.LLMs.Classes**: Chat message models
- **Hazina.LLMs.Gemini**: Gemini LLM integration
- **Microsoft.Extensions.Configuration**: Configuration loading
- **Microsoft.Extensions.Logging**: Logging abstractions

## Roadmap

Future implementations (Steps 2-10 from the Google ADK plan):

- [ ] Workflow Agents (SequentialAgent, ParallelAgent, LoopAgent)
- [ ] Model Context Protocol (MCP) support
- [ ] Session Management with persistence
- [ ] Memory Bank for long-term memory
- [ ] Enhanced Event System with streaming
- [ ] Agent2Agent (A2A) Protocol
- [ ] Evaluation Framework
- [ ] Artifact Management
- [ ] Developer UI (ASP.NET Core MVC + React)

## License

MIT License - see LICENSE file for details

## Author

Prospergenics
