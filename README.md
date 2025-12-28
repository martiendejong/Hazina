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