# Hazina Storage Architecture Analysis
**Date:** 2026-01-06
**Purpose:** Analysis and decision document for evolving Hazina's storage architecture from file-based to SQLite-based knowledge database

---

## Executive Summary

Hazina currently uses a **multi-backend strategy pattern** with file-based storage as the default. The intended evolution is toward a **SQLite-based agent-first knowledge architecture** where the database is the primary knowledge layer and embeddings are a secondary, optional search index.

**Key Finding:** The current architecture is highly flexible but lacks a single source of truth. Migrating to SQLite as the primary storage would require significant refactoring and data migration, but the abstractions are well-designed to support this transition.

**Recommendation:** Incremental migration strategy (see [Recommended Path Forward](#recommended-path-forward) section).

---

## 1. Current Architecture Analysis

### 1.1 Storage Implementations

Hazina implements a **Strategy Pattern** for storage, supporting multiple backends:

| Backend | Status | Primary Use |
|---------|--------|-------------|
| **File-based** | ✅ Active, Default | Production |
| **Memory-based** | ✅ Active | Testing, temporary |
| **PostgreSQL** | ✅ Active | Supabase integration |
| **SQLite** | ❌ Obsolete placeholder | Never implemented |

#### File-Based Storage Structure

```
project_folder/
├── embeddings/
│   └── embeddings.json          # All embeddings (vectors + keys)
├── parts/
│   ├── chunks.json              # Document → chunk keys mapping
│   └── [chunk text files]       # Individual chunk content
├── metadata/
│   ├── doc1.metadata.json       # Per-document metadata
│   ├── doc2.metadata.json
│   └── ...
├── tag_relevance/               # LLM-computed tag scores
│   └── [tag score cache files]
└── [text files]                 # Source documents
```

**Key Characteristics:**
- Embeddings stored in single JSON file (`embeddings.json`)
- Metadata distributed across many JSON files (one per document)
- In-memory index built on startup for fast metadata queries
- Chunks stored as separate text files + JSON index
- No checksums or versioning for rebuildability

### 1.2 Core Components

#### EmbeddingStore (`Hazina.Store.EmbeddingStore`)

**File Implementations:**
- `EmbeddingFileStore` (obsolete) - Legacy file store
- `EmbeddingJsonFileStore` - Current file-based implementation
- `EmbeddingMemoryStore` - In-memory store
- `SqliteTextEmbeddingStore` (obsolete, never implemented)

**Database Implementations:**
- `PgVectorStore` - PostgreSQL with pgvector
- `PgVectorTextEmbeddingStore` - PostgreSQL text + embeddings

**Location:** `src/Core/Storage/Hazina.Store.EmbeddingStore/`

**Key Findings:**
- ❌ SQLite store marked obsolete (`SqliteTextEmbeddingStore.cs:9`)
- ✅ Clean abstraction via `ITextEmbeddingStore` interface
- ❌ File-based stores load entire embeddings into memory
- ❌ No support for multiple embeddings per item

#### DocumentStore (`Hazina.Store.DocumentStore`)

**Responsibilities:**
- Orchestrates EmbeddingStore, TextStore, ChunkStore, MetadataStore
- Document ingestion and chunking
- Semantic search via embeddings
- Metadata filtering via QueryableMetadataStore

**File Implementations:**
- `ChunkFileStore` - chunks.json (single file index)
- `DocumentMetadataFileStore` - one .metadata.json per document
- `QueryableMetadataFileStore` - file-based + in-memory index
- `TagRelevanceFileStore` - cached tag scores

**Location:** `src/Core/Storage/Hazina.Store.DocumentStore/`

**Key Findings:**
- ✅ Clean separation of concerns
- ✅ Supports both embedding-based and metadata-only search
- ❌ No concept of "source file checksums" for rebuildability
- ❌ Dual truth: both files and stores contain information

#### QueryableMetadataFileStore

**Implementation Details:**
- Stores metadata in separate JSON files (`doc_id.metadata.json`)
- Maintains **in-memory `ConcurrentDictionary<string, DocumentMetadata>`**
- Index rebuilt on startup by scanning `_rootFolder`
- Supports rich queries: tags, MIME types, dates, custom metadata, full-text search

**Location:** `src/Core/Storage/Hazina.Store.DocumentStore/Stores/File/QueryableMetadataFileStore.cs`

**Key Findings:**
- ✅ Sophisticated querying capabilities
- ✅ Fast in-memory queries after initial load
- ❌ Not scalable to millions of documents (all in memory)
- ❌ No SQL layer, all filtering in C#
- ❌ Index rebuild required on every startup

### 1.3 RAG Engine (`Hazina.AI.RAG`)

**Search Modes:**
1. **Embedding-based search** (default)
   - Requires `IVectorStore`
   - Generates query embedding
   - Performs vector similarity search

2. **Metadata-only search** (new capability)
   - Uses `IQueryableMetadataStore`
   - No embeddings required
   - Keyword search + metadata filtering

**Location:** `src/Core/AI/Hazina.AI.RAG/Core/RAGEngine.cs`

**Key Findings:**
- ✅ **Already supports metadata-first search!** (lines 108-112)
- ✅ Optional embeddings via `options.UseEmbeddings = false`
- ✅ Composite scoring with tag relevance, recency, position
- ✅ Good alignment with intended architecture

### 1.4 Agent Framework (`Hazina.AI.Agents`)

**Key Characteristics:**
- Uses `IProviderOrchestrator` for LLM calls
- Maintains conversation history in memory
- **Does not directly access files or databases**
- Interacts via abstractions (stores, tools, context)

**Location:** `src/Core/AI/Hazina.AI.Agents/Core/Agent.cs`

**Key Findings:**
- ✅ Good abstraction - agents don't know about storage
- ✅ Agent-first principle partially satisfied
- ❌ Storage layer still file-based beneath abstractions

---

## 2. Intended Architecture (Target State)

Based on the provided architectural vision:

### 2.1 Core Principles

1. **SQLite as Primary Knowledge Layer**
   - All metadata, tags, relationships, JSON objects, embeddings stored in SQLite
   - One database per project (`project.hazina.db`)
   - No standalone embeddings file

2. **Files as Source Material Only**
   - Files are not authoritative
   - Agents never query files directly
   - Only interact via database query layer

3. **Search Flow**
   ```
   Agent Query → SQL Metadata Filter → (Optional) Embedding Search → Agent Interpretation
   ```

4. **Embeddings as Secondary Index**
   - Optional, replaceable, degradable
   - Multiple embedding models per item supported
   - System must work without embeddings

5. **Full Rebuildability**
   - Database reconstructed from source files
   - File references, checksums, ingest versions tracked
   - Deterministic rebuild

### 2.2 Proposed Database Schema (Conceptual)

```sql
-- Core knowledge tables
CREATE TABLE items (
    id TEXT PRIMARY KEY,
    source_file TEXT,           -- Reference to source file
    file_checksum TEXT,         -- For rebuild validation
    ingest_version INTEGER,     -- Schema version for migrations
    mime_type TEXT,
    content TEXT,               -- Text content or JSON
    created_at TIMESTAMP,
    updated_at TIMESTAMP
);

CREATE TABLE metadata (
    item_id TEXT,
    key TEXT,
    value TEXT,                 -- JSON for complex values
    FOREIGN KEY (item_id) REFERENCES items(id)
);

CREATE TABLE tags (
    item_id TEXT,
    tag TEXT,
    FOREIGN KEY (item_id) REFERENCES items(id)
);

CREATE TABLE relationships (
    source_id TEXT,
    target_id TEXT,
    relationship_type TEXT,
    FOREIGN KEY (source_id) REFERENCES items(id),
    FOREIGN KEY (target_id) REFERENCES items(id)
);

-- Embeddings (secondary index)
CREATE TABLE embeddings (
    item_id TEXT,
    model TEXT,                 -- e.g., "text-embedding-3-small"
    vector BLOB,                -- Raw embedding bytes
    dimension INTEGER,
    created_at TIMESTAMP,
    FOREIGN KEY (item_id) REFERENCES items(id)
);

-- Full-text search (SQLite FTS5)
CREATE VIRTUAL TABLE items_fts USING fts5(item_id, content);
```

---

## 3. Comparison: Current vs Intended

| Aspect | Current (File-based) | Intended (SQLite) | Status |
|--------|---------------------|-------------------|--------|
| **Primary Storage** | `embeddings.json` + distributed `.metadata.json` | Single `project.hazina.db` | ❌ Major gap |
| **Embeddings Location** | File (`embeddings.json`) | Database table | ❌ |
| **Metadata Storage** | Distributed JSON files + in-memory index | Database table with SQL queries | ❌ |
| **Chunks** | `chunks.json` + text files | Database table | ❌ |
| **Multiple Embeddings** | Not supported | Supported | ❌ |
| **Rebuildability** | No checksums/versions | Full tracking | ❌ |
| **Agent Abstraction** | ✅ Agents use stores, not files | ✅ Agents use DB | ✅ Good |
| **Metadata-first Search** | ✅ Already supported in RAG | ✅ Required | ✅ Good |
| **Optional Embeddings** | Partial (RAG supports it) | Required | 🟡 Partial |
| **Source of Truth** | Files are truth | Database is truth | ❌ Major gap |

**Legend:**
- ✅ Compliant or already implemented
- 🟡 Partially compliant
- ❌ Non-compliant or missing

---

## 4. Architectural Conflicts and Risks

### 4.1 Design Conflicts

#### Conflict 1: Strategy Pattern vs Single Source of Truth
- **Current:** Multi-backend strategy allows file, memory, or database
- **Intended:** SQLite is the only backend (mono-backend)
- **Impact:** Requires removing or deprecating file/memory backends
- **Risk:** Breaking changes for all existing users

#### Conflict 2: Distributed State
- **Current:** State split across embeddings.json + chunks.json + many .metadata.json + text files
- **Intended:** Single database file with all state
- **Impact:** Complex data migration required
- **Risk:** Data loss if migration fails

#### Conflict 3: In-Memory Index
- **Current:** `QueryableMetadataFileStore` loads all metadata into memory for fast queries
- **Intended:** SQL queries against database (SQLite is disk-based)
- **Impact:** Potential performance regression for small datasets
- **Risk:** User perception of "slowdown" even if imperceptible

#### Conflict 4: Embeddings as Truth
- **Current:** `embeddings.json` is loaded and directly used for search
- **Intended:** Embeddings are secondary index, can be disabled
- **Impact:** Search must work without embeddings
- **Risk:** Current code assumes embeddings always exist

#### Conflict 5: No Dual Truth Protection
- **Current:** Files and stores can diverge (no checksums)
- **Intended:** Database tracks file checksums to detect drift
- **Impact:** Need file monitoring or explicit refresh
- **Risk:** Stale data in database

### 4.2 Migration Risks

| Risk | Probability | Impact | Severity |
|------|------------|--------|----------|
| Data loss during migration | Medium | High | 🔴 Critical |
| Breaking existing integrations | High | High | 🔴 Critical |
| Performance regression | Low | Medium | 🟡 Moderate |
| User resistance to change | Medium | Medium | 🟡 Moderate |
| Incomplete migration code | Medium | High | 🟠 High |
| SQLite corruption | Low | High | 🟠 High |
| Scale issues (large projects) | Low | Medium | 🟡 Moderate |

### 4.3 Backward Compatibility

**Breaking Changes Required:**
1. ❌ Default storage backend change (file → SQLite)
2. ❌ Storage path structure (folder → .db file)
3. ❌ No direct file access (apps relying on embeddings.json)
4. ❌ Configuration changes (StoreProvider API)

**Mitigations:**
- Provide migration tool
- Keep file-based stores as "legacy mode" (deprecated)
- Version configuration files
- Extensive migration documentation

---

## 5. Recommended Path Forward

### 5.1 Strategy: **Incremental Migration with Hybrid Transition**

**Rationale:**
- Minimizes risk by implementing SQLite backend alongside existing file backend
- Allows gradual user migration
- Provides rollback path
- Enables A/B testing of performance

### 5.2 Implementation Phases

#### Phase 1: **SQLite Store Implementation** (Foundation)
**Duration:** 2-3 weeks
**Effort:** High

**Tasks:**
1. Implement `SqliteEmbeddingStore` with proper FTS5 support
2. Implement `SqliteMetadataStore` with rich query capabilities
3. Implement `SqliteChunkStore` for chunk management
4. Implement `SqliteDocumentStore` orchestrator
5. Add schema versioning (`ingest_version` field)
6. Add file checksum tracking

**Deliverables:**
- `src/Core/Storage/Hazina.Store.Sqlite/` project
- Unit tests with 80%+ coverage
- Performance benchmarks vs file-based

**Acceptance Criteria:**
- All `ITextEmbeddingStore`, `IDocumentMetadataStore`, `IChunkStore` interfaces implemented
- Passes all existing DocumentStore tests
- Performance within 20% of file-based for typical workloads

---

#### Phase 2: **Migration Tooling** (Transition)
**Duration:** 1-2 weeks
**Effort:** Medium

**Tasks:**
1. Create `MigrationEngine` class
   - Reads embeddings.json, chunks.json, .metadata.json
   - Writes to SQLite with checksums
   - Validates data integrity
2. Create CLI migration tool (`hazina migrate file-to-sqlite`)
3. Add rollback capability (`hazina migrate sqlite-to-file`)
4. Create migration progress reporting
5. Handle large datasets (streaming, batching)

**Deliverables:**
- `src/Tools/Migration/Hazina.Tools.Migration/` project
- CLI command `hazina migrate`
- Migration verification tests

**Acceptance Criteria:**
- Can migrate 10k+ documents without failures
- Idempotent (running twice = same result)
- Progress reporting and error handling

---

#### Phase 3: **Hybrid Mode** (Coexistence)
**Duration:** 1 week
**Effort:** Low

**Tasks:**
1. Update `StoreProvider.GetStoreSetup()` to support SQLite backend
2. Add configuration option: `StorageBackend = "file" | "sqlite" | "postgres"`
3. Update documentation with migration guide
4. Create sample apps using SQLite backend

**Configuration Example:**
```json
{
  "HazinaStore": {
    "StorageBackend": "sqlite",
    "DatabasePath": "./project.hazina.db",
    "EmbeddingsOptional": true
  }
}
```

**Deliverables:**
- Updated `StoreProvider` with SQLite support
- Configuration schema updates
- Migration guide documentation

**Acceptance Criteria:**
- Users can opt-in to SQLite via config
- File-based remains default (no breaking changes yet)
- All demos work with both backends

---

#### Phase 4: **Embeddings-Optional Mode** (Graceful Degradation)
**Duration:** 1 week
**Effort:** Medium

**Tasks:**
1. Update `DocumentStore.RelevantItems()` to fall back to keyword search if no embeddings
2. Add `EmbeddingsEnabled` flag to StoreSetup
3. Update RAG engine to prefer metadata search when embeddings disabled
4. Update UI/CLI to show "embeddings disabled" status

**Behavior:**
```csharp
// When embeddings disabled:
var results = await store.RelevantItems(query);
// Falls back to full-text search + metadata scoring
// No embedding generation, no vector similarity
```

**Deliverables:**
- Updated `DocumentStore` with graceful degradation
- Tests for embeddings-disabled mode
- Documentation on performance implications

**Acceptance Criteria:**
- System works without embeddings
- Graceful degradation with clear status messages
- Performance acceptable for text-only search

---

#### Phase 5: **Rebuild & Validation** (Determinism)
**Duration:** 1 week
**Effort:** Medium

**Tasks:**
1. Add `RebuildDatabaseFromSources()` method
2. Track file checksums (SHA-256) in items table
3. Detect when source files changed
4. Implement incremental refresh (only changed files)
5. Add validation: `hazina validate` command

**Rebuild Flow:**
```
Source Files → Scan → Checksum Check → Update Only Changed → Rebuild Embeddings (optional)
```

**Deliverables:**
- `RebuildEngine` class
- CLI command `hazina rebuild`
- Incremental refresh logic

**Acceptance Criteria:**
- Deterministic: same sources = same database
- Incremental: only re-process changed files
- Fast: <1 min for 1000 unchanged files

---

#### Phase 6: **Default Switch** (Breaking Change)
**Duration:** 1 week
**Effort:** Low (coordination effort)

**Tasks:**
1. Change default `StorageBackend` to `"sqlite"`
2. File-based becomes `"file-legacy"` (deprecated)
3. Update all sample apps and demos to SQLite
4. Update README and getting-started guides
5. Release as major version bump (v2.0)

**Migration Path:**
- Users on file-based see deprecation warning
- Clear instructions to migrate or stay on legacy
- Legacy mode supported for 6 months

**Deliverables:**
- Updated defaults in `StoreProvider`
- Deprecation warnings
- v2.0 release notes

**Acceptance Criteria:**
- All new projects use SQLite by default
- Clear migration path for existing projects
- No silent breaking changes

---

#### Phase 7: **Documentation & Education** (Continuous)
**Throughout all phases**

**Tasks:**
1. Update ARCHITECTURE.md with SQLite-first design
2. Create migration FAQ
3. Create video tutorial for migration
4. Update CONTRIBUTING.md with new storage patterns
5. Create "Knowledge Storage & Search Model" documentation section (as specified in requirements)

**Documentation Sections:**
1. **Canonical Design Principle**
   - SQLite as primary storage
   - Files as source material
   - Embeddings as secondary index

2. **Role of Metadata** (first-class)
   - Always in database, always queryable
   - Agents search metadata first
   - System works without embeddings

3. **Role of Embeddings** (secondary)
   - Optional, replaceable, degradable
   - Multiple models per item
   - Rebuildable from source

4. **Agent-First Search Flow**
   - Metadata filtering (SQL)
   - Optional embedding search
   - Agent-side ranking

5. **SQLite Knowledge Database** (conceptual)
   - Schema overview
   - Query patterns
   - Indexing strategy

6. **Why This Matters**
   - Scalability
   - Reproducibility
   - Agent-centric design
   - Model independence

**Deliverables:**
- Updated documentation in `docs/`
- Blog post: "Why We Moved to SQLite"
- Tutorial videos
- Migration guide with examples

---

### 5.3 Alternative Strategies (Not Recommended)

#### Alternative 1: **Clean Break (Big Bang Migration)**
❌ **Not Recommended**
- Implement SQLite, remove file backend in single release
- **Pros:** Clean, no legacy code
- **Cons:** High risk, alienates users, hard to rollback

#### Alternative 2: **Keep Multi-Backend Forever**
❌ **Not Recommended**
- Add SQLite as 4th backend option, keep all backends
- **Pros:** No breaking changes, maximum flexibility
- **Cons:** Maintenance burden, no single source of truth, architectural confusion

#### Alternative 3: **Hybrid Forever (Supabase-style)**
🟡 **Consider if...**
- File for source docs, SQLite for embeddings/metadata only
- **Pros:** Leverages existing file scanning
- **Cons:** Dual truth remains, complexity

---

## 6. Impact Analysis

### 6.1 Performance Implications

| Operation | File-based | SQLite | Expected Change |
|-----------|-----------|--------|-----------------|
| **Metadata query (small dataset)** | Very fast (in-memory) | Fast (indexed) | -10% to +5% |
| **Metadata query (large dataset)** | Slow (load all into memory) | Fast (SQL query) | +500% to +2000% |
| **Embedding search** | Fast (in-memory) | Fast (FTS5 + vector) | ±10% |
| **Document ingest** | Fast (JSON write) | Medium (SQL insert) | -20% to -30% |
| **Startup time (small dataset)** | Fast | Very fast | +50% (no index rebuild) |
| **Startup time (large dataset)** | Very slow (load all) | Very fast | +10x to +100x |
| **Disk usage** | Moderate | Lower (compression) | -20% to -40% |

**Key Insights:**
- SQLite wins for large datasets (>10k documents)
- File-based wins for small datasets (<1k documents) on simple queries
- SQLite eliminates "load all into memory" bottleneck

### 6.2 Developer Experience

**Current (File-based):**
```csharp
// Setup
var setup = StoreProvider.GetStoreSetup(folder, apiKey);
// Works, but folder structure hidden
```

**Intended (SQLite):**
```csharp
// Setup
var setup = StoreProvider.GetStoreSetup(config);
// config.StorageBackend = "sqlite"
// config.DatabasePath = "./project.hazina.db"
// Single file, explicit, portable
```

**Improvements:**
- ✅ Single database file = easy to backup, copy, share
- ✅ SQL queries for advanced users
- ✅ Standard tooling (DB Browser for SQLite)
- ✅ Better for version control (single file vs folder)

---

## 7. Open Questions for Human Confirmation

Before proceeding with implementation, the following decisions require human input:

### Question 1: Migration Timeline
**Options:**
- A) Aggressive: Switch default in 3 months (risk: user disruption)
- B) Moderate: Switch default in 6 months (balanced)
- C) Conservative: Switch default in 12 months (safe)

**Recommendation:** B (6 months)

### Question 2: File-based Legacy Support
**Options:**
- A) Deprecate immediately, remove in 6 months
- B) Deprecate immediately, keep indefinitely (maintenance burden)
- C) No deprecation, keep both forever (architectural confusion)

**Recommendation:** A (deprecate + remove)

### Question 3: Breaking Changes Acceptable?
**Options:**
- A) Yes, bump to v2.0 and require migration
- B) No, maintain full backward compatibility forever
- C) Hybrid: Opt-in SQLite for now, force later

**Recommendation:** A (v2.0 with migration)

### Question 4: Supabase/PostgreSQL Integration
**Options:**
- A) Keep Supabase backend as alternative to SQLite
- B) Deprecate Supabase, SQLite only
- C) Keep Supabase for cloud, SQLite for local

**Recommendation:** C (both, different use cases)

### Question 5: Embedding Storage Strategy
**Options:**
- A) Store embeddings as BLOB in SQLite
- B) Store embeddings in separate vector database (e.g., Qdrant)
- C) Hybrid: SQLite for metadata, Qdrant for vectors

**Recommendation:** A for simplicity, document option C for scale

---

## 8. Next Steps (Awaiting Approval)

**Immediate actions required:**
1. ✅ Review this analysis document
2. ⏳ Answer open questions above
3. ⏳ Approve recommended strategy (incremental migration)
4. ⏳ Prioritize phases (which to start first)

**Proposed next action after approval:**
- Begin **Phase 1: SQLite Store Implementation**
- Create `src/Core/Storage/Hazina.Store.Sqlite/` project
- Implement core interfaces with FTS5 support
- Target completion: 2-3 weeks

**Blocking issues:**
- None currently. Ready to proceed upon approval.

---

## 9. References

**Files Analyzed:**
- `src/Core/Storage/Hazina.Store.EmbeddingStore/` (22 files)
- `src/Core/Storage/Hazina.Store.DocumentStore/` (43 files)
- `src/Core/AI/Hazina.AI.RAG/Core/RAGEngine.cs`
- `src/Core/AI/Hazina.AI.Agents/Core/Agent.cs`
- `src/Tools/Foundation/Hazina.Tools.Data/StoreProvider.cs`
- `docs/ARCHITECTURE.md`
- `CLAUDE.md`

**Key Code Locations:**
- Obsolete SQLite store: `Hazina.Store.EmbeddingStore/Stores/Database/SqliteTextEmbeddingStore.cs:9`
- File-based default: `Hazina.Tools.Data/StoreProvider.cs:11`
- Metadata-first search: `Hazina.AI.RAG/Core/RAGEngine.cs:108-112`
- In-memory index: `Hazina.Store.DocumentStore/Stores/File/QueryableMetadataFileStore.cs:19`

**Architectural Principles:**
- Agent-first: Agents use abstractions, not direct file access
- Metadata-first: RAG already supports metadata-only search
- Optional embeddings: Partially implemented, needs extension
- Single source of truth: **Primary gap to address**

---

## 10. Conclusion

Hazina's current file-based architecture is flexible and well-abstracted, but lacks a single source of truth and rebuildability guarantees. The proposed migration to SQLite as the primary knowledge layer aligns with the agent-first vision and provides:

1. **Scalability:** Better performance for large datasets
2. **Reproducibility:** Checksums and versioning for deterministic rebuilds
3. **Simplicity:** Single database file vs distributed JSON files
4. **Agent-centric:** Metadata-first queries, optional embeddings

**Recommended approach:** Incremental migration over 6-12 months with hybrid mode, allowing users to opt-in to SQLite while maintaining file-based legacy support temporarily.

**Critical success factors:**
- Robust migration tooling with validation
- Clear documentation and migration guides
- Performance parity or improvement for common use cases
- Zero data loss guarantee

**Risk mitigation:**
- Phase 3 (Hybrid Mode) provides safety net
- Rollback capability in migration tooling
- Extensive testing before default switch
- User communication and education

---

**Status:** ✅ Analysis Complete - Awaiting Human Approval to Proceed

**Next Review Date:** Upon stakeholder feedback

**Document Version:** 1.0
**Last Updated:** 2026-01-06
