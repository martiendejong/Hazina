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

### Workflow Agents

Workflow agents execute deterministic control flows without LLM reasoning.

#### SequentialAgent

Execute steps in order, one after another:

```csharp
using Hazina.LLMs.GoogleADK.Workflows;

var runtime = new AgentRuntime();

// Register sub-agents
runtime.RegisterAgent(new LlmAgent("analyzer", llmClient));
runtime.RegisterAgent(new LlmAgent("summarizer", llmClient));

// Create sequential workflow
var workflow = new SequentialAgent("DataPipeline", runtime)
    .AddStep("Analyze", "analyzer", "Analyze this data: {lastResult}")
    .AddStep("Summarize", "summarizer", "Summarize: {lastResult}")
    .StopOnError(true);

await workflow.InitializeAsync();
var result = await workflow.ExecuteAsync("Raw data input");
```

#### ParallelAgent

Execute steps concurrently:

```csharp
var workflow = new ParallelAgent("MultiAnalysis", runtime)
    .AddStep("SentimentAnalysis", "sentiment-agent", "Analyze sentiment")
    .AddStep("KeywordExtraction", "keyword-agent", "Extract keywords")
    .AddStep("Classification", "classifier-agent", "Classify content")
    .WithMaxDegreeOfParallelism(3)
    .WaitForAll(true);

await workflow.InitializeAsync();
var result = await workflow.ExecuteAsync("Analyze this text...");

// Access individual step results
var workflowResult = result.Metadata["workflowResult"] as WorkflowResult;
foreach (var stepResult in workflowResult.StepResults)
{
    Console.WriteLine($"{stepResult.Key}: {stepResult.Value.Output}");
}
```

#### LoopAgent

Repeat steps with conditions:

```csharp
var workflow = new LoopAgent("IterativeRefinement", runtime)
    .AddStep("Process", "processor-agent", "Process iteration {data.iteration}")
    .WithMaxIterations(5)
    .WithContinueCondition(ctx =>
    {
        var lastResult = ctx.GetLastResult();
        return lastResult?.Output?.Contains("continue") == true;
    })
    .WithBreakOnError(false)
    .CollectResults(true);

await workflow.InitializeAsync();
var result = await workflow.ExecuteAsync("Initial input");

// Access all iteration results
var allResults = result.Metadata["allIterationResults"] as List<Dictionary<string, AgentResult>>;
```

#### WorkflowEngine with Builders

Use the fluent workflow engine for easier composition:

```csharp
var runtime = new AgentRuntime();
var engine = new WorkflowEngine(runtime);

// Sequential workflow
var sequential = engine.Sequential("Pipeline")
    .AddStep("step1", "agent-1", "Process {lastResult}")
    .AddStep("step2", "agent-2", "{lastResult}")
    .StopOnError(true)
    .Build();

runtime.RegisterAgent(sequential);

// Parallel workflow
var parallel = engine.Parallel("Concurrent")
    .AddStep("task1", "agent-a", "Task A")
    .AddStep("task2", "agent-b", "Task B")
    .WithMaxDegreeOfParallelism(2)
    .Build();

runtime.RegisterAgent(parallel);

// Loop workflow
var loop = engine.Loop("Retry")
    .AddStep("attempt", "retry-agent", "Attempt {data.iteration}")
    .WithMaxIterations(3)
    .WithBreakOnError(false)
    .Build();

runtime.RegisterAgent(loop);

// Execute workflows
var result1 = await engine.ExecuteWorkflowAsync(sequential, "input");
var result2 = await engine.ExecuteWorkflowAsync(parallel, "input");
var result3 = await engine.ExecuteWorkflowAsync(loop, "input");
```

#### Workflow with Custom Actions

Use custom lambda actions instead of agents:

```csharp
var workflow = new SequentialAgent("CustomWorkflow", runtime)
    .AddStep("Validate", async ctx =>
    {
        var input = ctx.Get<string>("initialInput");
        var isValid = !string.IsNullOrEmpty(input);
        return isValid
            ? AgentResult.CreateSuccess("Valid")
            : AgentResult.CreateFailure("Invalid input");
    })
    .AddStep("Transform", async ctx =>
    {
        var lastResult = ctx.GetLastResult();
        var transformed = lastResult?.Output.ToUpper() ?? "";
        return AgentResult.CreateSuccess(transformed);
    });

await workflow.InitializeAsync();
var result = await workflow.ExecuteAsync("test");
```

#### Workflow Configuration from JSON

Define workflows in JSON and load them:

```json
{
  "name": "DataProcessingWorkflow",
  "type": "Sequential",
  "settings": {
    "stopOnError": true
  },
  "steps": [
    {
      "name": "ExtractData",
      "agentId": "extractor-agent",
      "input": "Extract from source",
      "continueOnError": false
    },
    {
      "name": "TransformData",
      "agentId": "transformer-agent",
      "input": "{lastResult}",
      "continueOnError": false
    },
    {
      "name": "LoadData",
      "agentId": "loader-agent",
      "input": "{lastResult}",
      "continueOnError": true
    }
  ]
}
```

Load and execute:

```csharp
var config = WorkflowConfiguration.LoadFromFile("workflow.json");
var factory = new WorkflowFactory(runtime);
var workflow = factory.CreateFromConfiguration(config);

runtime.RegisterAgent(workflow);
var result = await workflow.ExecuteAsync("start");
```

#### Conditional Steps

Add steps that only execute based on conditions:

```csharp
var workflow = new SequentialAgent("ConditionalWorkflow", runtime)
    .AddConditionalStep(
        "OptionalStep",
        ctx => ctx.Iteration > 2,  // Only execute after 3rd iteration
        "agent-id",
        "Conditional input"
    );
```

#### Template Variables in Workflow Inputs

Workflows support template variables in step inputs:

- `{lastResult}` - Output from the previous step
- `{stepId.output}` - Output from a specific step by ID
- `{data.key}` - Value from workflow context data

```csharp
var workflow = new SequentialAgent("TemplateWorkflow", runtime)
    .AddStep("Step1", "agent-1", "Process this")
    .AddStep("Step2", "agent-2", "Use result: {lastResult}")
    .AddStep("Step3", "agent-3", "Combine: {Step1.output} and {Step2.output}");
```

## Architecture

### Class Hierarchy

```
BaseAgent (abstract)
├── LlmAgent
├── WorkflowAgent (abstract)
│   ├── SequentialAgent
│   ├── ParallelAgent
│   └── LoopAgent
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

## Tool System & MCP Support

### Overview

The Google ADK implementation includes a comprehensive tool system with support for the **Model Context Protocol (MCP)**, enabling agents to discover and use tools from external MCP servers.

### Key Components

1. **ToolRegistry**: Centralized tool discovery and management
2. **MCP Client**: Connect to MCP servers (stdio, HTTP)
3. **MCP Server**: Expose Hazina tools via MCP protocol
4. **Tool Validation**: Schema validation and type checking
5. **Tool Adapters**: Seamless conversion between MCP and Hazina tools

### MCP Protocol

The Model Context Protocol enables:
- **Tool Discovery**: Automatic discovery of tools from MCP servers
- **Tool Execution**: Invoke tools on remote servers
- **Resource Access**: Read resources exposed by servers
- **Prompt Templates**: Use server-provided prompts

### Using MCP Tools

#### Connect to an MCP Server (stdio)

```csharp
using Hazina.LLMs.GoogleADK.Agents;
using Hazina.LLMs.GoogleADK.Tools.Registry;

var toolRegistry = new ToolRegistry();
var agent = new McpAgent("Assistant", llmClient, toolRegistry);

// Connect to a Node.js MCP server
await agent.ConnectToStdioServerAsync(
    serverCommand: "node",
    serverArgs: new[] { "mcp-server.js" },
    providerName: "FileSystemTools"
);

await agent.InitializeAsync();
var result = await agent.ExecuteAsync("List files in current directory");
```

#### Connect to an HTTP MCP Server

```csharp
await agent.ConnectToHttpServerAsync(
    serverUrl: "http://localhost:3000",
    providerName: "WebAPITools"
);
```

#### Use ToolEnabledAgent with Manual Tools

```csharp
var toolRegistry = new ToolRegistry();
var agent = new ToolEnabledAgent("Assistant", llmClient, toolRegistry);

// Add custom tool
var calculatorTool = new HazinaChatTool(
    name: "calculator",
    description: "Performs arithmetic",
    parameters: new List<ChatToolParameter>
    {
        new ChatToolParameter
        {
            Name = "operation",
            Type = "string",
            IsRequired = true
        }
    },
    execute: async (messages, call, ct) => "42"
);

agent.AddTool(calculatorTool);
await agent.InitializeAsync();
```

### Tool Registry Features

- **Tool Discovery**: Auto-discover tools from multiple providers
- **Tool Search**: Find tools by name, description, or tags
- **Category Filtering**: Group and filter tools by category
- **Validation**: Automatic validation of tool definitions and arguments
- **Schema Management**: JSON Schema generation and validation

### Architecture

```
┌─────────────────────────────────────────────────┐
│              McpAgent / ToolEnabledAgent        │
│  ┌───────────────────────────────────────────┐  │
│  │          Tool Registry                    │  │
│  │  - Tool Discovery                         │  │
│  │  - Tool Validation                        │  │
│  │  - Schema Management                      │  │
│  └───────────────────────────────────────────┘  │
└─────────────────────────────────────────────────┘
                    │
        ┌───────────┴───────────┐
        │                       │
┌───────▼──────┐      ┌────────▼────────┐
│  MCP Client  │      │  Manual Tools   │
│  (stdio/HTTP)│      │  (HazinaChatTool)│
└──────────────┘      └─────────────────┘
        │
┌───────▼──────────┐
│   MCP Server     │
│ (Node/Python/etc)│
└──────────────────┘
```

### Examples

See `Examples/McpToolsExample.cs` for comprehensive examples including:
1. Stdio MCP agent
2. HTTP MCP agent
3. Custom MCP client integration
4. Manual tool registration
5. Multi-server agent setup

## Session Management

### Overview

The session management system provides robust session lifecycle management, persistence, recovery, and hooks for monitoring and extending agent behavior.

### Key Components

1. **Session**: Session model with metadata, messages, and configuration
2. **SessionManager**: Manages session lifecycle and persistence
3. **SessionStorage**: Pluggable storage providers (in-memory, file-based)
4. **SessionRecoveryService**: Recover sessions after failures
5. **SessionMiddleware**: Hooks for session lifecycle events

### Session Features

- **Lifecycle Management**: Create, resume, pause, complete, terminate
- **Auto-persistence**: Configurable auto-save intervals
- **Timeout Management**: Automatic expiration of inactive sessions
- **Message History**: Trimmed conversation history with configurable limits
- **Recovery**: Recover sessions after crashes or restarts
- **Filtering**: Find sessions by agent, user, status, or tags
- **Hooks**: Extensible middleware for monitoring and analytics

### Using Sessions

#### Basic Session Usage

```csharp
using Hazina.LLMs.GoogleADK.Agents;
using Hazina.LLMs.GoogleADK.Sessions;
using Hazina.LLMs.GoogleADK.Sessions.Storage;

// Create session manager
var sessionStorage = new InMemorySessionStorage();
var sessionManager = new SessionManager(sessionStorage);

// Create session-enabled agent
var agent = new SessionEnabledAgent("ChatBot", llmClient, sessionManager);
await agent.InitializeAsync();

// Execute with auto-created session
var result = await agent.ExecuteWithSessionAsync("Hello!");
Console.WriteLine(result.Output);

// Continue conversation in same session
var result2 = await agent.ExecuteWithSessionAsync("Remember what I just said?");

// Complete session
await agent.CompleteSessionAsync();
```

#### Custom Configuration

```csharp
var config = new SessionConfiguration
{
    MaxMessages = 20,              // Keep only last 20 messages
    TimeoutMinutes = 60,           // 1-hour timeout
    AutoSaveIntervalSeconds = 30,  // Auto-save every 30 seconds
    PersistToStorage = true,
    EnableRecovery = true
};

var session = await agent.StartSessionAsync(
    userId: "user-123",
    configuration: config,
    metadata: new Dictionary<string, object>
    {
        ["user_language"] = "en",
        ["session_purpose"] = "support"
    }
);

session.Tags.Add("important");
```

#### Resume Existing Session

```csharp
// Resume a paused or interrupted session
var resumedSession = await agent.ResumeSessionAsync(sessionId);

if (resumedSession != null)
{
    Console.WriteLine($"Resumed with {resumedSession.Messages.Count} messages");
    await agent.ExecuteWithSessionAsync("Continue where we left off");
}
```

#### Session Recovery

```csharp
var recoveryService = new SessionRecoveryService(sessionStorage);

// Recover all active sessions for an agent
var activeSessions = await recoveryService.RecoverAgentSessionsAsync("ChatBot");

foreach (var session in activeSessions)
{
    await agent.ResumeSessionAsync(session.SessionId);
    // Continue processing
}
```

#### Session Hooks

```csharp
var middleware = new SessionMiddleware();

// Register built-in logging hook
middleware.RegisterHook(new LoggingSessionHook(logger));

// Register custom analytics hook
middleware.RegisterHook(new CustomAnalyticsHook());

// Hooks are triggered on lifecycle events
var session = await agent.StartSessionAsync();
await middleware.OnSessionCreatedAsync(session);
```

### Storage Providers

**In-Memory Storage** (Development/Testing):
```csharp
var storage = new InMemorySessionStorage();
```

**File-Based Storage** (Production):
```csharp
var storage = new FileSessionStorage("./sessions");
```

### Session Architecture

```
┌──────────────────────────────────────┐
│       SessionEnabledAgent            │
│  ┌────────────────────────────────┐  │
│  │    SessionManager              │  │
│  │  - Lifecycle Management        │  │
│  │  - Auto-save Timer             │  │
│  │  - Cleanup Timer               │  │
│  └────────────────────────────────┘  │
└──────────────────────────────────────┘
                 │
    ┌────────────┴────────────┐
    │                         │
┌───▼────────┐      ┌────────▼──────┐
│  Storage   │      │  Middleware   │
│ Providers  │      │    & Hooks    │
└────────────┘      └───────────────┘
```

### Examples

See `Examples/SessionExamples.cs` for comprehensive examples including:
1. Basic session usage
2. Custom configuration
3. Session resumption
4. Session recovery
5. Session hooks
6. Filtering and listing sessions

## Dependencies

- **Hazina.LLMs.Client**: ILLMClient interface
- **Hazina.LLMs.Classes**: Chat message models
- **Hazina.LLMs.Gemini**: Gemini LLM integration
- **Microsoft.Extensions.Configuration**: Configuration loading
- **Microsoft.Extensions.Logging**: Logging abstractions

## Roadmap

Implementation progress (Steps 1-10 from the Google ADK plan):

- [x] **Step 1: Core ADK Agent Architecture** - BaseAgent, LlmAgent, AgentContext, AgentState, EventBus, AgentRuntime ✅
- [x] **Step 2: Workflow Agents** - SequentialAgent, ParallelAgent, LoopAgent, WorkflowEngine, JSON configuration ✅
- [x] **Step 3: Enhanced Tool System with MCP Support** - Model Context Protocol integration, tool registry, validation ✅
- [x] **Step 4: Session Management** - Session persistence, lifecycle, recovery, storage providers ✅
- [ ] **Step 5: Memory Bank** - Long-term cross-session memory
- [ ] **Step 6: Enhanced Event System** - Bidirectional streaming
- [ ] **Step 7: Agent2Agent (A2A) Protocol** - Inter-agent communication
- [ ] **Step 8: Evaluation Framework** - Agent performance testing
- [ ] **Step 9: Artifact Management** - File and binary handling
- [ ] **Step 10: Developer UI** - ASP.NET Core MVC + React debugging interface

## License

MIT License - see LICENSE file for details

## Author

Prospergenics
