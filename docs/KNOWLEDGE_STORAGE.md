# Knowledge Storage & Search Model

This document defines the canonical storage and search architecture for Hazina. All implementations must conform to these design principles.

---

## Canonical Design Principle

Hazina is an **agent-first knowledge framework** where:

1. **Metadata is the primary knowledge layer** — structured data, tags, and properties are queryable without semantic search
2. **Embeddings are a replaceable search index** — a secondary acceleration layer, not the source of truth
3. **A project-scoped SQLite database is the single query and knowledge layer** — not an embedding file

### Design Decisions (Not Implementation Details)

These are architectural choices that define how Hazina operates:

| Principle | Description |
|-----------|-------------|
| **Database as truth** | The SQLite knowledge database is the canonical store. All queries route through it. |
| **Files as sources** | Documents, chunks, and source files are inputs, not the queryable layer. |
| **Embeddings as optional** | If embeddings are disabled, Hazina continues to function correctly. |
| **Rebuild over restore** | Embeddings can be regenerated from sources. They are not backup-critical. |

This architecture ensures Hazina remains:
- Independent of specific embedding providers
- Functional without vector search infrastructure
- Reproducible from source files alone

---

## Metadata: First-Class Citizen

Metadata, tags, and structural properties are **always stored in the database** and **always queryable**.

### What Metadata Includes

- Document source, type, and category
- Chunk position, parent document, and sequence
- Custom tags assigned by users or ingestion pipelines
- Structural properties (file path, checksum, last modified)
- Agent-defined labels and classifications

### Query Capabilities

Agents query metadata directly via SQL without requiring embeddings:

```sql
-- Find all chunks from a specific source
SELECT * FROM chunks WHERE source = 'api-reference.md';

-- Find chunks by tag
SELECT c.* FROM chunks c
JOIN chunk_tags ct ON c.id = ct.chunk_id
WHERE ct.tag = 'authentication';

-- Find recently modified content
SELECT * FROM items WHERE modified_at > datetime('now', '-7 days');
```

### Design Guarantee

```
Metadata queries never require embedding computation.
```

This ensures:
- Fast filtering before semantic search
- Deterministic results for structured queries
- Agent control over what content enters the search pipeline

**Relevance is not equivalent to semantic similarity.** An agent may determine relevance through:
- Source authority
- Recency
- Tag matching
- Structural position
- Explicit priority

---

## Embeddings: Secondary Search Index

Embeddings are **optional**, **replaceable**, and **serve only as search accelerators**.

### Role of Embeddings

| Use | Not For |
|-----|---------|
| Relevance ranking within filtered results | Primary truth |
| Semantic similarity scoring | Sole retrieval mechanism |
| Approximate nearest neighbor search | Replacing metadata queries |

### Design Properties

1. **Optional**: Embeddings can be disabled entirely. The system must function with keyword/metadata search alone.
2. **Replaceable**: Swap embedding providers (OpenAI, local models, etc.) without data loss. Re-embed from sources.
3. **Multiple per item**: A single chunk may have embeddings from multiple providers or models.
4. **Rebuild-safe**: Embeddings are derived data. They can be deleted and regenerated.

### Configuration

```json
{
  "Knowledge": {
    "EmbeddingsEnabled": true,
    "EmbeddingProvider": "openai",
    "FallbackToKeyword": true
  }
}
```

When `EmbeddingsEnabled: false`:
- Indexing stores metadata and text only
- Search uses keyword matching (BM25 or full-text)
- Agents receive results without similarity scores

**If embeddings are disabled, Hazina must continue to function correctly.**

---

## Agent-First Search Flow

Search in Hazina follows a layered pattern where **the agent controls the search strategy**:

```
┌─────────────────────────────────────────────────────────────┐
│                     Agent Query                              │
│  "Find authentication examples in recent API docs"          │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│             Step 1: Metadata Filtering (SQL)                 │
│  WHERE source LIKE 'api-%' AND modified_at > 7 days ago     │
│  Result: 45 chunks match                                     │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│        Step 2: Optional Embedding Similarity Search          │
│  Vector search within filtered set (if enabled)              │
│  Result: Top 10 by semantic similarity                       │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│         Step 3: Agent-Side Interpretation & Ranking          │
│  Agent reviews results, applies domain logic                 │
│  May discard low-relevance matches, request more context     │
└─────────────────────────────────────────────────────────────┘
```

### Key Principles

1. **The agent decides** whether to use embeddings for a given query
2. **Not every query is semantic** — exact matches, tag queries, and structural queries bypass embeddings
3. **Metadata filtering reduces search space** before expensive vector operations
4. **Agent interprets results** — similarity scores inform but don't dictate relevance

### Implementation

```csharp
public class AgentSearchService
{
    public async Task<SearchResult> SearchAsync(AgentQuery query)
    {
        // Step 1: Always start with metadata filtering
        var filtered = await _knowledgeDb.FilterByMetadataAsync(
            query.SourceFilter,
            query.TagFilter,
            query.DateRange
        );

        // Step 2: Optionally apply embedding search
        if (query.UseSemanticSearch && _config.EmbeddingsEnabled)
        {
            filtered = await _embeddingSearch.RankBySimilarityAsync(
                query.Text,
                filtered
            );
        }

        // Step 3: Return to agent for interpretation
        return new SearchResult
        {
            Items = filtered,
            SearchStrategy = query.UseSemanticSearch ? "hybrid" : "metadata"
        };
    }
}
```

---

## SQLite Knowledge Database

The knowledge database is conceptually structured as follows (implementation may vary):

### Logical Structure

```
┌─────────────────────────────────────────────────────────────┐
│                    Knowledge Database                        │
├─────────────────────────────────────────────────────────────┤
│  Items                                                       │
│  ├── id (unique identifier)                                 │
│  ├── content (text, JSON, or reference)                     │
│  ├── type (chunk, document, post, json_object)              │
│  ├── source_file (path to original file)                    │
│  └── checksum (integrity verification)                       │
├─────────────────────────────────────────────────────────────┤
│  Metadata                                                    │
│  ├── item_id → Items.id                                     │
│  ├── key (metadata property name)                           │
│  └── value (metadata property value)                        │
├─────────────────────────────────────────────────────────────┤
│  Tags                                                        │
│  ├── item_id → Items.id                                     │
│  └── tag (tag name)                                         │
├─────────────────────────────────────────────────────────────┤
│  Embeddings                                                  │
│  ├── item_id → Items.id                                     │
│  ├── provider (openai, local, etc.)                         │
│  ├── model (text-embedding-ada-002, etc.)                   │
│  └── vector (float array)                                   │
├─────────────────────────────────────────────────────────────┤
│  FileReferences                                              │
│  ├── item_id → Items.id                                     │
│  ├── file_path (absolute path to source)                    │
│  ├── checksum (SHA256 of file content)                      │
│  └── indexed_at (timestamp)                                  │
└─────────────────────────────────────────────────────────────┘
```

### Design Notes

- **Items** represent any queryable unit: chunks, documents, JSON objects, social posts
- **Metadata** is key-value, allowing arbitrary properties without schema changes
- **Tags** are a first-class concept for classification and filtering
- **Embeddings** are stored per-provider, allowing multiple models per item
- **FileReferences** track source provenance and enable change detection

This structure supports:
- Full-text search on content
- Metadata filtering via SQL
- Tag-based navigation
- Embedding similarity (when enabled)
- Source file integrity verification

---

## Migrating from Embedding Files

If your project previously used standalone embedding files:

### Before (Embedding File Approach)
```
project/
├── data/
│   ├── embeddings.json      ← Primary store (problematic)
│   └── documents/           ← Source files
```

### After (Knowledge Database Approach)
```
project/
├── data/
│   ├── knowledge.db         ← SQLite database (primary)
│   └── documents/           ← Source files (unchanged)
```

### Migration Principles

1. **Embeddings are rebuilt, not migrated** — regenerate from source files
2. **Metadata is extracted and stored** — parse from filenames, directories, file content
3. **Database becomes queryable immediately** — no embedding generation required for basic function

---

## Why This Matters

This architecture makes Hazina:

| Property | Benefit |
|----------|---------|
| **Scalable** | SQLite handles millions of items; embeddings computed on-demand |
| **Reproducible** | Delete database, re-index from sources, get identical results |
| **Agent-compatible** | Agents control search strategy, not locked to vector similarity |
| **Provider-independent** | Swap embedding models without data migration |
| **Offline-capable** | Metadata search works without API calls |

### For Agentic Workflows

Agents need:
- **Deterministic queries** for structured information (metadata)
- **Fuzzy search** for semantic exploration (embeddings)
- **Control** over which approach to use

Hazina provides both, with clear separation and agent control.

---

## Related Documentation

- [RAG Guide](RAG_GUIDE.md) — Document indexing, chunking, retrieval
- [Architecture Standards](ARCHITECTURE_STANDARDS.md) — Mandatory patterns
- [Supabase Setup](SUPABASE_SETUP.md) — Cloud database backend

---

*Last updated: 2026-01-03*
