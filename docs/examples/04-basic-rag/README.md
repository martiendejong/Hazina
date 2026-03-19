# Basic RAG - Document-Powered Q&A

**Build an AI that answers questions from your documents**

## What You'll Learn

- How to set up a RAG (Retrieval-Augmented Generation) engine
- How to index documents with metadata
- How to query with automatic context retrieval
- How chunking strategies work
- In-memory vs persistent storage

## Prerequisites

- .NET 8.0 or higher
- OpenAI API key
- Basic understanding of [Hello World](../01-hello-world/) example

## What is RAG?

RAG (Retrieval-Augmented Generation) combines:
1. **Retrieval**: Find relevant documents from your knowledge base
2. **Augmentation**: Add retrieved context to the AI prompt
3. **Generation**: AI generates answers based on your documents

**Result**: Accurate, citation-backed answers from your own data.

## Running the Example

```bash
# Set your API key
export OPENAI_API_KEY=sk-your-key-here

# Run
dotnet run
```

Expected output:
```
=== Basic RAG Example ===

Indexing documents...
✓ Indexed 3 documents

Question: What is Hazina?
Answer: Hazina is a production-ready AI framework for .NET that provides multi-provider
orchestration, RAG capabilities, and agent workflows out of the box.

Retrieved 2 documents:
  [95%] overview.md
  [87%] architecture.md

Cost: $0.0012
```

## Code Walkthrough

### 1. Setup AI and Vector Store

```csharp
var ai = QuickSetup.SetupOpenAI(apiKey);

// In-memory storage for development
var vectorStore = new InMemoryVectorStore();

// Create RAG engine
var rag = new RAGEngine(ai, vectorStore);
```

**What's happening:**
- `InMemoryVectorStore` stores document embeddings in memory (fast, but not persistent)
- `RAGEngine` combines AI with vector store for context-aware responses
- For production, use `PgVectorStore` (see [Production RAG](../05-production-rag/))

### 2. Index Documents

```csharp
var documents = new List<Document>
{
    new()
    {
        Id = "doc1",
        Content = "Hazina is a production-ready AI framework for .NET...",
        Metadata = new Dictionary<string, object>
        {
            ["source"] = "overview.md",
            ["category"] = "getting-started"
        }
    }
};

await rag.IndexDocumentsAsync(documents);
```

**What's happening:**
- Each document has `Id`, `Content`, and `Metadata`
- Metadata enables filtering (e.g., "only search getting-started docs")
- Indexing generates embeddings (vector representations) for semantic search

### 3. Query with Context

```csharp
var response = await rag.QueryAsync("What is Hazina?");

Console.WriteLine($"Answer: {response.Answer}");
Console.WriteLine($"Retrieved {response.RetrievedDocuments.Count} documents");
```

**What's happening:**
- RAG automatically finds relevant documents
- Injects documents as context into the AI prompt
- AI generates answer based on your documents (not just training data!)

### 4. Examine Retrieved Documents

```csharp
foreach (var doc in response.RetrievedDocuments)
{
    Console.WriteLine($"  [{doc.Similarity:P0}] {doc.Metadata["source"]}");
}
```

**What's happening:**
- `Similarity` shows how relevant each document is (0-100%)
- Documents are ranked by relevance
- You can require minimum similarity (e.g., 70%) for quality control

## Key Concepts

### Document Structure

```csharp
new Document
{
    Id = "unique-id",              // Unique identifier
    Content = "Text content...",    // Actual document text
    Metadata = new()                // Searchable properties
    {
        ["source"] = "filename.md",
        ["author"] = "John Doe",
        ["category"] = "tutorial",
        ["date"] = "2026-03-19"
    }
}
```

### Metadata-First Architecture

Hazina uses a **metadata-first** approach:
- Database stores all metadata (always queryable)
- Embeddings are optional (acceleration only)
- You can search by metadata even without embeddings

```csharp
// Metadata search (no embeddings needed)
var results = await rag.SearchAsync("query", new SearchOptions
{
    MetadataFilter = new()
    {
        ["category"] = "tutorial",
        ["author"] = "John Doe"
    }
});
```

### Chunking Long Documents

For long documents, split into chunks for better retrieval:

```csharp
using Hazina.AI.RAG.Embeddings;

var chunker = new TextChunker(new TextChunkingOptions
{
    Strategy = ChunkingStrategy.Paragraph,  // Split by paragraphs
    ChunkSize = 1000,                       // ~1000 tokens per chunk
    OverlapSize = 100                       // 100 token overlap between chunks
});

var longDocument = await File.ReadAllTextAsync("long-article.md");

var chunks = chunker.ChunkText(longDocument, new()
{
    ["source"] = "long-article.md",
    ["author"] = "John Doe"
});

var documents = chunks.Select(chunk => new Document
{
    Id = $"long-article_{chunk.Index}",
    Content = chunk.Text,
    Metadata = chunk.Metadata  // Preserves original metadata + adds chunk index
}).ToList();

await rag.IndexDocumentsAsync(documents);
```

**Chunking Strategies:**
- `FixedSize`: Split at fixed token count (simple, consistent)
- `Sentence`: Split at sentence boundaries (better semantic coherence)
- `Paragraph`: Split at paragraph boundaries (best for articles)

### Query Options

```csharp
var response = await rag.QueryAsync("question", new RAGQueryOptions
{
    TopK = 5,                    // Retrieve top 5 most relevant documents
    MinSimilarity = 0.7,         // Only use documents with ≥70% similarity
    MaxContextLength = 4000,     // Maximum tokens for context
    RequireCitation = true       // AI must cite sources in answer
});
```

## Loading Documents from Files

```csharp
// Create documents folder
Directory.CreateDirectory("documents");

// Add some text files
File.WriteAllText("documents/guide1.txt", "Content for guide 1...");
File.WriteAllText("documents/guide2.txt", "Content for guide 2...");

// Load all .txt files
var documents = new List<Document>();

foreach (var file in Directory.GetFiles("documents", "*.txt"))
{
    var content = await File.ReadAllTextAsync(file);
    var fileName = Path.GetFileName(file);

    documents.Add(new Document
    {
        Id = fileName,
        Content = content,
        Metadata = new()
        {
            ["source"] = fileName,
            ["loaded_at"] = DateTime.UtcNow
        }
    });
}

await rag.IndexDocumentsAsync(documents);
Console.WriteLine($"Indexed {documents.Count} files");
```

## Extending This Example

### Add More Documents

```csharp
var additionalDocs = new List<Document>
{
    new()
    {
        Id = "faq1",
        Content = "Q: How do I install Hazina? A: Use 'dotnet add package Hazina.AI.FluentAPI'",
        Metadata = new() { ["source"] = "faq.md", ["category"] = "installation" }
    },
    new()
    {
        Id = "faq2",
        Content = "Q: Does Hazina support local LLMs? A: Yes, via Ollama integration.",
        Metadata = new() { ["source"] = "faq.md", ["category"] = "features" }
    }
};

await rag.IndexDocumentsAsync(additionalDocs);
```

### Filter by Metadata

```csharp
var response = await rag.QueryAsync("installation help", new RAGQueryOptions
{
    TopK = 3,
    MetadataFilter = new()
    {
        ["category"] = "installation"  // Only search installation docs
    }
});
```

### Interactive Q&A Loop

```csharp
Console.WriteLine("Ask questions about your documents (or 'quit' to exit)\n");

while (true)
{
    Console.Write("Question: ");
    var question = Console.ReadLine();

    if (string.IsNullOrEmpty(question) || question.ToLower() == "quit")
        break;

    var response = await rag.QueryAsync(question);

    Console.WriteLine($"\nAnswer: {response.Answer}");
    Console.WriteLine($"Sources: {response.RetrievedDocuments.Count} documents\n");
}
```

## Troubleshooting

### "No relevant documents found"

**Problem**: Query doesn't match document content.

**Solution**:
- Check document content is indexed correctly
- Lower `MinSimilarity` threshold
- Increase `TopK` to retrieve more documents

### "Index is empty" error

**Problem**: Documents not indexed yet.

**Solution**:
```csharp
// Verify documents were indexed
var indexResult = await rag.IndexDocumentsAsync(documents);
Console.WriteLine($"Indexed: {indexResult.IndexedDocuments}/{indexResult.TotalDocuments}");
```

### High API costs

**Problem**: Every query generates embeddings for the query.

**Solution**:
- Use caching (query same question multiple times = 1 API call)
- Use persistent storage like PostgreSQL (see [Production RAG](../05-production-rag/))
- Consider [RAG without embeddings](../06-rag-no-embeddings/) for metadata-based search

## Performance Tips

1. **Batch indexing**: Index multiple documents at once
2. **Chunk long documents**: Better retrieval granularity
3. **Use metadata filters**: Reduce search space
4. **Persistent storage**: Avoid re-indexing on every run
5. **Cache queries**: Reuse embeddings for repeated questions

## Next Steps

- [Production RAG with PostgreSQL](../05-production-rag/) - Persistent, scalable storage
- [RAG without Embeddings](../06-rag-no-embeddings/) - Metadata-first search
- [High-Confidence RAG](../07-high-confidence-rag/) - Add Neurochain for critical questions
- [Context Engineering](../14-context-engineering/) - Optimize context windows

## Full Code

See [Program.cs](Program.cs) for the complete, runnable code.

---

**Congratulations! You've built a document-powered AI.**

This is the foundation of production RAG systems. Next step: swap `InMemoryVectorStore` for `PgVectorStore` and you have a scalable, production-ready knowledge base.
