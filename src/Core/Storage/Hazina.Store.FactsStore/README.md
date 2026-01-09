# Hazina.Store.FactsStore

Language-agnostic facts storage for context engineering.

## Overview

`Hazina.Store.FactsStore` provides compact, relevant facts storage for intelligent context retrieval. Facts are short, language-agnostic representations (symbolic or minimal natural language) that can be efficiently retrieved via semantic search, metadata filtering, or direct lookup.

## Features

- ✅ **Flexible Storage**: Store facts as symbolic (e.g., `building_X_sensors=24`) or minimal NL (e.g., "Building X has 24 sensors")
- ✅ **Multiple Retrieval Modes**: Semantic search (embeddings), metadata filtering, tag-based, type-based
- ✅ **Language Independence**: Facts are conceptual and language-agnostic
- ✅ **SQLite Backend**: Fast, embedded, zero-config database
- ✅ **Batch Operations**: Efficient bulk inserts and queries
- ✅ **Optional Embeddings**: Facts can have embeddings for semantic search, or rely purely on metadata

## Installation

Reference the project:

```xml
<ProjectReference Include="path/to/Hazina.Store.FactsStore/Hazina.Store.FactsStore.csproj" />
```

## Quick Start

### 1. Register the Facts Store

```csharp
services.AddSqliteFactsStore("path/to/facts.db");
```

Or with configuration:

```json
{
  "FactsStore": {
    "DatabasePath": "c:/stores/myapp/facts.db"
  }
}
```

```csharp
services.AddSqliteFactsStore(configuration);
```

### 2. Add Facts

```csharp
var factsStore = serviceProvider.GetRequiredService<IFactsStore>();

var fact = new Fact
{
    Content = "Building X has 24 sensors",
    Type = "numeric",
    Tags = new List<string> { "iot", "sensors", "building" },
    Metadata = new Dictionary<string, string>
    {
        ["entity"] = "building_X",
        ["domain"] = "iot"
    },
    RelevanceScore = 0.95
};

var factId = await factsStore.AddAsync(fact);
```

### 3. Query Facts

**By Tags:**
```csharp
var iotFacts = await factsStore.GetByTagsAsync(new List<string> { "iot", "sensors" });
```

**By Type:**
```csharp
var numericFacts = await factsStore.GetByTypeAsync("numeric");
```

**Complex Query:**
```csharp
var query = new FactQuery
{
    Types = new List<string> { "numeric", "state" },
    Tags = new List<string> { "iot" },
    Metadata = new Dictionary<string, string>
    {
        ["domain"] = "iot"
    },
    TopK = 10
};

var facts = await factsStore.QueryAsync(query);
```

**Semantic Search:**
```csharp
var queryEmbedding = await GenerateEmbeddingAsync("How many sensors?");

var query = new FactQuery
{
    QueryEmbedding = queryEmbedding,
    TopK = 5,
    MinSimilarity = 0.7
};

var facts = await factsStore.QueryAsync(query);
```

## Fact Model

```csharp
public class Fact
{
    public string Id { get; set; }               // Unique identifier
    public string Content { get; set; }          // Short text or symbolic
    public string Type { get; set; }             // "concept", "entity", "rule", "state", etc.
    public Dictionary<string, string> Metadata { get; set; }
    public List<string> Tags { get; set; }
    public DateTime Created { get; set; }
    public DateTime? Updated { get; set; }
    public float[]? Embedding { get; set; }      // Optional for semantic search
    public double RelevanceScore { get; set; }   // 0.0-1.0
}
```

## Fact Types

Recommended fact types:

- **`concept`**: Conceptual knowledge (e.g., "Modbus is an industrial protocol")
- **`entity`**: Entity properties (e.g., "Building X is in Zone 3")
- **`rule`**: Business rules (e.g., "Sensors must report every 5 minutes")
- **`state`**: Current state (e.g., "Pipeline is active")
- **`numeric`**: Numeric facts (e.g., "Building X has 24 sensors")
- **`enum`**: Enumerated values (e.g., "Pipeline type is Modbus")

## Performance

- **Index on Type and RelevanceScore**: Fast filtering and sorting
- **JSON Storage for Metadata/Tags**: Flexible schema
- **Batch Operations**: Efficient bulk inserts
- **Embedding Storage as BLOB**: Compact binary format

## Use Cases

### Context Engineering
```csharp
// Add compact facts about the system
await factsStore.AddAsync(new Fact
{
    Content = "User prefers dark mode",
    Type = "state",
    Tags = new List<string> { "user", "preferences" },
    Metadata = new Dictionary<string, string> { ["userId"] = "123" }
});
```

### Multi-Language Support
```csharp
// Store symbolic facts (language-independent)
await factsStore.AddAsync(new Fact
{
    Content = "building_X_sensors=24",
    Type = "numeric",
    Tags = new List<string> { "iot", "sensors" }
});

// LLM translates to target language during context packing
```

### Fact Extraction from Documents
```csharp
// After processing a document, extract and store key facts
var facts = await ExtractFactsAsync(document);
await factsStore.AddBatchAsync(facts);
```

## Thread Safety

`SqliteFactsStore` uses a single `SqliteConnection` and is **not thread-safe** by default. For multi-threaded scenarios, use one of:

1. Register as **Scoped** instead of Singleton
2. Use a connection pool
3. Serialize access with locks

## Extending

To add a new backend (e.g., PostgreSQL, Redis):

1. Implement `IFactsStore`
2. Add extension method in `ServiceCollectionExtensions`

Example:
```csharp
services.AddRedisFactsStore(configuration);
```

## Related

- [`Hazina.AI.ContextEngineering`](../../AI/Hazina.AI.ContextEngineering/README.md) - Full context engineering orchestration
- [`Hazina.Store.EmbeddingStore`](../Hazina.Store.EmbeddingStore/README.md) - Embeddings storage
- [`Hazina.Store.DocumentStore`](../Hazina.Store.DocumentStore/README.md) - Full document storage

## License

Part of the Hazina framework.
