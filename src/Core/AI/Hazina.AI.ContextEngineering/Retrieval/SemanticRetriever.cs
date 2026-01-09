using Hazina.AI.ContextEngineering.Interfaces;
using Hazina.AI.ContextEngineering.Models;
using Hazina.AI.RAG.Core;
using Microsoft.Extensions.Logging;

namespace Hazina.AI.ContextEngineering.Retrieval;

/// <summary>
/// Retriever that uses semantic/embedding-based search
/// (wraps existing RAGEngine for backwards compatibility)
/// </summary>
public class SemanticRetriever : IContextRetriever
{
    private readonly RAGEngine _ragEngine;
    private readonly ILogger<SemanticRetriever>? _logger;

    /// <inheritdoc />
    public string Name => "semantic";

    /// <summary>
    /// Creates a new semantic retriever
    /// </summary>
    /// <param name="ragEngine">RAG engine instance</param>
    /// <param name="logger">Optional logger</param>
    public SemanticRetriever(
        RAGEngine ragEngine,
        ILogger<SemanticRetriever>? logger = null)
    {
        _ragEngine = ragEngine ?? throw new ArgumentNullException(nameof(ragEngine));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<RetrievalResult>> RetrieveAsync(
        RetrievalQuery query,
        CancellationToken cancellationToken = default)
    {
        var documents = await _ragEngine.SearchAsync(
            query.QueryText,
            query.TopK,
            query.MinSimilarity,
            cancellationToken
        );

        _logger?.LogDebug(
            "Semantic retriever found {Count} results for query: {Query}",
            documents.Count,
            query.QueryText
        );

        return documents.Select(doc => new RetrievalResult
        {
            Id = doc.Id,
            Content = doc.Content,
            Score = doc.Similarity,
            Source = Name,
            Type = "chunk",
            Metadata = doc.Metadata,
            Tags = doc.Metadata.TryGetValue("tags", out var tagsObj) && tagsObj is List<string> tags
                ? tags
                : new List<string>(),
            Timestamp = doc.Metadata.TryGetValue("timestamp", out var tsObj) && tsObj is DateTime ts
                ? ts
                : null
        }).ToList();
    }
}
