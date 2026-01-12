# GraphRAG Phase 2: Graph Construction Pipeline

## Overview

The Graph Construction Pipeline transforms unstructured text into a structured knowledge graph by extracting entities, identifying relationships, normalizing duplicates, and persisting to storage.

## Pipeline Architecture

```
Document Text
     ↓
┌────────────────────────────────────────┐
│  1. Entity Extraction (LLM-based)     │  ← IEntityExtractor
│     - Identifies named entities        │  ← Confidence scoring
│     - Assigns types and properties     │  ← Provenance tracking
└────────────────────┬───────────────────┘
                     ↓
┌────────────────────────────────────────┐
│  2. Relationship Extraction (LLM-based)│  ← IRelationshipExtractor
│     - Finds connections between entities│  ← Typed relationships
│     - Assigns confidence scores        │  ← Bidirectional support
└────────────────────┬───────────────────┘
                     ↓
┌────────────────────────────────────────┐
│  3. Entity Normalization               │  ← IEntityNormalizer
│     - Deduplicates entities            │  ← Multiple strategies
│     - Merges similar entities          │  ← Alias tracking
│     - Updates mention counts           │  ← Confidence updates
└────────────────────┬───────────────────┘
                     ↓
┌────────────────────────────────────────┐
│  4. Graph Persistence                  │  ← IGraphStore
│     - Stores entities and relationships│  ← Transactional
│     - Maintains graph structure        │  ← Indexing
└────────────────────────────────────────┘
```

## Components

### 1. Entity Extraction

**Interface:** `IEntityExtractor`
**Implementation:** `LLMEntityExtractor`

Extracts named entities from text using LLM-based analysis.

**Supported Entity Types:**
- Person
- Organization
- Location
- Concept
- Event
- Product
- Document
- Topic

**Features:**
- JSON-based structured prompts
- Confidence scoring (0.0-1.0)
- Automatic validation against schema
- Provenance tracking (source document IDs)
- Configurable minimum confidence threshold

**Configuration:**
```csharp
GraphRAGConfig config = new()
{
    MinEntityConfidence = 0.7, // Skip entities below 70% confidence
    MaxEntitiesPerDocument = 100 // Limit per document
};
```

**Example Usage:**
```csharp
var extractor = new LLMEntityExtractor(orchestrator, logger, config);
var entities = await extractor.ExtractEntitiesAsync(
    "John Smith works at Microsoft in Seattle.",
    documentId: "doc-001");

// Result:
// - Entity: "John Smith" (Person, confidence: 0.95)
// - Entity: "Microsoft" (Organization, confidence: 0.98)
// - Entity: "Seattle" (Location, confidence: 0.92)
```

### 2. Relationship Extraction

**Interface:** `IRelationshipExtractor`
**Implementation:** `LLMRelationshipExtractor`

Identifies relationships between extracted entities.

**Supported Relationship Types:**
- WORKS_FOR
- LOCATED_IN
- CREATED_BY
- INFLUENCED_BY
- PART_OF
- MEMBER_OF
- OWNS
- MANAGES
- PRODUCES
- USES
- RELATED_TO (generic)

**Features:**
- Context-aware extraction (uses original text)
- Directional relationships (source → target)
- Bidirectional relationship support
- Confidence scoring
- Source text snippet preservation

**Configuration:**
```csharp
GraphRAGConfig config = new()
{
    MinRelationshipConfidence = 0.6, // Skip below 60%
    MaxRelationshipsPerDocument = 200 // Limit per document
};
```

**Example Usage:**
```csharp
var extractor = new LLMRelationshipExtractor(orchestrator, logger, config);
var relationships = await extractor.ExtractRelationshipsAsync(
    "John Smith works at Microsoft in Seattle.",
    entities,
    documentId: "doc-001");

// Result:
// - Relationship: "John Smith" WORKS_FOR "Microsoft" (confidence: 0.94)
// - Relationship: "Microsoft" LOCATED_IN "Seattle" (confidence: 0.88)
```

### 3. Entity Normalization

**Interface:** `IEntityNormalizer`
**Implementation:** `EntityNormalizationService`

Deduplicates and merges entities to maintain graph consistency.

**Normalization Strategies:**

1. **Exact Match** (`EntityNormalizationStrategy.ExactMatch`)
   - Case-insensitive name and type matching
   - Fast, deterministic

2. **Fuzzy Match** (`EntityNormalizationStrategy.FuzzyMatch`)
   - Levenshtein distance calculation
   - Alias matching
   - Configurable similarity threshold

3. **Embedding Similarity** (`EntityNormalizationStrategy.EmbeddingSimilarity`)
   - Cosine similarity on entity embeddings
   - Most accurate, requires embeddings
   - Falls back to fuzzy match if no embeddings

4. **LLM-Based** (`EntityNormalizationStrategy.LLMBased`)
   - Reserved for future enhancement
   - Uses LLM to determine entity equivalence

**Configuration:**
```csharp
GraphRAGConfig config = new()
{
    NormalizationStrategy = EntityNormalizationStrategy.FuzzyMatch,
    EntitySimilarityThreshold = 0.85 // 85% similarity for merge
};
```

**Merge Behavior:**
When entities are merged:
- Properties are combined (new properties added)
- Source documents are aggregated
- Aliases are tracked (original names preserved)
- Mention count is incremented
- Confidence is updated (weighted average)
- Last updated timestamp refreshed

**Example:**
```csharp
// First mention
Entity1: "John Smith" (Person, confidence: 0.95, mentions: 1)

// Second mention (variant)
Entity2: "J. Smith" (Person, confidence: 0.90)

// After normalization (fuzzy match)
MergedEntity: "John Smith" (Person, confidence: 0.925, mentions: 2)
  Aliases: ["J. Smith"]
```

### 4. Graph Construction Coordinator

**Class:** `GraphConstructionPipeline`

Orchestrates the complete pipeline from document to knowledge graph.

**Process Flow:**
1. Extract entities from text
2. Extract relationships between entities
3. Normalize entities (deduplicate)
4. Persist entities to graph store
5. Update relationship entity IDs (after normalization)
6. Persist relationships to graph store

**Features:**
- Atomic processing per document
- Detailed statistics and logging
- Error handling and recovery
- Batch processing support
- Respects configured limits

**Example Usage:**
```csharp
var pipeline = new GraphConstructionPipeline(
    entityExtractor,
    relationshipExtractor,
    entityNormalizer,
    graphStore,
    logger,
    config);

var result = await pipeline.ProcessDocumentAsync(
    documentId: "doc-001",
    documentText: "..."
);

// Result statistics:
// - EntitiesExtracted: 15
// - RelationshipsExtracted: 22
// - EntitiesMerged: 3 (deduplicated)
// - EntitiesPersisted: 12
// - RelationshipsPersisted: 22
// - Duration: 2.3 seconds
```

**Batch Processing:**
```csharp
var documents = new Dictionary<string, string>
{
    ["doc-001"] = "Text 1...",
    ["doc-002"] = "Text 2...",
    ["doc-003"] = "Text 3..."
};

var results = await pipeline.ProcessDocumentsBatchAsync(documents);
```

## Storage Interface

**Interface:** `IGraphStore`

Phase 2 defines the storage interface, with basic in-memory implementation.

**Operations:**
- `AddEntitiesAsync` - Store entities
- `AddRelationshipsAsync` - Store relationships
- `GetEntityByIdAsync` - Retrieve by ID
- `SearchEntitiesByNameAsync` - Search by name
- `SearchEntitiesByEmbeddingAsync` - Vector search
- `GetRelationshipsAsync` - Get entity relationships
- `GetNeighborsAsync` - Graph traversal
- `FindPathsAsync` - Path finding
- `SearchByLinkageAsync` - Linkage-based search
- `UpdateEntityAsync` - Update entity
- `DeleteEntityAsync` - Delete entity
- `GetStatisticsAsync` - Graph statistics

**Implementations:**
- `InMemoryGraphStore` - Simple in-memory storage (Phase 2, testing only)
- `SQLiteGraphStore` - Production SQLite storage (Phase 3)
- `Neo4jGraphStore` - Optional Neo4j storage (Phase 3)

## Configuration

All Phase 2 components use `GraphRAGConfig`:

```csharp
public class GraphRAGConfig
{
    // Entity extraction
    public double MinEntityConfidence { get; set; } = 0.7;
    public int MaxEntitiesPerDocument { get; set; } = 100;

    // Relationship extraction
    public double MinRelationshipConfidence { get; set; } = 0.6;
    public int MaxRelationshipsPerDocument { get; set; } = 200;

    // Normalization
    public EntityNormalizationStrategy NormalizationStrategy { get; set; } = EntityNormalizationStrategy.FuzzyMatch;
    public double EntitySimilarityThreshold { get; set; } = 0.85;

    // Schema validation
    public GraphSchema Schema { get; set; } = GraphSchema.CreatePermissive();

    // Storage (used in Phase 3)
    public GraphStorageType StorageType { get; set; } = GraphStorageType.InMemory;
}
```

## Dependency Injection

Register Phase 2 services:

```csharp
services.AddSingleton<GraphRAGConfig>(config);
services.AddScoped<IEntityExtractor, LLMEntityExtractor>();
services.AddScoped<IRelationshipExtractor, LLMRelationshipExtractor>();
services.AddScoped<IEntityNormalizer, EntityNormalizationService>();
services.AddScoped<IGraphStore, InMemoryGraphStore>(); // Phase 2 only
services.AddScoped<GraphConstructionPipeline>();
```

## Testing Recommendations

1. **Entity Extraction Tests**
   - Test with various entity types
   - Test confidence filtering
   - Test schema validation

2. **Relationship Extraction Tests**
   - Test all relationship types
   - Test bidirectional relationships
   - Test confidence filtering

3. **Normalization Tests**
   - Test exact match merging
   - Test fuzzy matching with variants
   - Test alias tracking
   - Test embedding similarity

4. **Pipeline Integration Tests**
   - Test complete document processing
   - Test entity deduplication
   - Test batch processing
   - Test error handling

## Next Steps

**Phase 3: Graph Storage & Query Interface**
- SQLite implementation with full-text search
- Neo4j implementation (optional)
- Advanced query capabilities
- Performance optimizations

**Phase 4: Hybrid Retrieval Layer**
- Combine vector and graph search
- Result fusion algorithms
- Query rewriting and expansion

**Phase 5: Explainability Layer**
- Trace objects showing reasoning paths
- Evidence attribution
- Confidence propagation

## Backwards Compatibility

Phase 2 is 100% opt-in:
- Existing RAG functionality unchanged
- No breaking changes to public APIs
- New services registered independently
- Can be enabled per query via `RAGQueryOptions`

To enable GraphRAG for a query:
```csharp
var options = new RAGQueryOptions
{
    UseGraphSearch = true,
    UseHybridFusion = true
};
```

## Performance Considerations

1. **LLM Calls**: Entity and relationship extraction require 2 LLM calls per document
2. **Normalization**: Fuzzy matching is O(n) per entity, embedding similarity is faster
3. **Storage**: In-memory store is fast but not persistent (use Phase 3 implementations for production)
4. **Batch Processing**: Process documents in batches to amortize overhead

## References

- Phase 1: Data Model (`docs/graph_model.md`)
- Phase 3: Storage Implementations (TBD)
- Phase 4: Hybrid Retrieval (TBD)
- GraphRAG Research Paper: [Microsoft Research](https://www.microsoft.com/en-us/research/project/graphrag/)
