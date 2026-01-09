# Storage Domain Entry Point

## Start Here
- **Store Provider**: `../Tools/Foundation/Hazina.Tools.Data/StoreProvider.cs` - Get configured stores
- **Configuration**: `../Tools/Foundation/Hazina.Tools.Core/Config/HazinaStoreConfig.cs`
- **SQLite Backend**: `Hazina.Store.Sqlite/` - Recommended for local development

## Key Flows

### 1. Quick Setup (SQLite)
```csharp
var config = new HazinaStoreConfig {
    SqliteSettings = new SqliteSettings {
        Enabled = true,
        DatabasePath = "./hazina.db"
    }
};
var stores = StoreProvider.GetStoreSetup(config);
```

### 2. Cloud Setup (Supabase)
```csharp
var config = new HazinaStoreConfig {
    SupabaseSettings = new SupabaseSettings {
        Enabled = true,
        Url = "https://xxx.supabase.co",
        ConnectionString = "..."
    }
};
var stores = StoreProvider.GetStoreSetup(config);
```

### 3. Store Documents
```csharp
await stores.MetadataStore.SaveMetadataAsync(docId, metadata);
await stores.ChunkStore.SaveChunksAsync(docId, chunks);
await stores.EmbeddingStore.SaveEmbeddingsAsync(docId, embeddings);
```

## Projects in This Domain
| Project | Purpose | Criticality |
|---------|---------|-------------|
| `Hazina.Store.DocumentStore` | Document metadata & chunks | CRITICAL |
| `Hazina.Store.EmbeddingStore` | Vector embeddings | CRITICAL |
| `Hazina.Store.Sqlite` | SQLite backend | IMPORTANT |

## Storage Backends
| Backend | Use Case | Setup Complexity |
|---------|----------|------------------|
| **File-based** | Development, small projects | Zero config |
| **SQLite** | Local, medium projects | Single file |
| **PostgreSQL** | Production, large scale | Server required |
| **Supabase** | Cloud, managed | Account required |

## Dependencies
- Requires: Nothing (storage is foundational)
- Optional: `Microsoft.Data.Sqlite` (for SQLite)
- Optional: `Npgsql` (for PostgreSQL)
- Optional: `supabase-csharp` (for Supabase)
