using Hazina.Demo.GenericApi.Entities;

namespace Hazina.Demo.GenericApi.Services;

/// <summary>
/// Service for semantic search using vector embeddings.
/// </summary>
public interface ISemanticSearchService
{
    /// <summary>
    /// Search for documents semantically similar to the query.
    /// </summary>
    /// <param name="query">Natural language search query</param>
    /// <param name="topK">Maximum results to return</param>
    /// <param name="minScore">Minimum similarity score (0-1)</param>
    /// <returns>List of matching documents with scores</returns>
    Task<List<SearchHit>> SearchAsync(string query, int topK = 10, float minScore = 0.5f);

    /// <summary>
    /// Generate embedding for a document.
    /// </summary>
    Task EmbedDocumentAsync(Document document);

    /// <summary>
    /// Generate embedding for text.
    /// </summary>
    Task<float[]> GetEmbeddingAsync(string text);
}

/// <summary>
/// A search result with score
/// </summary>
public class SearchHit
{
    public Document Document { get; set; } = null!;
    public float Score { get; set; }
}
