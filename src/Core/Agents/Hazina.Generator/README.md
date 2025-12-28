# Hazina.Generator

Document generation engine with automatic RAG (Retrieval-Augmented Generation), providing intelligent responses that leverage document stores for context.

## Overview

This library provides the core generation engine for the Hazina platform:

- **DocumentGenerator**: Main engine for generating responses with automatic RAG
- **Automatic Context Injection**: Injects relevant documents based on semantic similarity
- **Conversation History**: Maintains message history with smart truncation
- **Store Modification**: AI can create, modify, move, and delete documents
- **Structured Responses**: Generate typed JSON responses
- **Streaming Support**: Real-time response streaming
- **Multiple Stores**: Access to read-only and writable document stores
- **Token Budget Management**: Automatically fits relevant documents within token limits

## Quick Start

### Installation

```xml
<ProjectReference Include="..\\path\\to\\Hazina.Generator\\Hazina.Generator.csproj" />
```

### Basic Usage

```csharp
using Hazina.Generator;

// Setup
var llmClient = new OpenAIClientWrapper(new OpenAIConfig("sk-..."));
var documentStore = new DocumentStore(...);  // Your document store
var baseMessages = new List<HazinaChatMessage>
{
    new(HazinaMessageRole.System, "You are a helpful assistant.")
};

var generator = new DocumentGenerator(
    store: documentStore,
    baseMessages: baseMessages,
    client: llmClient,
    readonlyStores: new List<IDocumentStore>()
);

// Simple request (no RAG)
var response = await generator.GetResponse(
    "What is the capital of France?",
    CancellationToken.None,
    history: null,
    addRelevantDocuments: false,
    addFilesList: false
);

Console.WriteLine(response.Result);  // "The capital of France is Paris."

// Request with RAG (automatic context injection)
var ragResponse = await generator.GetResponse(
    "Explain the authentication flow in the codebase",
    CancellationToken.None,
    history: conversationHistory,
    addRelevantDocuments: true,  // Injects relevant documents
    addFilesList: true            // Includes file listing
);

Console.WriteLine(ragResponse.Result);
Console.WriteLine($"Cost: ${ragResponse.TokenUsage.TotalCost:F4}");
```

## Key Classes

### DocumentGenerator

**Location**: `Core/DocumentGenerator.cs:11`

Main generation engine that orchestrates RAG, history management, and LLM interaction.

**Constructor**:
```csharp
public DocumentGenerator(
    IDocumentStore store,              // Writable document store
    List<HazinaChatMessage> baseMessages,  // System prompts
    ILLMClient client,                 // LLM provider
    List<IDocumentStore> readonlyStores    // Additional read-only stores
)
```

**Properties**:
```csharp
public int MaxTokens { get; set; } = 6000;  // Token budget for RAG documents
public EmbeddingMatcher EmbeddingMatcher    // Relevancy matcher
```

**Key Methods**:

**GetResponse** - Generate response with RAG:
```csharp
Task<LLMResponse<string>> GetResponse(
    string message,
    CancellationToken cancel,
    IEnumerable<HazinaChatMessage>? history = null,
    bool addRelevantDocuments = true,
    bool addFilesList = true,
    IToolsContext? toolsContext = null,
    List<ImageData>? images = null)
```

**StreamResponse** - Streaming generation:
```csharp
Task<LLMResponse<string>> StreamResponse(
    string message,
    CancellationToken cancel,
    Action<string> onChunkReceived,
    IEnumerable<HazinaChatMessage>? history = null,
    bool addRelevantDocuments = true,
    bool addFilesList = true,
    IToolsContext? toolsContext = null,
    List<ImageData>? images = null)
```

**GetResponse&lt;T&gt;** - Structured response:
```csharp
Task<LLMResponse<TResponse?>> GetResponse<TResponse>(
    string message,
    CancellationToken cancel,
    ...)
    where TResponse : ChatResponse<TResponse>, new()
```

**UpdateStore** - AI modifies documents:
```csharp
Task<LLMResponse<string>> UpdateStore(
    string message,
    CancellationToken cancel,
    ...)
```

**GetImage** - Generate image:
```csharp
Task<LLMResponse<HazinaGeneratedImage>> GetImage(
    string message,
    CancellationToken cancel,
    ...)
```

**Usage Example**:
```csharp
var llmClient = new OpenAIClientWrapper(new OpenAIConfig("sk-..."));
var store = new DocumentStore(...);
var baseMessages = new List<HazinaChatMessage>
{
    new(HazinaMessageRole.System, "You are an expert C# developer.")
};

var generator = new DocumentGenerator(
    store,
    baseMessages,
    llmClient,
    new List<IDocumentStore>()  // No additional stores
);

// Set token budget for RAG
generator.MaxTokens = 8000;

// Simple question
var response1 = await generator.GetResponse(
    "How do I sort a list in C#?",
    CancellationToken.None
);

// With RAG - injects relevant code from store
var history = new List<HazinaChatMessage>();
var response2 = await generator.GetResponse(
    "Find all TODO comments in the authentication code",
    CancellationToken.None,
    history: history,
    addRelevantDocuments: true,  // Search store for auth code
    addFilesList: true
);

// Streaming response
await generator.StreamResponse(
    "Explain the database schema",
    CancellationToken.None,
    chunk => Console.Write(chunk),
    history: history,
    addRelevantDocuments: true
);

// Structured response
public class CodeAnalysis : ChatResponse<CodeAnalysis>
{
    public int TotalFiles { get; set; }
    public List<string> IssuesFound { get; set; }
    public override CodeAnalysis _example => new()
    {
        TotalFiles = 42,
        IssuesFound = new List<string> { "Issue 1", "Issue 2" }
    };
    public override string _signature =>
        "CodeAnalysis(TotalFiles: int, IssuesFound: string[])";
}

var analysis = await generator.GetResponse<CodeAnalysis>(
    "Analyze the codebase for issues",
    CancellationToken.None,
    history: history,
    addRelevantDocuments: true
);

Console.WriteLine($"Files: {analysis.Result?.TotalFiles}");
foreach (var issue in analysis.Result?.IssuesFound ?? new())
{
    Console.WriteLine($"- {issue}");
}
```

### UpdateStoreResponse

**Location**: `Models/UpdateStoreResponse.cs:3`

Structured response for document store modifications.

**Properties**:
```csharp
public List<ModifyDocumentRequest>? Modifications { get; set; }
public List<DeleteDocumentRequest>? Deletions { get; set; }
public List<MoveDocumentRequest>? Moves { get; set; }
public string ResponseMessage { get; set; }
```

**Usage Example**:
```csharp
var response = await generator.UpdateStore(
    "Create a new file called 'notes.txt' with my meeting notes",
    CancellationToken.None,
    history: history,
    addRelevantDocuments: true
);

// The AI generates structured response:
// {
//   "Modifications": [
//     {
//       "Name": "Meeting Notes",
//       "Path": "notes.txt",
//       "Contents": "# Meeting Notes\n\n- Topic 1\n- Topic 2"
//     }
//   ],
//   "ResponseMessage": "I've created notes.txt with your meeting notes."
// }

// Generator automatically applies modifications to the store
Console.WriteLine(response.Result);  // "I've created notes.txt..."
```

## How It Works

### RAG Pipeline

When you call `GetResponse` with `addRelevantDocuments: true`:

1. **Query Construction**: Combines history + base messages + current query
2. **Semantic Search**: Searches all stores for relevant documents
3. **Relevancy Ranking**: Ranks results by cosine similarity
4. **Token Budget**: Selects top documents that fit within `MaxTokens` (default 6000)
5. **Context Injection**: Injects selected documents as Assistant messages
6. **File Listing** (optional): Adds complete file tree
7. **Message Assembly**: Combines: RAG docs → base messages → recent history → current query
8. **LLM Call**: Sends enriched context to LLM
9. **Response**: Returns result with token usage

**Example Flow**:
```
User Query: "How does authentication work?"
    ↓
Semantic Search: Finds 50 chunks related to "authentication"
    ↓
Relevancy Ranking: [0.92, 0.87, 0.84, 0.81, ...]
    ↓
Token Budget (6000): Selects top 8 chunks (~5500 tokens)
    ↓
Context Injection:
  - Assistant: "auth/login.cs chunk 0: class LoginController..."
  - Assistant: "auth/token.cs chunk 2: public async Task..."
  - ... (6 more chunks)
  - Assistant: "Files: auth/login.cs, auth/token.cs, ..."
  - System: "You are an expert developer."
  - User (history): "Tell me about the app"
  - Assistant (history): "This is a web application..."
  - User: "How does authentication work?"
    ↓
LLM generates informed response using injected context
```

### Conversation History Management

```csharp
// History is automatically managed:
// - Takes last 20 messages from history
// - Reserves 3 slots for most recent exchanges
// - Earlier messages: positions 0 to (numMessages - 3)
// - RAG documents inserted after early history
// - Base messages added
// - Recent 3 messages added
// - Current query added last
```

**Message Order**:
```
1. Early conversation history (messages 0 to N-3)
2. RAG documents (relevant chunks)
3. File listing (if requested)
4. Base messages (system prompt)
5. Recent conversation history (last 3 messages)
6. Current query
```

### Store Modification

When you call `UpdateStore`, the AI can modify the document store:

```csharp
var response = await generator.UpdateStore(
    "Rename 'old.txt' to 'archive/old.txt' and create 'new.txt'",
    CancellationToken.None,
    history: history
);
```

**AI generates**:
```json
{
  "Moves": [
    { "Path": "old.txt", "NewPath": "archive/old.txt" }
  ],
  "Modifications": [
    {
      "Name": "New File",
      "Path": "new.txt",
      "Contents": "New content here"
    }
  ],
  "Deletions": null,
  "ResponseMessage": "I've moved old.txt to the archive folder and created new.txt."
}
```

**Generator automatically executes**:
1. Moves: `await Store.Move("old.txt", "archive/old.txt", false)`
2. Modifications: `await Store.Store("new.txt", "New content here", null, false)`
3. Deletions: (none in this example)

## Usage Examples

### Basic RAG Query

```csharp
var generator = new DocumentGenerator(store, baseMessages, llmClient, readonlyStores);

var response = await generator.GetResponse(
    "What security measures are in place?",
    CancellationToken.None,
    history: null,
    addRelevantDocuments: true,  // Searches for security-related docs
    addFilesList: true
);

Console.WriteLine(response.Result);
// "Based on the codebase, the security measures include:
//  1. JWT token authentication (auth/jwt.cs)
//  2. Password hashing with bcrypt (auth/hash.cs)
//  3. Rate limiting on API endpoints (middleware/ratelimit.cs)
//  ..."

Console.WriteLine($"Tokens: {response.TokenUsage.TotalTokens}");
Console.WriteLine($"Cost: ${response.TokenUsage.TotalCost:F4}");
```

### Multi-Turn Conversation with RAG

```csharp
var history = new List<HazinaChatMessage>();

// Turn 1
var response1 = await generator.GetResponse(
    "What files handle user authentication?",
    CancellationToken.None,
    history: history,
    addRelevantDocuments: true
);

history.Add(new HazinaChatMessage(HazinaMessageRole.User,
    "What files handle user authentication?"));
history.Add(new HazinaChatMessage(HazinaMessageRole.Assistant,
    response1.Result));

Console.WriteLine(response1.Result);
// "The authentication is handled by:
//  - auth/login.cs
//  - auth/token.cs
//  - auth/middleware.cs"

// Turn 2 - has context from turn 1
var response2 = await generator.GetResponse(
    "Show me how the JWT validation works",
    CancellationToken.None,
    history: history,
    addRelevantDocuments: true  // Now searches for JWT + validation
);

history.Add(new HazinaChatMessage(HazinaMessageRole.User,
    "Show me how the JWT validation works"));
history.Add(new HazinaChatMessage(HazinaMessageRole.Assistant,
    response2.Result));

Console.WriteLine(response2.Result);
// "The JWT validation is implemented in auth/token.cs:
//  ```csharp
//  public async Task<bool> ValidateToken(string token) { ... }
//  ```"

var totalCost = response1.TokenUsage.TotalCost + response2.TokenUsage.TotalCost;
Console.WriteLine($"Conversation cost: ${totalCost:F4}");
```

### Streaming Response with RAG

```csharp
Console.Write("Response: ");

var response = await generator.StreamResponse(
    "Explain the database migration system",
    CancellationToken.None,
    chunk => Console.Write(chunk),  // Prints in real-time
    history: conversationHistory,
    addRelevantDocuments: true,
    addFilesList: true
);

Console.WriteLine($"\n\nTokens: {response.TokenUsage.TotalTokens}");
Console.WriteLine($"Cost: ${response.TokenUsage.TotalCost:F4}");
```

### Store Modification (AI Edits Files)

```csharp
var response = await generator.UpdateStore(
    "Add a TODO comment to login.cs reminding us to add rate limiting",
    CancellationToken.None,
    history: history,
    addRelevantDocuments: true  // Finds login.cs content
);

// AI generates:
// {
//   "Modifications": [{
//     "Name": "Login Controller",
//     "Path": "auth/login.cs",
//     "Contents": "// TODO: Add rate limiting\npublic class LoginController { ... }"
//   }],
//   "ResponseMessage": "I've added a TODO comment to login.cs."
// }

// Generator automatically updates the file
Console.WriteLine(response.Result);  // "I've added a TODO comment to login.cs."
```

### Multiple Stores (Read-Only + Writable)

```csharp
var codebaseStore = new DocumentStore(...);  // Project code
var docsStore = new DocumentStore(...);      // Documentation
var notesStore = new DocumentStore(...);     // Personal notes (writable)

var generator = new DocumentGenerator(
    store: notesStore,              // Can modify this one
    baseMessages: baseMessages,
    client: llmClient,
    readonlyStores: new List<IDocumentStore>
    {
        codebaseStore,  // Read-only
        docsStore       // Read-only
    }
);

// RAG searches ALL stores
var response = await generator.GetResponse(
    "How do I implement caching? Add notes to my personal store.",
    CancellationToken.None,
    addRelevantDocuments: true
);

// Searches: codebaseStore (for cache code) + docsStore (for cache docs)
// Can only modify: notesStore
```

### Structured Response with RAG

```csharp
public class SecurityAudit : ChatResponse<SecurityAudit>
{
    public int VulnerabilitiesFound { get; set; }
    public List<string> CriticalIssues { get; set; }
    public List<string> Recommendations { get; set; }

    public override SecurityAudit _example => new()
    {
        VulnerabilitiesFound = 3,
        CriticalIssues = new List<string> { "SQL Injection in login.cs" },
        Recommendations = new List<string> { "Use parameterized queries" }
    };

    public override string _signature =>
        "SecurityAudit(VulnerabilitiesFound: int, CriticalIssues: string[], Recommendations: string[])";
}

var audit = await generator.GetResponse<SecurityAudit>(
    "Audit the authentication code for security vulnerabilities",
    CancellationToken.None,
    history: null,
    addRelevantDocuments: true  // Searches for auth code
);

Console.WriteLine($"Found {audit.Result?.VulnerabilitiesFound} vulnerabilities");
foreach (var issue in audit.Result?.CriticalIssues ?? new())
{
    Console.WriteLine($"CRITICAL: {issue}");
}
foreach (var rec in audit.Result?.Recommendations ?? new())
{
    Console.WriteLine($"→ {rec}");
}
```

### Token Budget Control

```csharp
var generator = new DocumentGenerator(store, baseMessages, llmClient, readonlyStores);

// Low budget - fewer docs
generator.MaxTokens = 2000;
var quickResponse = await generator.GetResponse(
    "Summary of the project",
    CancellationToken.None,
    addRelevantDocuments: true
);

// High budget - more comprehensive context
generator.MaxTokens = 12000;
var detailedResponse = await generator.GetResponse(
    "Detailed explanation of the entire architecture",
    CancellationToken.None,
    addRelevantDocuments: true
);
```

### Image Generation

```csharp
var imageResponse = await generator.GetImage(
    "A diagram showing the authentication flow with login, validation, and session creation",
    CancellationToken.None,
    history: null,
    addRelevantDocuments: true  // Can use code context
);

var image = imageResponse.Result;
Console.WriteLine($"Image URL: {image.ImageUri}");

if (image.ImageBytes != null)
{
    await File.WriteAllBytesAsync("auth-flow.png", image.ImageBytes.ToArray());
}
```

## Configuration

### Token Budget

Control how many tokens of context are injected:

```csharp
generator.MaxTokens = 6000;  // Default
generator.MaxTokens = 3000;  // Faster, cheaper, less context
generator.MaxTokens = 12000; // Slower, more expensive, more context
```

### Base Messages

System prompts that appear in every request:

```csharp
var baseMessages = new List<HazinaChatMessage>
{
    new(HazinaMessageRole.System, "You are an expert C# developer."),
    new(HazinaMessageRole.System, "Always provide code examples."),
    new(HazinaMessageRole.System, "Be concise and accurate.")
};

var generator = new DocumentGenerator(store, baseMessages, llmClient, readonlyStores);
```

### RAG Control

Fine-tune RAG behavior per request:

```csharp
// No RAG - just LLM knowledge
var response1 = await generator.GetResponse(
    "What is 2+2?",
    CancellationToken.None,
    addRelevantDocuments: false,
    addFilesList: false
);

// RAG only - no file listing
var response2 = await generator.GetResponse(
    "Explain the auth code",
    CancellationToken.None,
    addRelevantDocuments: true,
    addFilesList: false
);

// Full context - RAG + file listing
var response3 = await generator.GetResponse(
    "What files are in the project and what do they do?",
    CancellationToken.None,
    addRelevantDocuments: true,
    addFilesList: true
);
```

## Dependencies

- **Hazina.Store.DocumentStore**: Document storage and retrieval
- **Hazina.Store.EmbeddingStore**: Embedding matching (`EmbeddingMatcher`)
- **Hazina.LLMs.Client**: LLM abstraction (`ILLMClient`)
- **Hazina.LLMs.Classes**: Message and response types
- **OpenAI**: OpenAI SDK (can be swapped for other providers)

## See Also

- [Hazina.AgentFactory](../Hazina.AgentFactory/README.md) - Uses DocumentGenerator for agents
- [Hazina.Store.DocumentStore](../../Storage/Hazina.Store.DocumentStore/README.md) - Document storage
- [Hazina.LLMs.Client](../../LLMs/Hazina.LLMs.Client/README.md) - LLM abstraction
- [Hazina.LLMs.OpenAI](../../LLMs.Providers/Hazina.LLMs.OpenAI/README.md) - OpenAI provider
