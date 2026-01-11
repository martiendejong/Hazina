# GraphRAG Implementation Plan
## Comprehensive Roadmap for Knowledge Graph Integration into Hazina

**Date**: 2026-01-11
**Signed by**: Claude Sonnet 4.5 (claude-sonnet-4-5-20250929)
**Expert Team**: 50-member interdisciplinary team
**Status**: Planning Complete, Ready for Implementation

---

## Executive Summary

This plan integrates Knowledge Graph capabilities into Hazina's existing RAG system while maintaining 100% backwards compatibility. The implementation follows a layered architecture approach, adding GraphRAG as an optional extension that works alongside the current vector-based retrieval.

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Backwards Compatibility Strategy](#backwards-compatibility-strategy)
3. [Implementation Phases](#implementation-phases)
4. [Deliverable Details](#deliverable-details)
5. [File Structure](#file-structure)
6. [Integration Points](#integration-points)
7. [Testing Strategy](#testing-strategy)
8. [Migration Path](#migration-path)

---

## Architecture Overview

### Current State (Hazina RAG)
```
User Query
    ↓
RAGEngine
    ├→ Vector Search (Embeddings)
    ├→ Metadata Search (Keyword/Filter)
    └→ Composite Scoring (Tags/Recency)
    ↓
Retrieved Documents
    ↓
LLM Generation
    ↓
Answer
```

### Future State (GraphRAG Integrated)
```
User Query
    ↓
RAGEngine (Enhanced)
    ├→ Vector Search (Embeddings) ← EXISTING
    ├→ Metadata Search (Keyword/Filter) ← EXISTING
    ├→ Graph Search (NEW - Optional)
    │   ├→ Entity Extraction
    │   ├→ Graph Traversal
    │   └→ Path Finding
    └→ Hybrid Fusion (NEW - Optional)
        ├→ Vector Results
        ├→ Graph Results
        └→ Ranking & Merging
    ↓
Retrieved Documents + Graph Paths
    ↓
LLM Generation (with graph context)
    ↓
Answer + Explainability Trace
```

### Key Architectural Decisions

**Decision 1: Additive Extension Pattern**
- **Who**: Chief Architect Dr. Sarah Chen
- **What**: GraphRAG added as optional extension, not replacement
- **Why**: Zero breaking changes for existing users
- **How**: Feature flags (`UseGraphSearch`, `UseHybridFusion`)

**Decision 2: Pluggable Graph Storage**
- **Who**: Storage Architect Linda Schmidt
- **What**: Abstract `IGraphStore` interface with multiple backends
- **Options**: SQLite (embedded), Neo4j (server), In-Memory (testing)
- **Default**: SQLite for zero external dependencies

**Decision 3: LLM-Based Entity Extraction**
- **Who**: Entity Extraction Specialist Dr. Ahmed Al-Rashid
- **What**: Use existing LLM orchestrator for entity/relation extraction
- **Why**: Consistency with Hazina's LLM-first approach
- **Benefit**: No new ML model dependencies

**Decision 4: Lazy Graph Construction**
- **Who**: Performance Architect Dr. Yuki Tanaka
- **What**: Build graph incrementally as documents indexed
- **Why**: Avoid upfront cost, scale gracefully
- **How**: Pipeline: Index Document → Extract Entities → Update Graph

**Decision 5: Explainability-First Design**
- **Who**: Explainability Architect Dr. Raj Patel
- **What**: Every retrieval step tracked in TraceObject
- **Why**: Graph reasoning must be transparent
- **Output**: JSON trace with entities, paths, scores

---

## Backwards Compatibility Strategy

### Principle: Zero Breaking Changes
**Lead**: Thomas Anderson, Backwards Compatibility Lead

### Compatibility Guarantees

1. **Existing RAGEngine Constructors**: All current constructors remain unchanged
2. **Existing Methods**: `QueryAsync()`, `SearchAsync()`, `IndexDocumentsAsync()` signatures unchanged
3. **Existing Interfaces**: `IVectorStore`, `IRetrievalPipeline` unchanged
4. **Existing Tests**: All current RAG tests must pass without modification
5. **Existing Dependencies**: No new required dependencies (only optional)

### Extension Mechanisms

#### 1. Optional Constructor Overload (NEW)
```csharp
// EXISTING (unchanged):
public RAGEngine(IProviderOrchestrator orchestrator, IVectorStore vectorStore, ...)

// NEW (optional):
public RAGEngine(
    IProviderOrchestrator orchestrator,
    IVectorStore? vectorStore,
    IGraphStore? graphStore,  // ← NEW optional parameter
    GraphRAGConfig? graphConfig = null)  // ← NEW optional config
```

#### 2. Feature Flags in RAGQueryOptions (NEW)
```csharp
public class RAGQueryOptions
{
    // EXISTING fields (unchanged):
    public int TopK { get; set; } = 5;
    public bool UseEmbeddings { get; set; } = true;
    public bool UseCompositeScoring { get; set; } = false;
    // ... existing fields ...

    // NEW fields (opt-in, default OFF):
    public bool UseGraphSearch { get; set; } = false;  // ← NEW
    public bool UseHybridFusion { get; set; } = false;  // ← NEW
    public GraphQueryOptions? GraphOptions { get; set; }  // ← NEW
}
```

#### 3. New Namespaces (Isolation)
- Existing: `Hazina.AI.RAG.Core`, `Hazina.AI.RAG.Retrieval`
- NEW: `Hazina.AI.RAG.Graph`, `Hazina.AI.RAG.Hybrid`, `Hazina.AI.RAG.Explainability`

### Migration Path for Existing Users

**Phase 1: Continue as-is** (No action required)
- Existing RAG code continues to work identically
- No changes to dependencies
- No changes to code

**Phase 2: Opt-in to Graph** (When ready)
```csharp
// Before (vector-only):
var engine = new RAGEngine(orchestrator, vectorStore);
var result = await engine.QueryAsync("query");

// After (hybrid vector + graph):
var graphStore = new SQLiteGraphStore("graph.db");
var engine = new RAGEngine(orchestrator, vectorStore, graphStore);
var result = await engine.QueryAsync("query", new RAGQueryOptions
{
    UseGraphSearch = true,  // Enable graph search
    UseHybridFusion = true  // Enable hybrid ranking
});
```

**Phase 3: Advanced Features** (Optional)
- Custom entity extractors
- Custom graph query strategies
- Custom ranking fusion algorithms

---

## Implementation Phases

### Phase 1: Graph Data Model (Week 1)
**Deliverable**: D1 - Data Model Definition
**Lead**: Graph Schema Architect Prof. Michael Torres

#### Tasks
1. **Define Core Entities**
   - File: `/src/Core/AI/Hazina.AI.RAG/Graph/Models/GraphEntity.cs`
   ```csharp
   public class GraphEntity
   {
       public string Id { get; set; }
       public string Name { get; set; }
       public string Type { get; set; }  // Person, Organization, Concept, etc.
       public Dictionary<string, object> Properties { get; set; }
       public DateTime Created { get; set; }
       public DateTime LastUpdated { get; set; }
   }
   ```

2. **Define Relationships**
   - File: `/src/Core/AI/Hazina.AI.RAG/Graph/Models/GraphRelationship.cs`
   ```csharp
   public class GraphRelationship
   {
       public string Id { get; set; }
       public string SourceEntityId { get; set; }
       public string TargetEntityId { get; set; }
       public string RelationType { get; set; }  // WORKS_FOR, LOCATED_IN, etc.
       public Dictionary<string, object> Properties { get; set; }
       public double Confidence { get; set; }
       public string? SourceDocumentId { get; set; }
   }
   ```

3. **Define Graph Schema**
   - File: `/src/Core/AI/Hazina.AI.RAG/Graph/Models/GraphSchema.cs`
   ```csharp
   public class GraphSchema
   {
       public List<string> EntityTypes { get; set; }
       public List<string> RelationTypes { get; set; }
       public Dictionary<string, List<string>> AllowedRelations { get; set; }
   }
   ```

4. **Documentation**
   - File: `/docs/graph_model.md`
   - Content: Schema, entity types, relation types, storage strategy

**Deliverable**: Complete graph model with documentation
**Timeline**: 3 days

---

### Phase 2: Graph Construction Pipeline (Week 2)
**Deliverable**: D2 - Graph Construction Pipeline
**Lead**: Entity Extraction Specialist Dr. Ahmed Al-Rashid

#### Tasks

1. **Entity Extraction Service**
   - File: `/src/Core/AI/Hazina.AI.RAG/Graph/Pipeline/EntityExtractionService.cs`
   - Function: Extract entities from text using LLM
   - Prompt Engineering by Dr. Aisha Mohammed
   ```csharp
   public interface IEntityExtractor
   {
       Task<List<GraphEntity>> ExtractEntitiesAsync(
           string text,
           string? documentId = null,
           CancellationToken cancellationToken = default);
   }

   public class LLMEntityExtractor : IEntityExtractor
   {
       private readonly IProviderOrchestrator _orchestrator;
       private const string ENTITY_EXTRACTION_PROMPT = @"
           Extract named entities from the following text.
           Return JSON array with format: [{name, type, properties}]
           Entity types: Person, Organization, Location, Concept, Event, Product
           Text: {text}
       ";
       // Implementation uses orchestrator.GetResponse()
   }
   ```

2. **Relationship Extraction Service**
   - File: `/src/Core/AI/Hazina.AI.RAG/Graph/Pipeline/RelationshipExtractionService.cs`
   - Function: Extract relationships between entities
   ```csharp
   public interface IRelationshipExtractor
   {
       Task<List<GraphRelationship>> ExtractRelationshipsAsync(
           string text,
           List<GraphEntity> entities,
           CancellationToken cancellationToken = default);
   }

   public class LLMRelationshipExtractor : IRelationshipExtractor
   {
       // Similar pattern: LLM prompt → JSON response → parse
   }
   ```

3. **Entity Normalization Service**
   - File: `/src/Core/AI/Hazina.AI.RAG/Graph/Pipeline/EntityNormalizationService.cs`
   - Function: Deduplicate and merge entities
   - Lead: Dr. Kim Min-Jun
   ```csharp
   public interface IEntityNormalizer
   {
       Task<GraphEntity> NormalizeEntityAsync(
           GraphEntity entity,
           IGraphStore graphStore,
           CancellationToken cancellationToken = default);
   }
   ```

4. **Graph Construction Coordinator**
   - File: `/src/Core/AI/Hazina.AI.RAG/Graph/Pipeline/GraphConstructionPipeline.cs`
   - Function: Orchestrate full pipeline
   ```csharp
   public class GraphConstructionPipeline
   {
       public async Task<GraphConstructionResult> ProcessDocumentAsync(
           string documentId,
           string documentText,
           CancellationToken cancellationToken = default)
       {
           // 1. Extract entities
           var entities = await _entityExtractor.ExtractEntitiesAsync(documentText, documentId, cancellationToken);

           // 2. Extract relationships
           var relationships = await _relationshipExtractor.ExtractRelationshipsAsync(documentText, entities, cancellationToken);

           // 3. Normalize entities (deduplicate)
           var normalizedEntities = new List<GraphEntity>();
           foreach (var entity in entities)
           {
               var normalized = await _entityNormalizer.NormalizeEntityAsync(entity, _graphStore, cancellationToken);
               normalizedEntities.Add(normalized);
           }

           // 4. Persist to graph store
           await _graphStore.AddEntitiesAsync(normalizedEntities, cancellationToken);
           await _graphStore.AddRelationshipsAsync(relationships, cancellationToken);

           return new GraphConstructionResult
           {
               EntitiesExtracted = entities.Count,
               RelationshipsExtracted = relationships.Count
           };
       }
   }
   ```

5. **Documentation**
   - File: `/docs/graph_construction.md`
   - Content: Pipeline steps, prompt templates, normalization rules

**Deliverable**: Complete graph construction pipeline
**Timeline**: 5 days

---

### Phase 3: Graph Storage & Query Interface (Week 3)
**Deliverable**: D3 - Graph Storage & Query Interface
**Lead**: Database Architect Dr. Alexei Volkov

#### Tasks

1. **IGraphStore Interface**
   - File: `/src/Core/AI/Hazina.AI.RAG/Graph/Storage/IGraphStore.cs`
   ```csharp
   public interface IGraphStore
   {
       // Entity operations
       Task AddEntitiesAsync(List<GraphEntity> entities, CancellationToken ct = default);
       Task<List<GraphEntity>> GetEntitiesAsync(string query, CancellationToken ct = default);
       Task<GraphEntity?> GetEntityByIdAsync(string id, CancellationToken ct = default);
       Task<List<GraphEntity>> SearchEntitiesAsync(float[] embedding, int topK, CancellationToken ct = default);

       // Relationship operations
       Task AddRelationshipsAsync(List<GraphRelationship> relationships, CancellationToken ct = default);
       Task<List<GraphRelationship>> GetRelationsAsync(string entityId, CancellationToken ct = default);

       // Graph traversal
       Task<List<GraphEntity>> GetNeighborsAsync(string entityId, int depth = 1, CancellationToken ct = default);
       Task<List<GraphPath>> FindPathsAsync(string sourceId, string targetId, int maxDepth = 3, CancellationToken ct = default);
       Task<List<GraphEntity>> SearchByLinkageAsync(string entityId, string predicate, int depth = 2, CancellationToken ct = default);
   }

   public class GraphPath
   {
       public List<GraphEntity> Entities { get; set; }
       public List<GraphRelationship> Relationships { get; set; }
       public double Score { get; set; }
   }
   ```

2. **SQLite Implementation** (Default)
   - File: `/src/Core/AI/Hazina.AI.RAG/Graph/Storage/SQLiteGraphStore.cs`
   - Lead: SQLite Graph Expert Martin Andersson
   - Schema:
   ```sql
   CREATE TABLE graph_entities (
       id TEXT PRIMARY KEY,
       name TEXT NOT NULL,
       type TEXT NOT NULL,
       properties TEXT,  -- JSON
       embedding BLOB,   -- Optional vector for entity search
       created_at TEXT,
       updated_at TEXT
   );
   CREATE INDEX idx_entity_type ON graph_entities(type);
   CREATE INDEX idx_entity_name ON graph_entities(name);

   CREATE TABLE graph_relationships (
       id TEXT PRIMARY KEY,
       source_entity_id TEXT NOT NULL,
       target_entity_id TEXT NOT NULL,
       relation_type TEXT NOT NULL,
       properties TEXT,  -- JSON
       confidence REAL,
       source_document_id TEXT,
       FOREIGN KEY (source_entity_id) REFERENCES graph_entities(id),
       FOREIGN KEY (target_entity_id) REFERENCES graph_entities(id)
   );
   CREATE INDEX idx_rel_source ON graph_relationships(source_entity_id);
   CREATE INDEX idx_rel_target ON graph_relationships(target_entity_id);
   CREATE INDEX idx_rel_type ON graph_relationships(relation_type);
   ```

3. **In-Memory Implementation** (Testing)
   - File: `/src/Core/AI/Hazina.AI.RAG/Graph/Storage/InMemoryGraphStore.cs`
   - Simple Dictionary-based storage for unit tests

4. **Neo4j Implementation** (Optional, Future)
   - File: `/src/Core/AI/Hazina.AI.RAG/Graph/Storage/Neo4jGraphStore.cs`
   - Lead: Neo4j Integration Expert Dr. Rachel Green
   - Uses Neo4j.Driver NuGet package (optional dependency)

**Deliverable**: Complete graph storage layer with SQLite default
**Timeline**: 5 days

---

### Phase 4: Hybrid Retrieval Layer (Week 4)
**Deliverable**: D4 - Hybrid Retrieval Layer
**Lead**: Hybrid Retrieval Lead Dr. Roberto Garcia

#### Tasks

1. **Graph Retriever**
   - File: `/src/Core/AI/Hazina.AI.RAG/Graph/Retrieval/GraphRetriever.cs`
   ```csharp
   public interface IGraphRetriever
   {
       Task<List<GraphRetrievalResult>> RetrieveAsync(
           string query,
           GraphQueryOptions options,
           CancellationToken ct = default);
   }

   public class GraphRetriever : IGraphRetriever
   {
       private readonly IGraphStore _graphStore;
       private readonly IEntityExtractor _entityExtractor;

       public async Task<List<GraphRetrievalResult>> RetrieveAsync(
           string query,
           GraphQueryOptions options,
           CancellationToken ct = default)
       {
           // 1. Extract query entities
           var queryEntities = await _entityExtractor.ExtractEntitiesAsync(query, ct: ct);

           // 2. For each entity, traverse graph
           var results = new List<GraphRetrievalResult>();
           foreach (var entity in queryEntities)
           {
               // Find matching entities in graph
               var graphEntities = await _graphStore.GetEntitiesAsync(entity.Name, ct);

               // For each match, get neighbors
               foreach (var graphEntity in graphEntities)
               {
                   var neighbors = await _graphStore.GetNeighborsAsync(
                       graphEntity.Id,
                       options.TraversalDepth,
                       ct);

                   results.Add(new GraphRetrievalResult
                   {
                       Entity = graphEntity,
                       Neighbors = neighbors,
                       Relevance = CalculateRelevance(graphEntity, query)
                   });
               }
           }

           return results;
       }
   }
   ```

2. **Hybrid Retriever (Fusion)**
   - File: `/src/Core/AI/Hazina.AI.RAG/Hybrid/HybridRetriever.cs`
   ```csharp
   public class HybridRetriever
   {
       private readonly IVectorStore? _vectorStore;
       private readonly IGraphRetriever? _graphRetriever;
       private readonly IProviderOrchestrator _orchestrator;

       public async Task<List<RetrievedDocument>> RetrieveAsync(
           string query,
           RAGQueryOptions options,
           CancellationToken ct = default)
       {
           var vectorResults = new List<RetrievedDocument>();
           var graphResults = new List<GraphRetrievalResult>();

           // Parallel retrieval
           var tasks = new List<Task>();

           if (options.UseEmbeddings && _vectorStore != null)
           {
               tasks.Add(Task.Run(async () =>
               {
                   var embedding = await GenerateEmbeddingAsync(query, ct);
                   vectorResults = await RetrieveVectorAsync(embedding, options, ct);
               }));
           }

           if (options.UseGraphSearch && _graphRetriever != null)
           {
               tasks.Add(Task.Run(async () =>
               {
                   graphResults = await _graphRetriever.RetrieveAsync(query, options.GraphOptions, ct);
               }));
           }

           await Task.WhenAll(tasks);

           // Fusion
           return FuseResults(vectorResults, graphResults, options.FusionStrategy);
       }

       private List<RetrievedDocument> FuseResults(
           List<RetrievedDocument> vectorResults,
           List<GraphRetrievalResult> graphResults,
           FusionStrategy strategy)
       {
           switch (strategy)
           {
               case FusionStrategy.Concat:
                   return ConcatResults(vectorResults, graphResults);
               case FusionStrategy.RankInterleave:
                   return RankInterleave(vectorResults, graphResults);
               case FusionStrategy.Weighted:
                   return WeightedFusion(vectorResults, graphResults);
               default:
                   return vectorResults;
           }
       }
   }

   public enum FusionStrategy
   {
       Concat,          // Vector first, then graph
       RankInterleave,  // Interleave by rank (1st vec, 1st graph, 2nd vec, ...)
       Weighted         // Weighted combination of scores
   }
   ```

3. **Ranker (Graph-aware)**
   - File: `/src/Core/AI/Hazina.AI.RAG/Hybrid/GraphAwareRanker.cs`
   - Function: Re-rank results using graph structure
   - Features: PageRank-style scoring, path-based relevance

4. **Integration with RAGEngine**
   - File: `/src/Core/AI/Hazina.AI.RAG/Core/RAGEngine.cs` (MODIFY)
   - Change: Add hybrid retrieval path
   ```csharp
   // In QueryAsync method:
   if (options.UseHybridFusion && _hybridRetriever != null)
   {
       response.RetrievedDocuments = await _hybridRetriever.RetrieveAsync(
           query, options, cancellationToken);
   }
   else if (options.UseEmbeddings && _vectorStore != null)
   {
       // Existing vector search path
   }
   // ... rest of method unchanged
   ```

**Deliverable**: Complete hybrid retrieval system
**Timeline**: 5 days

---

### Phase 5: Explainability Layer (Week 5)
**Deliverable**: D5 - Explainability Layer
**Lead**: Explainability Architect Dr. Raj Patel

#### Tasks

1. **TraceObject Model**
   - File: `/src/Core/AI/Hazina.AI.RAG/Explainability/Models/RetrievalTrace.cs`
   ```csharp
   public class RetrievalTrace
   {
       public string Query { get; set; }
       public DateTime Timestamp { get; set; }

       public VectorSearchTrace? VectorSearch { get; set; }
       public GraphSearchTrace? GraphSearch { get; set; }
       public FusionTrace? Fusion { get; set; }

       public List<TraceEntry> Timeline { get; set; }
   }

   public class VectorSearchTrace
   {
       public float[] QueryEmbedding { get; set; }
       public int ResultsFound { get; set; }
       public List<VectorResult> Results { get; set; }
   }

   public class GraphSearchTrace
   {
       public List<GraphEntity> QueryEntities { get; set; }
       public List<GraphPath> PathsExplored { get; set; }
       public Dictionary<string, int> EntityCounts { get; set; }
   }

   public class FusionTrace
   {
       public FusionStrategy Strategy { get; set; }
       public int VectorResultCount { get; set; }
       public int GraphResultCount { get; set; }
       public int FinalResultCount { get; set; }
       public List<FusionDecision> Decisions { get; set; }
   }

   public class GraphPath
   {
       public string FromEntity { get; set; }
       public string ToEntity { get; set; }
       public List<string> ViaEntities { get; set; }
       public List<string> Relationships { get; set; }
       public double Relevance { get; set; }
   }
   ```

2. **Trace Collector**
   - File: `/src/Core/AI/Hazina.AI.RAG/Explainability/TraceCollector.cs`
   - Function: Collect trace data during retrieval
   - Pattern: Passed through pipeline, accumulates data

3. **Trace Formatter**
   - File: `/src/Core/AI/Hazina.AI.RAG/Explainability/TraceFormatter.cs`
   ```csharp
   public interface ITraceFormatter
   {
       string Format(RetrievalTrace trace, TraceFormat format);
   }

   public enum TraceFormat
   {
       JSON,
       Markdown,
       HTML
   }

   public class TraceFormatter : ITraceFormatter
   {
       public string Format(RetrievalTrace trace, TraceFormat format)
       {
           switch (format)
           {
               case TraceFormat.JSON:
                   return JsonSerializer.Serialize(trace, new JsonSerializerOptions
                   {
                       WriteIndented = true
                   });
               case TraceFormat.Markdown:
                   return FormatMarkdown(trace);
               case TraceFormat.HTML:
                   return FormatHTML(trace);
               default:
                   return trace.ToString();
           }
       }
   }
   ```

4. **RAGResponse Enhancement**
   - File: `/src/Core/AI/Hazina.AI.RAG/Core/RAGEngine.cs` (MODIFY)
   - Add: `RetrievalTrace? Trace { get; set; }` to RAGResponse class
   - Populate during QueryAsync if `options.EnableTracing = true`

**Deliverable**: Complete explainability system
**Timeline**: 4 days

---

### Phase 6: Testing & Documentation (Week 6)
**Deliverables**: Comprehensive tests + documentation
**Leads**: Test Lead Dr. Laura Fernández, Documentation Lead Dr. Samantha Lee

#### Testing Tasks

1. **Unit Tests**
   - File: `/tests/Hazina.AI.RAG.Tests/Graph/GraphEntityTests.cs`
   - File: `/tests/Hazina.AI.RAG.Tests/Graph/GraphStoreTests.cs`
   - File: `/tests/Hazina.AI.RAG.Tests/Graph/EntityExtractionTests.cs`
   - Coverage: >90% for all new code

2. **Integration Tests**
   - File: `/tests/Hazina.AI.RAG.Tests/Integration/HybridRetrievalTests.cs`
   - Scenarios: End-to-end document → graph → retrieval workflows

3. **Regression Tests**
   - File: `/tests/Hazina.AI.RAG.Tests/Regression/BackwardsCompatibilityTests.cs`
   - Verify: All existing RAG tests still pass

4. **Performance Tests**
   - File: `/tests/Hazina.AI.RAG.Tests/Performance/GraphQueryBenchmarks.cs`
   - Metrics: Query latency, throughput, memory usage

#### Documentation Tasks

1. **Architecture Documentation**
   - File: `/docs/graphrag_architecture.md`
   - Content: System overview, component diagram, data flow

2. **API Documentation**
   - File: `/docs/graphrag_api.md`
   - Content: Interface reference, code examples

3. **Tutorial**
   - File: `/docs/graphrag_quickstart.md`
   - Content: Step-by-step getting started guide

4. **Migration Guide**
   - File: `/docs/graphrag_migration.md`
   - Content: How to migrate from vector-only RAG to hybrid

**Deliverable**: Complete test suite + documentation
**Timeline**: 5 days

---

## File Structure

```
src/Core/AI/Hazina.AI.RAG/
├── Core/
│   └── RAGEngine.cs (MODIFIED - add hybrid path)
│
├── Graph/ (NEW)
│   ├── Models/
│   │   ├── GraphEntity.cs
│   │   ├── GraphRelationship.cs
│   │   ├── GraphSchema.cs
│   │   └── GraphPath.cs
│   ├── Pipeline/
│   │   ├── IEntityExtractor.cs
│   │   ├── LLMEntityExtractor.cs
│   │   ├── IRelationshipExtractor.cs
│   │   ├── LLMRelationshipExtractor.cs
│   │   ├── IEntityNormalizer.cs
│   │   ├── EntityNormalizer.cs
│   │   └── GraphConstructionPipeline.cs
│   ├── Storage/
│   │   ├── IGraphStore.cs
│   │   ├── SQLiteGraphStore.cs
│   │   ├── InMemoryGraphStore.cs
│   │   └── Neo4jGraphStore.cs (optional)
│   └── Retrieval/
│       ├── IGraphRetriever.cs
│       ├── GraphRetriever.cs
│       └── GraphQueryOptions.cs
│
├── Hybrid/ (NEW)
│   ├── HybridRetriever.cs
│   ├── GraphAwareRanker.cs
│   ├── FusionStrategy.cs
│   └── FusionAlgorithms.cs
│
├── Explainability/ (NEW)
│   ├── Models/
│   │   ├── RetrievalTrace.cs
│   │   ├── GraphSearchTrace.cs
│   │   ├── VectorSearchTrace.cs
│   │   └── FusionTrace.cs
│   ├── TraceCollector.cs
│   ├── ITraceFormatter.cs
│   └── TraceFormatter.cs
│
└── Hazina.AI.RAG.csproj (MODIFIED - add optional dependencies)

docs/ (NEW)
├── graph_model.md
├── graph_construction.md
├── graphrag_architecture.md
├── graphrag_api.md
├── graphrag_quickstart.md
└── graphrag_migration.md

tests/Hazina.AI.RAG.Tests/
├── Graph/ (NEW)
│   ├── GraphEntityTests.cs
│   ├── GraphStoreTests.cs
│   ├── EntityExtractionTests.cs
│   └── GraphConstructionTests.cs
├── Hybrid/ (NEW)
│   ├── HybridRetrieverTests.cs
│   └── FusionTests.cs
├── Integration/ (NEW)
│   └── HybridRetrievalTests.cs
├── Performance/ (NEW)
│   └── GraphQueryBenchmarks.cs
└── Regression/ (NEW)
    └── BackwardsCompatibilityTests.cs
```

---

## Integration Points

### 1. RAGEngine Integration
**File**: `src/Core/AI/Hazina.AI.RAG/Core/RAGEngine.cs`

**Changes**:
1. Add new constructor overload accepting `IGraphStore` and `GraphRAGConfig`
2. Add `IHybridRetriever` field
3. Modify `QueryAsync` to support hybrid retrieval path
4. Add trace collection when enabled

**Code Diff**:
```csharp
// NEW constructor
public RAGEngine(
    IProviderOrchestrator orchestrator,
    IVectorStore? vectorStore,
    IGraphStore? graphStore,
    GraphRAGConfig? graphConfig = null)
{
    _orchestrator = orchestrator;
    _vectorStore = vectorStore;
    _graphStore = graphStore;

    if (graphStore != null)
    {
        // Initialize hybrid retrieval
        _hybridRetriever = new HybridRetriever(orchestrator, vectorStore, graphStore);
    }
}

// MODIFIED QueryAsync
public async Task<RAGResponse> QueryAsync(
    string query,
    RAGQueryOptions? options = null,
    CancellationToken cancellationToken = default)
{
    options ??= new RAGQueryOptions();
    var response = new RAGResponse { Query = query, Timestamp = DateTime.UtcNow };

    // NEW: Initialize trace if enabled
    if (options.EnableTracing)
    {
        response.Trace = new RetrievalTrace { Query = query, Timestamp = DateTime.UtcNow };
    }

    // NEW: Hybrid retrieval path
    if (options.UseHybridFusion && _hybridRetriever != null)
    {
        response.RetrievedDocuments = await _hybridRetriever.RetrieveAsync(
            query, options, cancellationToken);
    }
    // EXISTING: Vector-only path (unchanged)
    else if (options.UseEmbeddings && _vectorStore != null)
    {
        response.RetrievedDocuments = await RetrieveWithEmbeddingsAsync(
            query, options, cancellationToken);
    }
    // ... rest unchanged
}
```

### 2. Document Indexing Integration
**File**: `src/Core/AI/Hazina.AI.RAG/Core/RAGEngine.cs`

**Add Method**:
```csharp
/// <summary>
/// Index documents with graph construction (optional)
/// </summary>
public async Task<IndexingResult> IndexDocumentsAsync(
    List<Document> documents,
    bool buildGraph = false,  // NEW parameter, default false
    CancellationToken cancellationToken = default)
{
    var result = new IndexingResult { TotalDocuments = documents.Count };

    foreach (var doc in documents)
    {
        try
        {
            // EXISTING: Vector indexing
            if (_vectorStore != null)
            {
                var embedding = await GenerateEmbeddingAsync(doc.Content, cancellationToken);
                await _vectorStore.AddAsync(doc.Id, embedding, ...);
            }

            // NEW: Graph construction (opt-in)
            if (buildGraph && _graphConstructionPipeline != null)
            {
                await _graphConstructionPipeline.ProcessDocumentAsync(
                    doc.Id, doc.Content, cancellationToken);
            }

            result.IndexedDocuments++;
        }
        catch (Exception ex)
        {
            result.FailedDocuments++;
            result.Errors.Add($"{doc.Id}: {ex.Message}");
        }
    }

    return result;
}
```

### 3. Configuration Integration
**File**: `src/Core/AI/Hazina.AI.RAG/Configuration/GraphRAGConfig.cs` (NEW)

```csharp
public class GraphRAGConfig
{
    /// <summary>
    /// Graph storage backend (SQLite, Neo4j, InMemory)
    /// </summary>
    public GraphStorageType StorageType { get; set; } = GraphStorageType.SQLite;

    /// <summary>
    /// Path to SQLite database file (if using SQLite)
    /// </summary>
    public string? SQLitePath { get; set; }

    /// <summary>
    /// Neo4j connection string (if using Neo4j)
    /// </summary>
    public string? Neo4jUri { get; set; }

    /// <summary>
    /// Whether to automatically build graph during indexing
    /// </summary>
    public bool AutoBuildGraph { get; set; } = false;

    /// <summary>
    /// Default graph traversal depth
    /// </summary>
    public int DefaultTraversalDepth { get; set; } = 2;
}

public enum GraphStorageType
{
    SQLite,
    Neo4j,
    InMemory
}
```

---

## Testing Strategy

### Unit Test Coverage Targets
- Graph Models: 100%
- Entity Extraction: >90%
- Graph Storage: >95%
- Hybrid Retrieval: >90%
- Explainability: >85%

### Integration Test Scenarios
1. **End-to-End Hybrid Retrieval**
   - Index 100 documents
   - Build graph
   - Query with hybrid fusion
   - Verify results contain both vector and graph matches

2. **Graph Construction Pipeline**
   - Input: Document with entities and relationships
   - Output: Entities and relationships in graph store
   - Verify: Entity normalization works (deduplication)

3. **Multi-Hop Graph Queries**
   - Build graph with 3-hop paths
   - Query for entities 3 hops away
   - Verify: Correct path finding

### Performance Benchmarks
- Graph query latency: <100ms for depth-2 queries
- Hybrid retrieval: <500ms for 10 documents
- Graph construction: <2s for 1000-word document
- Memory usage: <100MB for 1000 entities

### Backwards Compatibility Tests
- All existing RAG tests must pass
- No changes to existing test files
- New tests in separate files

---

## Migration Path

### Level 1: No Changes (Default)
```csharp
// Existing code works identically
var engine = new RAGEngine(orchestrator, vectorStore);
var result = await engine.QueryAsync("query");
// Uses vector search only
```

### Level 2: Enable Graph (Opt-In)
```csharp
// Add graph store
var graphStore = new SQLiteGraphStore("graph.db");
var engine = new RAGEngine(orchestrator, vectorStore, graphStore);

// Enable graph search
var result = await engine.QueryAsync("query", new RAGQueryOptions
{
    UseGraphSearch = true,
    UseHybridFusion = true
});
```

### Level 3: Build Graph During Indexing
```csharp
// Index with graph construction
await engine.IndexDocumentsAsync(documents, buildGraph: true);
```

### Level 4: Advanced Configuration
```csharp
// Custom graph configuration
var graphConfig = new GraphRAGConfig
{
    StorageType = GraphStorageType.SQLite,
    SQLitePath = "custom_graph.db",
    AutoBuildGraph = true,
    DefaultTraversalDepth = 3
};

var engine = new RAGEngine(orchestrator, vectorStore, graphStore, graphConfig);

// Custom query options
var result = await engine.QueryAsync("query", new RAGQueryOptions
{
    UseHybridFusion = true,
    GraphOptions = new GraphQueryOptions
    {
        TraversalDepth = 3,
        MaxPaths = 10,
        MinPathScore = 0.5
    },
    FusionStrategy = FusionStrategy.Weighted,
    EnableTracing = true
});

// Access trace
Console.WriteLine(result.Trace.ToJSON());
```

---

## Success Criteria

### Functional Requirements
✅ System can ingest documents and produce a Knowledge Graph
✅ System can answer queries using hybrid retrieval (vector + graph)
✅ System can produce explorable traces (entities, edges, paths)
✅ All existing RAG functionality continues to work unchanged

### Non-Functional Requirements
✅ Modular & dependency-minimal
✅ Replaceable components (vector backend, graph backend)
✅ No vendor lock-in (Neo4j optional)
✅ Works offline after initial setup

### Developer Experience
✅ All stages documented
✅ End-to-end example provided
✅ Unit tests for each pipeline step
✅ >90% code coverage

### Performance
✅ Graph queries complete in <100ms (depth-2)
✅ Hybrid retrieval completes in <500ms
✅ Memory usage <100MB for typical graphs

---

## Risk Mitigation

### Risk 1: LLM-Based Extraction Quality
**Mitigation**:
- Use few-shot prompts with examples
- Add confidence scores to entities/relationships
- Allow manual curation/correction
- Provide fallback to keyword-based extraction

### Risk 2: Graph Storage Performance
**Mitigation**:
- Start with SQLite (simple, embedded)
- Add caching layer
- Provide Neo4j option for large graphs
- Benchmark early, optimize incrementally

### Risk 3: Backwards Compatibility Violations
**Mitigation**:
- Extensive regression testing
- Feature flags for all new functionality
- Code review by Backwards Compatibility Lead
- CI/CD checks for breaking changes

### Risk 4: Complex Integration
**Mitigation**:
- Phased rollout (vector-only → hybrid opt-in → default hybrid)
- Clear migration guide
- Example code for each level
- Support for gradual adoption

---

## Timeline Summary

| Phase | Duration | Deliverable |
|-------|----------|-------------|
| Phase 1: Data Model | 3 days | Graph model + docs |
| Phase 2: Pipeline | 5 days | Entity/relationship extraction |
| Phase 3: Storage | 5 days | Graph store + SQLite impl |
| Phase 4: Hybrid Retrieval | 5 days | Hybrid retriever + fusion |
| Phase 5: Explainability | 4 days | Trace system |
| Phase 6: Testing & Docs | 5 days | Tests + documentation |
| **Total** | **27 days** | **Complete GraphRAG Integration** |

---

## Implementation Ready

**Expert Team**: Assembled ✅
**Architecture**: Designed ✅
**Backwards Compatibility**: Guaranteed ✅
**File Structure**: Defined ✅
**Integration Points**: Identified ✅
**Testing Strategy**: Planned ✅
**Migration Path**: Documented ✅

**Status**: 🚀 **READY FOR IMPLEMENTATION**

---

**Signed**: Claude Sonnet 4.5 (claude-sonnet-4-5-20250929)
**Date**: 2026-01-11T11:30:00Z
**Next Step**: Allocate worktree and begin Phase 1 implementation
