# Hazina.AgentFactory

Agent management framework for creating, configuring, and orchestrating multi-agent systems with RAG (Retrieval-Augmented Generation) capabilities.

## Overview

This library provides the complete agent infrastructure for the Hazina platform:

- **AgentManager**: Central orchestrator for all agents, stores, and flows
- **HazinaAgent**: Individual agents with specialized capabilities and tools
- **HazinaFlow**: Multi-agent workflows and orchestrations
- **Configuration**: JSON-based agent and store configuration
- **QuickAgentCreator**: Programmatic agent creation
- **Conversation History**: Shared conversation context across agents

## Quick Start

### Installation

```xml
<ProjectReference Include="..\path\to\Hazina.AgentFactory\Hazina.AgentFactory.csproj" />
```

### Basic Usage

```csharp
using Hazina.AgentFactory;

// 1. Create AgentManager
var manager = new AgentManager(
    storesJsonPath: "stores.json",
    agentsJsonPath: "agents.json",
    flowsJsonPath: "flows.json",
    openAIApiKey: "sk-...",
    logFilePath: "hazina.log"
);

// 2. Load configuration
await manager.LoadStoresAndAgents();

// 3. Get an agent
var agent = manager.GetAgent("my_agent");

// 4. Make a request with RAG
var response = await agent.Generator.GetResponse(
    "What files are in the project?",
    CancellationToken.None,
    history: manager.History,
    addRelevantDocuments: true,
    addFilesList: true,
    toolsContext: agent.Tools
);

Console.WriteLine(response.Result);
Console.WriteLine($"Cost: ${response.TokenUsage.TotalCost:F4}");
```

## Key Classes

### AgentManager

**Location**: `Core/AgentManager.cs:13`

Central orchestrator managing all agents, document stores, and conversation history.

**Constructors**:
```csharp
// From file paths
public AgentManager(
    string storesJsonPath,
    string agentsJsonPath,
    string flowsJsonPath,
    string openAIApiKey,
    string logFilePath,
    string googleProjectId = "")

// With custom LLM client
public AgentManager(
    string storesJsonPath,
    string agentsJsonPath,
    string flowsJsonPath,
    ILLMClient llmClient,
    string openAIApiKey,
    string logFilePath,
    string googleProjectId = "")
```

**Properties**:
```csharp
public List<HazinaChatMessage> History { get; }        // Conversation history
public IReadOnlyList<IDocumentStore> Stores { get; }   // All stores
public IReadOnlyList<HazinaAgent> Agents { get; }      // All agents
public IReadOnlyList<HazinaFlow> Flows { get; }        // All flows
```

**Key Methods**:
```csharp
// Load configuration
public async Task LoadStoresAndAgents()

// Get agent by name
public HazinaAgent GetAgent(string name)

// Get store by name
public IDocumentStore GetStore(string name)

// Get flow by name
public HazinaFlow GetFlow(string name)
```

**Usage Example**:
```csharp
var manager = new AgentManager(
    "stores.json",
    "agents.json",
    "flows.json",
    "sk-...",
    "hazina.log"
);

await manager.LoadStoresAndAgents();

// Access agents
foreach (var agent in manager.Agents)
{
    Console.WriteLine($"Agent: {agent.Name}");
}

// Shared conversation history
manager.History.Add(new HazinaChatMessage(HazinaMessageRole.User, "Hello"));

// Agent has access to history
var agent = manager.GetAgent("assistant");
var response = await agent.Generator.GetResponse(
    "Continue our conversation",
    CancellationToken.None,
    history: manager.History
);
```

### HazinaAgent

**Location**: `Core/HazinaAgent.cs:1`

Individual agent with a generator, tools, and specialized configuration.

**Properties**:
```csharp
public string Name { get; set; }
public DocumentGenerator Generator { get; set; }
public IToolsContext Tools { get; set; }
public bool IsCoder { get; set; }
```

**Constructor**:
```csharp
public HazinaAgent(
    string name,
    DocumentGenerator generator,
    IToolsContext tools,
    bool isCoder = false)
```

**Usage Example**:
```csharp
var agent = manager.GetAgent("code_assistant");

// Simple request
var response = await agent.Generator.GetResponse(
    "How do I sort a list in C#?",
    CancellationToken.None
);

// With tools and RAG
var ragResponse = await agent.Generator.GetResponse(
    "Find all TODO comments in the codebase",
    CancellationToken.None,
    history: manager.History,
    addRelevantDocuments: true,
    addFilesList: true,
    toolsContext: agent.Tools
);

// Streaming response
await agent.Generator.StreamResponse(
    "Explain the architecture",
    CancellationToken.None,
    chunk => Console.Write(chunk),
    history: manager.History,
    addRelevantDocuments: true
);
```

### HazinaFlow

**Location**: `Core/HazinaFlow.cs`

Multi-agent workflow orchestration.

**Properties**:
```csharp
public string Name { get; set; }
public string Description { get; set; }
public List<string> AgentSequence { get; set; }
```

### AgentConfig

**Location**: `Configuration/AgentConfig.cs:2`

Configuration for an agent.

**Properties**:
```csharp
public string Name { get; set; }
public string Description { get; set; }
public string Prompt { get; set; }              // System prompt
public List<StoreRef> Stores { get; set; }      // Access to stores
public List<string> Functions { get; set; }     // Available tools
public List<string> CallsAgents { get; set; }   // Can call these agents
public List<string> CallsFlows { get; set; }    // Can execute these flows
public bool ExplicitModify { get; set; }        // Requires approval for modifications
```

**Example agents.json**:
```json
{
  "agents": [
    {
      "name": "code_assistant",
      "description": "Helps with coding tasks",
      "prompt": "You are an expert C# developer.",
      "stores": [
        { "name": "codebase", "access": "read" }
      ],
      "functions": ["read_file", "search_code"],
      "callsAgents": [],
      "callsFlows": [],
      "explicitModify": false
    }
  ]
}
```

### StoreConfig

**Location**: `Configuration/StoreConfig.cs`

Configuration for document stores.

**Example stores.json**:
```json
{
  "stores": [
    {
      "name": "codebase",
      "type": "file",
      "path": "C:\\projects\\myproject",
      "embeddings": "embeddings.json",
      "recursive": true
    },
    {
      "name": "docs",
      "type": "pgvector",
      "connectionString": "Host=localhost;Database=docs",
      "recursive": false
    }
  ]
}
```

### QuickAgentCreator

**Location**: `Core/QuickAgentCreator.cs`

Programmatic agent creation without JSON configuration.

**Usage Example**:
```csharp
var creator = manager._quickAgentCreator;

var agent = await creator.CreateAgent(
    name: "temp_agent",
    systemPrompt: "You are a helpful assistant.",
    stores: new[] { manager.GetStore("codebase") },
    tools: new ToolsContext()
);

var response = await agent.Generator.GetResponse(
    "Help me with this task",
    CancellationToken.None
);
```

## Configuration Files

### agents.json

```json
{
  "agents": [
    {
      "name": "architect",
      "description": "Software architecture expert",
      "prompt": "You are a software architect specializing in .NET applications.",
      "stores": [
        { "name": "codebase", "access": "read" },
        { "name": "docs", "access": "read" }
      ],
      "functions": [
        "read_file",
        "search_code",
        "list_files"
      ],
      "callsAgents": ["code_assistant"],
      "callsFlows": [],
      "explicitModify": false
    }
  ]
}
```

### stores.json

```json
{
  "stores": [
    {
      "name": "codebase",
      "type": "file",
      "path": "C:\\projects\\myapp\\src",
      "embeddings": "codebase_embeddings.json",
      "metadata": "codebase_metadata",
      "chunks": "codebase_chunks.json",
      "texts": "codebase_texts",
      "recursive": true
    }
  ]
}
```

### flows.json

```json
{
  "flows": [
    {
      "name": "code_review",
      "description": "Complete code review workflow",
      "agents": ["architect", "code_assistant", "tester"]
    }
  ]
}
```

## Usage Examples

### Multi-Agent Conversation

```csharp
var manager = new AgentManager("stores.json", "agents.json", "flows.json", apiKey, "log.txt");
await manager.LoadStoresAndAgents();

// Agent 1: Architect
var architect = manager.GetAgent("architect");
var plan = await architect.Generator.GetResponse(
    "Design a user authentication system",
    CancellationToken.None,
    history: manager.History,
    addRelevantDocuments: true
);

manager.History.Add(new HazinaChatMessage(HazinaMessageRole.Assistant, plan.Result));

// Agent 2: Code Assistant (has context from architect)
var coder = manager.GetAgent("code_assistant");
var code = await coder.Generator.GetResponse(
    "Implement the authentication system designed above",
    CancellationToken.None,
    history: manager.History,  // Has architect's plan
    addRelevantDocuments: true,
    toolsContext: coder.Tools
);

Console.WriteLine($"Total cost: ${plan.TokenUsage.TotalCost + code.TokenUsage.TotalCost:F4}");
```

### RAG with Multiple Stores

```csharp
// Agent has access to multiple stores
var agent = manager.GetAgent("researcher");

var response = await agent.Generator.GetResponse(
    "Find all authentication-related code",
    CancellationToken.None,
    addRelevantDocuments: true,  // Searches all agent's stores
    addFilesList: true            // Includes file listings
);
```

### Programmatic Agent Creation

```csharp
var tools = new ToolsContext();
tools.Add(new HazinaChatTool(
    "custom_tool",
    "My custom tool",
    parameters,
    async (msgs, call, cancel) => "Tool result"
));

var agent = await manager._quickAgentCreator.CreateAgent(
    "dynamic_agent",
    "You are a specialized assistant.",
    new[] { manager.GetStore("data") },
    tools
);
```

## Dependencies

- **Hazina.Generator**: Document generation with RAG
- **Hazina.Store.DocumentStore**: Document storage
- **Hazina.LLMs.Client**: LLM abstraction
- **Hazina.LLMs.OpenAI**: OpenAI provider
- **System.Text.Json**: JSON configuration parsing

## See Also

- [Hazina.Generator](../Hazina.Generator/README.md) - Document generation engine
- [Hazina.Store.DocumentStore](../../Storage/Hazina.Store.DocumentStore/README.md) - Document storage
- [Hazina.LLMs.Client](../../LLMs/Hazina.LLMs.Client/README.md) - LLM client
