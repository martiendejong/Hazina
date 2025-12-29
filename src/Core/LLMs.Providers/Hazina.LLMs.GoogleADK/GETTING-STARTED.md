# Getting Started with Hazina Google ADK

A comprehensive tutorial for building AI agents using the Hazina Google Agent Development Kit (ADK) implementation.

## Table of Contents

1. [Installation](#installation)
2. [Quick Start](#quick-start)
3. [Step-by-Step Tutorial](#step-by-step-tutorial)
   - [Step 1: Your First Agent](#step-1-your-first-agent)
   - [Step 2: Building a Workflow](#step-2-building-a-workflow)
   - [Step 3: Adding Tools](#step-3-adding-tools)
   - [Step 4: Session Management](#step-4-session-management)
   - [Step 5: Memory Bank](#step-5-memory-bank)
   - [Step 6: Event Streaming](#step-6-event-streaming)
   - [Step 7: Multi-Agent Communication](#step-7-multi-agent-communication)
   - [Step 8: Testing and Evaluation](#step-8-testing-and-evaluation)
   - [Step 9: Artifact Management](#step-9-artifact-management)
   - [Step 10: Monitoring and Debugging](#step-10-monitoring-and-debugging)
4. [Production Deployment](#production-deployment)

---

## Installation

### Prerequisites

- .NET 8.0 SDK or later
- A Gemini API key (get one at [Google AI Studio](https://makersuite.google.com/app/apikey))
- Visual Studio 2022 or VS Code with C# extension

### Install via NuGet

```bash
dotnet add package Hazina.LLMs.GoogleADK
dotnet add package Hazina.LLMs.Gemini
```

### Or Clone and Build

```bash
git clone https://github.com/prospergenics/devgpt.git
cd devgpt/src/Core/LLMs.Providers/Hazina.LLMs.GoogleADK
dotnet build
```

---

## Quick Start

Create your first agent in 5 minutes:

```csharp
using Hazina.LLMs.GoogleADK.Agents;
using Hazina.LLMs.GoogleADK.Core;
using Hazina.LLMs.Gemini;

// Configure Gemini client
var config = new GeminiConfig
{
    ApiKey = "your-api-key-here",
    Model = "gemini-2.0-flash-exp"
};
var llmClient = new GeminiClientWrapper(config);

// Create an agent
var agent = new LlmAgent("Assistant", llmClient)
{
    SystemInstructions = "You are a helpful assistant."
};

await agent.InitializeAsync();

// Execute
var result = await agent.ExecuteAsync("Hello! What can you help me with?");
Console.WriteLine(result.Output);

await agent.DisposeAsync();
```

---

## Step-by-Step Tutorial

### Step 1: Your First Agent

**Goal**: Create a simple LLM-powered agent that can hold conversations.

#### 1.1 Create the Project

```bash
dotnet new console -n MyFirstAgent
cd MyFirstAgent
dotnet add package Hazina.LLMs.GoogleADK
dotnet add package Hazina.LLMs.Gemini
```

#### 1.2 Configure Your LLM

Create `appsettings.json`:

```json
{
  "Gemini": {
    "ApiKey": "your-api-key-here",
    "Model": "gemini-2.0-flash-exp"
  }
}
```

#### 1.3 Build the Agent

```csharp
using Hazina.LLMs.GoogleADK.Agents;
using Hazina.LLMs.GoogleADK.Core;
using Hazina.LLMs.Gemini;
using Microsoft.Extensions.Configuration;

// Load configuration
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var geminiConfig = new GeminiConfig();
config.GetSection("Gemini").Bind(geminiConfig);

var llmClient = new GeminiClientWrapper(geminiConfig);

// Create agent with personality
var agent = new LlmAgent("CodingAssistant", llmClient)
{
    SystemInstructions = @"You are an expert C# developer.
You provide clear, concise code examples and explanations.
You follow best practices and modern .NET patterns."
};

await agent.InitializeAsync();

// Interactive loop
Console.WriteLine("Coding Assistant Ready! (type 'exit' to quit)");
while (true)
{
    Console.Write("\nYou: ");
    var input = Console.ReadLine();
    if (string.IsNullOrEmpty(input) || input.ToLower() == "exit") break;

    var result = await agent.ExecuteAsync(input);
    Console.WriteLine($"\nAssistant: {result.Output}");

    // Show token usage
    if (result.Metadata.TryGetValue("tokenUsage", out var usage))
    {
        Console.WriteLine($"\n[Tokens used: {usage}]");
    }
}

await agent.DisposeAsync();
```

#### 1.4 Run It

```bash
dotnet run
```

**What You Learned**:
- How to create an LlmAgent
- How to configure system instructions
- How to handle conversations
- How to access metadata like token usage

---

### Step 2: Building a Workflow

**Goal**: Create multi-step workflows that orchestrate multiple operations.

#### 2.1 Sequential Workflow

Create a data processing pipeline:

```csharp
using Hazina.LLMs.GoogleADK.Workflows;
using Hazina.LLMs.GoogleADK.Core;

var runtime = new AgentRuntime();

// Create agents for each step
var extractor = new LlmAgent("DataExtractor", llmClient)
{
    SystemInstructions = "Extract key data points from text. Return JSON."
};

var validator = new LlmAgent("DataValidator", llmClient)
{
    SystemInstructions = "Validate JSON data. Return 'valid' or list of errors."
};

var transformer = new LlmAgent("DataTransformer", llmClient)
{
    SystemInstructions = "Transform JSON to SQL INSERT statements."
};

await runtime.RegisterAgentAsync(extractor);
await runtime.RegisterAgentAsync(validator);
await runtime.RegisterAgentAsync(transformer);

// Create sequential workflow
var workflow = new SequentialAgent("DataPipeline", runtime)
    .AddStep("Extract", extractor.AgentId, "{input}")
    .AddStep("Validate", validator.AgentId, "{lastResult}")
    .AddStep("Transform", transformer.AgentId, "{lastResult}");

await workflow.InitializeAsync();

// Process data
var rawData = "Customer: John Doe, Email: john@example.com, Purchase: $99.99";
var result = await workflow.ExecuteAsync(rawData);

Console.WriteLine("SQL Output:");
Console.WriteLine(result.Output);
```

#### 2.2 Parallel Workflow

Process multiple tasks concurrently:

```csharp
var parallelFlow = new ParallelAgent("MultiAnalysis", runtime)
    .AddBranch("SentimentAnalysis", sentimentAgent.AgentId, "{input}")
    .AddBranch("KeywordExtraction", keywordAgent.AgentId, "{input}")
    .AddBranch("CategoryClassification", categoryAgent.AgentId, "{input}");

await parallelFlow.InitializeAsync();

var review = "This product is amazing! Great quality, fast shipping.";
var result = await parallelFlow.ExecuteAsync(review);

// Access all branch results
foreach (var step in result.Metadata["steps"] as List<object>)
{
    Console.WriteLine(step);
}
```

#### 2.3 Loop Workflow

Iterative processing with conditions:

```csharp
var loop = new LoopAgent("CodeReviewer", runtime, reviewAgent.AgentId)
{
    MaxIterations = 5,
    ContinueCondition = ctx =>
    {
        var lastOutput = ctx.GetLastResult()?.Output ?? "";
        return !lastOutput.Contains("APPROVED");
    }
};

await loop.InitializeAsync();
var codeToReview = File.ReadAllText("MyClass.cs");
var reviewResult = await loop.ExecuteAsync(codeToReview);
```

**What You Learned**:
- Sequential workflows for pipelines
- Parallel workflows for concurrent processing
- Loop workflows with conditions
- Template variables ({input}, {lastResult})

---

### Step 3: Adding Tools

**Goal**: Enable agents to call external functions and APIs.

#### 3.1 Register Tools

```csharp
using Hazina.LLMs.GoogleADK.Tools;
using Hazina.LLMs.GoogleADK.Agents;

var toolRegistry = new ToolRegistry();

// Add a simple calculator tool
var calculator = new HazinaChatTool(
    name: "calculate",
    description: "Performs basic arithmetic operations",
    parameters: new List<ChatToolParameter>
    {
        new ChatToolParameter
        {
            Name = "expression",
            Type = "string",
            Description = "Math expression (e.g., '2 + 2', '10 * 5')",
            IsRequired = true
        }
    },
    execute: async (messages, call, ct) =>
    {
        var expr = call.Arguments["expression"].ToString();
        var result = EvaluateExpression(expr); // Your evaluation logic
        return result.ToString();
    }
);

toolRegistry.RegisterTool(calculator);

// Create tool-enabled agent
var agent = new ToolEnabledAgent("MathBot", llmClient, toolRegistry);
await agent.InitializeAsync();

var result = await agent.ExecuteAsync("What is 123 * 456?");
Console.WriteLine(result.Output); // Agent will call the calculator tool
```

#### 3.2 Connect to MCP Server

Use Model Context Protocol to connect to external tool servers:

```csharp
var mcpAgent = new McpAgent("FileSystemAgent", llmClient, toolRegistry);

// Connect to a Node.js MCP server that provides file operations
await mcpAgent.ConnectToStdioServerAsync(
    serverCommand: "node",
    serverArgs: new[] { "mcp-server.js" },
    providerName: "FileSystem"
);

await mcpAgent.InitializeAsync();

// Agent can now use file system tools
var result = await mcpAgent.ExecuteAsync("List all .cs files in the current directory");
```

**What You Learned**:
- Creating custom tools
- Tool parameters and execution
- Model Context Protocol integration
- Tool-enabled agents

---

### Step 4: Session Management

**Goal**: Add persistent, recoverable sessions to your agents.

#### 4.1 Basic Session Usage

```csharp
using Hazina.LLMs.GoogleADK.Sessions;
using Hazina.LLMs.GoogleADK.Sessions.Storage;

// Create session storage
var sessionStorage = new FileSessionStorage("./sessions");
var sessionManager = new SessionManager(sessionStorage);

// Create session-enabled agent
var agent = new SessionEnabledAgent("ChatBot", llmClient, sessionManager)
{
    SystemInstructions = "You are a helpful assistant. Remember our conversation."
};

await agent.InitializeAsync();

// Start a session
var session = await agent.StartSessionAsync(
    userId: "user-123",
    metadata: new Dictionary<string, object>
    {
        ["user_name"] = "John",
        ["session_type"] = "support"
    }
);

Console.WriteLine($"Session started: {session.SessionId}");

// Have a conversation
await agent.ExecuteWithSessionAsync("My name is John");
await agent.ExecuteWithSessionAsync("I need help with billing");
await agent.ExecuteWithSessionAsync("What's my name?"); // Agent remembers!

// Complete session
await agent.CompleteSessionAsync();
```

#### 4.2 Resume Sessions

```csharp
// Later... resume the session
var sessionId = "previous-session-id";
var resumed = await agent.ResumeSessionAsync(sessionId);

if (resumed != null)
{
    Console.WriteLine($"Resumed session with {resumed.Messages.Count} messages");
    await agent.ExecuteWithSessionAsync("Let's continue where we left off");
}
```

#### 4.3 Session Recovery

```csharp
var recovery = new SessionRecoveryService(sessionStorage);

// Recover all active sessions after crash
var activeSessions = await recovery.RecoverAgentSessionsAsync("ChatBot");

foreach (var session in activeSessions)
{
    await agent.ResumeSessionAsync(session.SessionId);
    Console.WriteLine($"Recovered session: {session.SessionId}");
}
```

**What You Learned**:
- Session lifecycle management
- Persistent conversation history
- Session resumption
- Recovery after failures

---

### Step 5: Memory Bank

**Goal**: Give agents long-term memory across sessions.

#### 5.1 Create Memory Bank

```csharp
using Hazina.LLMs.GoogleADK.Memory;
using Hazina.LLMs.GoogleADK.Memory.Storage;

var memoryStorage = new InMemoryMemoryStorage();
var memoryBank = new MemoryBank(memoryStorage);

// Store memories
await memoryBank.StoreMemoryAsync(
    content: "User John prefers dark mode",
    type: MemoryType.Semantic,
    importance: 0.8,
    tags: new List<string> { "preferences", "user-john" }
);

await memoryBank.StoreMemoryAsync(
    content: "John asked about Python on Jan 15, 2024",
    type: MemoryType.Episodic,
    importance: 0.5,
    tags: new List<string> { "history", "user-john" }
);

await memoryBank.StoreMemoryAsync(
    content: "To commit code: git add . && git commit -m 'message'",
    type: MemoryType.Procedural,
    importance: 0.9,
    tags: new List<string> { "procedures", "git" }
);
```

#### 5.2 Search Memories

```csharp
// Text search
var results = await memoryBank.SearchByTextAsync("dark mode", limit: 5);

foreach (var memory in results)
{
    Console.WriteLine($"[{memory.Type}] {memory.Content}");
    Console.WriteLine($"  Importance: {memory.Importance}, Strength: {memory.Strength}");
}

// Tag search
var johnMemories = await memoryBank.GetMemoriesByTagAsync("user-john");
```

#### 5.3 Memory-Enabled Agent

```csharp
var agent = new MemoryEnabledAgent(
    name: "SmartAssistant",
    llmClient: llmClient,
    sessionManager: sessionManager,
    memoryBank: memoryBank
);

await agent.InitializeAsync();

// Agent automatically uses memories
var result = await agent.ExecuteWithMemoryAsync("What are John's preferences?");
Console.WriteLine(result.Output); // Will recall "dark mode" preference
```

**What You Learned**:
- Types of memory (Episodic, Semantic, Procedural)
- Storing and searching memories
- Memory importance and strength
- Memory-enabled agents

---

### Step 6: Event Streaming

**Goal**: Monitor agent activity in real-time.

#### 6.1 Basic Event Subscription

```csharp
using Hazina.LLMs.GoogleADK.Events;

var agent = new LlmAgent("TrackedAgent", llmClient);

// Subscribe to events
agent.SubscribeToEvent<AgentStartedEvent>(evt =>
{
    Console.WriteLine($"Agent started: {evt.AgentId}");
});

agent.SubscribeToEvent<AgentCompletedEvent>(evt =>
{
    Console.WriteLine($"Completed in {evt.Duration.TotalSeconds:F2}s");
    Console.WriteLine($"Success: {evt.Success}");
});

agent.SubscribeToEvent<StreamChunkEvent>(evt =>
{
    Console.Write(evt.Chunk); // Stream output as it's generated
});

await agent.InitializeAsync();
await agent.ExecuteAsync("Tell me a story");
```

#### 6.2 Streaming Event Bus

```csharp
var eventBus = new StreamingEventBus();

// Create filtered stream
eventBus.CreateStream<AgentErrorEvent>("errors-only");

// Consume error events
_ = Task.Run(async () =>
{
    await foreach (var error in eventBus.GetEventStream("errors-only"))
    {
        Console.Error.WriteLine($"ERROR: {error.ErrorMessage}");
        // Log to monitoring system, send alerts, etc.
    }
});
```

#### 6.3 Server-Sent Events (SSE)

```csharp
// For web applications
eventBus.CreateStream<AgentEvent>("sse-stream");
var sseStream = new ServerSentEventStream(eventBus, "sse-stream");

// In your ASP.NET Core controller:
public async Task StreamEvents(CancellationToken ct)
{
    Response.ContentType = "text/event-stream";
    await sseStream.StreamToWriterAsync(Response.Body, ct);
}
```

**What You Learned**:
- Event subscription and handling
- Streaming events with filters
- Real-time monitoring
- Server-Sent Events for web apps

---

### Step 7: Multi-Agent Communication

**Goal**: Build systems where agents collaborate.

#### 7.1 Agent Discovery

```csharp
using Hazina.LLMs.GoogleADK.A2A;
using Hazina.LLMs.GoogleADK.A2A.Registry;
using Hazina.LLMs.GoogleADK.A2A.Transport;

var directory = new InMemoryAgentDirectory();
var transport = new InProcessA2ATransport();
await transport.StartAsync();

// Create specialized agents
var analyst = new A2AEnabledAgent("DataAnalyst", llmClient, directory, transport)
    .AddCapability("analyze", "Analyzes data and generates insights", new List<string> { "analytics", "data" });

var writer = new A2AEnabledAgent("ReportWriter", llmClient, directory, transport)
    .AddCapability("write_report", "Writes professional reports", new List<string> { "writing", "reports" });

await analyst.InitializeAsync();
await writer.InitializeAsync();

// Discover agents
var analyticsAgents = await analyst.DiscoverAgentsAsync("analyze");
Console.WriteLine($"Found {analyticsAgents.Count} analytics agents");
```

#### 7.2 Task Delegation

```csharp
// Manager agent delegates to specialists
var manager = new A2AEnabledAgent("ProjectManager", llmClient, directory, transport);
await manager.InitializeAsync();

// Delegate analysis task
var analysisResult = await manager.DelegateTaskAsync(
    "analyze",
    "Sales data for Q1 2024: [data here]"
);

Console.WriteLine($"Analysis complete: {analysisResult.Success}");
Console.WriteLine(analysisResult.Payload);

// Delegate report writing
var reportResult = await manager.DelegateTaskAsync(
    "write_report",
    $"Create report from: {analysisResult.Payload}"
);
```

#### 7.3 Direct Communication

```csharp
// Send request directly
var response = await manager.SendRequestToAgentAsync(
    targetAgentId: analyst.AgentId,
    requestType: "custom_analysis",
    payload: "Analyze customer churn"
);

// Broadcast notification
await manager.NotifyAgentsAsync(
    new List<string> { analyst.AgentId, writer.AgentId },
    "system_update",
    "New data available for processing"
);
```

**What You Learned**:
- Agent registration and discovery
- Capability-based task delegation
- Direct agent-to-agent communication
- Broadcasting notifications

---

### Step 8: Testing and Evaluation

**Goal**: Systematically test and benchmark your agents.

#### 8.1 Create Test Cases

```csharp
using Hazina.LLMs.GoogleADK.Evaluation;
using Hazina.LLMs.GoogleADK.Evaluation.Models;

var runner = new EvaluationRunner();

// Define test case
var testCase = new TestCase
{
    Name = "Capital Question",
    Input = "What is the capital of France?",
    ExpectedOutput = "Paris",
    Difficulty = TestCaseDifficulty.Easy
};

// Run test
var result = await runner.RunTestCaseAsync(agent, testCase);

Console.WriteLine($"Test: {result.TestCase.Name}");
Console.WriteLine($"Passed: {result.Passed}");
Console.WriteLine($"Score: {result.Score:F2}");
Console.WriteLine($"Actual: {result.ActualOutput}");
Console.WriteLine($"Duration: {result.Duration.TotalMilliseconds:F0}ms");
```

#### 8.2 Test Suites

```csharp
var suite = new TestSuite
{
    Name = "Geography Knowledge",
    Description = "Test general geography knowledge",
    TestCases = new List<TestCase>
    {
        new() { Input = "Capital of France?", ExpectedOutput = "Paris" },
        new() { Input = "Capital of Japan?", ExpectedOutput = "Tokyo" },
        new() { Input = "Capital of Brazil?", ExpectedOutput = "Brasilia" },
        new() { Input = "Capital of Australia?", ExpectedOutput = "Canberra" }
    }
};

var suiteResult = await runner.RunTestSuiteAsync(agent, suite);

Console.WriteLine($"\nResults: {suiteResult.PassedTests}/{suiteResult.TotalTests} passed");
Console.WriteLine($"Pass Rate: {suiteResult.PassRate:P}");
Console.WriteLine($"Average Score: {suiteResult.AverageScore:F2}");
```

#### 8.3 Benchmarks

```csharp
var benchmark = new Benchmark
{
    Name = "General Knowledge Benchmark",
    TestSuites = new List<TestSuite> { geographySuite, historySuite, scienceSuite }
};

var benchmarkRunner = new BenchmarkRunner();
var benchResult = await benchmarkRunner.RunBenchmarkAsync(agent, benchmark);

Console.WriteLine("\nBenchmark Results:");
Console.WriteLine($"Pass Rate: {benchResult.Metrics.PassRate:P}");
Console.WriteLine($"Average Latency: {benchResult.Metrics.AverageLatencyMs:F2}ms");
Console.WriteLine($"P95 Latency: {benchResult.Metrics.P95LatencyMs:F2}ms");
Console.WriteLine($"P99 Latency: {benchResult.Metrics.P99LatencyMs:F2}ms");
```

#### 8.4 Compare Agents

```csharp
var agent1 = new LlmAgent("Agent1", llmClient1);
var agent2 = new LlmAgent("Agent2", llmClient2);

var comparison = await benchmarkRunner.CompareBenchmarkAsync(
    new List<BaseAgent> { agent1, agent2 },
    benchmark
);

Console.WriteLine($"Best Agent: {comparison.BestAgentId}");
foreach (var result in comparison.Results)
{
    Console.WriteLine($"{result.Key}: {result.Value.Metrics.PassRate:P}");
}
```

#### 8.5 Generate Reports

```csharp
var reporter = new EvaluationReporter();

// Markdown report
var markdown = reporter.GenerateMarkdownReport(benchResult);
await reporter.SaveReportAsync(markdown, "benchmark-results.md");

// HTML report
var html = reporter.GenerateHtmlReport(benchResult);
await reporter.SaveReportAsync(html, "benchmark-results.html");

// JSON export
var json = reporter.GenerateJsonReport(benchResult);
await reporter.SaveReportAsync(json, "benchmark-results.json");
```

**What You Learned**:
- Creating test cases and suites
- Running benchmarks
- Performance metrics (latency percentiles)
- Comparing multiple agents
- Generating reports in multiple formats

---

### Step 9: Artifact Management

**Goal**: Handle files and binary data produced by agents.

#### 9.1 Basic Artifact Creation

```csharp
using Hazina.LLMs.GoogleADK.Artifacts;
using Hazina.LLMs.GoogleADK.Artifacts.Storage;

var storage = new FileSystemArtifactStorage("./artifacts");
var manager = new ArtifactManager(storage);

// Create text artifact
var textArtifact = await manager.CreateFromTextAsync(
    text: "# Report\n\nGenerated by AI agent.",
    name: "report.md",
    agentId: "report-generator"
);

Console.WriteLine($"Created artifact: {textArtifact.ArtifactId}");

// Create from file
var fileArtifact = await manager.CreateFromFileAsync(
    filePath: "input.csv",
    agentId: "data-processor"
);

// Create binary artifact
var imageBytes = File.ReadAllBytes("chart.png");
var imageArtifact = await manager.CreateFromBinaryAsync(
    data: imageBytes,
    name: "chart.png",
    mimeType: "image/png",
    artifactType: ArtifactType.Image,
    agentId: "chart-generator"
);
```

#### 9.2 Artifact-Enabled Agents

```csharp
var agent = new ArtifactEnabledAgent("ReportGenerator", llmClient, manager)
{
    SystemInstructions = "Generate professional reports as markdown files."
};

await agent.InitializeAsync();

// Agent produces artifacts
var result = await agent.ExecuteAsync("Generate a sales report for Q1");

// Access produced artifacts
var producedArtifacts = agent.GetProducedArtifacts();
foreach (var artifactId in producedArtifacts)
{
    var artifact = await manager.GetArtifactAsync(artifactId);
    Console.WriteLine($"Produced: {artifact.Name} ({artifact.Size} bytes)");

    // Export artifact
    await manager.ExportArtifactAsync(artifactId, $"./output/{artifact.Name}");
}
```

#### 9.3 Search and Filter

```csharp
// List all artifacts
var allArtifacts = await storage.ListArtifactsAsync();

// Filter by agent
var reportArtifacts = allArtifacts
    .Where(a => a.AgentId == "report-generator")
    .ToList();

// Filter by type
var images = allArtifacts
    .Where(a => a.Type == ArtifactType.Image)
    .ToList();

// Filter by tags
var monthlyReports = allArtifacts
    .Where(a => a.Tags.Contains("monthly"))
    .ToList();
```

**What You Learned**:
- Creating artifacts from text, files, and binary data
- Artifact-enabled agents
- Artifact storage and retrieval
- Searching and filtering artifacts

---

### Step 10: Monitoring and Debugging

**Goal**: Monitor, debug, and optimize your agents in production.

#### 10.1 Agent Monitor

```csharp
using Hazina.LLMs.GoogleADK.DeveloperUI;

var monitor = new AgentMonitor(maxHistorySize: 1000);

// Register agents
monitor.RegisterAgent(agent1);
monitor.RegisterAgent(agent2);

// Get agent status
var agents = monitor.GetAgents();
foreach (var agentInfo in agents)
{
    Console.WriteLine($"{agentInfo.Name}:");
    Console.WriteLine($"  Status: {agentInfo.Status}");
    Console.WriteLine($"  Events: {agentInfo.EventCount}");
    Console.WriteLine($"  Last Active: {agentInfo.LastEventAt}");
}

// Monitor events in real-time
_ = Task.Run(async () =>
{
    while (true)
    {
        await Task.Delay(1000);
        var stats = monitor.GetStatistics();
        Console.WriteLine($"\rActive: {stats.ActiveAgents}/{stats.TotalAgents} | Events: {stats.TotalEvents}");
    }
});
```

#### 10.2 Debug Execution

```csharp
var controller = new AgentController();
controller.RegisterAgent(agent);

// Execute with full debugging
var debugInfo = await controller.ExecuteWithDebuggingAsync(
    agentId: agent.AgentId,
    input: "Process this complex query"
);

Console.WriteLine("Debug Information:");
Console.WriteLine($"  Execution ID: {debugInfo.ExecutionId}");
Console.WriteLine($"  Status: {debugInfo.Status}");
Console.WriteLine($"  Duration: {debugInfo.Duration?.TotalMilliseconds:F2}ms");
Console.WriteLine($"  Input: {debugInfo.Input}");
Console.WriteLine($"  Output: {debugInfo.Output}");

if (debugInfo.State != null)
{
    Console.WriteLine("  State:");
    foreach (var kvp in debugInfo.State)
    {
        Console.WriteLine($"    {kvp.Key}: {kvp.Value}");
    }
}

if (debugInfo.Errors.Any())
{
    Console.WriteLine("  Errors:");
    foreach (var error in debugInfo.Errors)
    {
        Console.WriteLine($"    - {error}");
    }
}
```

#### 10.3 Performance Profiling

```csharp
// Run multiple executions
Console.WriteLine("Running performance tests...");
for (int i = 0; i < 20; i++)
{
    await controller.ExecuteWithDebuggingAsync(agent.AgentId, $"Test {i}");
}

// Get performance profile
var profile = controller.GetPerformanceProfile(agent.AgentId);

Console.WriteLine("\nPerformance Profile:");
Console.WriteLine($"  Total Executions: {profile.TotalExecutions}");
Console.WriteLine($"  Average Time: {profile.AverageExecutionTime.TotalMilliseconds:F2}ms");
Console.WriteLine($"  Min Time: {profile.MinExecutionTime.TotalMilliseconds:F2}ms");
Console.WriteLine($"  Max Time: {profile.MaxExecutionTime.TotalMilliseconds:F2}ms");
Console.WriteLine($"  Success Rate: {profile.SuccessRate:P}");
```

#### 10.4 Execution History

```csharp
// View execution history
var history = controller.GetExecutionHistory(agent.AgentId, limit: 10);

Console.WriteLine("Recent Executions:");
foreach (var execution in history)
{
    Console.WriteLine($"  [{execution.StartTime:HH:mm:ss}] {execution.Status}");
    Console.WriteLine($"    Duration: {execution.Duration?.TotalMilliseconds:F0}ms");
    Console.WriteLine($"    Input: {execution.Input.Substring(0, Math.Min(50, execution.Input.Length))}...");
}
```

#### 10.5 Agent Control

```csharp
// Update agent configuration dynamically
controller.UpdateAgentConfiguration(agent.AgentId, new Dictionary<string, object>
{
    ["maxRetries"] = 3,
    ["timeout"] = 30000,
    ["temperature"] = 0.7
});

// Pause agent
controller.PauseAgent(agent.AgentId);
Console.WriteLine("Agent paused for maintenance...");

await Task.Delay(5000); // Do maintenance

// Resume agent
controller.ResumeAgent(agent.AgentId);
Console.WriteLine("Agent resumed");
```

**What You Learned**:
- Real-time agent monitoring
- Debug execution with state capture
- Performance profiling
- Execution history tracking
- Dynamic agent control

---

## Production Deployment

### Best Practices

1. **Error Handling**
   ```csharp
   try
   {
       var result = await agent.ExecuteAsync(input);
   }
   catch (OperationCanceledException)
   {
       // Handle cancellation
   }
   catch (Exception ex)
   {
       logger.LogError(ex, "Agent execution failed");
       // Retry or fallback logic
   }
   ```

2. **Logging**
   ```csharp
   var loggerFactory = LoggerFactory.Create(builder =>
   {
       builder.AddConsole();
       builder.AddFile("logs/agent-{Date}.log");
   });

   var logger = loggerFactory.CreateLogger<LlmAgent>();
   var agent = new LlmAgent("Production", llmClient, logger: logger);
   ```

3. **Configuration Management**
   ```csharp
   // Use strongly-typed configuration
   var config = builder.Configuration
       .GetSection("AgentSettings")
       .Get<AgentConfiguration>();
   ```

4. **Rate Limiting**
   ```csharp
   // Implement rate limiting for LLM calls
   var rateLimiter = new SemaphoreSlim(10); // Max 10 concurrent requests

   await rateLimiter.WaitAsync();
   try
   {
       var result = await agent.ExecuteAsync(input);
   }
   finally
   {
       rateLimiter.Release();
   }
   ```

5. **Graceful Shutdown**
   ```csharp
   public async Task StopAsync(CancellationToken cancellationToken)
   {
       await agent.DisposeAsync();
       await sessionManager.DisposeAsync();
       await monitor.DisposeAsync();
   }
   ```

### ASP.NET Core Integration

```csharp
// Startup.cs or Program.cs
builder.Services.AddSingleton<ILLMClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var geminiConfig = new GeminiConfig();
    config.GetSection("Gemini").Bind(geminiConfig);
    return new GeminiClientWrapper(geminiConfig);
});

builder.Services.AddSingleton<SessionManager>(sp =>
{
    var storage = new FileSessionStorage("./sessions");
    return new SessionManager(storage);
});

builder.Services.AddScoped<LlmAgent>(sp =>
{
    var llm = sp.GetRequiredService<ILLMClient>();
    var logger = sp.GetRequiredService<ILogger<LlmAgent>>();
    return new LlmAgent("API", llm, logger: logger);
});
```

### Kubernetes Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: hazina-agent
spec:
  replicas: 3
  template:
    spec:
      containers:
      - name: agent
        image: mycompany/hazina-agent:1.0
        env:
        - name: Gemini__ApiKey
          valueFrom:
            secretKeyRef:
              name: gemini-secret
              key: api-key
        resources:
          requests:
            memory: "512Mi"
            cpu: "500m"
          limits:
            memory: "1Gi"
            cpu: "1000m"
```

---

## Next Steps

- Explore the [Examples](Examples/) folder for complete working examples
- Check out the [API Documentation](API.md) for detailed reference
- Join the community on [GitHub Discussions](https://github.com/prospergenics/devgpt/discussions)
- Read the [Architecture Guide](ARCHITECTURE.md) for deeper understanding

## Support

- Issues: https://github.com/prospergenics/devgpt/issues
- Documentation: https://github.com/prospergenics/devgpt/wiki
- Email: support@prospergenics.com

---

Happy building with Hazina Google ADK!
