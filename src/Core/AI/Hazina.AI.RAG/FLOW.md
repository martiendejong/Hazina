# RAG Engine Flow

## Happy Path: Index Document

```
1. User calls rag.IndexDocumentsAsync(documents)
2. For each document:
   a. Extract text (or use BinaryProcessor for PDFs/images)
   b. Create DocumentMetadata
   c. Split into chunks via TextChunker
   d. Generate embeddings for each chunk
   e. Store: metadata, chunks, embeddings, text
3. Return success count
```

## Happy Path: Search

```
1. User calls rag.SearchAsync(query, topK: 10)
2. Generate embedding for query
3. Vector similarity search against all stored embeddings
4. Sort by similarity score (descending)
5. Apply optional reranking
6. Return top K results with scores
```

## Happy Path: Ask with Context

```
1. User calls rag.AskWithContextAsync(question)
2. Search for relevant documents (SearchAsync)
3. Take top K chunks
4. Build context string from chunks
5. Construct augmented prompt:
   - System: "Answer based on this context: {context}"
   - User: original question
6. Send to LLM via ProviderOrchestrator
7. Return answer with citations
```

## Sequence Diagram: Indexing

```
User          RAGEngine       Chunker      Generator      Store
  │               │              │             │            │
  │ IndexDocs()   │              │             │            │
  │──────────────►│              │             │            │
  │               │              │             │            │
  │               │ Split()      │             │            │
  │               │─────────────►│             │            │
  │               │◄─────────────│             │            │
  │               │   chunks[]   │             │            │
  │               │              │             │            │
  │               │ Generate()   │             │            │
  │               │─────────────────────────── │            │
  │               │◄─────────────────────────► │            │
  │               │   embeddings[]             │            │
  │               │              │             │            │
  │               │ Store()      │             │            │
  │               │───────────────────────────────────────► │
  │               │              │             │            │
  │◄──────────────│              │             │            │
  │   count       │              │             │            │
```

## Sequence Diagram: Search

```
User          RAGEngine       Generator      VectorStore
  │               │              │               │
  │ Search()      │              │               │
  │──────────────►│              │               │
  │               │              │               │
  │               │ Generate()   │               │
  │               │─────────────►│               │
  │               │◄─────────────│               │
  │               │ queryEmbed   │               │
  │               │              │               │
  │               │ SearchSimilar()             │
  │               │─────────────────────────────►│
  │               │◄─────────────────────────────│
  │               │   results[]                  │
  │               │              │               │
  │◄──────────────│              │               │
  │   results[]   │              │               │
```

## Error Paths

### Embedding Generation Fails
```
1. Text sent to embedding API
2. API returns error (rate limit, invalid input)
3. RetryPolicy attempts retry
4. If persistent: document marked as failed
5. Continue with remaining documents
6. Return partial success count
```

### No Results Found
```
1. Query embedding generated
2. Vector search returns empty (all below minSimilarity)
3. Return empty results array
4. Caller should handle gracefully
```

### Context Too Large
```
1. Search returns many relevant chunks
2. Combined context exceeds token limit
3. TokenCounter truncates to maxTokens
4. Warning logged about truncation
5. Proceed with truncated context
```

### LLM Call Fails in AskWithContext
```
1. Context built successfully
2. LLM call fails
3. ProviderOrchestrator handles failover
4. If all providers fail: throw exception
5. Caller receives error, context is not lost
```

## Chunking Strategies

| Strategy | Chunk Size | Overlap | Best For |
|----------|------------|---------|----------|
| FixedSize | 1000 tokens | 100 tokens | General purpose |
| Sentence | Varies | 1 sentence | Natural text |
| Paragraph | Varies | 0 | Structured docs |
| Semantic | Varies | 0 | Code, technical |

## Key Decision Points

```
                    Document arrives
                          │
                          ▼
                 ┌─────────────────┐
                 │ Is it binary?   │
                 └─────────────────┘
                    │         │
                  Yes        No
                    │         │
                    ▼         │
             BinaryProcessor  │
             (extract/summarize)
                    │         │
                    └────┬────┘
                         │
                         ▼
                 ┌─────────────────┐
                 │ Text too large? │
                 └─────────────────┘
                    │         │
                  Yes        No
                    │         │
                    ▼         ▼
              Split chunks   Single chunk
                    │         │
                    └────┬────┘
                         │
                         ▼
                 Generate embeddings
                         │
                         ▼
                 Store all data
```

## Search Quality Optimization

```
Query: "How does authentication work?"
         │
         ▼
    ┌─────────────────────────────┐
    │ 1. Basic Vector Search      │
    │    Returns: [0.89, 0.85,    │
    │              0.82, 0.80...]  │
    └─────────────────────────────┘
         │
         ▼
    ┌─────────────────────────────┐
    │ 2. Reranking (optional)     │
    │    LLM-based relevance      │
    │    scoring                  │
    └─────────────────────────────┘
         │
         ▼
    ┌─────────────────────────────┐
    │ 3. Diversity Filter         │
    │    Remove near-duplicates   │
    └─────────────────────────────┘
         │
         ▼
    ┌─────────────────────────────┐
    │ 4. Return Top K             │
    │    Final ranked results     │
    └─────────────────────────────┘
```
