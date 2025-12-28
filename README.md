# Hazina Repository

Hazina is a collection of agents, tooling, services, demos, and apps for LLM-powered workflows.

## Quick Start
- Restore dependencies: `dotnet restore Hazina.sln`
- Build everything: `dotnet build Hazina.sln`
- Run tests: `dotnet test Hazina.sln` (or per-project test csproj)

## Using the DocumentStore

The DocumentStore is a core component for storing, embedding, and querying documents with semantic search capabilities.

### 1. Instantiating a DocumentStore

```csharp
using Hazina.Store.EmbeddingStore;

// Configure your LLM client (example with OpenAI)
var openAIConfig = new OpenAIConfig("your-api-key");
var llmClient = new OpenAIClientWrapper(openAIConfig);

// Create the required stores
var embeddingStore = new EmbeddingFileStore(@"C:\myproject\data.embed", llmClient);
var textStore = new TextFileStore(@"C:\myproject\documents");
var chunkStore = new ChunkFileStore(@"C:\myproject\data.chunks");
var metadataStore = new DocumentMetadataFileStore(@"C:\myproject\data.metadata");

// Instantiate the DocumentStore
var documentStore = new DocumentStore(
    embeddingStore,
    textStore,
    chunkStore,
    metadataStore,
    llmClient
);
```

### 2. Storing and Embedding Documents

#### Store text content
```csharp
// Store a simple text document
await documentStore.Store(
    "document-id",
    "This is my document content...",
    metadata: new Dictionary<string, string> { { "author", "John Doe" } },
    split: true  // Automatically split into chunks for large documents
);
```

#### Store binary files (PDF, images, etc.)
```csharp
// Store binary content with automatic text extraction
byte[] pdfBytes = File.ReadAllBytes("report.pdf");
await documentStore.Store(
    "report-2024",
    pdfBytes,
    "application/pdf",
    metadata: new Dictionary<string, string> { { "year", "2024" } }
);
```

#### Store from file path
```csharp
// Store directly from a file (auto-detects type and extracts text)
await documentStore.StoreFromFile(
    "my-doc",
    @"C:\documents\myfile.docx",
    metadata: new Dictionary<string, string> { { "department", "Engineering" } }
);
```

#### Update embeddings
```csharp
// Generate embeddings for a specific document
await documentStore.Embed("document-id");

// Or update all embeddings in the store
await documentStore.UpdateEmbeddings();
```

### 3. Running Relevancy Queries

#### Get relevant document IDs
```csharp
// Find documents relevant to a query (returns document IDs)
var relevantDocs = await documentStore.RelevantItems(
    "What are the best practices for API design?"
);

foreach (var docId in relevantDocs)
{
    var content = await documentStore.Get(docId);
    Console.WriteLine($"Document: {docId}");
    Console.WriteLine(content);
}
```

#### Get detailed embeddings with similarity scores
```csharp
// Get embeddings with similarity scores and metadata
var embeddings = await documentStore.Embeddings(
    "machine learning algorithms"
);

foreach (var embedding in embeddings)
{
    Console.WriteLine($"Similarity: {embedding.Similarity:F3}");
    Console.WriteLine($"Document: {embedding.Document.Key}");

    // Get the actual text content
    var text = await embedding.GetText(embedding.Document.Key);
    Console.WriteLine($"Content: {text}");
    Console.WriteLine($"Parent Document: {embedding.ParentDocumentKey}");
}
```

### 4. Retrieving Documents

#### Get document content
```csharp
// Retrieve full document content
string content = await documentStore.Get("document-id");
```

#### Get specific chunk
```csharp
// Retrieve a specific chunk by its key
string chunkContent = await documentStore.GetChunk("document-id chunk 0");
```

#### Get document with all chunks and metadata
```csharp
// Get complete document information
var docWithChunks = await documentStore.GetDocumentWithChunks("document-id");

Console.WriteLine($"Document: {docWithChunks.Key}");
Console.WriteLine($"Content: {docWithChunks.Content}");
Console.WriteLine($"Metadata: {docWithChunks.Metadata.MimeType}");
Console.WriteLine($"Chunks: {string.Join(", ", docWithChunks.ChunkKeys)}");
```

#### Get metadata only
```csharp
// Retrieve document metadata
var metadata = await documentStore.GetMetadata("document-id");
Console.WriteLine($"Created: {metadata.Created}");
Console.WriteLine($"Size: {metadata.Size} bytes");
Console.WriteLine($"Type: {metadata.MimeType}");
```

### 5. Updating Documents

#### Move/rename a document
```csharp
// Move or rename a document
await documentStore.Move("old-document-id", "new-document-id", split: true);
```

#### Remove a document
```csharp
// Remove document and all its chunks/embeddings
await documentStore.Remove("document-id");
```

### 6. Listing and Navigation

#### List all documents
```csharp
// List all documents (non-recursive, root level only)
var allDocs = await documentStore.List();

// List all documents recursively
var allDocsRecursive = await documentStore.List(recursive: true);

// List documents in a specific folder
var folderDocs = await documentStore.List("projects/2024", recursive: false);
```

#### Get hierarchical tree
```csharp
// Get a tree structure of all documents
var tree = await documentStore.Tree();
foreach (var node in tree)
{
    Console.WriteLine($"- {node.Value}");
}
```

### Example: Complete Workflow

```csharp
// 1. Setup
var config = new OpenAIConfig("sk-...");
var llm = new OpenAIClientWrapper(config);
var store = new DocumentStore(
    new EmbeddingFileStore(@"C:\data\docs.embed", llm),
    new TextFileStore(@"C:\data\docs"),
    new ChunkFileStore(@"C:\data\docs.chunks"),
    new DocumentMetadataFileStore(@"C:\data\docs.metadata"),
    llm
);

// 2. Add documents
await store.StoreFromFile("readme", @"C:\projects\README.md");
await store.StoreFromFile("guide", @"C:\projects\GUIDE.pdf");
await store.Store("notes", "Important notes about the project...");

// 3. Update embeddings
await store.UpdateEmbeddings();

// 4. Query
var results = await store.Embeddings("How do I configure the application?");
foreach (var result in results.Take(3))
{
    Console.WriteLine($"\nRelevance: {result.Similarity:P1}");
    Console.WriteLine($"From: {result.ParentDocumentKey ?? result.Document.Key}");

    var text = await result.GetText(result.Document.Key);
    Console.WriteLine($"Content: {text.Substring(0, Math.Min(200, text.Length))}...");
}

// 5. List all stored documents
var docs = await store.List(recursive: true);
Console.WriteLine($"\nTotal documents: {docs.Count}");
```

## Using AgentFactory and AgentManager

The AgentFactory and AgentManager provide high-level orchestration for creating and managing AI agents with tool capabilities.

### 1. Setting Up AgentManager

```csharp
// Initialize AgentManager with configuration files
var agentManager = new AgentManager(
    storesJsonPath: "stores.hazina",
    agentsJsonPath: "agents.hazina",
    flowsJsonPath: "flows.hazina",
    openAIApiKey: "sk-...",
    logFilePath: @"C:\logs\hazina.log",
    googleProjectId: "" // Optional, for BigQuery
);

// Load all stores and agents from config
await agentManager.LoadStoresAndAgents();
```

### 2. Making Requests to Agents

#### Simple request (returns string only)
```csharp
var result = await agentManager.SendMessage(
    "Explain how authentication works",
    cancellationToken,
    agentName: "my_agent" // Optional, uses first agent if not specified
);

Console.WriteLine(result); // The response text
```

#### Request with full token tracking
```csharp
// Get the agent directly for access to token usage
var agent = agentManager.GetAgent("my_agent");

var response = await agent.Generator.GetResponse<IsReadyResult>(
    "What files are in the project?",
    cancellationToken,
    agentManager.History,  // Conversation history
    addRelevantDocuments: true,
    addFilesList: true,
    agent.Tools,
    images: null
);

// Access the response
Console.WriteLine($"Response: {response.Result.Message}");

// Access token usage
Console.WriteLine($"Input tokens: {response.TokenUsage.InputTokens}");
Console.WriteLine($"Output tokens: {response.TokenUsage.OutputTokens}");
Console.WriteLine($"Total tokens: {response.TokenUsage.TotalTokens}");
Console.WriteLine($"Input cost: ${response.TokenUsage.InputCost:F4}");
Console.WriteLine($"Output cost: ${response.TokenUsage.OutputCost:F4}");
Console.WriteLine($"Total cost: ${response.TokenUsage.TotalCost:F4}");
Console.WriteLine($"Model: {response.TokenUsage.ModelName}");
```

#### Streaming response with token tracking
```csharp
var agent = agentManager.GetAgent("my_agent");

var response = await agent.Generator.StreamResponse(
    "Write a detailed explanation of the codebase",
    cancellationToken,
    onChunkReceived: chunk => Console.Write(chunk), // Handle each streaming chunk
    history: agentManager.History,
    addRelevantDocuments: true,
    addFilesList: true,
    toolsContext: agent.Tools,
    images: null
);

Console.WriteLine($"\n\nStreaming complete. Tokens used: {response.TokenUsage.TotalTokens}");
Console.WriteLine($"Total cost: ${response.TokenUsage.TotalCost:F4}");
```

### 3. Tracking Token Usage Across Multiple Requests

```csharp
var agent = agentManager.GetAgent("my_agent");
var totalUsage = new TokenUsageInfo();

var queries = new[] {
    "List all TypeScript files",
    "Find authentication code",
    "Explain the database schema"
};

foreach (var query in queries)
{
    var response = await agent.Generator.GetResponse(
        query,
        cancellationToken,
        agentManager.History
    );

    // Aggregate token usage using the + operator
    totalUsage += response.TokenUsage;

    Console.WriteLine($"\nQuery: {query}");
    Console.WriteLine($"Tokens: {response.TokenUsage.TotalTokens}");
    Console.WriteLine($"Cost: ${response.TokenUsage.TotalCost:F4}");
}

Console.WriteLine($"\n--- Summary ---");
Console.WriteLine($"Total input tokens: {totalUsage.InputTokens}");
Console.WriteLine($"Total output tokens: {totalUsage.OutputTokens}");
Console.WriteLine($"Total tokens: {totalUsage.TotalTokens}");
Console.WriteLine($"Total cost: ${totalUsage.TotalCost:F4}");
```

### 4. Working with Flows

```csharp
// Execute a flow (sequence of agents)
var flowResult = await agentManager.SendMessage_Flow(
    "Process this document end-to-end",
    cancellationToken,
    flowName: "document_processing_flow"
);

Console.WriteLine($"Flow result: {flowResult}");
```

### 5. Creating Agents Programmatically

```csharp
// Using QuickAgentCreator (via AgentManager)
var creator = agentManager._quickAgentCreator;

// Create a custom agent without registering it
var customAgent = await creator.AgentFactory.CreateUnregisteredAgent(
    name: "temp_agent",
    systemPrompt: "You are a helpful code reviewer",
    stores: new[] { (myDocumentStore, write: false) },
    function: new[] { "git", "dotnet" },
    agents: Array.Empty<string>(),
    flows: Array.Empty<string>(),
    isCoder: false
);

var response = await customAgent.Generator.GetResponse(
    "Review the authentication code",
    cancellationToken
);

Console.WriteLine($"Review: {response.Result}");
Console.WriteLine($"Cost: ${response.TokenUsage.TotalCost:F4}");
```

### 6. Using Typed Responses

```csharp
// Define a custom response type
public class AnalysisResult : ChatResponse<AnalysisResult>
{
    public string Summary { get; set; }
    public List<string> Issues { get; set; }
    public int Severity { get; set; }
}

// Request with typed response
var agent = agentManager.GetAgent("analyzer");
var response = await agent.Generator.GetResponse<AnalysisResult>(
    "Analyze the security of this code",
    cancellationToken,
    agentManager.History
);

// Access typed result
Console.WriteLine($"Summary: {response.Result?.Summary}");
Console.WriteLine($"Issues found: {response.Result?.Issues.Count}");
Console.WriteLine($"Severity: {response.Result?.Severity}");

// Token usage still available
Console.WriteLine($"Analysis cost: ${response.TokenUsage.TotalCost:F4}");
```

### 7. Configuration Files

#### stores.hazina
```
Name: my_code_store
Description: All project source code
Path: C:\Projects\MyApp
FileFilters: *.cs,*.ts,*.js,*.json
SubDirectory:
ExcludePattern: bin,obj,node_modules
```

#### agents.hazina
```
Name: code_assistant
Description: Helps with code questions and modifications
Prompt: You are an expert code assistant. Help the user understand and modify the codebase.
Stores: my_code_store|False
Functions: git,dotnet
CallsAgents:
CallsFlows:
ExplicitModify: False
```

### Token Usage Reference

All LLM responses return `LLMResponse<T>` which contains:
- **Result**: The actual response (string, typed object, etc.)
- **TokenUsage**: `TokenUsageInfo` object with:
  - `InputTokens`: Tokens in the prompt
  - `OutputTokens`: Tokens in the response
  - `TotalTokens`: Sum of input + output
  - `InputCost`: Cost of input tokens (in USD)
  - `OutputCost`: Cost of output tokens (in USD)
  - `TotalCost`: Total cost (in USD)
  - `ModelName`: The model used (e.g., "gpt-4", "claude-3-opus")

### Example: Complete Workflow with Cost Tracking

```csharp
// Setup
var agentManager = new AgentManager(
    "stores.hazina", "agents.hazina", "flows.hazina",
    "sk-...", @"C:\logs\hazina.log"
);
await agentManager.LoadStoresAndAgents();

var agent = agentManager.GetAgent("code_assistant");
var sessionUsage = new TokenUsageInfo();

// Interactive session
var questions = new[] {
    "What's the project structure?",
    "Find all API endpoints",
    "Show me the authentication flow"
};

foreach (var question in questions)
{
    Console.WriteLine($"\nQ: {question}");

    var response = await agent.Generator.GetResponse(
        question,
        CancellationToken.None,
        agentManager.History,
        addRelevantDocuments: true,
        addFilesList: true,
        agent.Tools
    );

    Console.WriteLine($"A: {response.Result}");
    Console.WriteLine($"   Tokens: {response.TokenUsage.TotalTokens} | Cost: ${response.TokenUsage.TotalCost:F4}");

    sessionUsage += response.TokenUsage;
}

// Session summary
Console.WriteLine("\n=== Session Summary ===");
Console.WriteLine($"Questions asked: {questions.Length}");
Console.WriteLine($"Total tokens: {sessionUsage.TotalTokens:N0}");
Console.WriteLine($"Total cost: ${sessionUsage.TotalCost:F4}");
Console.WriteLine($"Model: {sessionUsage.ModelName}");
```

## Solution Organization

The solution is organized into logical categories to improve navigation and maintainability.

**Architecture Diagram**: See [solution-architecture.drawio](solution-architecture.drawio) for a visual representation of how the categories relate to each other.

### 1. Agents
Core agent orchestration, factory patterns, and content generation engines.
- Hazina.AgentFactory
- Hazina.DynamicAPI
- Hazina.Generator

### 2. LLM Core
Foundation libraries for LLM interactions, including client abstractions, data contracts, and tool definitions.
- Hazina.LLMClientTools
- Hazina.LLMs.Classes
- Hazina.LLMs.Client
- Hazina.LLMs.Helpers
- Hazina.LLMs.Tools

### 3. LLM Providers
Concrete integrations with AI service providers and adapters.
- Hazina.LLMs.Anthropic
- Hazina.LLMs.Gemini
- Hazina.LLMs.HuggingFace
- Hazina.LLMs.Mistral
- Hazina.LLMs.OpenAI
- Hazina.LLMs.SemanticKernel

### 4. Storage & UI
Data persistence layers and shared user interface components.
- Hazina.Store.DocumentStore
- Hazina.Store.EmbeddingStore
- Hazina.ChatShared

### 5. Tools Foundation
Core utilities, extension methods, base types, and fundamental tool infrastructure.
- Hazina.Tools.AI.Agents
- Hazina.Tools.Core
- Hazina.Tools.Data
- Hazina.Tools.Extensions
- Hazina.Tools.Models
- Hazina.Tools.TextExtraction

### 6. Tools Common
Shared infrastructure and models used across multiple tool categories.
- Hazina.Tools.Common.Infrastructure.AspNetCore
- Hazina.Tools.Common.Models

### 7. Services - Core
Essential services for chat, embeddings, prompts, and storage operations.
- Hazina.Tools.Services
- Hazina.Tools.Services.Chat
- Hazina.Tools.Services.Embeddings
- Hazina.Tools.Services.Prompts
- Hazina.Tools.Services.Store

### 8. Services - Data
Data processing, ingestion, and file operation services.
- Hazina.Tools.Services.BigQuery
- Hazina.Tools.Services.ContentRetrieval
- Hazina.Tools.Services.DataGathering
- Hazina.Tools.Services.FileOps
- Hazina.Tools.Services.Intake

### 9. Services - Integration
External system connectors and third-party integrations.
- Hazina.Tools.Services.Social
- Hazina.Tools.Services.Web
- Hazina.Tools.Services.WordPress

### 10. Desktop Apps
Windows desktop applications for building, viewing, and managing Hazina workflows.
- Hazina.App.AppBuilder
- Hazina.App.EmbeddingsViewer
- Hazina.App.ExplorerIntegration
- Hazina.App.Windows

### 11. CLI & Web Apps
Command-line tools and web applications.
- Hazina.App.ClaudeCode
- Hazina.App.HtmlMockupGenerator

### 12. Demos
Example applications demonstrating Hazina capabilities.
- Hazina.Demo.Crosslink
- Hazina.Demo.FolderToPostgres
- Hazina.Demo.Llama
- Hazina.Demo.PDFMaker
- Hazina.Demo.Postgres

## Notes
- `.local` projects are developer-specific variants not referenced in the main solution file.
- XML docs are emitted on build under `bin/Debug/net8.0/*.xml` for library projects.