# Getting Started with Hazina

**A step-by-step guide to building production-ready AI applications with Hazina**

## Table of Contents

1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Installation](#installation)
4. [Core Concepts](#core-concepts)
5. [Your First Hazina Application](#your-first-hazina-application)
6. [Multi-Provider Setup](#multi-provider-setup)
7. [Adding RAG Capabilities](#adding-rag-capabilities)
8. [Working with Agents](#working-with-agents)
9. [Production Deployment](#production-deployment)
10. [Next Steps](#next-steps)

---

## Overview

Hazina is a production-ready AI framework for .NET that provides:

- **Multi-provider orchestration** - Seamlessly switch between OpenAI, Anthropic, local models
- **RAG (Retrieval-Augmented Generation)** - Context-aware responses from your documents
- **Agentic workflows** - Tool-using autonomous agents
- **Production monitoring** - Built-in cost tracking, health checks, fault detection
- **Modular architecture** - Use only what you need, scale without rewriting

**Key Principle**: Code you write on day 1 scales to production without changes. Infrastructure is configured, not coded.

---

## Prerequisites

### Required
- **.NET 8.0 or higher** (.NET 10.0 recommended)
- **C# knowledge** (intermediate level)
- **API keys** for at least one LLM provider:
  - OpenAI API key (recommended for getting started)
  - Or Anthropic API key
  - Or local LLM setup (Ollama)

### Optional
- **PostgreSQL** for production storage (Supabase works great)
- **Docker** for containerized deployment
- **Visual Studio 2022** or **VS Code** with C# Dev Kit

---

## Installation

### Option 1: Start from NuGet (Recommended)

Create a new console application and add Hazina packages:

```bash
# Create new project
dotnet new console -n MyHazinaApp
cd MyHazinaApp

# Add core packages
dotnet add package Hazina.AI.FluentAPI --version 1.0.1
dotnet add package Hazina.LLMs.OpenAI --version 1.0.1

# Optional: Add more capabilities
dotnet add package Hazina.AI.RAG --version 1.0.1        # For document Q&A
dotnet add package Hazina.AI.Agents --version 1.0.1     # For agents
dotnet add package Hazina.Tools.Data --version 1.0.1    # For database tools
```

### Option 2: Clone and Build from Source

```bash
# Clone repository
git clone https://github.com/martiendejong/Hazina.git
cd Hazina

# Build QuickStart solution (fastest build, core features)
dotnet restore Hazina.QuickStart.sln
dotnet build Hazina.QuickStart.sln

# Or build specific area
dotnet build Hazina.AI.sln      # AI features only
dotnet build Hazina.Core.sln    # Infrastructure only
dotnet build Hazina.Tools.sln   # Tools and services
```

See [SOLUTIONS.md](../SOLUTIONS.md) for guidance on which solution file to use.

---

## Core Concepts

### 1. Provider Orchestration

Hazina abstracts away LLM provider differences. You write code once, swap providers via configuration.

```csharp
// OpenAI
var ai = QuickSetup.SetupOpenAI(openAiKey);

// Anthropic
var ai = QuickSetup.SetupAnthropic(anthropicKey);

// Your code stays the same!
var response = await ai.GetResponse(messages);
```

### 2. FluentAPI

Hazina provides a fluent, composable API for AI operations:

```csharp
var result = await Hazina.AI()
    .WithProvider("openai")
    .WithFaultDetection(minConfidence: 0.9)
    .WithCostTracking(budgetLimit: 10.0m)
    .Ask("What is the capital of France?")
    .ExecuteAsync();
```

### 3. Modular Architecture

Since Phase 4 standardization (March 2026), all Hazina libraries multi-target .NET 8.0, 9.0, and 10.0:

- **Core Libraries** (`src/Core/**`) - Multi-targeted, maximum compatibility
- **Tool Libraries** (`src/Tools/**`) - Multi-targeted, plug-and-play
- **Applications** (`apps/**`) - Single-targeted to .NET 10.0 for latest features

Reference any Hazina package from .NET 8.0+ projects without version conflicts.

### 4. Metadata-First Architecture

Hazina uses a **metadata-first** approach where embeddings are optional:

- **Database is truth** - PostgreSQL or SQLite holds all queryable data
- **Metadata is primary** - Tags, properties, structure always queryable
- **Embeddings are secondary** - Optional acceleration for semantic search

This means full functionality even when embeddings are disabled. See [Knowledge Storage Guide](KNOWLEDGE_STORAGE.md) for details.

---

## Your First Hazina Application

### Hello World

Create `Program.cs`:

```csharp
using Hazina.AI.FluentAPI.Configuration;

// Setup AI (uses environment variable OPENAI_API_KEY)
var ai = QuickSetup.SetupOpenAI(
    Environment.GetEnvironmentVariable("OPENAI_API_KEY")!
);

// Simple query
var messages = new List<HazinaChatMessage>
{
    new() { Role = HazinaMessageRole.User, Text = "Hello! What is 2+2?" }
};

var response = await ai.GetResponse(messages);
Console.WriteLine(response.Content.Text);
```

Run it:

```bash
# Set your API key (Windows)
set OPENAI_API_KEY=sk-your-key-here

# Or Linux/Mac
export OPENAI_API_KEY=sk-your-key-here

# Run
dotnet run
```

**Output**: `Hello! 2+2 equals 4.`

### Conversation Loop

Extend to an interactive chat:

```csharp
using Hazina.AI.FluentAPI.Configuration;

var ai = QuickSetup.SetupOpenAI(Environment.GetEnvironmentVariable("OPENAI_API_KEY")!);

var history = new List<HazinaChatMessage>
{
    new()
    {
        Role = HazinaMessageRole.System,
        Text = "You are a helpful assistant."
    }
};

Console.WriteLine("Chat with AI (type 'quit' to exit)\n");

while (true)
{
    Console.Write("You: ");
    var input = Console.ReadLine();

    if (string.IsNullOrEmpty(input) || input.ToLower() == "quit")
        break;

    // Add user message
    history.Add(new HazinaChatMessage
    {
        Role = HazinaMessageRole.User,
        Text = input
    });

    // Get AI response
    var response = await ai.GetResponse(history);

    // Add to history
    history.Add(new HazinaChatMessage
    {
        Role = HazinaMessageRole.Assistant,
        Text = response.Content.Text
    });

    Console.WriteLine($"AI: {response.Content.Text}\n");
}
```

---

## Multi-Provider Setup

### Automatic Failover

Set up OpenAI with Anthropic as backup:

```csharp
using Hazina.AI.FluentAPI.Configuration;

var ai = QuickSetup.SetupWithFailover(
    primaryKey: Environment.GetEnvironmentVariable("OPENAI_API_KEY")!,
    fallbackKey: Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")!
);

// If OpenAI fails, automatically retries with Anthropic
var response = await ai.GetResponse(messages);
```

### Cost Optimization

Automatically use the cheapest available provider:

```csharp
var ai = QuickSetup.SetupCostOptimized(
    Environment.GetEnvironmentVariable("OPENAI_API_KEY")!,
    Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
);

// Always routes to cheapest provider for the given model capabilities
```

### Manual Provider Selection

```csharp
var orchestrator = new ProviderOrchestrator();

orchestrator.AddProvider("openai", new OpenAIClientWrapper(openAiKey));
orchestrator.AddProvider("anthropic", new AnthropicClientWrapper(anthropicKey));

// Use specific provider
orchestrator.SetDefaultProvider("openai");
var response1 = await orchestrator.GetResponse(messages);

// Switch provider
orchestrator.SetDefaultProvider("anthropic");
var response2 = await orchestrator.GetResponse(messages);
```

### Selection Strategies

```csharp
var orchestrator = new ProviderOrchestrator();
orchestrator.AddProvider("openai", openAiClient);
orchestrator.AddProvider("anthropic", anthropicClient);

// Cheapest first
orchestrator.SetDefaultStrategy(SelectionStrategy.LeastCost);

// Fastest response
orchestrator.SetDefaultStrategy(SelectionStrategy.FastestResponse);

// Round-robin
orchestrator.SetDefaultStrategy(SelectionStrategy.RoundRobin);

// Weighted (60% OpenAI, 40% Anthropic)
orchestrator.SetDefaultStrategy(SelectionStrategy.Weighted,
    new Dictionary<string, double>
    {
        ["openai"] = 0.6,
        ["anthropic"] = 0.4
    });
```

---

## Adding RAG Capabilities

### Basic RAG Setup

Add packages:

```bash
dotnet add package Hazina.AI.RAG --version 1.0.1
dotnet add package Hazina.Storage.Embeddings --version 1.0.1
```

Create a RAG engine:

```csharp
using Hazina.AI.FluentAPI.Configuration;
using Hazina.AI.RAG.Core;

var ai = QuickSetup.SetupOpenAI(Environment.GetEnvironmentVariable("OPENAI_API_KEY")!);

// In-memory storage for development
var vectorStore = new InMemoryVectorStore();

// Create RAG engine
var rag = new RAGEngine(ai, vectorStore);

// Index documents
await rag.IndexDocumentsAsync(new List<Document>
{
    new()
    {
        Content = "Hazina is a production-ready AI framework for .NET.",
        Metadata = new() { ["source"] = "overview.md" }
    },
    new()
    {
        Content = "RAG combines retrieval with generation for accurate answers.",
        Metadata = new() { ["source"] = "rag-guide.md" }
    }
});

Console.WriteLine("Documents indexed!\n");

// Query with context
var response = await rag.QueryAsync("What is Hazina?");
Console.WriteLine($"Answer: {response.Answer}");
Console.WriteLine($"Sources: {response.RetrievedDocuments.Count} documents used");
```

### Load Documents from Files

```csharp
using Hazina.AI.RAG.Embeddings;

var chunker = new TextChunker(new TextChunkingOptions
{
    Strategy = ChunkingStrategy.Paragraph,
    ChunkSize = 1000,
    OverlapSize = 100
});

var documents = new List<Document>();

foreach (var file in Directory.GetFiles("documents", "*.txt"))
{
    var content = await File.ReadAllTextAsync(file);
    var chunks = chunker.ChunkText(content, new() { ["source"] = Path.GetFileName(file) });

    documents.AddRange(chunks.Select(chunk => new Document
    {
        Id = $"{Path.GetFileName(file)}_{chunk.Index}",
        Content = chunk.Text,
        Metadata = chunk.Metadata
    }));
}

await rag.IndexDocumentsAsync(documents);
```

### Production RAG with PostgreSQL

```bash
dotnet add package Hazina.Tools.Data --version 1.0.1
dotnet add package Npgsql --version 8.0.0
```

```csharp
using Hazina.Tools.Data;

var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? "Host=localhost;Database=myrag;Username=postgres;Password=postgres";

// PostgreSQL with pgvector
var vectorStore = new PgVectorStore(connectionString);
await vectorStore.InitializeAsync();

var rag = new RAGEngine(ai, vectorStore);

// Same code as before - documents persist across restarts!
```

### RAG Without Embeddings

Hazina functions correctly without embeddings (uses metadata + keyword search):

```csharp
var rag = new RAGEngine(ai, vectorStore, config: new RAGConfig
{
    UseEmbeddings = false  // Full functionality, faster indexing, no embedding API costs
});

// Search still works via keyword matching and metadata
var response = await rag.QueryAsync("authentication flow");
```

See [RAG Guide](RAG_GUIDE.md) for complete documentation.

---

## Working with Agents

### Create Your First Agent

```bash
dotnet add package Hazina.AI.Agents --version 1.0.1
```

```csharp
using Hazina.AI.Agents;

var agent = new Agent(
    name: "ResearchAssistant",
    description: "Helps with research tasks",
    orchestrator: ai
);

var result = await agent.ExecuteAsync("Research the history of machine learning");
Console.WriteLine(result.Result);
```

### Agent with Tools

```csharp
// Create calculator tool
public class CalculatorTool : ITool
{
    public string Name => "calculator";
    public string Description => "Performs mathematical calculations";

    public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var expression = parameters["expression"].ToString();
        // Evaluate expression...
        return result.ToString();
    }
}

// Register tool
var agent = new Agent("MathAssistant", "Math helper", ai);
agent.RegisterTool(new CalculatorTool());

// Agent can now use the calculator
var response = await agent.ExecuteAsync("Calculate 123 * 456");
```

### Multi-Agent Coordination

```csharp
var coordinator = new MultiAgentCoordinator();

coordinator.AddAgent(new Agent("researcher", researchPrompt, ai));
coordinator.AddAgent(new Agent("writer", writerPrompt, ai));
coordinator.AddAgent(new Agent("reviewer", reviewerPrompt, ai));

// Sequential workflow
var result = await coordinator.ExecuteAsync(
    "Write a blog post about AI ethics",
    CoordinationStrategy.Sequential
);

// Parallel workflow
var result = await coordinator.ExecuteAsync(
    "Analyze this data from multiple perspectives",
    CoordinationStrategy.Parallel
);
```

See [Agents Guide](AGENTS_GUIDE.md) for complete documentation.

---

## Production Deployment

### Environment Configuration

Create `appsettings.Production.json`:

```json
{
  "Hazina": {
    "Providers": {
      "OpenAI": {
        "ApiKey": "${OPENAI_API_KEY}",
        "Model": "gpt-4",
        "MaxRetries": 3,
        "TimeoutSeconds": 30
      },
      "Anthropic": {
        "ApiKey": "${ANTHROPIC_API_KEY}",
        "Model": "claude-3-opus",
        "Enabled": true
      }
    },
    "Orchestration": {
      "DefaultProvider": "openai",
      "FallbackProvider": "anthropic",
      "SelectionStrategy": "LeastCost"
    },
    "Storage": {
      "ConnectionString": "${DATABASE_URL}",
      "UseEmbeddings": true
    },
    "Monitoring": {
      "EnableCostTracking": true,
      "BudgetLimit": 100.0,
      "EnableHealthChecks": true,
      "LogLevel": "Information"
    }
  }
}
```

### Cost Tracking

```csharp
var ai = QuickSetup.SetupOpenAI(apiKey);

// Enable cost tracking
ai.EnableCostTracking(budgetLimit: 50.0m);

// Use normally
var response = await ai.GetResponse(messages);

// Check costs
var costs = ai.GetCostReport();
Console.WriteLine($"Total cost: ${costs.TotalCost}");
Console.WriteLine($"Budget remaining: ${costs.RemainingBudget}");

// Cost alerts
ai.OnCostLimitApproaching += (sender, args) =>
{
    Console.WriteLine($"Warning: {args.PercentUsed:P0} of budget used!");
};
```

### Health Monitoring

```csharp
var ai = QuickSetup.SetupWithFailover(openAiKey, anthropicKey);

ai.EnableHealthMonitoring();

// Check health
var health = await ai.GetHealthStatus();
Console.WriteLine($"Status: {health.Status}");
Console.WriteLine($"Primary provider: {health.PrimaryProviderStatus}");
Console.WriteLine($"Fallback provider: {health.FallbackProviderStatus}");

// Circuit breaker automatically disables unhealthy providers
```

### Docker Deployment

Create `Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "MyHazinaApp.dll"]
```

Build and run:

```bash
docker build -t my-hazina-app .
docker run -e OPENAI_API_KEY=$OPENAI_API_KEY -e DATABASE_URL=$DATABASE_URL my-hazina-app
```

---

## Next Steps

### Tutorials
- [30-Minute RAG Tutorial](quickstart.md) - Build production RAG in 30 minutes
- [Advanced Scenarios](ADVANCED_SCENARIOS.md) - Complex use cases and patterns

### Feature Guides
- [Knowledge Storage](KNOWLEDGE_STORAGE.md) - Metadata-first architecture
- [RAG Guide](RAG_GUIDE.md) - Document indexing, retrieval, generation
- [Agents Guide](AGENTS_GUIDE.md) - Tool calling, workflows, coordination
- [Neurochain Guide](NEUROCHAIN_GUIDE.md) - Multi-layer reasoning
- [Production Monitoring](PRODUCTION_MONITORING_GUIDE.md) - Metrics, health checks

### Reference
- [API Reference](apidoc/api/index.html) - Complete API documentation
- [Architecture](ARCHITECTURE.md) - Design principles and patterns
- [Package Registry](../PACKAGES_REGISTRY.md) - All 99+ packages with descriptions
- [Services Registry](../SERVICES_REGISTRY.md) - All service interfaces

### Examples
- [Demo Applications](../apps/Demos/) - Complete working examples
- [Integration Tests](../apps/Testing/) - Real-world scenarios

### Community
- [Contributing](../CONTRIBUTING.md) - How to contribute
- [Migration Guide](MIGRATION_GUIDE.md) - Upgrading from v1.x
- [API Changelog](API_CHANGELOG.md) - Version history

---

**You're ready to build production AI with Hazina!**

Remember: Code you write on day 1 scales to production without changes. Start with `InMemoryVectorStore`, deploy with `PgVectorStore`. Start with OpenAI, add Anthropic failover later. Infrastructure is configured, not coded.
