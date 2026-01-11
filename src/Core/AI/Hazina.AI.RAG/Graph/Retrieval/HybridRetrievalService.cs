namespace Hazina.AI.RAG.Graph.Retrieval;

using Hazina.AI.RAG.Configuration;
using Hazina.AI.RAG.Core;
using Hazina.AI.RAG.Graph.Models;
using Hazina.AI.RAG.Graph.Storage;
using Microsoft.Extensions.Logging;

/// <summary>
/// Hybrid retrieval service combining vector similarity search with knowledge graph traversal.
/// </summary>
public class HybridRetrievalService
{
    private readonly IDocumentStore _documentStore;
    private readonly IGraphStore _graphStore;
    private readonly ILogger<HybridRetrievalService> _logger;
    private readonly GraphRAGConfig _config;

    public HybridRetrievalService(
        IDocumentStore documentStore,
        IGraphStore graphStore,
        ILogger<HybridRetrievalService> logger,
        GraphRAGConfig config)
    {
        _documentStore = documentStore ?? throw new ArgumentNullException(nameof(documentStore));
        _graphStore = graphStore ?? throw new ArgumentNullException(nameof(graphStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Performs hybrid retrieval combining vector search and graph traversal.
    /// </summary>
    /// <param name="queryEmbedding">Query embedding vector</param>
    /// <param name="topK">Number of top results to return</param>
    /// <param name="options">Hybrid retrieval options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Ranked list of hybrid retrieval results</returns>
    public async Task<List<HybridRetrievalResult>> RetrieveAsync(
        float[] queryEmbedding,
        int topK = 10,
        HybridRetrievalOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new HybridRetrievalOptions();

        // Step 1: Vector similarity search
        var vectorResults = await PerformVectorSearchAsync(queryEmbedding, topK * 2, cancellationToken);
        _logger.LogDebug("Vector search returned {Count} results", vectorResults.Count);

        // Step 2: Graph-based expansion
        var graphResults = await ExpandWithGraphAsync(vectorResults, options, cancellationToken);
        _logger.LogDebug("Graph expansion added {Count} additional results",
            graphResults.Count - vectorResults.Count);

        // Step 3: Fusion and re-ranking
        var fusedResults = FuseResults(vectorResults, graphResults, options);

        // Step 4: Return top-K
        var finalResults = fusedResults
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();

        _logger.LogInformation("Hybrid retrieval returned {Count} results (vector: {Vector}, graph: {Graph})",
            finalResults.Count,
            finalResults.Count(r => r.Source == RetrievalSource.VectorSearch),
            finalResults.Count(r => r.Source == RetrievalSource.GraphTraversal));

        return finalResults;
    }

    private async Task<List<HybridRetrievalResult>> PerformVectorSearchAsync(
        float[] queryEmbedding,
        int topK,
        CancellationToken cancellationToken)
    {
        // Note: IDocumentStore.RelevantItems() and Embeddings() take string queries,
        // but we have a pre-computed embedding vector. For now, we'll search all chunks
        // manually using the embedding store's search capability.

        var results = new List<HybridRetrievalResult>();

        try
        {
            // Get all document chunks from the document store
            var allDocuments = await _documentStore.List("", recursive: true);

            // For each document, get its chunks and calculate similarity
            var scoredChunks = new List<(string docId, string chunkKey, double similarity, string text)>();

            foreach (var docId in allDocuments.Take(1000)) // Limit for performance
            {
                try
                {
                    var docWithChunks = await _documentStore.GetDocumentWithChunks(docId);
                    if (docWithChunks?.ChunkKeys == null) continue;

                    foreach (var chunkKey in docWithChunks.ChunkKeys)
                    {
                        // Get chunk embedding and calculate similarity
                        var chunkEmbedding = await _documentStore.EmbeddingStore.GetEmbedding(chunkKey);
                        if (chunkEmbedding?.Data == null || chunkEmbedding.Data.Count == 0) continue;

                        var similarity = CalculateCosineSimilarity(queryEmbedding, chunkEmbedding.Data.ToArray());
                        var chunkText = await _documentStore.GetChunk(chunkKey) ?? "";

                        scoredChunks.Add((docId, chunkKey, similarity, chunkText));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process document {DocumentId}", docId);
                }
            }

            // Sort by similarity and take top K
            var topChunks = scoredChunks
                .OrderByDescending(c => c.similarity)
                .Take(topK);

            foreach (var (docId, chunkKey, similarity, text) in topChunks)
            {
                results.Add(new HybridRetrievalResult
                {
                    DocumentId = docId,
                    ChunkId = chunkKey,
                    Text = text,
                    Score = similarity,
                    Source = RetrievalSource.VectorSearch,
                    VectorScore = similarity
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vector search failed");
            throw;
        }

        return results;
    }

    private static double CalculateCosineSimilarity(float[] vector1, double[] vector2)
    {
        if (vector1.Length != vector2.Length)
        {
            throw new ArgumentException("Vectors must have the same dimension");
        }

        double dotProduct = 0;
        double magnitude1 = 0;
        double magnitude2 = 0;

        for (int i = 0; i < vector1.Length; i++)
        {
            dotProduct += vector1[i] * vector2[i];
            magnitude1 += vector1[i] * vector1[i];
            magnitude2 += vector2[i] * vector2[i];
        }

        magnitude1 = Math.Sqrt(magnitude1);
        magnitude2 = Math.Sqrt(magnitude2);

        if (magnitude1 == 0 || magnitude2 == 0)
        {
            return 0;
        }

        return dotProduct / (magnitude1 * magnitude2);
    }

    private async Task<List<HybridRetrievalResult>> ExpandWithGraphAsync(
        List<HybridRetrievalResult> vectorResults,
        HybridRetrievalOptions options,
        CancellationToken cancellationToken)
    {
        if (!options.UseGraphExpansion)
        {
            return vectorResults;
        }

        var expandedResults = new List<HybridRetrievalResult>(vectorResults);
        var seenDocuments = new HashSet<string>(vectorResults.Select(r => r.DocumentId));

        foreach (var result in vectorResults.Take(options.MaxGraphExpansionSeeds))
        {
            // Find entities mentioned in this document
            var entities = await FindEntitiesInDocument(result.DocumentId, cancellationToken);

            foreach (var entity in entities.Take(options.MaxEntitiesPerDocument))
            {
                // Get related entities via graph traversal
                var relatedEntities = await _graphStore.GetNeighborsAsync(
                    entity.Id,
                    options.GraphTraversalDepth,
                    cancellationToken);

                // Find documents containing these related entities
                foreach (var relatedEntity in relatedEntities)
                {
                    foreach (var docId in relatedEntity.SourceDocuments)
                    {
                        if (seenDocuments.Add(docId))
                        {
                            // Fetch actual document text from document store
                            string documentText;
                            try
                            {
                                // Try to get the chunk first, fall back to full document
                                documentText = await _documentStore.GetChunk(docId)
                                    ?? await _documentStore.Get(docId);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to retrieve document {DocumentId} from document store. Using placeholder.", docId);
                                documentText = $"[Document {docId} - related via entity {relatedEntity.Name}]";
                            }

                            expandedResults.Add(new HybridRetrievalResult
                            {
                                DocumentId = docId,
                                ChunkId = docId, // Simplified
                                Text = documentText,
                                Score = 0.0, // Will be scored during fusion
                                Source = RetrievalSource.GraphTraversal,
                                GraphPath = new GraphPath
                                {
                                    Entities = new List<GraphEntity> { entity, relatedEntity }
                                }
                            });
                        }
                    }
                }
            }
        }

        return expandedResults;
    }

    private async Task<List<GraphEntity>> FindEntitiesInDocument(
        string documentId,
        CancellationToken cancellationToken)
    {
        // Use the new GetEntitiesByDocumentAsync method to efficiently find entities
        // associated with this document via the entity_source_documents table
        return await _graphStore.GetEntitiesByDocumentAsync(documentId, cancellationToken);
    }

    private List<HybridRetrievalResult> FuseResults(
        List<HybridRetrievalResult> vectorResults,
        List<HybridRetrievalResult> graphResults,
        HybridRetrievalOptions options)
    {
        var fusionStrategy = options.FusionStrategy;

        switch (fusionStrategy)
        {
            case FusionStrategy.WeightedSum:
                return FuseByWeightedSum(vectorResults, graphResults, options);

            case FusionStrategy.ReciprocalRankFusion:
                return FuseByReciprocalRank(vectorResults, graphResults, options);

            case FusionStrategy.MaxScore:
                return FuseByMaxScore(vectorResults, graphResults);

            default:
                throw new ArgumentException($"Unknown fusion strategy: {fusionStrategy}");
        }
    }

    private List<HybridRetrievalResult> FuseByWeightedSum(
        List<HybridRetrievalResult> vectorResults,
        List<HybridRetrievalResult> graphResults,
        HybridRetrievalOptions options)
    {
        var results = new Dictionary<string, HybridRetrievalResult>();

        // Add vector results
        foreach (var result in vectorResults)
        {
            var key = result.ChunkId ?? result.DocumentId;
            result.Score = result.VectorScore * options.VectorWeight;
            results[key] = result;
        }

        // Add/merge graph results
        foreach (var result in graphResults)
        {
            var key = result.ChunkId ?? result.DocumentId;

            if (results.TryGetValue(key, out var existing))
            {
                // Merge scores
                existing.Score += (result.GraphPath?.Score ?? 0.5) * options.GraphWeight;
                existing.GraphPath = result.GraphPath; // Add graph context
            }
            else
            {
                result.Score = (result.GraphPath?.Score ?? 0.5) * options.GraphWeight;
                results[key] = result;
            }
        }

        return results.Values.ToList();
    }

    private List<HybridRetrievalResult> FuseByReciprocalRank(
        List<HybridRetrievalResult> vectorResults,
        List<HybridRetrievalResult> graphResults,
        HybridRetrievalOptions options)
    {
        var results = new Dictionary<string, HybridRetrievalResult>();
        const int k = 60; // RRF constant

        // Process vector results
        for (var i = 0; i < vectorResults.Count; i++)
        {
            var result = vectorResults[i];
            var key = result.ChunkId ?? result.DocumentId;
            var rrf = 1.0 / (k + i + 1);
            result.Score = rrf * options.VectorWeight;
            results[key] = result;
        }

        // Process graph results
        for (var i = 0; i < graphResults.Count; i++)
        {
            var result = graphResults[i];
            var key = result.ChunkId ?? result.DocumentId;
            var rrf = 1.0 / (k + i + 1);

            if (results.TryGetValue(key, out var existing))
            {
                existing.Score += rrf * options.GraphWeight;
                existing.GraphPath = result.GraphPath;
            }
            else
            {
                result.Score = rrf * options.GraphWeight;
                results[key] = result;
            }
        }

        return results.Values.ToList();
    }

    private List<HybridRetrievalResult> FuseByMaxScore(
        List<HybridRetrievalResult> vectorResults,
        List<HybridRetrievalResult> graphResults)
    {
        var results = new Dictionary<string, HybridRetrievalResult>();

        foreach (var result in vectorResults)
        {
            var key = result.ChunkId ?? result.DocumentId;
            result.Score = result.VectorScore;
            results[key] = result;
        }

        foreach (var result in graphResults)
        {
            var key = result.ChunkId ?? result.DocumentId;
            var graphScore = result.GraphPath?.Score ?? 0.5;

            if (results.TryGetValue(key, out var existing))
            {
                existing.Score = Math.Max(existing.Score, graphScore);
                existing.GraphPath = result.GraphPath;
            }
            else
            {
                result.Score = graphScore;
                results[key] = result;
            }
        }

        return results.Values.ToList();
    }
}

/// <summary>
/// Result from hybrid retrieval combining vector and graph search.
/// </summary>
public class HybridRetrievalResult
{
    public string DocumentId { get; set; } = string.Empty;
    public string? ChunkId { get; set; }
    public string Text { get; set; } = string.Empty;
    public double Score { get; set; }
    public RetrievalSource Source { get; set; }
    public double VectorScore { get; set; }
    public GraphPath? GraphPath { get; set; }
}

/// <summary>
/// Source of retrieval result.
/// </summary>
public enum RetrievalSource
{
    VectorSearch,
    GraphTraversal,
    Hybrid
}

/// <summary>
/// Options for hybrid retrieval.
/// </summary>
public class HybridRetrievalOptions
{
    public bool UseGraphExpansion { get; set; } = true;
    public int GraphTraversalDepth { get; set; } = 2;
    public int MaxGraphExpansionSeeds { get; set; } = 5;
    public int MaxEntitiesPerDocument { get; set; } = 3;
    public FusionStrategy FusionStrategy { get; set; } = FusionStrategy.WeightedSum;
    public double VectorWeight { get; set; } = 0.7;
    public double GraphWeight { get; set; } = 0.3;
}

/// <summary>
/// Strategy for fusing vector and graph results.
/// </summary>
public enum FusionStrategy
{
    WeightedSum,
    ReciprocalRankFusion,
    MaxScore
}
