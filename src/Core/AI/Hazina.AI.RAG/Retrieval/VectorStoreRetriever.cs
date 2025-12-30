using Hazina.AI.Providers.Core;
using Hazina.AI.RAG.Interfaces;
using Hazina.Store.EmbeddingStore;

namespace Hazina.AI.RAG.Retrieval;

/// <summary>
/// Adapts IVectorSearchStore + IEmbeddingGenerator to IRetriever interface.
/// Handles query embedding generation and vector search in a single call.
/// </summary>
public class VectorStoreRetriever : IRetriever
{
    private readonly IVectorSearchStore _vectorSearchStore;
    private readonly IProviderOrchestrator _orchestrator;

    public VectorStoreRetriever(
        IVectorSearchStore vectorSearchStore,
        IProviderOrchestrator orchestrator)
    {
        _vectorSearchStore = vectorSearchStore ?? throw new ArgumentNullException(nameof(vectorSearchStore));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
    }

    public async Task<List<IRetrievalCandidate>> GetCandidatesAsync(
        string query,
        int topK = 20,
        double minSimilarity = 0.0,
        CancellationToken cancellationToken = default)
    {
        var embeddingArray = await _orchestrator.GenerateEmbedding(query);
        var embedding = new Embedding(embeddingArray);

        var results = await _vectorSearchStore.SearchSimilarAsync(
            embedding,
            topK,
            minSimilarity,
            cancellationToken
        );

        return results.Select(r => new RetrievalCandidate
        {
            ChunkId = r.Info.Key,
            SourceDocumentId = r.ParentDocumentKey ?? r.Info.Key,
            OriginalScore = r.Similarity,
            RerankScore = null,
            Text = GetTextFromMetadata(r),
            Metadata = r.Metadata
        } as IRetrievalCandidate).ToList();
    }

    private static string GetTextFromMetadata(ScoredEmbedding scored)
    {
        if (scored.Metadata != null &&
            scored.Metadata.TryGetValue("text", out var textObj) &&
            textObj is string text)
        {
            return text;
        }

        return scored.Info.Checksum;
    }
}
