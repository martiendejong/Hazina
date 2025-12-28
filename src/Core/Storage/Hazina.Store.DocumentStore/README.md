# Hazina.Store.DocumentStore

Document storage and retrieval system with automatic chunking, embedding, and semantic search capabilities.

## Overview

This library provides a comprehensive document storage solution that:
- Stores documents with automatic text extraction from binary files
- Splits large documents into token-sized chunks
- Generates and stores vector embeddings for semantic search
- Maintains metadata for all documents
- Supports relevancy-based search using cosine similarity
- Tracks relationships between chunks and parent documents

## Quick Start

```bash
dotnet add package Hazina.Store.DocumentStore
# Or as project reference:
dotnet add reference path/to/Hazina.Store.DocumentStore.csproj
```

```csharp
using Hazina.Store.DocumentStore;
using Hazina.Store.EmbeddingStore;

// Setup
var llmClient = new OpenAIClientWrapper(new OpenAIConfig("sk-..."));
var documentStore = new DocumentStore(
    new EmbeddingFileStore(@"C:\data\embeddings.json", llmClient),
    new TextFileStore(@"C:\data\documents"),
    new ChunkFileStore(@"C:\data\chunks.json"),
    new DocumentMetadataFileStore(@"C:\data\metadata"),
    llmClient
);

// Store document
await documentStore.Store("my-doc", "content here", metadata: null, split: true);

// Search
var results = await documentStore.Embeddings("search query");
```

## Key Classes

### `DocumentStore` (Core/DocumentStore.cs:9)

Main orchestrator for all document operations.

**Constructor**:
```csharp
public DocumentStore(
    ITextEmbeddingStore embeddingStore,   // Vector embeddings
    ITextStore textStore,                  // Document text
    IChunkStore chunkStore,                // Chunk relationships
    IDocumentMetadataStore metadataStore,  // Metadata
    ILLMClient llmClient                   // AI client
)
```

**Primary Methods**:

`Store` text:
```csharp
await documentStore.Store(
    "docs/guide.md",
    "# API Guide\nAuthentication...",
    metadata: new Dictionary<string, string> { { "Author", "John" } },
    split: true  // Split into chunks
);
```

`Store` binary:
```csharp
await documentStore.Store(
    "diagrams/auth.png",
    File.ReadAllBytes("auth.png"),
    "image/png",
    metadata: null
);
```

`StoreFromFile`:
```csharp
await documentStore.StoreFromFile("report", @"C:\files\report.pdf", metadata);
```

`Search` with relevancy:
```csharp
var results = await documentStore.Embeddings("authentication flow");
foreach (var r in results.Take(5))
{
    Console.WriteLine($"[{r.Similarity:F2}] {r.ParentDocumentKey}");
    var text = await r.GetText(r.Document.Key);
}
```

`Get` document:
```csharp
var doc = await documentStore.GetDocumentWithChunks("my-doc");
Console.WriteLine($"Chunks: {doc.ChunkKeys.Count}");
```

**What it does**:
1. Creates metadata chunk for each document (searchable)
2. Splits large documents into ~1000 token chunks
3. Generates embeddings for all chunks
4. Maintains parent-child relationships
5. Provides semantic search via cosine similarity

### `BinaryDocumentProcessor` (Processors/BinaryDocumentProcessor.cs:8)

Extracts text and generates AI summaries from binary files.

**Methods**:
```csharp
// Check if MIME type is binary
bool isBinary = processor.IsBinary("image/png");  // true

// Extract text from binary
string text = await processor.ExtractText(pdfBytes, "application/pdf");

// Generate AI summary (uses vision API for images)
string summary = await processor.GenerateSummary(imageBytes, "image/png");
// Returns: "Flowchart showing authentication process with login,
//          validation, and session creation steps..."

// Detect MIME type
string mimeType = processor.DetectMimeType(@"C:\file.docx");
// Returns: "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
```

**Supported Types**:
- **Images** (PNG, JPEG, GIF): Vision API generates detailed descriptions
- **PDFs**: Extracts basic info (full text extraction planned)
- **Text files**: UTF-8 decoding
- **Other**: File signature and size

### `DocumentMetadata` (Models/DocumentMetadata.cs:4)

Stores document metadata.

```csharp
public class DocumentMetadata
{
    public string Id { get; set; }                     // "docs/api-guide.md"
    public string OriginalPath { get; set; }           // "C:\docs\api-guide.md"
    public string MimeType { get; set; }               // "text/markdown"
    public long Size { get; set; }                     // 12345
    public DateTime Created { get; set; }              // 2024-01-15T10:30:00Z
    public Dictionary<string, string> CustomMetadata;  // { "Author": "John" }
    public bool IsBinary { get; set; }                 // false
    public string? Summary { get; set; }               // AI summary for binaries

    // Convert to searchable text
    public string ToChunkText() { ... }
}
```

**ToChunkText() output**:
```
Document ID: docs/api-guide.md
Original Path: C:\docs\api-guide.md
MIME Type: text/markdown
Size: 12345 bytes
Created: 2024-01-15 10:30:00 UTC
Is Binary: False
Custom Metadata:
  Author: John
  Version: 2.0
```

### `DocumentWithChunks` (Models/DocumentWithChunks.cs:3)

Complete document with all chunks.

```csharp
var doc = await documentStore.GetDocumentWithChunks("my-doc");

Console.WriteLine($"Key: {doc.Key}");
Console.WriteLine($"Size: {doc.Metadata.Size} bytes");
Console.WriteLine($"Chunks: {string.Join(", ", doc.ChunkKeys)}");
// Chunks: my-doc.metadata, my-doc chunk 0, my-doc chunk 1, my-doc chunk 2
```

## Storage Implementations

### ChunkFileStore (Stores/File/ChunkFileStore.cs:4)

File-based chunk-to-document mapping.

```csharp
var store = new ChunkFileStore(@"C:\data\chunks.json");
await store.Store("doc1", new[] { "doc1.metadata", "doc1 chunk 0", "doc1 chunk 1" });

var chunks = await store.Get("doc1");  // Returns all chunk keys
var parent = await store.GetParentDocument("doc1 chunk 0");  // Returns "doc1"
```

**JSON format**:
```json
{
  "doc1.txt": ["doc1.txt.metadata", "doc1.txt chunk 0", "doc1.txt chunk 1"],
  "image.png": ["image.png.metadata", "image.png chunk 0"]
}
```

### DocumentMetadataFileStore (Stores/File/DocumentMetadataFileStore.cs)

File-based metadata storage (one file per document).

```csharp
var store = new DocumentMetadataFileStore(@"C:\data\metadata");
await store.Store("doc1", metadata);

var meta = await store.Get("doc1");
bool exists = await store.Exists("doc1");
await store.Remove("doc1");
```

Creates: `C:\data\metadata\doc1.metadata.json`

### Memory Stores

For testing or temporary use:
```csharp
var chunkStore = new ChunkMemoryStore();
var metadataStore = new DocumentMetadataMemoryStore();
```

### PostgreSQL Stores

For production:
```csharp
var chunkStore = new PostgresChunkStore(connectionString);
var metadataStore = new PostgresDocumentMetadataStore(connectionString);
```

## How It Works

### Text Document Flow

```csharp
await documentStore.Store("code/login.cs", sourceCode, metadata, split: true);
```

1. ✓ Creates `DocumentMetadata`
2. ✓ Converts metadata to searchable text via `ToChunkText()`
3. ✓ Embeds metadata: `"code/login.cs.metadata"`
4. ✓ Splits content into ~1000 token chunks
5. ✓ Embeds each chunk: `"code/login.cs chunk 0"`, `"chunk 1"`, etc.
6. ✓ Stores chunk relationships in `ChunkStore`

**Result**: 1 metadata chunk + N content chunks, all searchable

### Binary Document Flow

```csharp
await documentStore.Store("diagram.png", imageBytes, "image/png", metadata);
```

1. ✓ Sends image to vision API
2. ✓ Gets description: "Flowchart showing authentication with login box, validation step..."
3. ✓ Creates metadata with summary
4. ✓ Embeds metadata chunk (includes summary)
5. ✓ Splits and embeds description

**Result**: Image searchable via AI-generated description

### Semantic Search

```csharp
var results = await documentStore.Embeddings("user authentication");
```

1. ✓ Generates query embedding
2. ✓ Calculates cosine similarity with all stored embeddings
3. ✓ Sorts by similarity
4. ✓ Returns with scores and parent info

**Output**:
```
Similarity: 0.92, Chunk: "code/login.cs chunk 0", Parent: "code/login.cs"
Similarity: 0.87, Chunk: "code/login.cs.metadata", Parent: "code/login.cs"
Similarity: 0.84, Chunk: "diagram.png.metadata", Parent: "diagram.png"
```

## Complete Example

```csharp
// 1. Setup
var llmClient = new OpenAIClientWrapper(new OpenAIConfig("sk-..."));
var store = new DocumentStore(
    new EmbeddingFileStore(@"C:\data\embeddings.json", llmClient),
    new TextFileStore(@"C:\data\docs"),
    new ChunkFileStore(@"C:\data\chunks.json"),
    new DocumentMetadataFileStore(@"C:\data\metadata"),
    llmClient
);

// 2. Store documents
await store.StoreFromFile("guide", @"C:\docs\api-guide.md",
    new Dictionary<string, string> { { "Version", "2.0" } });

await store.Store("arch-diagram",
    File.ReadAllBytes(@"C:\diagrams\architecture.png"),
    "image/png",
    new Dictionary<string, string> { { "Type", "Diagram" } });

// 3. Update embeddings
await store.UpdateEmbeddings();

// 4. Search
var results = await store.Embeddings("API authentication endpoints");

foreach (var result in results.Take(3))
{
    Console.WriteLine($"\n[Similarity: {result.Similarity:F3}]");
    Console.WriteLine($"From: {result.ParentDocumentKey}");

    var text = await result.GetText(result.Document.Key);
    Console.WriteLine($"Preview: {text.Substring(0, 150)}...");

    if (result.ParentDocumentKey != null)
    {
        var meta = await store.GetMetadata(result.ParentDocumentKey);
        Console.WriteLine($"Type: {meta.MimeType}, Size: {meta.Size} bytes");
    }
}

// 5. List documents
var all = await store.List(recursive: true);
Console.WriteLine($"\nTotal: {all.Count} documents");

// 6. Remove
await store.Remove("old-doc");
```

## Interfaces

### `IDocumentStore`

Core document operations interface.

**Key methods**: `Store`, `Get`, `GetChunk`, `GetDocumentWithChunks`, `GetMetadata`, `Embed`, `UpdateEmbeddings`, `RelevantItems`, `Embeddings`, `Remove`, `Move`, `List`, `Tree`

### `IChunkStore`

Manages chunk-to-document relationships.

**Methods**: `Store`, `Get`, `Remove`, `ListNames`, `GetParentDocument`

### `IDocumentMetadataStore`

Metadata storage.

**Methods**: `Store`, `Get`, `Remove`, `Exists`

## Dependencies

- **Hazina.Store.EmbeddingStore** - Vector embeddings
- **Hazina.LLMs.Client** - LLM abstraction
- **Hazina.LLMs.Helpers** - DocumentSplitter, TokenCounter
- **MathNet.Numerics** - Vector operations
- **System.Text.Json** - Serialization

## Storage Backends

| Backend | Chunk Store | Metadata Store | Use Case |
|---------|-------------|----------------|----------|
| File | ChunkFileStore | DocumentMetadataFileStore | Dev, small datasets |
| Memory | ChunkMemoryStore | DocumentMetadataMemoryStore | Testing |
| PostgreSQL | PostgresChunkStore | PostgresDocumentMetadataStore | Production |

## See Also

- Root README - Complete usage with AgentManager
- Hazina.Store.EmbeddingStore - Embedding implementations
- Hazina.Generator - Uses DocumentStore for RAG
