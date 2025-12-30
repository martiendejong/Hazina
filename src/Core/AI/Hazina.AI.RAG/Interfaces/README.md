# Retrieval Pipeline Interfaces

This folder contains the core abstractions for the retrieval and reranking pipeline.

## Architecture

The retrieval pipeline follows a clean separation of concerns:

```
Query → IRetriever → Candidates → IReranker → Ranked Results
```

### Interfaces

#### `IRetriever`
Responsible for initial candidate retrieval from a vector store or document store.
- Generates embeddings for the query
- Performs similarity search
- Returns top-K candidates with original similarity scores

#### `IRetrievalCandidate`
Represents a single retrieval result with:
- `ChunkId` - unique identifier for the chunk
- `SourceDocumentId` - parent document identifier
- `OriginalScore` - initial similarity score from vector search
- `RerankScore` - optional score from reranking (null if not reranked)
- `Text` - actual content
- `Metadata` - extensible metadata dictionary

#### `IReranker`
Refines retrieval results using various strategies:
- No-op (baseline)
- LLM-based relevance scoring
- Cross-encoder models (future)
- Hybrid approaches (future)

#### `IRetrievalPipeline`
Orchestrates the full pipeline:
1. Retrieve top-K candidates
2. Rerank candidates
3. Return top-N results

## Implementations

### Retrievers
- **VectorStoreRetriever** - Adapts `IVectorSearchStore` to `IRetriever`

### Rerankers
- **NoOpReranker** - Pass-through reranker for baseline comparisons
- **LlmJudgeReranker** - Uses LLM to score relevance (0-10 scale) with stable prompts

### Pipeline
- **RetrievalPipeline** - Default implementation coordinating retrieval and reranking

## Dependency Injection

Use `RetrievalServiceExtensions` to register the pipeline:

```csharp
services.AddRetrievalPipeline(options =>
{
    options.RerankingStrategy = RerankingStrategy.LlmJudge;
    options.LlmJudgeOptions = new LlmJudgeRerankerOptions
    {
        MaxDocumentLength = 2000
    };
});
```

Or register custom implementations:

```csharp
services.AddRetriever<MyCustomRetriever>();
services.AddReranker<MyCustomReranker>();
services.AddCustomRetrievalPipeline<MyCustomPipeline>();
```

## Design Constraints

1. **No hard dependencies on specific LLM providers** - Uses `IProviderOrchestrator` abstraction
2. **Swappable via DI** - All components are interface-based
3. **Configurable reranking flow** - Retrieve top-K (e.g., 20), rerank to top-N (e.g., 5)
4. **Candidate metadata preserved** - Both original and rerank scores available for analysis

## Future Extensions

- Cross-encoder reranker
- Hybrid reranker (combine multiple strategies)
- Diversity-based reranking
- Graph-based expansion (see Phase 5)
