# RAG (Retrieval-Augmented Generation) Guide

## Overview

Hazina's RAG engine combines metadata-driven retrieval with optional vector search for context-aware responses.

> **Architecture Note**: Hazina uses a knowledge database as the primary query layer, with embeddings as an optional secondary index. See [Knowledge Storage & Search Model](KNOWLEDGE_STORAGE.md) for the design principles.

## Features

- Document indexing with metadata and optional embeddings
- Metadata-first filtering with SQL queries
- Optional semantic search with similarity scoring
- Context building and ranking
- Intelligent text chunking
- Result reranking
- Integration with NeuroChain for higher confidence
- Full functionality with embeddings disabled

## Quick Start

```csharp
var orchestrator = new ProviderOrchestrator();
var vectorStore = new PgVectorStore(connectionString);
var ragEngine = new RAGEngine(orchestrator, vectorStore);

// Index documents
var documents = new List<Document>
{
    new Document
    {
        Content = "Quantum entanglement is a physical phenomenon...",
        Metadata = new Dictionary<string, object>
        {
            ["source"] = "physics_textbook.pdf",
            ["page"] = 42
        }
    }
};

var indexResult = await ragEngine.IndexDocumentsAsync(documents);
Console.WriteLine($"Indexed: {indexResult.IndexedDocuments}/{indexResult.TotalDocuments}");

// Query with RAG
var response = await ragEngine.QueryAsync(
    "Explain quantum entanglement",
    new RAGQueryOptions
    {
        TopK = 5,
        MinSimilarity = 0.7,
        MaxContextLength = 4000
    }
);

Console.WriteLine($"Answer: {response.Answer}");
Console.WriteLine($"Retrieved {response.RetrievedDocuments.Count} documents");
```

## Document Indexing

### Basic Indexing

```csharp
var documents = new List<Document>
{
    new Document
    {
        Id = "doc1",
        Content = "Content to index",
        Metadata = new Dictionary<string, object>
        {
            ["category"] = "science",
            ["author"] = "John Doe"
        }
    }
};

var result = await ragEngine.IndexDocumentsAsync(documents);
```

### Chunking Long Documents

```csharp
var chunker = new TextChunker(new TextChunkingOptions
{
    Strategy = ChunkingStrategy.Sentence,
    ChunkSize = 1000,
    OverlapSize = 200
});

var chunks = chunker.ChunkText(longDocument, metadata);

var documents = chunks.Select(chunk => new Document
{
    Id = $"{documentId}_chunk_{chunk.Index}",
    Content = chunk.Text,
    Metadata = chunk.Metadata
}).ToList();

await ragEngine.IndexDocumentsAsync(documents);
```

### Chunking Strategies

```csharp
// Fixed size chunks
var chunker = new TextChunker(new TextChunkingOptions
{
    Strategy = ChunkingStrategy.FixedSize,
    ChunkSize = 512,
    OverlapSize = 128
});

// Sentence-based chunks
var chunker = new TextChunker(new TextChunkingOptions
{
    Strategy = ChunkingStrategy.Sentence,
    ChunkSize = 1000
});

// Paragraph-based chunks
var chunker = new TextChunker(new TextChunkingOptions
{
    Strategy = ChunkingStrategy.Paragraph
});
```

## Querying

### Basic Query

```csharp
var response = await ragEngine.QueryAsync("What is machine learning?");
Console.WriteLine(response.Answer);
```

### Advanced Query Options

```csharp
var options = new RAGQueryOptions
{
    TopK = 10,                    // Retrieve top 10 documents
    MinSimilarity = 0.75,         // Minimum similarity threshold
    MaxContextLength = 8000,      // Max tokens for context
    UseNeurochain = true,         // Use multi-layer reasoning
    MinConfidence = 0.9,          // High confidence required
    RequireCitation = true        // Include citations in answer
};

var response = await ragEngine.QueryAsync("Complex question", options);

// Review retrieved documents
foreach (var doc in response.RetrievedDocuments)
{
    Console.WriteLine($"[{doc.Similarity:P0}] {doc.Content.Substring(0, 100)}...");
}
```

### Search Without Generation

```csharp
// Just retrieve similar documents
var documents = await ragEngine.SearchAsync(
    "search query",
    topK: 5,
    minSimilarity: 0.7
);

foreach (var doc in documents)
{
    Console.WriteLine($"{doc.Id}: {doc.Similarity:P0}");
    Console.WriteLine(doc.Content);
}
```

## Reranking

Improve retrieval quality with reranking:

```csharp
var reranker = new Reranker(orchestrator, new RerankingOptions
{
    Strategy = RerankingStrategy.Hybrid,
    SimilarityWeight = 0.5,
    LLMWeight = 0.5
});

var retrievedDocs = await ragEngine.SearchAsync("query");
var rerankedDocs = await reranker.RerankAsync("query", retrievedDocs);

// Reranked documents are now in better relevance order
```

### Reranking Strategies

```csharp
// Similarity-based (fast, uses embeddings only)
var reranker = new Reranker(options: new RerankingOptions
{
    Strategy = RerankingStrategy.Similarity
});

// LLM-based (slower, more accurate)
var reranker = new Reranker(orchestrator, new RerankingOptions
{
    Strategy = RerankingStrategy.LLMBased
});

// Hybrid (balanced)
var reranker = new Reranker(orchestrator, new RerankingOptions
{
    Strategy = RerankingStrategy.Hybrid,
    SimilarityWeight = 0.3,
    LLMWeight = 0.7
});
```

### Diversity and Filtering

```csharp
// Filter by relevance
var filtered = reranker.FilterByRelevance(documents, minRelevance: 0.8);

// Diversify results (reduce redundancy)
var diverse = reranker.DiversifyResults(
    documents,
    maxResults: 5,
    diversityThreshold: 0.8
);
```

## Advanced Features

### With NeuroChain

```csharp
var neurochain = new NeuroChainOrchestrator(orchestrator);
var ragEngine = new RAGEngine(orchestrator, vectorStore, neurochain);

var response = await ragEngine.QueryAsync(
    "Complex research question",
    new RAGQueryOptions
    {
        UseNeurochain = true,
        MinConfidence = 0.95
    }
);

// Higher confidence, multi-layer reasoning
Console.WriteLine($"Confidence: {response.FinalConfidence:P0}");
```

### Custom Context Building

```csharp
// The RAG engine builds context automatically, but you can customize:
var retrievedDocs = await ragEngine.SearchAsync("query", topK: 10);

// Custom reranking
var reranked = await CustomRerank(retrievedDocs);

// Custom context assembly
var context = BuildCustomContext(reranked, maxLength: 4000);

// Generate with custom context
var messages = new List<HazinaChatMessage>
{
    new HazinaChatMessage
    {
        Role = HazinaMessageRole.System,
        Text = "You are a helpful assistant. Use the provided context to answer questions."
    },
    new HazinaChatMessage
    {
        Role = HazinaMessageRole.User,
        Text = $"Context:\n{context}\n\nQuestion: {query}"
    }
};

var response = await orchestrator.GetResponse(messages);
```

## Knowledge Storage Architecture

Hazina uses a **metadata-first** approach to knowledge storage:

### Design Principles

1. **Metadata is primary** — Tags, properties, and structure are always queryable
2. **Embeddings are secondary** — Optional acceleration for semantic search
3. **Database is truth** — SQLite (local) or PostgreSQL (production) holds all queryable data
4. **Files are sources** — Documents are inputs, not the query layer

### Search Strategy

```csharp
// Metadata-first search (always available)
var results = await ragEngine.SearchAsync("query", new SearchOptions
{
    MetadataFilter = new Dictionary<string, object>
    {
        ["source"] = "api-reference.md",
        ["category"] = "authentication"
    },
    UseSemanticSearch = false  // Pure metadata search
});

// Hybrid search (metadata + embeddings)
var results = await ragEngine.SearchAsync("query", new SearchOptions
{
    MetadataFilter = new Dictionary<string, object>
    {
        ["category"] = "security"
    },
    UseSemanticSearch = true  // Add embedding similarity ranking
});
```

### Embeddings Are Optional

Hazina functions correctly without embeddings:

```csharp
var rag = new RAGEngine(ai, vectorStore, config: new RAGConfig
{
    UseEmbeddings = false  // Metadata and keyword search only
});

// Search still works — uses full-text search and metadata
var results = await rag.SearchAsync("authentication flow");
```

When embeddings are disabled:
- Indexing stores content and metadata (faster)
- Search uses keyword matching (BM25/full-text)
- Results are ranked by text relevance, not vector similarity

See [Knowledge Storage & Search Model](KNOWLEDGE_STORAGE.md) for complete architecture details.

---

## Vector Stores

When embeddings are enabled, vector stores provide similarity search:

### Supported Stores

```csharp
// PostgreSQL with pgvector (production)
var vectorStore = new PgVectorStore(connectionString);

// In-memory (development/testing)
var vectorStore = new InMemoryVectorStore();

// File-based (local persistence)
var vectorStore = new FileVectorStore(path);
```

### Custom Vector Store

```csharp
public class CustomVectorStore : IVectorStore
{
    public async Task AddAsync(
        string id,
        float[] embedding,
        Dictionary<string, object> metadata,
        CancellationToken cancellationToken)
    {
        // Your implementation
    }

    public async Task<List<VectorSearchResult>> SearchAsync(
        float[] queryEmbedding,
        int topK,
        CancellationToken cancellationToken)
    {
        // Your implementation
    }
}
```

## Performance Optimization

### Batch Indexing

```csharp
// Index in batches for better performance
const int batchSize = 100;
for (int i = 0; i < allDocuments.Count; i += batchSize)
{
    var batch = allDocuments.Skip(i).Take(batchSize).ToList();
    await ragEngine.IndexDocumentsAsync(batch);
    Console.WriteLine($"Indexed {i + batch.Count}/{allDocuments.Count}");
}
```

### Caching

```csharp
// Cache embeddings
var embeddingCache = new Dictionary<string, float[]>();

foreach (var doc in documents)
{
    if (!embeddingCache.ContainsKey(doc.Id))
    {
        var embedding = await GenerateEmbedding(doc.Content);
        embeddingCache[doc.Id] = embedding;
    }
}
```

### Optimal Chunk Size

```csharp
// Experiment with chunk sizes for your use case
var chunkSizes = new[] { 256, 512, 1024, 2048 };

foreach (var size in chunkSizes)
{
    var chunker = new TextChunker(new TextChunkingOptions
    {
        ChunkSize = size,
        OverlapSize = size / 4
    });

    // Test and measure
    var metrics = await TestChunkSize(chunker);
    Console.WriteLine($"Size {size}: {metrics}");
}
```

## Error Handling

```csharp
try
{
    var response = await ragEngine.QueryAsync("question");

    if (!response.Success)
    {
        Console.WriteLine($"Query failed: {response.Error}");
        return;
    }

    if (response.RetrievedDocuments.Count == 0)
    {
        Console.WriteLine("No relevant documents found");
        return;
    }

    Console.WriteLine(response.Answer);
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
```

## Best Practices

1. **Chunk Size**: 500-1000 tokens for most use cases
2. **Overlap**: 10-20% of chunk size
3. **TopK**: Start with 5-10, adjust based on results
4. **Similarity Threshold**: 0.7 is a good starting point
5. **Reranking**: Use for critical applications
6. **Citations**: Enable for factual questions
7. **NeuroChain**: Use for complex queries requiring high confidence

## Example: Complete RAG System

```csharp
// Setup
var orchestrator = new ProviderOrchestrator();
var vectorStore = new PgVectorStore(connectionString);
var neurochain = new NeuroChainOrchestrator(orchestrator);
var ragEngine = new RAGEngine(orchestrator, vectorStore, neurochain);
var reranker = new Reranker(orchestrator);
var chunker = new TextChunker();

// Index documents
var documents = await LoadDocumentsAsync();
var allChunks = new List<Document>();

foreach (var doc in documents)
{
    var chunks = chunker.ChunkText(doc.Content, doc.Metadata);
    allChunks.AddRange(chunks.Select(c => new Document
    {
        Id = $"{doc.Id}_{c.Index}",
        Content = c.Text,
        Metadata = c.Metadata
    }));
}

await ragEngine.IndexDocumentsAsync(allChunks);

// Query
var response = await ragEngine.QueryAsync(
    "What are the main themes?",
    new RAGQueryOptions
    {
        TopK = 10,
        MinSimilarity = 0.75,
        UseNeurochain = true,
        MinConfidence = 0.9,
        RequireCitation = true
    }
);

// Rerank for better results
var reranked = await reranker.RerankAsync(
    "What are the main themes?",
    response.RetrievedDocuments
);

Console.WriteLine($"Answer: {response.Answer}");
Console.WriteLine($"Sources: {response.RetrievedDocuments.Count}");
```
