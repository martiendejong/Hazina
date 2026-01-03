# Hazina – Storage & Search Architecture

## Overview

Hazina uses an **agent-first knowledge architecture** where the database is the primary knowledge layer and embeddings are a secondary, optional search index.

---

## Core Principles

### 1. Database as Primary Knowledge Layer

All metadata, tags, relationships, JSON objects, and embeddings are stored in a **project-scoped SQLite knowledge database**.

```
┌─────────────────────────────────────────────────────────────┐
│                    KNOWLEDGE DATABASE                        │
│                    (SQLite per project)                      │
├─────────────────────────────────────────────────────────────┤
│  • Document metadata (tags, MIME types, dates)              │
│  • Relationships between documents                          │
│  • JSON objects and structured data                         │
│  • Embeddings (multiple models per item)                    │
│  • Tag relevance indices                                    │
│  • Search indices                                           │
└─────────────────────────────────────────────────────────────┘
```

### 2. Embeddings as Secondary Index

Embeddings are **no longer stored in standalone files** but as records in the database, enabling:

- **Multiple embedding models per item** (OpenAI, local models, etc.)
- **Full rebuildability** from source material
- **Graceful degradation** when embeddings are unavailable
- **Model versioning** and migration support

```
┌─────────────────────────────────────────────────────────────┐
│                    EMBEDDINGS TABLE                          │
├─────────────────────────────────────────────────────────────┤
│  document_id  │  model_id  │  vector  │  created  │  hash   │
├───────────────┼────────────┼──────────┼───────────┼─────────┤
│  doc_001      │  openai    │  [...]   │  2026-01  │  abc123 │
│  doc_001      │  local     │  [...]   │  2026-01  │  def456 │
│  doc_002      │  openai    │  [...]   │  2026-01  │  ghi789 │
└─────────────────────────────────────────────────────────────┘
```

### 3. Files as Source Material Only

Files and chunk files on disk are treated **only as source material**, never as authoritative knowledge.

```
┌─────────────────────────────────────────────────────────────┐
│                    FILE SYSTEM                               │
│              (Source Material Only)                          │
├─────────────────────────────────────────────────────────────┤
│  uploads/                                                    │
│    ├── document.pdf          ← Original file                │
│    ├── document.pdf.txt      ← Extracted text (source)      │
│    └── image.png             ← Original file                │
│                                                              │
│  ❌ NOT authoritative knowledge                              │
│  ❌ NOT queried directly by agents                          │
│  ❌ NOT relied upon for structure or meaning                │
└─────────────────────────────────────────────────────────────┘
```

### 4. Agent-First Design

**Agents never query files directly** and never rely on filesystem structure for meaning.

All agent interactions flow through the knowledge database:

```
┌──────────┐     ┌──────────────────┐     ┌─────────────────┐
│  Agent   │ ──▶ │  Knowledge DB    │ ──▶ │  Results        │
│          │     │  (SQL + Vector)  │     │  (Ranked)       │
└──────────┘     └──────────────────┘     └─────────────────┘
      │                                           │
      │         ┌──────────────────┐              │
      └────────▶│  File System     │◀─────────────┘
                │  (Read-only ref) │   (Only for content retrieval)
                └──────────────────┘
```

---

## Canonical Search Flow

Search in Hazina follows this canonical flow:

```
┌─────────────────────────────────────────────────────────────┐
│                    SEARCH PIPELINE                           │
└─────────────────────────────────────────────────────────────┘

     ┌─────────────────────────────────────────────────────┐
     │  STEP 1: METADATA & STRUCTURAL FILTERING            │
     │  ─────────────────────────────────────────────────  │
     │  • SQL queries on metadata tables                   │
     │  • Tag filtering (ALL/ANY match)                    │
     │  • MIME type filtering                              │
     │  • Date range filtering                             │
     │  • Path pattern matching                            │
     │  • Custom metadata key-value queries                │
     │                                                     │
     │  Interface: IQueryableMetadataStore                 │
     └─────────────────────────────────────────────────────┘
                            │
                            ▼
     ┌─────────────────────────────────────────────────────┐
     │  STEP 2: EMBEDDING-BASED RELEVANCE (OPTIONAL)       │
     │  ─────────────────────────────────────────────────  │
     │  • Cosine similarity search on vectors              │
     │  • Pre-filtered by Step 1 results                   │
     │  • Model-specific search (select embedding model)   │
     │  • Graceful skip if embeddings unavailable          │
     │                                                     │
     │  Interface: IVectorStore                            │
     └─────────────────────────────────────────────────────┘
                            │
                            ▼
     ┌─────────────────────────────────────────────────────┐
     │  STEP 3: COMPOSITE SCORING & RANKING                │
     │  ─────────────────────────────────────────────────  │
     │  • Query-adaptive tag scoring (LLM or heuristic)    │
     │  • Recency weighting (exponential decay)            │
     │  • Position boosting                                │
     │  • Composite score calculation                      │
     │                                                     │
     │  Formula:                                           │
     │  Score = α×Similarity + β×TagScore                  │
     │        + γ×RecencyScore + δ×PositionScore           │
     │                                                     │
     │  Interface: ICompositeScorer, ITagScoringService    │
     └─────────────────────────────────────────────────────┘
                            │
                            ▼
     ┌─────────────────────────────────────────────────────┐
     │  STEP 4: AGENT-LEVEL INTERPRETATION                 │
     │  ─────────────────────────────────────────────────  │
     │  • Context assembly from ranked results             │
     │  • Tool-specific filtering and formatting           │
     │  • Agent reasoning over retrieved content           │
     │                                                     │
     │  Interface: RAGEngine, Agent Tools                  │
     └─────────────────────────────────────────────────────┘
```

---

## Key Design Decisions

### Metadata is First-Class

Metadata is **always queryable**, regardless of embedding availability:

```csharp
// Always works - no embeddings required
var results = await metadataStore.QueryAsync(new MetadataFilter
{
    Tags = new List<string> { "evidence", "research" },
    MimeTypePrefix = "application/pdf",
    CreatedAfter = DateTime.UtcNow.AddDays(-30)
});
```

### Embeddings are Optional, Replaceable, and Degradable

If embeddings are disabled or unavailable, Hazina **must continue to function**:

```csharp
// RAGEngine with embeddings
var engine = new RAGEngine(orchestrator, metadataStore, vectorStore);

// RAGEngine WITHOUT embeddings (still works)
var engine = new RAGEngine(orchestrator, metadataStore, vectorStore: null);

// Query with embeddings disabled
var options = new RAGQueryOptions
{
    UseEmbeddings = false,  // Skip vector search
    MetadataFilter = filter,
    KeywordSearchText = "search term"
};
```

### Composite Scoring Enhances, Not Replaces

Composite scoring is **additive enhancement**, not a replacement:

| Scenario | Behavior |
|----------|----------|
| Scoring enabled, embeddings enabled | Full composite scoring |
| Scoring enabled, embeddings disabled | Tag + recency + position scoring |
| Scoring disabled, embeddings enabled | Cosine similarity only |
| Scoring disabled, embeddings disabled | Metadata filtering + keyword search |

---

## Interface Hierarchy

```
IDocumentMetadataStore (base)
    │
    └── IQueryableMetadataStore (extended query capabilities)
            │
            ├── QueryAsync(MetadataFilter)
            ├── SearchTextAsync(text, filter)
            ├── GetMatchingIdsAsync(filter)
            └── CountAsync(filter)

ITagScoringService (query-adaptive scoring)
    │
    ├── ScoreTagsAsync(tags, queryContext)
    └── GetOrComputeScoresAsync(tags, context, cacheAge)

ICompositeScorer (multi-signal ranking)
    │
    ├── Score(document, tagIndex, options)
    └── ScoreAndRank(documents, tagIndex, options)

ITagRelevanceStore (score persistence)
    │
    ├── StoreAsync(index)
    ├── GetByChecksumAsync(checksum)
    └── GetLatestAsync()
```

---

## Implementation Guidelines

### For Agent Developers

1. **Never read files directly** - always go through knowledge stores
2. **Always check for null** on optional services (embeddings, scoring)
3. **Use metadata filtering first** - it's fast and always available
4. **Treat embeddings as enhancement** - not requirement

### For Store Implementers

1. **Metadata must be queryable without embeddings**
2. **Support multiple embedding models per document**
3. **Implement graceful degradation** for missing data
4. **Cache expensive computations** (tag scores, embeddings)

### For Configuration

```json
{
  "Storage": {
    "DatabasePath": "knowledge.db",
    "EmbeddingsEnabled": true,
    "DefaultEmbeddingModel": "openai"
  },
  "CompositeScoring": {
    "Enabled": true,
    "UseLLMTagScoring": true,
    "TagScoreCacheHours": 24
  }
}
```

---

## Migration Notes

### From File-Based to Database Storage

1. Metadata migrates to SQLite tables
2. Embeddings migrate to embeddings table with model versioning
3. File-based stores remain as fallback/legacy support
4. Chunk files become rebuildable from source

### Backwards Compatibility

- `DocumentMetadataFileStore` → continues to work (legacy)
- `QueryableMetadataFileStore` → continues to work (file-based)
- `QueryableMetadataSqliteStore` → new recommended store
- All interfaces remain stable

---

## Summary

| Principle | Implementation |
|-----------|----------------|
| Database is truth | SQLite knowledge DB per project |
| Embeddings are optional | Multiple models, rebuildable, degradable |
| Files are source only | Never queried by agents directly |
| Metadata is first-class | Always queryable via SQL |
| Search is layered | Metadata → Embeddings → Scoring → Agent |
| Scoring enhances | Composite scoring is additive, not required |

**The architecture ensures Hazina remains functional and useful even when advanced features (embeddings, LLM scoring) are unavailable or disabled.**
