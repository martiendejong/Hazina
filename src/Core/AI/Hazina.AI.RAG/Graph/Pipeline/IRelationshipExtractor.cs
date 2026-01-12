namespace Hazina.AI.RAG.Graph.Pipeline;

using Hazina.AI.RAG.Graph.Models;

/// <summary>
/// Extracts relationships between entities from text.
/// </summary>
public interface IRelationshipExtractor
{
    /// <summary>
    /// Extracts relationships between the given entities based on text context.
    /// </summary>
    /// <param name="text">The text to analyze</param>
    /// <param name="entities">Entities already extracted from the text</param>
    /// <param name="documentId">Optional document ID for provenance</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of relationships found between entities</returns>
    Task<List<GraphRelationship>> ExtractRelationshipsAsync(
        string text,
        List<GraphEntity> entities,
        string? documentId = null,
        CancellationToken cancellationToken = default);
}
