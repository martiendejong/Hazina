# Hazina Vector Store

Hazina ships three vector storage backends behind the same `IEmbeddingStore` /
`IVectorSearchStore` contracts so applications can swap backends by changing
configuration only.

| Package                       | Backend                       | Use case                                         |
| ----------------------------- | ----------------------------- | ------------------------------------------------ |
| `Hazina.Store.EmbeddingStore` | Abstractions + Postgres core  | Reference / shared types                         |
| `Hazina.Store.PgVector`       | PostgreSQL + `pgvector`       | Production workloads, large indices, HNSW/IVFFlat |
| `Hazina.Store.Sqlite`         | SQLite (single file)          | Local development, embedded apps, tests          |

All three packages publish symbols (`.snupkg`) and SourceLink, so step-through
debugging works directly from the published NuGet packages.

## Install

```bash
dotnet add package Hazina.Store.EmbeddingStore
dotnet add package Hazina.Store.PgVector   # production
# or
dotnet add package Hazina.Store.Sqlite     # local
```

## Wire it up — pgvector backend

```csharp
using Hazina.Store;
using Hazina.Store.PgVector;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPgVectorEmbeddingStore(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("Vectors")!;
    options.TableName        = "embeddings";
    options.IndexType        = PgVectorIndexType.Hnsw;   // or IvfFlat
    options.Dimensions       = 1536;                      // OpenAI text-embedding-3-small
});
```

## Wire it up — SQLite backend

```csharp
using Hazina.Store.Sqlite;

builder.Services.AddSqliteEmbeddingStore(options =>
{
    options.DatabasePath = "data/vectors.db";
    options.EnableFts5   = true;   // hybrid keyword + vector retrieval
});
```

## Search example

```csharp
public sealed class SearchService(IEmbeddingStore store, IEmbeddingProvider embedder)
{
    public async Task<IReadOnlyList<EmbeddingMatch>> SearchAsync(string query, int topK = 5)
    {
        var vector = await embedder.EmbedAsync(query);
        return await store.SearchAsync(vector, topK);
    }
}
```

## Choosing a backend

- **Local development / tests** → `Hazina.Store.Sqlite`. Zero infrastructure,
  single file. FTS5 hybrid retrieval is built in.
- **Production / >1M vectors** → `Hazina.Store.PgVector`. Use HNSW for low
  latency reads, IVFFlat for very large datasets where build time matters.
- **Just need the contracts** → `Hazina.Store.EmbeddingStore`. Reference it
  from libraries that should not pin a backend.

See `docs/examples/04-basic-rag` for an end-to-end RAG example using
`IDocumentStore` + `IEmbeddingStore`.
