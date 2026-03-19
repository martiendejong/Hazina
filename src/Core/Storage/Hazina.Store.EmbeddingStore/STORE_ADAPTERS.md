# Embedding Store Adapters

This document describes all available embedding store implementations and how to use them.

## Core Interfaces

### IEmbeddingStore
Basic CRUD operations for embeddings:
- `StoreAsync(key, embedding, checksum)` - Store or update an embedding
- `GetAsync(key)` - Retrieve an embedding
- `RemoveAsync(key)` - Delete an embedding
- `ExistsAsync(key)` - Check if an embedding exists

### IEnumerableEmbeddingStore
Adds enumeration capability:
- `GetAllAsync()` - Stream all embeddings (async enumerable)

### IVectorSearchStore
Adds vector similarity search:
- `SearchSimilarAsync(queryEmbedding, topK, minSimilarity)` - Find similar embeddings

### IBatchEmbeddingStore
Adds batch operations for efficiency:
- `StoreBatchAsync(batch)` - Store multiple embeddings atomically
- `GetBatchAsync(keys)` - Retrieve multiple embeddings

## Available Implementations

### 1. EmbeddingJsonFileStore (File-based)
**Location:** `Stores/File/EmbeddingJsonFileStore.cs`

**Features:**
- JSON serialization with atomic writes (write-temp-rename pattern)
- SHA256 checksum validation to prevent corruption
- Thread-safe operations with locking
- Suitable for small to medium datasets (< 10k embeddings)
- Implements: `IEmbeddingStore`, `IEnumerableEmbeddingStore`, `IVectorSearchStore`, `IBatchEmbeddingStore`

**Usage:**
```csharp
var store = new EmbeddingJsonFileStore("path/to/embeddings.json");
await store.StoreAsync("doc1", embedding, checksum);
var results = await store.SearchSimilarAsync(queryEmbedding, topK: 10);
```

**Storage Format:**
- Main file: `embeddings.json`
- Checksum file: `embeddings.json.sha256`
- Temp files during writes: `embeddings.json.tmp`, `embeddings.json.sha256.tmp`

### 2. EmbeddingFileStore (Legacy, File-based)
**Location:** `Stores/File/EmbeddingFileStore.cs`

**Status:** Obsolete - use `EmbeddingJsonFileStore` instead

**Features:**
- Backward compatible with old format
- Atomic writes with checksum validation
- CSV fallback for legacy data

### 3. PgVectorStore (PostgreSQL + pgvector)
**Location:** `Stores/Database/PgVectorStore.cs`

**Features:**
- Production-ready for large datasets (millions of embeddings)
- Native vector similarity search using pgvector extension
- Efficient indexing with HNSW or IVF
- Implements: `IEmbeddingStore`, `IVectorSearchStore`, `IBatchEmbeddingStore`

**Requirements:**
- PostgreSQL 12+ with pgvector extension
- Npgsql NuGet package

**Usage:**
```csharp
var store = new PgVectorStore(connectionString, tableName: "embeddings");
await store.InitializeAsync(); // Creates table and indexes
await store.StoreBatchAsync(largeBatch); // Efficient bulk operations
```

### 4. PgVectorTextEmbeddingStore (PostgreSQL wrapper)
**Location:** `Stores/Database/PgVectorTextEmbeddingStore.cs`

**Features:**
- Wrapper around PgVectorStore for text storage
- Implements legacy `ITextEmbeddingStore` interface

### 5. SqliteTextEmbeddingStore (SQLite)
**Location:** `Stores/Database/SqliteTextEmbeddingStore.cs`

**Features:**
- Lightweight local database
- Good for medium-sized datasets
- Single-file portability

### 6. FaissTextEmbeddingStore (FAISS)
**Location:** `Stores/Faiss/FaissTextEmbeddingStore.cs`

**Features:**
- High-performance vector search using Facebook's FAISS library
- Excellent for read-heavy workloads
- Requires FAISS native binaries

### 7. EmbeddingMemoryStore (In-memory)
**Location:** `Stores/Memory/EmbeddingMemoryStore.cs`

**Features:**
- Fast in-memory storage
- Perfect for testing or temporary data
- No persistence
- Thread-safe

### 8. RedisEmbeddingStore (Redis - Stub)
**Location:** `Stores/Memory/RedisEmbeddingStore.cs`

**Status:** Stub implementation (requires StackExchange.Redis)

**Features (when implemented):**
- Distributed caching
- High-performance access
- Horizontal scalability
- Optional persistence with RediSearch module for vector search

**Implementation Requirements:**
1. Add `StackExchange.Redis` NuGet package
2. Implement methods using Redis commands (HSET, HGET, HDEL)
3. Use RediSearch module for vector similarity search (optional)

**Planned Usage:**
```csharp
var store = new RedisEmbeddingStore("localhost:6379");
await store.StoreAsync("doc1", embedding, checksum);
```

## Helper Services

### EmbeddingCompactionService
**Location:** `Services/EmbeddingCompactionService.cs`

**Features:**
- Removes orphaned embeddings (embeddings without matching documents)
- Verifies integrity of embedding-document links
- Repairs broken links

**Usage:**
```csharp
var compaction = new EmbeddingCompactionService(
    embeddingStore,
    documentExistsCheck: key => documentStore.ExistsAsync(key)
);

// Remove orphaned embeddings
var removed = await compaction.CompactAsync();

// Verify integrity
var result = await compaction.VerifyIntegrityAsync();
Console.WriteLine(result.Message);

// Auto-repair issues
var repaired = await compaction.RepairAsync();
```

### BatchIndexingService
**Location:** `Services/BatchIndexingService.cs`

**Features:**
- Efficient batch indexing with parallel embedding generation
- Progress reporting via `IProgress<BatchIndexProgress>`
- Concurrency control with SemaphoreSlim
- Automatic checksum calculation

**Usage:**
```csharp
var batchService = new BatchIndexingService(
    store,
    generator,
    maxConcurrency: 4
);

var progress = new Progress<BatchIndexProgress>(p =>
    Console.WriteLine(p.ToString())
);

var items = new[] {
    ("doc1", "text content 1"),
    ("doc2", "text content 2"),
    // ... thousands more
};

var indexed = await batchService.BatchIndexAsync(items, progress);
Console.WriteLine($"Indexed {indexed} documents");
```

## Choosing the Right Store

| Store | Dataset Size | Use Case | Pros | Cons |
|-------|-------------|----------|------|------|
| **EmbeddingJsonFileStore** | < 10k | Development, small apps | Simple, portable | Not scalable |
| **EmbeddingMemoryStore** | Any (RAM limited) | Testing, caching | Fast, simple | No persistence |
| **SqliteTextEmbeddingStore** | < 100k | Embedded apps | Single file, portable | Limited concurrency |
| **PgVectorStore** | Millions | Production | Scalable, fast search | Requires PostgreSQL |
| **FaissTextEmbeddingStore** | Millions | Read-heavy | Very fast search | Complex setup |
| **RedisEmbeddingStore** | Any | Distributed systems | Fast, scalable | Requires Redis cluster |

## Migration Path

### From Legacy to New Architecture

If you're using the old `ITextEmbeddingStore` interface:

1. Use `LegacyTextEmbeddingStoreAdapter` for backward compatibility:
```csharp
IEmbeddingStore newStore = new EmbeddingJsonFileStore("embeddings.json");
IEmbeddingGenerator generator = new LLMEmbeddingGenerator(llmClient);
ITextEmbeddingStore legacyStore = new LegacyTextEmbeddingStoreAdapter(newStore, generator);
```

2. Gradually migrate to new architecture:
```csharp
// Old way
await legacyStore.StoreEmbedding("key", "text");

// New way (separation of concerns)
var embedding = await generator.GenerateAsync("text");
var checksum = ComputeChecksum("text");
await store.StoreAsync("key", embedding, checksum);

// Or use EmbeddingService for convenience
var service = new EmbeddingService(store, generator);
await service.StoreTextAsync("key", "text");
```

## Atomic Write Pattern

All file-based stores use the write-temp-rename pattern to prevent corruption:

1. Write data to temporary file (`file.json.tmp`)
2. Calculate and write checksum to temporary file (`file.json.sha256.tmp`)
3. Atomically rename temp files to final destination
4. If any step fails, temp files are cleaned up

This ensures that crashes or power failures cannot corrupt the data file.

## Checksum Validation

All stores support SHA256 checksums for data integrity:

- **On Write:** Checksum is calculated and stored alongside data
- **On Read:** Checksum is verified against stored hash
- **Mismatch:** Warning is logged but operation continues (graceful degradation)

Benefits:
- Detect data corruption from disk errors
- Verify cache validity (invalidate when source text changes)
- Enable incremental updates (skip re-indexing unchanged documents)

## Performance Tips

1. **Use batch operations** when indexing many documents:
   ```csharp
   await batchStore.StoreBatchAsync(largeBatch); // Single write
   ```

2. **Control concurrency** during embedding generation:
   ```csharp
   var service = new BatchIndexingService(store, generator, maxConcurrency: 4);
   ```

3. **Choose the right store** for your dataset size (see table above)

4. **Use IVectorSearchStore** instead of loading all embeddings:
   ```csharp
   // Bad: Load all embeddings
   var allEmbeddings = enumerableStore.GetAllAsync().ToBlockingEnumerable().ToArray();

   // Good: Search for similar embeddings
   var results = await vectorStore.SearchSimilarAsync(query, topK: 10);
   ```

5. **Enable proper indexing** for PostgreSQL:
   ```sql
   CREATE INDEX ON embeddings USING hnsw (vector vector_cosine_ops);
   ```

## Contributing New Adapters

To add a new store adapter:

1. Implement `IEmbeddingStore` at minimum
2. Optionally implement `IEnumerableEmbeddingStore`, `IVectorSearchStore`, `IBatchEmbeddingStore`
3. Follow atomic write patterns for file-based stores
4. Include comprehensive XML documentation
5. Add unit tests
6. Update this README

See `RedisEmbeddingStore.cs` for a stub example showing the expected structure.
