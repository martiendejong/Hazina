using Hazina.API.Search.Integration;
using Hazina.API.Search.Models;
using Hazina.AI.RAG;
using Hazina.AI.RAG.Core;

namespace Hazina.API.Search.Services;

/// <summary>
/// Service for performing RAG-powered searches using real Hazina RAGEngine
/// </summary>
public class SearchService
{
    private readonly RAGStoreManager _storeManager;
    private readonly ILogger<SearchService> _logger;

    public SearchService(
        RAGStoreManager storeManager,
        ILogger<SearchService> logger)
    {
        _storeManager = storeManager;
        _logger = logger;
    }

    /// <summary>
    /// Perform a RAG-powered search
    /// </summary>
    public async Task<SearchResponse> SearchAsync(Guid storeId, SearchRequest request)
    {
        var startTime = DateTime.UtcNow;

        // Get the Hazina store instance
        var instance = await _storeManager.GetStoreInstanceAsync(storeId);

        // Build RAG query options
        var queryOptions = new RAGQueryOptions
        {
            TopK = request.TopK ?? 10,
            MinSimilarity = request.MinRelevanceScore ?? 0.7,
            UseEmbeddings = true
        };

        // Perform RAG query
        var ragResponse = await instance.RAGEngine.QueryAsync(request.Query, queryOptions);

        // Map to API response
        var response = new SearchResponse
        {
            Query = request.Query,
            Answer = ragResponse.Answer ?? string.Empty,
            SourceDocuments = ragResponse.RetrievedDocuments?.Select(doc => new SourceDocument
            {
                DocumentId = ParseDocumentGuid(doc.Id),
                ChunkIndex = 0,
                Text = doc.Content,
                RelevanceScore = (float)doc.Similarity,
                Metadata = doc.Metadata ?? new Dictionary<string, object>()
            }).ToList() ?? new List<SourceDocument>(),
            Metadata = new SearchMetadata
            {
                TotalResults = ragResponse.RetrievedDocuments?.Count ?? 0,
                ProcessingTimeMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds,
                Model = "gpt-4o",
                TokensUsed = 0
            }
        };

        _logger.LogInformation(
            "Search completed for store {StoreId}: {ResultCount} results in {TimeMs}ms",
            storeId, response.Metadata.TotalResults, response.Metadata.ProcessingTimeMs);

        return response;
    }

    /// <summary>
    /// Search for similar documents without answer generation
    /// </summary>
    public async Task<List<SourceDocument>> SearchDocumentsAsync(
        Guid storeId,
        string query,
        int topK = 10,
        float minRelevanceScore = 0.7f)
    {
        var instance = await _storeManager.GetStoreInstanceAsync(storeId);

        // Use RAGEngine search without answer generation
        var results = await instance.RAGEngine.SearchAsync(query, topK, minRelevanceScore);

        return results.Select(doc => new SourceDocument
        {
            DocumentId = ParseDocumentGuid(doc.Id),
            ChunkIndex = 0,
            Text = doc.Content,
            RelevanceScore = (float)doc.Similarity,
            Metadata = doc.Metadata ?? new Dictionary<string, object>()
        }).ToList();
    }

    /// <summary>
    /// Search using DocumentStore's embedding-based search
    /// </summary>
    public async Task<List<SourceDocument>> SearchByEmbeddingAsync(
        Guid storeId,
        string query,
        int topK = 10)
    {
        var instance = await _storeManager.GetStoreInstanceAsync(storeId);

        // Use DocumentStore's embedding search
        var embeddings = await instance.DocumentStore.Embeddings(query);

        return embeddings
            .Take(topK)
            .Select(e => new SourceDocument
            {
                DocumentId = ParseDocumentGuid(e.Document?.Key ?? string.Empty),
                ChunkIndex = 0,
                Text = e.Document?.Key ?? string.Empty,
                RelevanceScore = (float)e.Similarity,
                Metadata = new Dictionary<string, object>
                {
                    ["storeName"] = e.StoreName ?? string.Empty,
                    ["parentDocument"] = e.ParentDocumentKey ?? string.Empty
                }
            }).ToList();
    }

    /// <summary>
    /// Parse document ID from string key to Guid
    /// </summary>
    private static Guid ParseDocumentGuid(string key)
    {
        if (string.IsNullOrEmpty(key))
            return Guid.Empty;

        // Document IDs are in format: {storeId:N}/{docId:N}/{filename}
        var parts = key.Split('/');
        if (parts.Length >= 2 && Guid.TryParse(parts[1], out var docId))
        {
            return docId;
        }

        return Guid.Empty;
    }
}
