# Hazina.AI.ContextEngineering

**Language-independent, fully configurable context engineering layer for RAG systems.**

## Overview

`Hazina.AI.ContextEngineering` is a complete end-to-end system for intelligent context assembly. It retrieves information from multiple sources, fuses results intelligently, and packs them into optimally formatted context within token budgets.

### What is Context Engineering?

Context engineering is the practice of systematically assembling the right information for LLM queries. Instead of simple semantic search, it combines:
- **Facts**: Compact, symbolic knowledge (e.g., "building_X_sensors=24")
- **Semantic chunks**: Full-text passages from document search
- **Metadata**: Structured information about documents and entities
- **Direct lookup**: Specific items by ID

The system then fuses these results intelligently and packs them into a formatted context that fits within token budgets.

## Features

### Complete End-to-End System
- ✅ **Storage Layer**: SQLite-based facts store with optional embeddings
- ✅ **Retrieval Layer**: 4 retrievers (semantic, facts, metadata, ID lookup)
- ✅ **Fusion Engine**: 3 strategies (WeightedSum, RRF, MaxScore) with deduplication
- ✅ **Configuration System**: Fully policy-driven with JSON persistence and 7 built-in presets
- ✅ **Packing Layer**: Section-based assembly with token budget management
- ✅ **Orchestration**: Single entry point that coordinates all layers

### Key Capabilities
- ✅ **Language-Agnostic**: Works with any content representation or language
- ✅ **Fully Configurable**: Every decision controlled by policies (retrieval, scoring, packing)
- ✅ **Token Budget Management**: Automatic trimming with priority-based section preservation
- ✅ **Tag & Recency Boosting**: Boost scores for tag matches and recent content
- ✅ **Extensible**: Easy to add custom retrievers, fusion strategies, or section formatters
- ✅ **Tested**: 33 unit tests with 100% pass rate

## Installation

Reference the project:

```xml
<ProjectReference Include="path/to/Hazina.AI.ContextEngineering/Hazina.AI.ContextEngineering.csproj" />
```

## Quick Start

### The Simple Way (Recommended)

Most users should use the **ContextEngineOrchestrator** - the complete end-to-end entry point:

```csharp
// 1. Register services
services.AddContextEngineering();

// 2. Inject and use
public class MyService
{
    private readonly IContextEngine _contextEngine;

    public MyService(IContextEngine contextEngine)
    {
        _contextEngine = contextEngine;
    }

    public async Task<string> GetAnswerAsync(string question)
    {
        // Get enriched context (using default config)
        var context = await _contextEngine.GetContextAsync(question);

        // Use with your LLM
        var answer = await llm.AskAsync(question, context);
        return answer;
    }
}
```

**Output Example:**
```
[FACTS]
- Building X has 24 sensors (score: 0.95)
- Sensor pipeline type is Modbus (score: 0.88)

[METADATA]
Tags: iot, sensors, building-x
Documents: 3 relevant documents found

[CHUNKS]
[Source: semantic] [Score: 0.92] Building X contains 24 temperature sensors...
[Source: semantic] [Score: 0.87] The Modbus protocol is used for communication...

[QUERY]
How many sensors are in Building X?
```

### Using Configuration Presets

```csharp
// Semantic-focused (80% semantic, 20% facts)
var context = await _contextEngine.GetContextAsync(
    "How do I configure sensor pipelines?",
    ContextEngineConfig.SemanticFocused);

// Facts-focused (100% facts, minimal context, 4000 tokens)
var context = await _contextEngine.GetContextAsync(
    "How many sensors?",
    ContextEngineConfig.Compact);

// Tag-focused (strong tag matching boost)
var config = ContextEngineConfig.TagFocused;
config.RetrievalPolicy.Tags = new() { "iot", "sensors" };
var context = await _contextEngine.GetContextAsync(
    "Show IoT documentation",
    config);

// All 7 presets: Default, SemanticFocused, FactsFocused, TagFocused, RecencyFocused, Compact, Comprehensive
```

### Custom Configuration

```csharp
var config = new ContextEngineConfig
{
    Name = "Production Config",
    RetrievalPolicy = new RetrievalPolicy
    {
        SemanticEnabled = true,
        SemanticTopK = 10,
        SemanticWeight = 0.7,
        FactsEnabled = true,
        FactsTopK = 5,
        FactsWeight = 0.3,
        Tags = new() { "production", "api" }
    },
    ScoringPolicy = new ScoringPolicy
    {
        Strategy = FusionStrategy.WeightedSum,
        UseTagBoost = true,
        TagBoostPower = 1.5,
        UseRecencyBoost = true,
        RecencyMaxAgeDays = 7
    },
    PackingPolicy = new PackingPolicy
    {
        MaxTokens = 12000,
        Sections = new() { "facts", "chunks", "query" },
        TrimToFit = true,
        TrimPriority = new() { "query", "facts", "chunks" }
    },
    FinalTopK = 10
};

// Validate before use
var errors = config.Validate();
if (!errors.Any())
{
    // Save for reuse
    config.ToFile("production-config.json");

    // Use it
    var context = await _contextEngine.GetContextAsync(query, config);
}
```

## Advanced Usage

### Using Individual Retrievers

```csharp
// Facts retriever
var factRetriever = serviceProvider.GetRequiredService<FactRetriever>();
var factResults = await factRetriever.RetrieveAsync(new RetrievalQuery
{
    QueryText = "sensor configuration",
    Tags = new List<string> { "iot", "sensors" },
    TopK = 5
});

// Semantic retriever
var semanticRetriever = serviceProvider.GetRequiredService<SemanticRetriever>();
var semanticResults = await semanticRetriever.RetrieveAsync(new RetrievalQuery
{
    QueryText = "How do I configure the sensor pipeline?",
    TopK = 8,
    MinSimilarity = 0.7
});

// Metadata retriever
var metadataRetriever = serviceProvider.GetRequiredService<MetadataRetriever>();
var metadataResults = await metadataRetriever.RetrieveAsync(new RetrievalQuery
{
    QueryText = "sensor",
    MetadataFilters = new Dictionary<string, string> { ["domain"] = "iot" },
    TopK = 5
});

// ID lookup retriever
var idRetriever = serviceProvider.GetRequiredService<IdLookupRetriever>();
var idResults = await idRetriever.RetrieveAsync(new RetrievalQuery
{
    Ids = new List<string> { "fact-123", "doc-456" }
});
```

### 3. Use Fusion Engine

```csharp
var fusionEngine = serviceProvider.GetRequiredService<FusionEngine>();

// Retrieve from multiple sources
var retrieverResults = new Dictionary<string, List<RetrievalResult>>
{
    ["semantic"] = semanticResults,
    ["facts"] = factResults,
    ["metadata"] = metadataResults
};

// Configure weights
var weights = new Dictionary<string, double>
{
    ["semantic"] = 0.6,
    ["facts"] = 0.3,
    ["metadata"] = 0.1
};

// Fuse results
var fused = fusionEngine.Fuse(
    retrieverResults,
    weights,
    FusionStrategy.WeightedSum,
    topK: 10
);

foreach (var result in fused)
{
    Console.WriteLine($"[{result.Source}] [{result.Score:F2}] {result.Content}");
}
```

## Retrievers

### FactRetriever
Retrieves compact facts from the facts store.

**Best for**: Quick, relevant facts (e.g., "Building X has 24 sensors")

**Configuration**:
```csharp
var query = new RetrievalQuery
{
    QueryText = "sensor count",
    Types = new List<string> { "numeric", "state" },
    Tags = new List<string> { "iot" },
    TopK = 5
};
```

### SemanticRetriever
Uses embedding-based vector search.

**Best for**: Finding semantically related content

**Configuration**:
```csharp
var query = new RetrievalQuery
{
    QueryText = "How do I configure the sensor pipeline?",
    TopK = 8,
    MinSimilarity = 0.7
};
```

### MetadataRetriever
Uses metadata filtering and keyword search.

**Best for**: Filtering by document properties (type, tags, custom metadata)

**Configuration**:
```csharp
var query = new RetrievalQuery
{
    QueryText = "sensor",
    MetadataFilters = new Dictionary<string, string>
    {
        ["domain"] = "iot",
        ["type"] = "configuration"
    },
    Tags = new List<string> { "sensors", "pipeline" },
    TopK = 5
};
```

### IdLookupRetriever
Direct ID-based lookup.

**Best for**: Retrieving specific known items

**Configuration**:
```csharp
var query = new RetrievalQuery
{
    Ids = new List<string> { "fact-123", "doc-456", "chunk-789" }
};
```

## Fusion Strategies

### WeightedSum
Combines scores with configured weights:
```
final_score = w1 * score1 + w2 * score2 + w3 * score3
```

**Use when**: You have clear preferences for retriever importance

### Reciprocal Rank Fusion (RRF)
Combines based on ranks, not scores:
```
final_score = Σ(weight / (k + rank))
```

**Use when**: Different retrievers use incompatible scoring scales

### MaxScore
Takes the maximum score across all retrievers.

**Use when**: You want the highest confidence match from any source

## Example: Complete Retrieval Pipeline

```csharp
public class ContextRetriever
{
    private readonly FactRetriever _factRetriever;
    private readonly SemanticRetriever _semanticRetriever;
    private readonly MetadataRetriever _metadataRetriever;
    private readonly FusionEngine _fusionEngine;

    public async Task<List<RetrievalResult>> RetrieveContextAsync(string query)
    {
        // 1. Retrieve from all sources in parallel
        var retrievalTasks = new[]
        {
            RetrieveFacts(query),
            RetrieveSemantic(query),
            RetrieveMetadata(query)
        };

        var results = await Task.WhenAll(retrievalTasks);

        // 2. Organize by source
        var retrieverResults = new Dictionary<string, List<RetrievalResult>>
        {
            ["facts"] = results[0],
            ["semantic"] = results[1],
            ["metadata"] = results[2]
        };

        // 3. Configure fusion
        var weights = new Dictionary<string, double>
        {
            ["semantic"] = 0.6,
            ["facts"] = 0.3,
            ["metadata"] = 0.1
        };

        // 4. Fuse and return
        return _fusionEngine.Fuse(
            retrieverResults,
            weights,
            FusionStrategy.WeightedSum,
            topK: 10
        );
    }

    private async Task<List<RetrievalResult>> RetrieveFacts(string query)
    {
        return await _factRetriever.RetrieveAsync(new RetrievalQuery
        {
            QueryText = query,
            TopK = 5
        });
    }

    private async Task<List<RetrievalResult>> RetrieveSemantic(string query)
    {
        return await _semanticRetriever.RetrieveAsync(new RetrievalQuery
        {
            QueryText = query,
            TopK = 8,
            MinSimilarity = 0.7
        });
    }

    private async Task<List<RetrievalResult>> RetrieveMetadata(string query)
    {
        return await _metadataRetriever.RetrieveAsync(new RetrievalQuery
        {
            QueryText = query,
            TopK = 5
        });
    }
}
```

## Extending: Custom Retrievers

```csharp
public class CustomRetriever : IContextRetriever
{
    public string Name => "custom";

    public async Task<List<RetrievalResult>> RetrieveAsync(
        RetrievalQuery query,
        CancellationToken cancellationToken = default)
    {
        // Your custom retrieval logic
        var results = await YourCustomSearchAsync(query);

        return results.Select(r => new RetrievalResult
        {
            Id = r.Id,
            Content = r.Content,
            Score = r.Relevance,
            Source = Name,
            Type = "custom"
        }).ToList();
    }
}

// Register
services.AddRetriever<CustomRetriever>();
```

## Performance Tips

1. **Parallel Retrieval**: Call retrievers in parallel with `Task.WhenAll`
2. **TopK Tuning**: Retrieve more candidates initially, fuse to smaller final set
3. **Weight Tuning**: Adjust weights based on query type
4. **Caching**: Cache frequent queries at fusion level

## Related

- [`Hazina.Store.FactsStore`](../../Storage/Hazina.Store.FactsStore/README.md) - Facts storage
- [`Hazina.AI.RAG`](../Hazina.AI.RAG/README.md) - Semantic search
- [`Hazina.Store.DocumentStore`](../../Storage/Hazina.Store.DocumentStore/README.md) - Document storage

## Next Steps

This is **Feature 2 of 5** in the Context Engineering layer:
- ✅ Feature 1: Storage Layer
- ✅ Feature 2: Retrieval + Fusion (this)
- ⏳ Feature 3: Configuration Policies
- ⏳ Feature 4: Packing + Orchestration
- ⏳ Feature 5: Tests + Documentation

## License

Part of the Hazina framework.
