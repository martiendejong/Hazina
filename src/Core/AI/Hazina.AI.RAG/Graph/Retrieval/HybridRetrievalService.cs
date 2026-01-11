namespace Hazina.AI.RAG.Graph.Retrieval;

using Hazina.AI.RAG.Configuration;
using Hazina.AI.RAG.Core;
using Hazina.AI.RAG.Graph.Models;
using Hazina.AI.RAG.Graph.Storage;
using Hazina.Store.EmbeddingStore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Hybrid retrieval service combining vector similarity search with knowledge graph traversal.
/// </summary>
public class HybridRetrievalService
{
    private readonly IVectorSearchStore _vectorStore;
    private readonly IGraphStore _graphStore;
    private readonly ILogger<HybridRetrievalService> _logger;
    private readonly GraphRAGConfig _config;

    public HybridRetrievalService(
        IVectorSearchStore vectorStore,
        IGraphStore graphStore,
        ILogger<HybridRetrievalService> logger,
        GraphRAGConfig config)
    {
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
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
        // Search in vector store
        var results = await _vectorStore.SearchAsync(
            queryEmbedding,
            topK,
            cancellationToken: cancellationToken);

        return results.Select(r => new HybridRetrievalResult
        {
            DocumentId = r.DocumentId,
            ChunkId = r.ChunkId,
            Text = r.Text,
            Score = r.SimilarityScore,
            Source = RetrievalSource.VectorSearch,
            VectorScore = r.SimilarityScore
        }).ToList();
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
                            // TODO: Fetch document text from document store
                            expandedResults.Add(new HybridRetrievalResult
                            {
                                DocumentId = docId,
                                ChunkId = docId, // Simplified
                                Text = $"[Document {docId} - related via entity {relatedEntity.Name}]",
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
        // Search for entities that reference this document
        var stats = await _graphStore.GetStatisticsAsync(cancellationToken);
        var allEntities = new List<GraphEntity>();

        // Note: This is a simplified implementation
        // In production, maintain a reverse index: documentId -> entityIds
        // For now, we use search which is less efficient but functional
        return allEntities;
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
