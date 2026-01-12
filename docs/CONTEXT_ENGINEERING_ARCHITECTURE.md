# Context Engineering Architecture

**Date**: 2026-01-09
**Author**: Claude Sonnet 4.5
**Status**: Design Draft

## Overview

The Context Engineering layer provides a language-agnostic, fully configurable framework for intelligent context retrieval and assembly. It sits on top of Hazina's existing RAG infrastructure and orchestrates multiple retrieval strategies to build optimal context for LLM queries.

## Design Principles

1. **Language Independence**: All internal representations are language-agnostic (concepts, tags, embeddings)
2. **Full Configurability**: Every retrieval, scoring, and packing decision is policy-driven
3. **Backwards Compatibility**: Builds on existing Hazina.AI.RAG without breaking changes
4. **Multi-Layer Retrieval**: Combines semantic, metadata, facts, and direct lookup strategies
5. **Policy-Based Fusion**: Configurable weights and scoring for combining retrieval results

## Architecture Layers

```
┌─────────────────────────────────────────────────────────────┐
│              ContextEngineOrchestrator                      │
│  (Main entry point - coordinates all layers)                │
└────────────────────┬────────────────────────────────────────┘
                     │
        ┌────────────┼────────────┐
        │            │            │
        v            v            v
┌───────────┐ ┌─────────────┐ ┌──────────────┐
│ Retrieval │ │   Fusion    │ │   Packing    │
│   Layer   │ │    Layer    │ │    Layer     │
└─────┬─────┘ └──────┬──────┘ └──────┬───────┘
      │              │               │
      v              v               v
┌───────────────────────────────────────────────────┐
│              Configuration Layer                  │
│  (RetrievalPolicy, ScoringPolicy, PackingPolicy)  │
└───────────────────┬───────────────────────────────┘
                    │
                    v
┌───────────────────────────────────────────────────┐
│               Storage Layer                       │
│  (Documents, Embeddings, Facts, Metadata)         │
└───────────────────────────────────────────────────┘
```

## Component Details

### 1. Storage Layer

**Purpose**: Provide unified access to multiple store types

**Components**:
- `IFactsStore` - NEW: Store for compact, relevant facts
- `IDocumentStore` - EXISTING: Full document content (reuse Hazina.Store.DocumentStore)
- `IEmbeddingStore` - EXISTING: Chunked embeddings (reuse Hazina.Store.EmbeddingStore)
- `IMetadataStore` - EXISTING: Titles, tags, metadata (part of DocumentStore)

**New Project**: `Hazina.Store.FactsStore`

**Fact Model**:
```csharp
public class Fact
{
    public string Id { get; set; }
    public string Content { get; set; }  // Short NL sentence or key-value
    public string Type { get; set; }  // "concept", "entity", "rule", "state"
    public Dictionary<string, string> Metadata { get; set; }
    public List<string> Tags { get; set; }
    public DateTime Created { get; set; }
    public DateTime? Updated { get; set; }
}
```

### 2. Retrieval Layer

**Purpose**: Provide multiple retrieval strategies

**Components**:
- `SemanticRetriever` - Uses embeddings for semantic search (extends existing VectorStoreRetriever)
- `MetadataRetriever` - Uses metadata filters (NEW wrapper around existing metadata search)
- `FactRetriever` - NEW: Retrieves from facts store
- `IdLookupRetriever` - NEW: Direct ID-based lookup across all stores

**New Project**: `Hazina.AI.ContextEngineering` (contains all retrievers)

**Interface**:
```csharp
public interface IContextRetriever
{
    Task<List<RetrievalResult>> RetrieveAsync(
        RetrievalQuery query,
        RetrievalPolicy policy,
        CancellationToken ct = default);
}
```

### 3. Fusion Layer

**Purpose**: Combine results from multiple retrievers

**Components**:
- `FusionEngine` - Combines results from multiple retrievers
- `RelevanceScorer` - Applies scoring policies
- `ResultDeduplicator` - Removes duplicate results
- `ResultReranker` - Final re-ranking based on composite score

**Fusion Strategies**:
- **Linear Combination**: `score = w1*semantic + w2*metadata + w3*facts`
- **RRF (Reciprocal Rank Fusion)**: `score = Σ 1/(k + rank_i)`
- **Custom Formula**: User-defined scoring function

### 4. Packing Layer

**Purpose**: Assemble final context from retrieval results

**Components**:
- `ContextPacker` - Assembles context sections
- `SectionFormatter` - Formats different section types
- `TokenBudget manager` - Ensures context fits within limits

**Context Structure**:
```
[FACTS]
- Fact 1
- Fact 2

[METADATA]
Tags: tag1, tag2
Relevant documents: 3

[SEMANTIC CHUNKS]
[Score: 0.92] Content chunk 1...
[Score: 0.87] Content chunk 2...

[QUERY]
User query here
```

### 5. Configuration Layer

**Purpose**: Policy-driven configuration for all components

**Policies**:

**RetrievalPolicy**:
```csharp
public class RetrievalPolicy
{
    public bool SemanticEnabled { get; set; } = true;
    public int SemanticTopK { get; set; } = 8;
    public double SemanticWeight { get; set; } = 0.6;

    public bool MetadataEnabled { get; set; } = true;
    public double MetadataWeight { get; set; } = 0.3;

    public bool FactsEnabled { get; set; } = true;
    public int FactsTopK { get; set; } = 5;
    public double FactsWeight { get; set; } = 0.1;

    public Dictionary<string, string>? MetadataFilters { get; set; }
}
```

**ScoringPolicy**:
```csharp
public class ScoringPolicy
{
    public string Formula { get; set; } = "(0.6*semantic) + (0.3*metadata) + (0.1*facts)";
    public double Threshold { get; set; } = 0.45;
    public bool UseTagBoost { get; set; } = true;
    public double TagBoostPower { get; set; } = 1.2;
}
```

**PackingPolicy**:
```csharp
public class PackingPolicy
{
    public int MaxTokens { get; set; } = 12000;
    public List<string> Sections { get; set; } = ["facts", "metadata", "chunks", "query"];
    public string TargetLanguage { get; set; } = "auto";
}
```

### 6. Orchestration

**ContextEngineOrchestrator**:
```csharp
public class ContextEngineOrchestrator
{
    public async Task<ContextResult> BuildContextAsync(
        string query,
        ContextEngineConfig config,
        CancellationToken ct = default)
    {
        // 1. Retrieve from multiple sources
        var retrievalResults = await ParallelRetrieveAsync(query, config.RetrievalPolicy);

        // 2. Fuse and score
        var fusedResults = await _fusionEngine.FuseAsync(retrievalResults, config.ScoringPolicy);

        // 3. Pack into context
        var context = await _packer.PackAsync(fusedResults, config.PackingPolicy);

        return context;
    }
}
```

## Project Structure

```
src/
├── Core/
│   ├── AI/
│   │   ├── Hazina.AI.RAG/                    (EXISTING - unchanged)
│   │   └── Hazina.AI.ContextEngineering/     (NEW)
│   │       ├── Interfaces/
│   │       │   ├── IContextRetriever.cs
│   │       │   ├── IFusionEngine.cs
│   │       │   └── IContextPacker.cs
│   │       ├── Retrieval/
│   │       │   ├── SemanticRetriever.cs
│   │       │   ├── MetadataRetriever.cs
│   │       │   ├── FactRetriever.cs
│   │       │   └── IdLookupRetriever.cs
│   │       ├── Fusion/
│   │       │   ├── FusionEngine.cs
│   │       │   ├── RelevanceScorer.cs
│   │       │   └── ResultReranker.cs
│   │       ├── Packing/
│   │       │   ├── ContextPacker.cs
│   │       │   └── SectionFormatter.cs
│   │       ├── Configuration/
│   │       │   ├── RetrievalPolicy.cs
│   │       │   ├── ScoringPolicy.cs
│   │       │   ├── PackingPolicy.cs
│   │       │   └── ContextEngineConfig.cs
│   │       ├── Orchestration/
│   │       │   └── ContextEngineOrchestrator.cs
│   │       └── Extensions/
│   │           └── ServiceCollectionExtensions.cs
│   └── Storage/
│       └── Hazina.Store.FactsStore/          (NEW)
│           ├── Interfaces/
│           │   └── IFactsStore.cs
│           ├── Models/
│           │   ├── Fact.cs
│           │   └── FactQuery.cs
│           ├── Implementations/
│           │   └── SqliteFactsStore.cs
│           └── Extensions/
│               └── ServiceCollectionExtensions.cs
```

## Implementation Phases

### Phase 1: Storage Layer (Feature 1)
- ✅ Create Hazina.Store.FactsStore project
- ✅ Implement IFactsStore interface
- ✅ Implement SqliteFactsStore
- ✅ Add DI extensions
- ✅ Create PR

### Phase 2: Retrieval + Fusion (Feature 2)
- Create Hazina.AI.ContextEngineering project
- Implement IContextRetriever interface
- Implement 4 retriever types
- Implement FusionEngine
- Create PR

### Phase 3: Configuration (Feature 3)
- Implement policy classes
- Add JSON serialization
- Add validation
- Create PR

### Phase 4: Packing + Orchestration (Feature 4)
- Implement ContextPacker
- Implement ContextEngineOrchestrator
- Add integration tests
- Create PR

### Phase 5: Tests + Documentation (Feature 5)
- Write unit tests for all components
- Write integration tests
- Write comprehensive documentation
- Create PR

## Language Independence Strategy

### Internal Representation
- Use concept tags: `concept:sensor_pipeline`, `entity:building_X`
- Store facts as symbolic or minimal NL
- Metadata keys are language-neutral
- Embeddings use multilingual models (or separate per language)

### Output Layer
- LLM translates final context to target language
- Configuration specifies `targetLanguage: "auto"` or specific locale
- Packing layer passes through symbolic content unchanged

## Backwards Compatibility

- Existing `RAGEngine` continues to work unchanged
- `ContextEngineOrchestrator` can wrap `RAGEngine` as semantic retriever
- All new components are opt-in
- Zero breaking changes to existing interfaces

## Example Usage

```csharp
// Configure
var config = new ContextEngineConfig
{
    RetrievalPolicy = new RetrievalPolicy
    {
        SemanticEnabled = true,
        SemanticTopK = 8,
        SemanticWeight = 0.6,

        MetadataEnabled = true,
        MetadataWeight = 0.3,

        FactsEnabled = true,
        FactsTopK = 5,
        FactsWeight = 0.1
    },
    ScoringPolicy = ScoringPolicy.Default,
    PackingPolicy = new PackingPolicy
    {
        MaxTokens = 12000,
        Sections = ["facts", "metadata", "chunks", "query"],
        TargetLanguage = "auto"
    }
};

// Use
var orchestrator = serviceProvider.GetRequiredService<ContextEngineOrchestrator>();
var result = await orchestrator.BuildContextAsync("How do I configure sensors?", config);

Console.WriteLine(result.AssembledContext);
// Output:
// [FACTS]
// - Building X has 24 sensors
// - Sensor pipeline type is Modbus
//
// [METADATA]
// Tags: iot, sensors, configuration
// ...
```

## Dependencies

- Hazina.AI.RAG (existing)
- Hazina.Store.EmbeddingStore (existing)
- Hazina.Store.DocumentStore (existing)
- Hazina.Store.FactsStore (new)
- Microsoft.Extensions.Configuration.Abstractions
- Microsoft.Extensions.DependencyInjection

## Testing Strategy

- Unit tests for each retriever
- Unit tests for fusion engine with different policies
- Unit tests for packing with different configurations
- Integration tests for full orchestration
- Performance benchmarks for retrieval latency

## Future Enhancements

- Support for custom retriever plugins
- Machine learning-based fusion (not just weighted)
- Dynamic policy adjustment based on query type
- Caching layer for repeated queries
- Telemetry and observability
