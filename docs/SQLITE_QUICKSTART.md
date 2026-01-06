# SQLite Storage Backend - Quick Start Guide

**Status**: ✅ Implemented (2026-01-06)
**Commit**: `DB_EMB2: Implement SQLite storage backend as configurable option`

## Overview

Hazina now supports SQLite as a storage backend option, providing single-file database storage ideal for:
- Local development and testing
- Embedded scenarios
- Portable projects
- Projects without external database requirements

**Key Features**:
- ✅ Single-file database (`hazina.db`)
- ✅ FTS5 full-text search
- ✅ File checksum tracking for rebuildability
- ✅ Schema versioning for migrations
- ✅ Optional embeddings (metadata-first mode)
- ✅ All existing backends remain supported

## Configuration Options

Hazina maintains its multi-backend strategy pattern. You can choose:

| Backend | Use Case | Configuration |
|---------|----------|---------------|
| **SQLite** | Local, embedded, portable | `SqliteSettings.Enabled = true` |
| **File-based** | Legacy, simple projects | Default (no config needed) |
| **PostgreSQL/Supabase** | Cloud, production, scale | `SupabaseSettings.Enabled = true` |

## Quick Start

### Option 1: Code Configuration

```csharp
using HazinaStore.Models;
using Hazina.Tools.Data;

var config = new HazinaStoreConfig
{
    ApiSettings = new ApiSettings
    {
        OpenApiKey = "your-openai-key"
    },
    SqliteSettings = new SqliteSettings
    {
        Enabled = true,
        DatabasePath = "./myproject.db" // Relative or absolute path
    }
};

// Get store setup - automatically uses SQLite
var storeSetup = StoreProvider.GetStoreSetup(config);

// Use the store
await storeSetup.Store.Store("doc1", "Hello World!", new DocumentMetadata
{
    Id = "doc1",
    MimeType = "text/plain",
    Tags = new List<string> { "example", "demo" }
});
```

### Option 2: JSON Configuration

**appsettings.json**:
```json
{
  "HazinaStore": {
    "ApiSettings": {
      "OpenApiKey": "your-openai-key"
    },
    "SqliteSettings": {
      "Enabled": true,
      "DatabasePath": "./hazina.db",
      "EmbeddingsOptional": false
    }
  }
}
```

**Usage**:
```csharp
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var config = configuration.GetSection("HazinaStore").Get<HazinaStoreConfig>();
var storeSetup = StoreProvider.GetStoreSetup(config);
```

### Option 3: Environment Variables

```bash
# Windows
set SQLITE_DB_PATH=C:\data\hazina.db
set OPENAI_API_KEY=your-key

# Linux/Mac
export SQLITE_DB_PATH=/data/hazina.db
export OPENAI_API_KEY=your-key
```

```csharp
var config = new HazinaStoreConfig
{
    SqliteSettings = new SqliteSettings
    {
        Enabled = true,
        DatabasePath = Environment.GetEnvironmentVariable("SQLITE_DB_PATH")
    }
};
```

## Advanced Features

### Metadata-First Mode (No Embeddings)

Save costs by using only metadata and keyword search:

```csharp
var config = new HazinaStoreConfig
{
    SqliteSettings = new SqliteSettings
    {
        Enabled = true,
        DatabasePath = "./hazina.db",
        EmbeddingsOptional = true // ← Disables embeddings
    }
};

var storeSetup = SqliteStoreProvider.GetMetadataOnlyStoreSetup(
    config.SqliteSettings,
    apiKey,
    projectFolder
);
```

**How it works**:
- No embedding generation (no OpenAI embedding API calls)
- Uses FTS5 full-text search for keyword matching
- Metadata filtering via SQL queries
- Tag-based categorization
- Perfect for text-heavy projects where semantic search isn't critical

### Custom Connection String

```csharp
var config = new HazinaStoreConfig
{
    SqliteSettings = new SqliteSettings
    {
        Enabled = true,
        ConnectionString = "Data Source=C:\\mydb.db;Mode=ReadWriteCreate;Cache=Shared"
    }
};
```

### Database Schema

The SQLite backend creates the following tables:

```sql
-- Core knowledge
items          -- Documents with checksums and metadata
metadata       -- Custom key-value metadata
tags           -- Document tags for categorization
chunks         -- Document chunks with parent references
embeddings     -- Vector embeddings (optional)
items_fts      -- FTS5 full-text search index

-- System
schema_info    -- Schema version tracking
```

## Migration from File-Based Storage

To migrate an existing project from file-based to SQLite:

1. **Keep your existing data** (optional backup):
   ```bash
   cp -r ./embeddings ./embeddings.backup
   ```

2. **Update configuration**:
   ```csharp
   // Change from:
   var storeSetup = StoreProvider.GetStoreSetup(folder, apiKey);

   // To:
   var config = new HazinaStoreConfig
   {
       ApiSettings = new ApiSettings { OpenApiKey = apiKey },
       SqliteSettings = new SqliteSettings
       {
           Enabled = true,
           DatabasePath = "./hazina.db"
       }
   };
   var storeSetup = StoreProvider.GetStoreSetup(config);
   ```

3. **Re-ingest your documents**:
   ```csharp
   // The new SQLite store will be empty initially
   // Re-run your document ingestion process
   foreach (var file in Directory.GetFiles(sourceFolder))
   {
       var content = File.ReadAllText(file);
       await storeSetup.Store.Store(file, content, new DocumentMetadata { ... });
   }
   ```

**Note**: A dedicated migration tool is planned for future releases to automate data transfer from file-based stores to SQLite.

## Performance Characteristics

| Operation | File-Based | SQLite | Notes |
|-----------|-----------|--------|-------|
| **Small datasets** (<1K docs) | Fast | Fast | Comparable performance |
| **Large datasets** (>10K docs) | Slow | Fast | SQLite scales better |
| **Startup time** | Slow (loads all) | Fast | No full index rebuild |
| **Disk usage** | Moderate | Lower | SQLite compression |
| **Metadata queries** | In-memory | SQL | SQLite more efficient |
| **Full-text search** | Slow | Fast | FTS5 optimized |

## Troubleshooting

### Database Locked Error
```
Error: database is locked
```

**Solution**: SQLite uses file-based locking. Ensure only one process accesses the database at a time, or use `Cache=Shared` in the connection string.

### FTS5 Not Available
```
Error: no such module: fts5
```

**Solution**: Ensure you're using a recent version of Microsoft.Data.Sqlite (9.0.0+) which includes FTS5 support.

### Path Not Found
```
Error: unable to open database file
```

**Solution**: Ensure the directory exists. SqliteSettings automatically creates the directory if using `DatabasePath`, but not for custom connection strings.

## Next Steps

- ✅ **Implemented**: SQLite backend with FTS5 search
- ✅ **Implemented**: File checksum tracking
- ✅ **Implemented**: Schema versioning
- ⏳ **Planned**: Migration tool (file → SQLite)
- ⏳ **Planned**: Performance benchmarks vs file-based
- ⏳ **Planned**: Advanced query examples

## Architecture Notes

The SQLite implementation follows Hazina's canonical storage model:

**Knowledge Layer Hierarchy**:
```
Items (source files with checksums)
  ↓
Metadata + Tags + Chunks (always queryable)
  ↓
Embeddings (optional, rebuildable)
```

**Search Flow**:
```
Agent Query
  ↓
SQL Metadata Filter (fast, always available)
  ↓
(Optional) Embedding Similarity Search
  ↓
Agent Interpretation
```

This design ensures:
- Database is the single source of truth
- Embeddings are secondary and optional
- System works without embeddings
- Full rebuildability via checksums
- Agent-first architecture

## Support

For questions or issues:
- See full architecture analysis: `docs/architecture-storage-analysis.md`
- File issues: [GitHub Issues](https://github.com/martiendejong/Hazina/issues)
- Review commit: `DB_EMB2: Implement SQLite storage backend`

---

**Last Updated**: 2026-01-06
**Version**: Hazina 2.0.0+
