# Supabase Integration Guide

This guide explains how to integrate Supabase as an optional database backend for Hazina.

## Overview

Supabase is an open-source Firebase alternative built on PostgreSQL. Hazina can optionally use Supabase for:
- Vector embeddings storage (using pgvector)
- Document metadata and chunks
- Text storage
- Future: File storage via Supabase Storage
- Future: Authentication via Supabase Auth

**Important**: Supabase integration is **completely optional**. Existing file-based storage continues to work without any changes.

## Prerequisites

1. **Supabase Account**: Sign up at [https://supabase.com](https://supabase.com)
2. **Create a Project**: Create a new project in the Supabase dashboard
3. **OpenAI API Key**: Still required for generating embeddings

## Quick Start

### 1. Get Your Supabase Credentials

From your Supabase project dashboard:

1. **Project URL**: Found under Settings → API
   - Example: `https://abcdefghijk.supabase.co`

2. **Anon Key**: Found under Settings → API → Project API keys
   - This is your public key (safe for client-side use)

3. **Service Role Key**: Found under Settings → API → Project API keys
   - This is your secret key (only use server-side)

4. **Database Connection String**: Found under Settings → Database → Connection string → URI
   - Example: `postgresql://postgres:[YOUR-PASSWORD]@db.abcdefghijk.supabase.co:5432/postgres`
   - You can also construct it manually:
     ```
     Host=db.your-project.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=your-password
     ```

### 2. Configure Hazina

#### Option A: Using appsettings.json

Copy `appsettings.supabase.json` to your application folder and configure:

```json
{
  "ApiSettings": {
    "OpenApiKey": "sk-..."
  },
  "SupabaseSettings": {
    "Enabled": true,
    "Url": "https://your-project.supabase.co",
    "AnonKey": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "ServiceRoleKey": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "ConnectionString": "Host=db.your-project.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=your-password"
  }
}
```

#### Option B: Using Environment Variables

Set these environment variables:

```bash
# Windows
set OPENAI_API_KEY=sk-...
set SUPABASE_CONNECTION_STRING=Host=db.your-project.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=your-password

# Linux/Mac
export OPENAI_API_KEY=sk-...
export SUPABASE_CONNECTION_STRING=Host=db.your-project.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=your-password
```

### 3. Initialize Database Schema

Before first use, initialize the Supabase database schema:

```csharp
using Hazina.Tools.Data;
using HazinaStore.Models;

var supabaseSettings = new SupabaseSettings
{
    Url = "https://your-project.supabase.co",
    AnonKey = "your-anon-key",
    ConnectionString = "Host=db.your-project.supabase.co;..."
};

// Initialize schema (creates tables and indexes)
await SupabaseStoreProvider.InitializeSupabaseSchemaAsync(
    supabaseSettings.GetConnectionString());
```

This creates the following tables:
- `embeddings` - Vector embeddings with pgvector
- `document_chunks` - Document chunk indexes
- `document_metadata` - Document metadata
- `texts` - Text storage

### 4. Use Supabase in Your Code

#### Full Supabase Mode (all data in Supabase)

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

// Get store setup - automatically uses Supabase when enabled
var storeSetup = StoreProvider.GetStoreSetup(config);
```

#### Hybrid Mode (files + Supabase)

Store text files locally, but use Supabase for embeddings and search:

```csharp
var config = new HazinaStoreConfig
{
    ApiSettings = new ApiSettings { OpenApiKey = "sk-..." },
    ProjectSettings = new ProjectSettings
    {
        ProjectsFolder = @"C:\Projects\hazina\data"
    },
    SupabaseSettings = new SupabaseSettings
    {
        Enabled = true,
        Url = "https://your-project.supabase.co",
        AnonKey = "your-anon-key",
        ConnectionString = "Host=db.your-project.supabase.co;..."
    }
};

// When folder is provided, uses hybrid mode
var storeSetup = StoreProvider.GetStoreSetup(config, folder: @"C:\Projects\hazina\data");
```

#### Direct Supabase Provider

```csharp
using Hazina.Tools.Data;

var storeSetup = SupabaseStoreProvider.GetSupabaseStoreSetup(
    supabaseSettings,
    apiKey: "sk-...",
    embeddingDimension: 1536
);
```

## Storage Modes

| Mode | Configuration | Use Case |
|------|--------------|----------|
| **File-based** | `Enabled: false` or omit SupabaseSettings | Default mode, no database needed |
| **Full Supabase** | `Enabled: true`, no ProjectsFolder | All data in cloud, no local files |
| **Hybrid** | `Enabled: true` + ProjectsFolder | Local files, cloud embeddings (recommended) |

## Testing Your Connection

```csharp
using Hazina.Tools.Data;

var connectionString = "Host=db.your-project.supabase.co;...";
var connected = await SupabaseStoreProvider.TestConnectionAsync(connectionString);

if (connected)
{
    Console.WriteLine("Successfully connected to Supabase!");
}
```

## Security Best Practices

1. **Never commit credentials** to version control
2. **Use environment variables** for sensitive data in production
3. **Use Service Role Key** only in server-side code
4. **Use Anon Key** for client-side code (with Row Level Security enabled)
5. **Enable Row Level Security (RLS)** in Supabase for production

### Setting up RLS (Row Level Security)

In Supabase SQL Editor, run:

```sql
-- Enable RLS on all tables
ALTER TABLE embeddings ENABLE ROW LEVEL SECURITY;
ALTER TABLE document_chunks ENABLE ROW LEVEL SECURITY;
ALTER TABLE document_metadata ENABLE ROW LEVEL SECURITY;
ALTER TABLE texts ENABLE ROW LEVEL SECURITY;

-- Create policies (example: allow authenticated users)
CREATE POLICY "Users can read own embeddings"
  ON embeddings FOR SELECT
  USING (auth.uid()::text = split_part(key, '/', 1));

-- Adjust policies based on your authentication needs
```

## Migration from File-based to Supabase

To migrate existing file-based data to Supabase:

1. Keep your existing files
2. Enable Supabase in hybrid mode
3. Run the demo application to re-process documents
4. Embeddings will be stored in Supabase
5. Original files remain accessible

## Troubleshooting

### Connection Issues

**Error: "Connection refused" or "Timeout"**
- Check your connection string format
- Verify database password
- Ensure your IP is allowed (Supabase → Settings → Database → Connection pooling)

**Error: "SSL connection required"**
- Add `SslMode=Require` to connection string:
  ```
  Host=db.your-project.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=your-password;SslMode=Require
  ```

### pgvector Issues

**Error: "extension 'vector' does not exist"**
- Supabase has pgvector pre-installed, but you need to enable it
- Run in Supabase SQL Editor:
  ```sql
  CREATE EXTENSION IF NOT EXISTS vector;
  ```

### Performance Optimization

For large datasets, create indexes:

```sql
-- Create IVFFlat index for faster similarity search
CREATE INDEX embeddings_ivfflat_idx
ON embeddings
USING ivfflat (embedding vector_cosine_ops)
WITH (lists = 100);

-- For exact search, use:
-- CREATE INDEX embeddings_hnsw_idx
-- ON embeddings
-- USING hnsw (embedding vector_cosine_ops);
```

## Example Applications

See the demo application at:
- `apps/Demos/Hazina.Demo.Supabase/`

## Additional Resources

- [Supabase Documentation](https://supabase.com/docs)
- [pgvector Documentation](https://github.com/pgvector/pgvector)
- [Supabase C# Client](https://supabase.com/docs/reference/csharp/introduction)

## Future Features

Planned enhancements:
- [ ] Supabase Storage integration for file uploads
- [ ] Supabase Auth integration for user management
- [ ] Supabase Realtime for live collaboration
- [ ] Edge Functions for serverless processing
- [ ] Migration utilities for bulk data transfer
