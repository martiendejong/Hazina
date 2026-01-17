# Storage Domain Architecture

## Overview Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           APPLICATION LAYER                                  │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │  RAGEngine  │  DocumentStore  │  Agent Memory  │  Your Application  │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                          STORE PROVIDER LAYER                                │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                        StoreProvider                                 │   │
│  │  StoreProvider.GetStoreSetup(config) ──► Returns configured stores  │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                      │                                       │
│         ┌────────────────────────────┼────────────────────────────┐         │
│         ▼                            ▼                            ▼         │
│  ┌─────────────────┐      ┌─────────────────┐      ┌─────────────────┐     │
│  │ SqliteSettings  │      │SupabaseSettings │      │PostgresSettings │     │
│  │ Enabled: true   │      │ Enabled: true   │      │ ConnectionString│     │
│  │ DbPath: "./db"  │      │ Url: "https://" │      │ "Host=..."      │     │
│  └─────────────────┘      └─────────────────┘      └─────────────────┘     │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                           STORE INTERFACES                                   │
│                                                                              │
│  ┌───────────────────┐  ┌───────────────────┐  ┌───────────────────┐       │
│  │  IEmbeddingStore  │  │  IMetadataStore   │  │    IChunkStore    │       │
│  │  • StoreAsync     │  │  • SaveMetadata   │  │  • Store          │       │
│  │  • GetAsync       │  │  • GetMetadata    │  │  • Get            │       │
│  │  • RemoveAsync    │  │  • Query          │  │  • GetParent      │       │
│  └───────────────────┘  └───────────────────┘  └───────────────────┘       │
│                                                                              │
│  ┌───────────────────┐  ┌───────────────────┐                               │
│  │ IVectorSearchStore│  │    ITextStore     │                               │
│  │  • SearchSimilar  │  │  • Store          │                               │
│  │  • topK, minSim   │  │  • Get            │                               │
│  └───────────────────┘  └───────────────────┘                               │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         STORAGE IMPLEMENTATIONS                              │
│                                                                              │
│  ┌───────────────────────────────────────────────────────────────────────┐ │
│  │                           FILE-BASED                                   │ │
│  │  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐       │ │
│  │  │EmbeddingFileStore│  │TextFileStore    │  │ChunkFileStore   │       │ │
│  │  │embeddings.json  │  │ /documents/     │  │chunks.json      │       │ │
│  │  └─────────────────┘  └─────────────────┘  └─────────────────┘       │ │
│  │  Best for: Development, small datasets, debugging                     │ │
│  └───────────────────────────────────────────────────────────────────────┘ │
│                                                                              │
│  ┌───────────────────────────────────────────────────────────────────────┐ │
│  │                            SQLite                                      │ │
│  │  ┌─────────────────────────────────────────────────────────────────┐ │ │
│  │  │                       hazina.db                                  │ │ │
│  │  │  ┌─────────┐ ┌──────────┐ ┌──────┐ ┌────────────┐ ┌─────────┐ │ │ │
│  │  │  │  items  │ │ metadata │ │ tags │ │   chunks   │ │embeddings│ │ │ │
│  │  │  └─────────┘ └──────────┘ └──────┘ └────────────┘ └─────────┘ │ │ │
│  │  │                                                                │ │ │
│  │  │  FTS5 Full-Text Search Index: items_fts                       │ │ │
│  │  └─────────────────────────────────────────────────────────────────┘ │ │
│  │  Best for: Local apps, medium datasets, single-file deployment       │ │
│  └───────────────────────────────────────────────────────────────────────┘ │
│                                                                              │
│  ┌───────────────────────────────────────────────────────────────────────┐ │
│  │                       PostgreSQL + pgvector                            │ │
│  │  ┌─────────────────────────────────────────────────────────────────┐ │ │
│  │  │  embeddings (vector[1536])  ──► HNSW/IVFFlat Index              │ │ │
│  │  │  document_chunks            ──► B-tree indexes                  │ │ │
│  │  │  document_metadata (JSONB)  ──► GIN indexes                     │ │ │
│  │  │  texts                      ──► Full-text search                │ │ │
│  │  └─────────────────────────────────────────────────────────────────┘ │ │
│  │  Best for: Production, large scale, concurrent access, cloud         │ │
│  └───────────────────────────────────────────────────────────────────────┘ │
│                                                                              │
│  ┌───────────────────────────────────────────────────────────────────────┐ │
│  │                          Supabase                                      │ │
│  │  ┌─────────────────────────────────────────────────────────────────┐ │ │
│  │  │  Same as PostgreSQL (Supabase is PostgreSQL)                    │ │ │
│  │  │  + Managed hosting                                               │ │ │
│  │  │  + REST API auto-generated                                       │ │ │
│  │  │  + Auth integration ready                                        │ │ │
│  │  │  + Realtime subscriptions                                        │ │ │
│  │  └─────────────────────────────────────────────────────────────────┘ │ │
│  │  Best for: Cloud-native, managed infrastructure, teams               │ │
│  └───────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Data Model

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          CANONICAL DATA MODEL                                │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                            ITEM                                      │   │
│  │  • id: string (unique key)                                          │   │
│  │  • checksum: string (SHA256 of content)                             │   │
│  │  • created_at: datetime                                              │   │
│  │  • updated_at: datetime                                              │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                      │                                       │
│           ┌──────────────────────────┼──────────────────────────┐           │
│           ▼                          ▼                          ▼           │
│  ┌─────────────────┐      ┌─────────────────┐      ┌─────────────────┐     │
│  │    METADATA     │      │      TAGS       │      │     CHUNKS      │     │
│  │  Key-value      │      │  Categorization │      │  Text segments  │     │
│  │  properties     │      │  and filtering  │      │  with positions │     │
│  │                 │      │                 │      │                 │     │
│  │  author: "John" │      │  ["api", "auth"]│      │  chunk 0: "..."│     │
│  │  version: "2.0" │      │                 │      │  chunk 1: "..."│     │
│  └─────────────────┘      └─────────────────┘      └────────┬────────┘     │
│                                                              │              │
│                                                              ▼              │
│                                                    ┌─────────────────┐     │
│                                                    │   EMBEDDINGS    │     │
│                                                    │  Vector[1536]   │     │
│                                                    │  Per chunk      │     │
│                                                    │  (OPTIONAL)     │     │
│                                                    └─────────────────┘     │
└─────────────────────────────────────────────────────────────────────────────┘

Key Principle: Embeddings are OPTIONAL and REBUILDABLE
─────────────────────────────────────────────────────
• Metadata + Tags + Chunks are the source of truth
• Embeddings can be regenerated from chunks
• Checksum tracking enables incremental rebuilds
• Metadata-first search possible without embeddings
```

## Configuration Flow

```
Application Startup
     │
     ▼
┌─────────────────────────────────────────────────────────────────┐
│  var config = new HazinaStoreConfig {                           │
│      ApiSettings = new ApiSettings { OpenApiKey = "sk-..." },  │
│                                                                  │
│      // Pick ONE of these:                                       │
│      SqliteSettings = new SqliteSettings {                      │
│          Enabled = true,                                         │
│          DatabasePath = "./hazina.db"                           │
│      }                                                           │
│      // OR                                                       │
│      SupabaseSettings = new SupabaseSettings {                  │
│          Enabled = true,                                         │
│          Url = "https://xxx.supabase.co",                       │
│          ConnectionString = "Host=..."                          │
│      }                                                           │
│  };                                                              │
└─────────────────────────────────────────────────────────────────┘
     │
     ▼
┌─────────────────────────────────────────────────────────────────┐
│  var stores = StoreProvider.GetStoreSetup(config);              │
└─────────────────────────────────────────────────────────────────┘
     │
     ▼
┌─────────────────────────────────────────────────────────────────┐
│  StoreSetup {                                                    │
│      EmbeddingStore: SqliteEmbeddingStore,                      │
│      MetadataStore: SqliteMetadataStore,                        │
│      ChunkStore: SqliteChunkStore,                              │
│      TextStore: SqliteTextStore                                 │
│  }                                                               │
└─────────────────────────────────────────────────────────────────┘
     │
     ▼
┌─────────────────────────────────────────────────────────────────┐
│  // Use stores                                                   │
│  await stores.MetadataStore.SaveMetadataAsync(docId, metadata); │
│  await stores.EmbeddingStore.StoreAsync(key, embedding, hash);  │
│  var results = await stores.EmbeddingStore.SearchSimilarAsync();│
└─────────────────────────────────────────────────────────────────┘
```

## Backend Selection Guide

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         WHICH BACKEND TO USE?                                │
│                                                                              │
│  START HERE                                                                  │
│      │                                                                       │
│      ▼                                                                       │
│  Is this production?                                                         │
│      │                                                                       │
│      ├── NO ──► Do you need persistence?                                    │
│      │              │                                                        │
│      │              ├── NO ──► Memory Store (testing only)                  │
│      │              │                                                        │
│      │              └── YES ──► How many documents?                         │
│      │                              │                                        │
│      │                              ├── < 1,000 ──► File-based             │
│      │                              │                                        │
│      │                              └── > 1,000 ──► SQLite                  │
│      │                                                                       │
│      └── YES ──► Need managed infrastructure?                               │
│                      │                                                       │
│                      ├── YES ──► Supabase                                   │
│                      │           (PostgreSQL + hosting + auth)              │
│                      │                                                       │
│                      └── NO ──► Self-hosted PostgreSQL + pgvector          │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘

Performance Comparison:
─────────────────────────────────────────────────────────────────────────────
│ Operation          │ File    │ SQLite  │ PostgreSQL │ Memory  │
├────────────────────┼─────────┼─────────┼────────────┼─────────┤
│ Single read        │ Fast    │ Fast    │ Fast       │ Fastest │
│ Single write       │ Fast    │ Fast    │ Fast       │ Fastest │
│ Bulk write (10K)   │ Slow    │ Fast    │ Fastest    │ Fast    │
│ Vector search (1K) │ ~100ms  │ ~50ms   │ ~10ms      │ ~20ms   │
│ Vector search (1M) │ Timeout │ ~5s     │ ~100ms     │ OOM     │
│ Startup time       │ Slow    │ Fast    │ Fast       │ Instant │
│ Concurrent access  │ Limited │ Limited │ Full       │ Full    │
─────────────────────────────────────────────────────────────────────────────
```

## Migration Flow

```
File-Based Storage                          SQLite Storage
─────────────────                          ───────────────
embeddings.json    ─────────────┐
chunks.json        ─────────────┼──► MigrationEngine ──► hazina.db
*.metadata.json    ─────────────┘         │
                                          │
                   ┌──────────────────────┘
                   ▼
         ┌─────────────────────────────────────────┐
         │  MigrationCommand.MigrateFileToSqlite  │
         │  Async(sourceFolder, dbPath)            │
         └─────────────────────────────────────────┘
                   │
                   ▼
         ┌─────────────────────────────────────────┐
         │  1. Scan source (count documents)       │
         │  2. Confirm with user                   │
         │  3. Migrate each document:              │
         │     - Metadata                          │
         │     - Embeddings                        │
         │     - Chunks                            │
         │     - Text content                      │
         │  4. Validate migration                  │
         │  5. Report results                      │
         └─────────────────────────────────────────┘
                   │
                   ▼
         ┌─────────────────────────────────────────┐
         │  Migration Results:                     │
         │  Status: Completed                      │
         │  Documents: 1500                        │
         │  Success: 1498                          │
         │  Failed: 2                              │
         │  Duration: 90.2s                        │
         └─────────────────────────────────────────┘
```

## Key Files

| Component | File |
|-----------|------|
| Store Provider | `Hazina.Tools.Data/StoreProvider.cs` |
| Store Config | `Hazina.Tools.Core/Config/HazinaStoreConfig.cs` |
| SQLite Settings | `Hazina.Tools.Core/Config/SqliteSettings.cs` |
| Supabase Settings | `Hazina.Tools.Core/Config/SupabaseSettings.cs` |
| SQLite Embedding | `Hazina.Store.Sqlite/SqliteEmbeddingStore.cs` |
| SQLite Metadata | `Hazina.Store.Sqlite/SqliteMetadataStore.cs` |
| SQLite Schema | `Hazina.Store.Sqlite/SqliteSchema.cs` |
| PgVector Store | `Hazina.Store.EmbeddingStore/Stores/Database/PgVectorStore.cs` |
| Migration Engine | `Hazina.Tools.Migration/FileToSqliteMigrationEngine.cs` |
| Document Store | `Hazina.Store.DocumentStore/Core/DocumentStore.cs` |
