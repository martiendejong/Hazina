# Hazina.Store.EmbeddingStore

A comprehensive embedding storage system with support for multiple backends including PostgreSQL with pgvector, SQLite, FAISS, in-memory, and file-based storage. Provides unified interfaces for embedding storage, vector similarity search, and batch operations.

## Overview

The EmbeddingStore library provides a modern, refactored architecture for managing text embeddings with proper separation of concerns:

- **Storage**: Persist embeddings across multiple backends (PostgreSQL, SQLite, files, memory)
- **Vector Search**: Efficient similarity search using native database features (pgvector) or in-memory algorithms
- **Batch Operations**: Optimized bulk ingestion and retrieval
- **Caching**: Checksum-based caching to avoid regenerating unchanged content
- **Legacy Support**: Backward-compatible adapters for existing code

### Key Features

- Multiple storage backends with consistent API
- Native vector search with pgvector (10-100x faster than in-memory)
- Automatic schema initialization for database backends
- Thread-safe operations
- Checksum-based caching
- Batch operations support
- Factory pattern for easy instantiation

## Quick Start

### Installation

Add a reference to the project:

```xml
<ProjectReference Include="..\path\to\Hazina.Store.EmbeddingStore\Hazina.Store.EmbeddingStore.csproj" />
```

### Basic Usage

```csharp
using Hazina.Store.EmbeddingStore;

// Create an embedding generator (requires an LLM client)
var generator = new LLMEmbeddingGenerator(llmClient, dimensions: 1536);

// Create a store using the factory
var (store, vectorSearch) = EmbeddingStoreFactory.CreateNew(
    "pgvector:Host=localhost;Database=embeddings",
    generator
);

// Create the embedding service
var embeddingService = new EmbeddingService(store, generator);

// Store text (embeddings are generated automatically)
await embeddingService.StoreTextAsync("doc1", "Hello, world!");

// Search for similar content
if (vectorSearch != null)
{
    var queryEmbedding = await embeddingService.GenerateQueryEmbeddingAsync("Hello");
    var results = await vectorSearch.SearchSimilarAsync(queryEmbedding, topK: 5);

    foreach (var result in results)
    {
        Console.WriteLine($"Key: {result.Info.Key}, Similarity: {result.Similarity:F4}");
    }
}
```

### Batch Operations

```csharp
// Store multiple documents efficiently
var documents = new[]
{
    ("doc1", "First document"),
    ("doc2", "Second document"),
    ("doc3", "Third document")
};

int generated = await embeddingService.StoreBatchAsync(documents);
Console.WriteLine($"Generated {generated} new embeddings");
```

## Key Classes

### EmbeddingService

**Location**: `C:\Projects\hazina\src\Core\Storage\Hazina.Store.EmbeddingStore\Core\EmbeddingService.cs`

Orchestration service that combines embedding generation and storage with proper separation of concerns.

**Constructor**:
```csharp
public EmbeddingService(IEmbeddingStore store, IEmbeddingGenerator generator)
```

**Key Methods**:

```csharp
// Store text with automatic embedding generation
public async Task<bool> StoreTextAsync(
    string key,
    string value,
    CancellationToken cancellationToken = default)

// Store pre-computed embedding
public async Task<bool> StoreEmbeddingAsync(
    string key,
    Embedding embedding,
    string checksum,
    CancellationToken cancellationToken = default)

// Batch store with automatic generation
public async Task<int> StoreBatchAsync(
    IEnumerable<(string key, string value)> items,
    CancellationToken cancellationToken = default)

// Generate query embedding without storing
public Task<Embedding> GenerateQueryEmbeddingAsync(
    string query,
    CancellationToken cancellationToken = default)

// Retrieve embedding
public Task<EmbeddingInfo?> GetAsync(
    string key,
    CancellationToken cancellationToken = default)
```

**Usage Example**:
```csharp
var service = new EmbeddingService(store, generator);

// Store with automatic caching
bool wasGenerated = await service.StoreTextAsync("mykey", "content");
if (!wasGenerated)
{
    Console.WriteLine("Used cached embedding (content unchanged)");
}

// Retrieve
var info = await service.GetAsync("mykey");
if (info != null)
{
    Console.WriteLine($"Embedding dimensions: {info.Data.Count}");
}
```

### EmbeddingStoreFactory

**Location**: `C:\Projects\hazina\src\Core\Storage\Hazina.Store.EmbeddingStore\Factories\EmbeddingStoreFactory.cs`

Factory for creating embedding stores with different backends.

**Key Methods**:

```csharp
// Create new architecture store
public static (IEmbeddingStore store, IVectorSearchStore? vectorSearch) CreateNew(
    string embeddingsSpec,
    IEmbeddingGenerator generator)

// Create legacy adapter for backward compatibility
public static ITextEmbeddingStore CreateLegacyAdapter(
    string embeddingsSpec,
    ILLMClient llmClient,
    int dimensions = 1536)

// Register custom store factory
public static void RegisterNew(
    string scheme,
    Func<string, IEmbeddingGenerator, IEmbeddingStore> factory)
```

**Usage Example**:
```csharp
// PostgreSQL with pgvector
var (pgStore, pgSearch) = EmbeddingStoreFactory.CreateNew(
    "pgvector:Host=localhost;Database=mydb;Username=user;Password=pass",
    generator
);

// In-memory store
var (memStore, memSearch) = EmbeddingStoreFactory.CreateNew(
    "memory:",
    generator
);

// File-based store
var (fileStore, fileSearch) = EmbeddingStoreFactory.CreateNew(
    "file:C:\\data\\embeddings.json",
    generator
);

// Register custom backend
EmbeddingStoreFactory.RegisterNew("redis", (spec, gen) =>
    new RedisEmbeddingStore(spec, gen.Dimensions));
```

### PgVectorStore

**Location**: `C:\Projects\hazina\src\Core\Storage\Hazina.Store.EmbeddingStore\Stores\Database\PgVectorStore.cs`

PostgreSQL + pgvector implementation with native vector search support. Implements efficient similarity search using pgvector's distance operators.

**Constructor**:
```csharp
public PgVectorStore(string connectionString, int dimension = 1536)
```

**Key Methods**:

```csharp
// Create vector index for efficient search
public async Task CreateIndexAsync(string indexType = "hnsw", int lists = 100)

// Native vector similarity search (IVectorSearchStore)
public async Task<List<ScoredEmbedding>> SearchSimilarAsync(
    Embedding queryEmbedding,
    int topK = 10,
    double minSimilarity = 0.0,
    CancellationToken cancellationToken = default)

// Batch operations (IBatchEmbeddingStore)
public async Task<int> StoreBatchAsync(
    IEnumerable<(string key, Embedding embedding, string checksum)> batch,
    CancellationToken cancellationToken = default)

public async Task<List<EmbeddingInfo>> GetBatchAsync(
    IEnumerable<string> keys,
    CancellationToken cancellationToken = default)
```

**Usage Example**:
```csharp
var store = new PgVectorStore(
    "Host=localhost;Database=embeddings;Username=admin;Password=secret",
    dimension: 1536
);

// Create HNSW index for fast search (call after bulk ingestion)
await store.CreateIndexAsync("hnsw");

// Store embedding
var embedding = new Embedding(new double[1536]);
await store.StoreAsync("doc1", embedding, "checksum123");

// Native vector search (10-100x faster than loading all and computing in-memory)
var queryEmbedding = new Embedding(new double[1536]);
var results = await store.SearchSimilarAsync(
    queryEmbedding,
    topK: 10,
    minSimilarity: 0.7
);

foreach (var result in results)
{
    Console.WriteLine($"{result.Info.Key}: {result.Similarity:F4}");
}

// Batch store
var batch = new[]
{
    ("doc1", embedding1, "checksum1"),
    ("doc2", embedding2, "checksum2"),
    ("doc3", embedding3, "checksum3")
};
await store.StoreBatchAsync(batch);
```

### EmbeddingMatcher

**Location**: `C:\Projects\hazina\src\Core\Storage\Hazina.Store.EmbeddingStore\Core\EmbeddingMatcher.cs`

Utility class for matching embeddings and managing token limits when working with search results.

**Properties**:
```csharp
public int MaxInputTokens = 8000;
public int MaxQueryTokens = 8000;
public TokenCounter TokenCounter = new TokenCounter();
```

**Key Methods**:

```csharp
// Cut off query to fit token limit
public string CutOffQuery(string query, int maxTokens = 0)

// Take top results while respecting token limits
public async Task<List<string>> TakeTop(
    List<RelevantEmbedding> total,
    int maxTokens = 0)

// Get embeddings with similarity scores
public static List<(double similarity, EmbeddingInfo document)> GetEmbeddingsWithSimilarity(
    Embedding query,
    IEnumerable<EmbeddingInfo> embeddings)
```

**Usage Example**:
```csharp
var matcher = new EmbeddingMatcher
{
    MaxQueryTokens = 4000
};

// Ensure query fits token limit
string query = "Very long query...";
string trimmed = matcher.CutOffQuery(query, maxTokens: 2000);

// Get similarity scores
var embeddings = await store.GetBatchAsync(keys);
var scored = EmbeddingMatcher.GetEmbeddingsWithSimilarity(queryEmbedding, embeddings);

// Take top results respecting token budget
var relevantEmbeddings = scored.Select(s => new RelevantEmbedding
{
    Document = s.document,
    GetText = async key => await textStore.GetAsync(key)
}).ToList();

var topDocuments = await matcher.TakeTop(relevantEmbeddings, maxTokens: 4000);
```

### LLMEmbeddingGenerator

**Location**: `C:\Projects\hazina\src\Core\Storage\Hazina.Store.EmbeddingStore\Generators\LLMEmbeddingGenerator.cs`

Generates embeddings using an LLM client (OpenAI, Anthropic, etc.).

**Constructor**:
```csharp
public LLMEmbeddingGenerator(ILLMClient llmClient, int dimensions = 1536)
```

**Key Methods**:

```csharp
// Generate single embedding
public async Task<Embedding> GenerateAsync(
    string text,
    CancellationToken cancellationToken = default)

// Generate batch of embeddings
public async Task<List<Embedding>> GenerateBatchAsync(
    IEnumerable<string> texts,
    CancellationToken cancellationToken = default)
```

**Usage Example**:
```csharp
var generator = new LLMEmbeddingGenerator(openAIClient, dimensions: 1536);

// Single generation
var embedding = await generator.GenerateAsync("Hello, world!");

// Batch generation (more efficient)
var texts = new[] { "First text", "Second text", "Third text" };
var embeddings = await generator.GenerateBatchAsync(texts);
```

### EmbeddingMemoryStore

**Location**: `C:\Projects\hazina\src\Core\Storage\Hazina.Store.EmbeddingStore\Stores\Memory\EmbeddingMemoryStore.cs`

In-memory embedding store with dictionary-based storage and native vector search.

**Constructor**:
```csharp
public EmbeddingMemoryStore()
```

**Usage Example**:
```csharp
var store = new EmbeddingMemoryStore();

// Store embeddings
await store.StoreAsync("key1", embedding1, "checksum1");
await store.StoreAsync("key2", embedding2, "checksum2");

// Vector search
var results = await store.SearchSimilarAsync(queryEmbedding, topK: 5);

// Batch operations
var batch = new[]
{
    ("key3", embedding3, "checksum3"),
    ("key4", embedding4, "checksum4")
};
await store.StoreBatchAsync(batch);
```

### SqliteTextEmbeddingStore

**Location**: `C:\Projects\hazina\src\Core\Storage\Hazina.Store.EmbeddingStore\Stores\Database\SqliteTextEmbeddingStore.cs`

SQLite-based embedding storage for local/embedded scenarios.

**Constructor**:
```csharp
public SqliteTextEmbeddingStore(string databasePath, ILLMClient provider)
```

**Usage Example**:
```csharp
var store = new SqliteTextEmbeddingStore(
    "C:\\data\\embeddings.db",
    llmClient
);

// Use legacy interface
await store.StoreEmbedding("doc1", "content");
var info = await store.GetEmbedding("doc1");
```

### FaissTextEmbeddingStore

**Location**: `C:\Projects\hazina\src\Core\Storage\Hazina.Store.EmbeddingStore\Stores\Faiss\FaissTextEmbeddingStore.cs`

FAISS-based embedding storage for high-performance similarity search.

**Constructor**:
```csharp
public FaissTextEmbeddingStore(string indexPath, ILLMClient provider)
```

**Usage Example**:
```csharp
var store = new FaissTextEmbeddingStore(
    "C:\\data\\faiss.index",
    llmClient
);

// Store and search using FAISS
await store.StoreEmbedding("doc1", "content");
var embeddings = store.Embeddings;
```

### EmbeddingFileStore / EmbeddingJsonFileStore

**Location**: `C:\Projects\hazina\src\Core\Storage\Hazina.Store.EmbeddingStore\Stores\File\EmbeddingJsonFileStore.cs`

JSON file-based embedding storage for simple persistence.

**Constructor**:
```csharp
public EmbeddingJsonFileStore(string filePath)
```

**Usage Example**:
```csharp
var store = new EmbeddingJsonFileStore("C:\\data\\embeddings.json");

// Store embeddings
await store.StoreAsync("doc1", embedding1, "checksum1");

// Embeddings are persisted to JSON file
var info = await store.GetAsync("doc1");
```

## Core Interfaces

### IEmbeddingStore

Core interface for storing and retrieving embeddings (CRUD operations only).

```csharp
Task<bool> StoreAsync(string key, Embedding embedding, string checksum);
Task<EmbeddingInfo?> GetAsync(string key);
Task<bool> RemoveAsync(string key);
Task<bool> ExistsAsync(string key);
```

### IVectorSearchStore

Interface for vector similarity search operations.

```csharp
Task<List<ScoredEmbedding>> SearchSimilarAsync(
    Embedding queryEmbedding,
    int topK = 10,
    double minSimilarity = 0.0,
    CancellationToken cancellationToken = default
);
```

### IBatchEmbeddingStore

Interface for batch operations.

```csharp
Task<int> StoreBatchAsync(
    IEnumerable<(string key, Embedding embedding, string checksum)> batch,
    CancellationToken cancellationToken = default);

Task<List<EmbeddingInfo>> GetBatchAsync(
    IEnumerable<string> keys,
    CancellationToken cancellationToken = default);
```

### IEmbeddingGenerator

Interface for generating embeddings.

```csharp
int Dimensions { get; }
Task<Embedding> GenerateAsync(string text, CancellationToken cancellationToken = default);
Task<List<Embedding>> GenerateBatchAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default);
```

### ITextEmbeddingStore (Obsolete)

Legacy interface maintained for backward compatibility. Use `IEmbeddingStore` + `IVectorSearchStore` + `IEmbeddingGenerator` instead.

```csharp
Task<bool> StoreEmbedding(string key, string value);
Task<EmbeddingInfo?> GetEmbedding(string key);
Task<bool> RemoveEmbedding(string key);
EmbeddingInfo[] Embeddings { get; }
```

## Usage Examples

### Complete RAG Pipeline

```csharp
// Setup
var llmClient = new OpenAIClient("api-key");
var generator = new LLMEmbeddingGenerator(llmClient, dimensions: 1536);
var (store, vectorSearch) = EmbeddingStoreFactory.CreateNew(
    "pgvector:Host=localhost;Database=rag",
    generator
);
var service = new EmbeddingService(store, generator);

// Index documents
var documents = new[]
{
    ("doc1", "C# is a programming language"),
    ("doc2", "Python is popular for data science"),
    ("doc3", "JavaScript runs in browsers")
};

await service.StoreBatchAsync(documents);

// Create index for fast search
if (store is PgVectorStore pgStore)
{
    await pgStore.CreateIndexAsync("hnsw");
}

// Search
var query = "What programming languages are there?";
var queryEmbedding = await service.GenerateQueryEmbeddingAsync(query);
var results = await vectorSearch!.SearchSimilarAsync(queryEmbedding, topK: 3);

foreach (var result in results)
{
    Console.WriteLine($"[{result.Similarity:F4}] {result.Info.Key}");
}
```

### Migrating from Legacy API

```csharp
// Old code (legacy)
var oldStore = EmbeddingStoreFactory.CreateFromSpec(
    "pgvector:connection-string",
    llmClient
);
await oldStore.StoreEmbedding("key", "value");
var embeddings = oldStore.Embeddings;

// New code (refactored)
var generator = new LLMEmbeddingGenerator(llmClient, 1536);
var (newStore, vectorSearch) = EmbeddingStoreFactory.CreateNew(
    "pgvector:connection-string",
    generator
);
var service = new EmbeddingService(newStore, generator);
await service.StoreTextAsync("key", "value");

// Or use legacy adapter
var adapter = EmbeddingStoreFactory.CreateLegacyAdapter(
    "pgvector:connection-string",
    llmClient,
    1536
);
await adapter.StoreEmbedding("key", "value");
```

### Custom Store Backend

```csharp
// Implement interfaces
public class RedisEmbeddingStore : IEmbeddingStore, IVectorSearchStore
{
    private readonly ConnectionMultiplexer _redis;
    private readonly int _dimension;

    public RedisEmbeddingStore(string connectionString, int dimension)
    {
        _redis = ConnectionMultiplexer.Connect(connectionString);
        _dimension = dimension;
    }

    public async Task<bool> StoreAsync(string key, Embedding embedding, string checksum)
    {
        var db = _redis.GetDatabase();
        var json = JsonSerializer.Serialize(new EmbeddingInfo(key, checksum, embedding));
        return await db.StringSetAsync(key, json);
    }

    // Implement other methods...
}

// Register with factory
EmbeddingStoreFactory.RegisterNew("redis", (spec, gen) =>
    new RedisEmbeddingStore(spec, gen.Dimensions));

// Use it
var (store, search) = EmbeddingStoreFactory.CreateNew(
    "redis:localhost:6379",
    generator
);
```

## Dependencies

- **Hazina.LLMs.Client**: LLM client interface
- **Hazina.LLMs.Helpers**: Embedding, Checksum, and utility classes
- **Npgsql** + **Pgvector**: PostgreSQL with pgvector support
- **Microsoft.Data.Sqlite**: SQLite support
- **System.Text.Json**: JSON serialization

## Architecture

The library follows a clean separation of concerns:

1. **Storage Layer** (`IEmbeddingStore`): Pure CRUD operations
2. **Search Layer** (`IVectorSearchStore`): Similarity search
3. **Generation Layer** (`IEmbeddingGenerator`): Embedding creation
4. **Orchestration Layer** (`EmbeddingService`): Coordinates storage + generation

This design allows:
- Using different storage backends without changing generation logic
- Swapping embedding providers without touching storage code
- Implementing custom search algorithms
- Testing components in isolation

## Performance Tips

1. **Use Batch Operations**: `StoreBatchAsync` is 5-10x faster than individual calls
2. **Create Vector Indexes**: Call `CreateIndexAsync()` after bulk ingestion for 10-100x search speedup
3. **Use Native Search**: pgvector's native search is much faster than loading all embeddings
4. **Cache Wisely**: Checksums prevent regenerating embeddings for unchanged content
5. **Choose Right Backend**:
   - **pgvector**: Best for production, large datasets, concurrent access
   - **FAISS**: Best for read-heavy workloads, advanced indexing
   - **Memory**: Best for testing, small datasets, temporary storage
   - **SQLite**: Best for embedded scenarios, single-user applications
   - **File**: Best for simplicity, version control, debugging

## See Also

- [Hazina.Store.DocumentStore](../../Storage/Hazina.Store.DocumentStore/README.md) - Document storage and retrieval
- [Hazina.LLMs.Client](../../LLMs/Hazina.LLMs.Client/README.md) - LLM client abstraction
- [Hazina.LLMs.Helpers](../../LLMs/Hazina.LLMs.Helpers/README.md) - Embedding and utility classes
- [Hazina.LLMs.OpenAI](../../LLMs.Providers/Hazina.LLMs.OpenAI/README.md) - OpenAI provider implementation
