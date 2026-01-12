namespace Hazina.AI.RAG.Graph.Pipeline;

using Hazina.AI.RAG.Graph.Models;

/// <summary>
/// Extracts named entities from text using LLM-based analysis.
/// </summary>
public interface IEntityExtractor
{
    /// <summary>
    /// Extracts entities from the given text.
    /// </summary>
    /// <param name="text">The text to analyze</param>
    /// <param name="documentId">Optional document ID for provenance tracking</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of extracted entities with confidence scores</returns>
    Task<List<GraphEntity>> ExtractEntitiesAsync(
        string text,
        string? documentId = null,
        CancellationToken cancellationToken = default);
}
