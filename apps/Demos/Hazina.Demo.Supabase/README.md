# Hazina Supabase Integration Demo

This demo application demonstrates how to integrate Hazina with Supabase for cloud-based document storage and semantic search.

## What This Demo Shows

1. **Connection Testing**: Verifies Supabase PostgreSQL connection
2. **Schema Initialization**: Creates required tables (embeddings, documents, metadata)
3. **Document Storage**: Stores text documents in Supabase
4. **Document Retrieval**: Retrieves documents from Supabase
5. **Semantic Search**: Performs vector similarity search using pgvector
6. **Storage Statistics**: Shows usage metrics

## Prerequisites

1. **Supabase Account**: Create a free account at [https://supabase.com](https://supabase.com)
2. **Supabase Project**: Create a new project in the dashboard
3. **.NET 8.0 SDK**: Required to run the application

## Quick Start

### 1. Get Your Supabase Credentials

From your Supabase dashboard:

- **Project URL**: Settings → API → Project URL
  - Example: `https://abcdefghijk.supabase.co`

- **Anon Key**: Settings → API → Project API keys → anon public

- **Connection String**: Settings → Database → Connection string → URI
  - Format: `postgresql://postgres:[YOUR-PASSWORD]@db.abcdefghijk.supabase.co:5432/postgres`
  - Convert to .NET format: `Host=db.abcdefghijk.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=[YOUR-PASSWORD]`

### 2. Set Environment Variables

**Windows:**
```cmd
set SUPABASE_URL=https://your-project.supabase.co
set SUPABASE_ANON_KEY=your-anon-key
set SUPABASE_CONNECTION_STRING=Host=db.your-project.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=your-password
```

**Linux/Mac:**
```bash
export SUPABASE_URL=https://your-project.supabase.co
export SUPABASE_ANON_KEY=your-anon-key
export SUPABASE_CONNECTION_STRING=Host=db.your-project.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=your-password
```

### 3. Run the Demo

```bash
cd apps/Demos/Hazina.Demo.Supabase
dotnet run
```

## Expected Output

```
=== Hazina Supabase Integration Demo ===

Configuration:
  Supabase URL: https://your-project.supabase.co
  Connection String: Host=db.your-project.supabase.co;...;Password=***

Step 1: Testing Supabase connection...
Connected to Supabase PostgreSQL: PostgreSQL 15.x...
✅ Connected successfully!

Step 2: Initializing Supabase schema...
Supabase schema initialized successfully.
✅ Schema initialized!

Step 3: Creating Supabase store setup...
✅ Store setup created!

Step 4: Storing a test document...
  Document: examples/supabase-demo.txt
  Content: This is a test document stored in Supabase using Hazina...
✅ Document stored!

Step 5: Retrieving the document...
  Retrieved: This is a test document stored in Supabase using Hazina...
✅ Document retrieved successfully!

Step 6: Demonstrating semantic search...
  Stored: docs/postgres.txt
  Stored: docs/supabase.txt
  Stored: docs/pgvector.txt
  Stored: docs/embeddings.txt
  Stored: docs/hazina.txt

  Searching for documents similar to: 'database system'
  Found 3 results:
    - docs/postgres.txt (similarity: 0.8523)
    - docs/supabase.txt (similarity: 0.7891)
    - docs/pgvector.txt (similarity: 0.6742)
✅ Semantic search complete!

Step 7: Storage statistics...
  Total documents stored: 6
  Embedding dimension: 8
  Storage backend: Supabase (PostgreSQL + pgvector)
  Document store: supabase-demo

=== Demo completed successfully! ===
```

## Viewing Your Data in Supabase

After running the demo, you can view your data in the Supabase dashboard:

1. Go to **Table Editor** in the Supabase dashboard
2. You'll see these tables:
   - `embeddings` - Vector embeddings
   - `document_chunks` - Document chunk indexes
   - `document_metadata` - Document metadata
   - `texts` - Text storage

3. Query your data using **SQL Editor**:
   ```sql
   -- View all embeddings
   SELECT key, created_at FROM embeddings;

   -- View all texts
   SELECT * FROM texts;

   -- Perform similarity search
   SELECT key, 1 - (embedding <=> '[0.1, 0.2, ...]') AS similarity
   FROM embeddings
   ORDER BY similarity DESC
   LIMIT 5;
   ```

## Integration with Your Application

After verifying the demo works, integrate Supabase into your application:

```csharp
using Hazina.Tools.Data;
using HazinaStore.Models;

var config = new HazinaStoreConfig
{
    ApiSettings = new ApiSettings { OpenApiKey = "sk-..." },
    SupabaseSettings = new SupabaseSettings
    {
        Enabled = true,
        Url = "https://your-project.supabase.co",
        AnonKey = "your-anon-key",
        ConnectionString = "Host=db.your-project.supabase.co;..."
    }
};

// Automatically uses Supabase when enabled
var storeSetup = StoreProvider.GetStoreSetup(config);
var documentStore = storeSetup.Store;

// Use the store
await documentStore.Store("my-doc.txt", "Content here");
var content = await documentStore.Get("my-doc.txt");
```

## Troubleshooting

### Connection Issues

**Error: "Connection refused"**
- Verify your connection string is correct
- Check your database password
- Ensure your IP is allowed in Supabase settings

**Error: "SSL connection required"**
- Add `SslMode=Require` to your connection string:
  ```
  Host=...;Port=5432;...;SslMode=Require
  ```

### pgvector Issues

**Error: "extension 'vector' does not exist"**
- Supabase has pgvector pre-installed, but you need to enable it
- Run in Supabase SQL Editor:
  ```sql
  CREATE EXTENSION IF NOT EXISTS vector;
  ```

## Next Steps

1. **Read the integration guide**: `docs/SUPABASE_SETUP.md`
2. **Enable Row Level Security**: Secure your tables in production
3. **Use real OpenAI embeddings**: Replace DummyLLMClient with OpenAIClientWrapper
4. **Explore hybrid mode**: Store files locally, embeddings in Supabase
5. **Scale your application**: Supabase handles auto-scaling

## Resources

- [Supabase Documentation](https://supabase.com/docs)
- [pgvector Documentation](https://github.com/pgvector/pgvector)
- [Hazina Supabase Setup Guide](../../../docs/SUPABASE_SETUP.md)
