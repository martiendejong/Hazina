# Build a Production-Ready RAG AI in C# in 30 Minutes (No Python)

Build a fully functional RAG (Retrieval-Augmented Generation) system that answers questions from your documents — and scales from demo to production without rewriting.

**Time required:** 30 minutes
**Prerequisites:** .NET 9.0+, OpenAI API key

## What You'll Build

A RAG AI that:
- Loads and indexes your documents
- Answers questions using relevant context
- Swaps LLM providers via config (no code changes)
- Connects to PostgreSQL/Supabase for production
- Adds multi-layer reasoning for high-confidence answers

## Step 1: Create Project (2 minutes)

```bash
dotnet new console -n MyRAGApp
cd MyRAGApp
dotnet add package Hazina.AI.FluentAPI
dotnet add package Hazina.AI.RAG
```

Set your API key:

**Windows:**
```bash
set OPENAI_API_KEY=sk-your-key-here
```

**Linux/Mac:**
```bash
export OPENAI_API_KEY=sk-your-key-here
```

## Step 2: Minimal RAG (5 minutes)

Replace `Program.cs` with:

```csharp
using Hazina.AI.FluentAPI.Configuration;
using Hazina.AI.RAG.Core;

// Setup AI provider
var ai = QuickSetup.SetupOpenAI(Environment.GetEnvironmentVariable("OPENAI_API_KEY")!);

// Create in-memory vector store (swap for PostgreSQL later)
var vectorStore = new InMemoryVectorStore();

// Create RAG engine
var rag = new RAGEngine(ai, vectorStore);

// Index some documents
await rag.IndexDocumentsAsync(new List<Document>
{
    new()
    {
        Id = "doc1",
        Content = "Hazina is a production-ready AI framework for .NET. It provides multi-provider orchestration, RAG, agents, and production monitoring out of the box.",
        Metadata = new() { ["source"] = "overview.md" }
    },
    new()
    {
        Id = "doc2",
        Content = "RAG (Retrieval-Augmented Generation) combines vector search with LLM generation. Documents are embedded, stored, and retrieved based on semantic similarity to answer questions accurately.",
        Metadata = new() { ["source"] = "rag-guide.md" }
    },
    new()
    {
        Id = "doc3",
        Content = "The FluentAPI provides a simple way to configure AI providers. Use QuickSetup.SetupOpenAI() for single provider, or SetupWithFailover() for automatic failover between providers.",
        Metadata = new() { ["source"] = "api-reference.md" }
    }
});

Console.WriteLine("Documents indexed!\n");

// Query loop
while (true)
{
    Console.Write("Ask a question (or 'quit'): ");
    var question = Console.ReadLine();

    if (string.IsNullOrEmpty(question) || question.ToLower() == "quit")
        break;

    var response = await rag.QueryAsync(question);

    Console.WriteLine($"\nAnswer: {response.Answer}");
    Console.WriteLine($"Sources: {response.RetrievedDocuments.Count} documents used");
    Console.WriteLine($"Top match: {response.RetrievedDocuments.FirstOrDefault()?.Similarity:P0} similarity\n");
}
```

Run it:
```bash
dotnet run
```

**Try these questions:**
- "What is Hazina?"
- "How does RAG work?"
- "How do I set up failover?"

## Step 3: Load Real Documents (5 minutes)

Create a `documents` folder and add some files, then update your code:

```csharp
using Hazina.AI.FluentAPI.Configuration;
using Hazina.AI.RAG.Core;
using Hazina.AI.RAG.Embeddings;

var ai = QuickSetup.SetupOpenAI(Environment.GetEnvironmentVariable("OPENAI_API_KEY")!);
var vectorStore = new InMemoryVectorStore();
var rag = new RAGEngine(ai, vectorStore);

// Load documents from folder
var documentsPath = Path.Combine(Directory.GetCurrentDirectory(), "documents");
var documents = new List<Document>();

if (Directory.Exists(documentsPath))
{
    foreach (var file in Directory.GetFiles(documentsPath, "*.txt"))
    {
        var content = await File.ReadAllTextAsync(file);
        var fileName = Path.GetFileName(file);

        // Use chunker for long documents
        var chunker = new TextChunker(new TextChunkingOptions
        {
            Strategy = ChunkingStrategy.Paragraph,
            ChunkSize = 1000,
            OverlapSize = 100
        });

        var chunks = chunker.ChunkText(content, new Dictionary<string, object>
        {
            ["source"] = fileName
        });

        // Add each chunk as a document
        foreach (var chunk in chunks)
        {
            documents.Add(new Document
            {
                Id = $"{fileName}_{chunk.Index}",
                Content = chunk.Text,
                Metadata = chunk.Metadata
            });
        }

        Console.WriteLine($"Loaded: {fileName} ({chunks.Count} chunks)");
    }
}

// Index all documents
var result = await rag.IndexDocumentsAsync(documents);
Console.WriteLine($"\nIndexed {result.IndexedDocuments} document chunks\n");

// Query loop (same as before)
while (true)
{
    Console.Write("Question: ");
    var question = Console.ReadLine();
    if (string.IsNullOrEmpty(question) || question == "quit") break;

    var response = await rag.QueryAsync(question, new RAGQueryOptions
    {
        TopK = 3,
        MinSimilarity = 0.6,
        RequireCitation = true
    });

    Console.WriteLine($"\n{response.Answer}\n");

    foreach (var doc in response.RetrievedDocuments.Take(3))
    {
        Console.WriteLine($"  [{doc.Similarity:P0}] {doc.Metadata.GetValueOrDefault("source")}");
    }
    Console.WriteLine();
}
```

## Step 4: Swap LLM Provider via Config (5 minutes)

No code changes needed — just switch the setup:

```csharp
// Option 1: OpenAI only
var ai = QuickSetup.SetupOpenAI(Environment.GetEnvironmentVariable("OPENAI_API_KEY")!);

// Option 2: OpenAI with Anthropic failover
var ai = QuickSetup.SetupWithFailover(
    Environment.GetEnvironmentVariable("OPENAI_API_KEY")!,
    Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")!
);

// Option 3: Cost-optimized (uses cheapest available)
var ai = QuickSetup.SetupCostOptimized(
    Environment.GetEnvironmentVariable("OPENAI_API_KEY")!,
    Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
);

// Option 4: Environment-based config
var ai = Environment.GetEnvironmentVariable("USE_FAILOVER") == "true"
    ? QuickSetup.SetupWithFailover(
        Environment.GetEnvironmentVariable("OPENAI_API_KEY")!,
        Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")!)
    : QuickSetup.SetupOpenAI(Environment.GetEnvironmentVariable("OPENAI_API_KEY")!);
```

**The RAG code stays exactly the same.** Provider switching happens at the infrastructure layer.

## Step 5: Add PostgreSQL/Supabase Backend (10 minutes)

For production, replace the in-memory store with PostgreSQL:

```bash
dotnet add package Hazina.Tools.Data
dotnet add package Npgsql
```

```csharp
using Hazina.AI.FluentAPI.Configuration;
using Hazina.AI.RAG.Core;
using Hazina.Tools.Data;

var ai = QuickSetup.SetupOpenAI(Environment.GetEnvironmentVariable("OPENAI_API_KEY")!);

// Switch to PostgreSQL with pgvector
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? "Host=localhost;Database=myragapp;Username=postgres;Password=postgres";

var vectorStore = new PgVectorStore(connectionString);

// Initialize schema (creates table if not exists)
await vectorStore.InitializeAsync();

var rag = new RAGEngine(ai, vectorStore);

// Rest of your code stays the same!
```

### Supabase Setup

1. Create a Supabase project at [supabase.com](https://supabase.com)
2. Enable the pgvector extension in SQL Editor:
   ```sql
   create extension if not exists vector;
   ```
3. Use your Supabase connection string:
   ```bash
   set DATABASE_URL=postgresql://postgres:[YOUR-PASSWORD]@db.[YOUR-PROJECT].supabase.co:5432/postgres
   ```

**Same code, production database.** Documents persist across restarts.

## Step 6: Enable/Disable Embeddings (2 minutes)

Hazina uses a **metadata-first** architecture where embeddings are optional. The system functions correctly without embeddings, using metadata filtering and keyword search instead.

```csharp
// With embeddings (default) - metadata + semantic search
var rag = new RAGEngine(ai, vectorStore);

// Without embeddings - metadata + keyword search
var rag = new RAGEngine(ai, vectorStore, config: new RAGConfig
{
    UseEmbeddings = false  // Full functionality, no vector search
});
```

Toggle via environment:
```csharp
var config = new RAGConfig
{
    UseEmbeddings = Environment.GetEnvironmentVariable("USE_EMBEDDINGS") != "false"
};
```

**Why disable embeddings?**
- Faster indexing (no API calls for embedding generation)
- Lower cost (no embedding API charges)
- Works offline (no external service dependency)
- Deterministic results (keyword matching is reproducible)

See [Knowledge Storage & Search Model](KNOWLEDGE_STORAGE.md) for the full architecture.

## Step 7: Add Multi-Layer Reasoning (5 minutes)

For high-stakes questions requiring 95%+ confidence:

```bash
dotnet add package Hazina.Neurochain.Core
```

```csharp
using Hazina.AI.FluentAPI.Configuration;
using Hazina.AI.RAG.Core;
using Hazina.Neurochain.Core;
using Hazina.Neurochain.Core.Layers;

var ai = QuickSetup.SetupOpenAI(Environment.GetEnvironmentVariable("OPENAI_API_KEY")!);
var vectorStore = new InMemoryVectorStore();

// Create Neurochain for high-confidence reasoning
var neurochain = new NeuroChainOrchestrator();
neurochain.AddLayer(new FastReasoningLayer(ai));    // Quick first pass
neurochain.AddLayer(new DeepReasoningLayer(ai));    // Thorough analysis
neurochain.AddLayer(new VerificationLayer(ai));     // Cross-validation

// RAG with Neurochain
var rag = new RAGEngine(ai, vectorStore, neurochain);

// Index documents...
await rag.IndexDocumentsAsync(documents);

// Query with multi-layer reasoning
var response = await rag.QueryAsync("What are the security implications?", new RAGQueryOptions
{
    UseNeurochain = true,    // Enable multi-layer reasoning
    MinConfidence = 0.9,     // Require 90% confidence
    RequireCitation = true
});

Console.WriteLine($"Answer: {response.Answer}");
Console.WriteLine($"Confidence: {response.Confidence:P0}");  // 95-99% with Neurochain
```

## Complete Production Example

Here's everything together:

```csharp
using Hazina.AI.FluentAPI.Configuration;
using Hazina.AI.RAG.Core;
using Hazina.AI.RAG.Embeddings;
using Hazina.Neurochain.Core;
using Hazina.Neurochain.Core.Layers;

// Configuration from environment
var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")!;
var anthropicKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
var useNeurochain = Environment.GetEnvironmentVariable("HIGH_CONFIDENCE") == "true";

// Setup AI with failover
var ai = !string.IsNullOrEmpty(anthropicKey)
    ? QuickSetup.SetupWithFailover(openAiKey, anthropicKey)
    : QuickSetup.SetupOpenAI(openAiKey);

// Setup vector store (PostgreSQL if configured, else in-memory)
IVectorStore vectorStore = !string.IsNullOrEmpty(databaseUrl)
    ? new PgVectorStore(databaseUrl)
    : new InMemoryVectorStore();

// Optional: Neurochain for high-confidence
NeuroChainOrchestrator? neurochain = null;
if (useNeurochain)
{
    neurochain = new NeuroChainOrchestrator();
    neurochain.AddLayer(new FastReasoningLayer(ai));
    neurochain.AddLayer(new DeepReasoningLayer(ai));
    neurochain.AddLayer(new VerificationLayer(ai));
}

// Create RAG engine
var rag = new RAGEngine(ai, vectorStore, neurochain);

// Load and chunk documents
var chunker = new TextChunker(new TextChunkingOptions
{
    Strategy = ChunkingStrategy.Paragraph,
    ChunkSize = 1000,
    OverlapSize = 100
});

var documents = new List<Document>();
foreach (var file in Directory.GetFiles("documents", "*.txt"))
{
    var content = await File.ReadAllTextAsync(file);
    var chunks = chunker.ChunkText(content, new() { ["source"] = Path.GetFileName(file) });

    documents.AddRange(chunks.Select(c => new Document
    {
        Id = $"{Path.GetFileName(file)}_{c.Index}",
        Content = c.Text,
        Metadata = c.Metadata
    }));
}

// Index
var indexResult = await rag.IndexDocumentsAsync(documents);
Console.WriteLine($"Indexed {indexResult.IndexedDocuments} chunks from {Directory.GetFiles("documents", "*.txt").Length} files");

// Query
var response = await rag.QueryAsync("What is the main topic?", new RAGQueryOptions
{
    TopK = 5,
    MinSimilarity = 0.7,
    UseNeurochain = useNeurochain,
    MinConfidence = 0.9,
    RequireCitation = true
});

Console.WriteLine($"\nAnswer: {response.Answer}");
Console.WriteLine($"Sources used: {response.RetrievedDocuments.Count}");
```

## Configuration Cheat Sheet

| Environment Variable | Purpose | Default |
|---------------------|---------|---------|
| `OPENAI_API_KEY` | OpenAI API key | Required |
| `ANTHROPIC_API_KEY` | Anthropic API key (enables failover) | Optional |
| `DATABASE_URL` | PostgreSQL connection string | In-memory |
| `USE_EMBEDDINGS` | Enable vector embeddings | `true` |
| `HIGH_CONFIDENCE` | Enable Neurochain reasoning | `false` |

## What's Next?

You now have a production-ready RAG system. To extend it:

1. **Understand the architecture**: See [Knowledge Storage & Search Model](KNOWLEDGE_STORAGE.md) for metadata-first design
2. **Add more document types**: See [RAG Guide](RAG_GUIDE.md) for PDF, images
3. **Add agent workflows**: See [Agents Guide](AGENTS_GUIDE.md) for tool calling
4. **Add monitoring**: See [Production Monitoring Guide](PRODUCTION_MONITORING_GUIDE.md)
5. **Deploy to Supabase**: See [Supabase Setup](SUPABASE_SETUP.md)

---

**This scales from demo → production without rewriting.**

The same code that runs on your laptop with in-memory storage runs in production with PostgreSQL. Swap providers, add failover, enable high-confidence reasoning — all through configuration.

That's the Hazina promise: **4 lines to production AI.**
