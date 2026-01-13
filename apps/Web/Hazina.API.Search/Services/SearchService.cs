using Hazina.API.Search.Integration;
using Hazina.API.Search.Models;

namespace Hazina.API.Search.Services;

/// <summary>
/// Service for performing RAG-powered searches
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

    public async Task<SearchResponse> SearchAsync(Guid storeId, SearchRequest request)
    {
        var startTime = DateTime.UtcNow;

        // Get the RAG engine for this store
        var ragEngine = await _storeManager.GetRAGEngineAsync(storeId);

        // Perform the search
        var ragRequest = new RAGRequest
        {
            Query = request.Query,
            TopK = request.TopK ?? 10,
            MinRelevanceScore = request.MinRelevanceScore ?? 0.7f,
            IncludeSourceDocuments = request.IncludeSourceDocuments,
            UseGenerativeAnswer = request.UseGenerativeAnswer,
            SystemPrompt = request.SystemPrompt
        };

        var ragResponse = await ragEngine.QueryAsync(ragRequest);

        // Map to API response
        var response = new SearchResponse
        {
            Query = request.Query,
            Answer = ragResponse.Answer,
            SourceDocuments = ragResponse.SourceDocuments?.Select(doc => new SourceDocument
            {
                DocumentId = doc.DocumentId,
                ChunkIndex = doc.ChunkIndex,
                Text = doc.Text,
                RelevanceScore = doc.RelevanceScore,
                Metadata = doc.Metadata
            }).ToList() ?? new List<SourceDocument>(),
            Metadata = new SearchMetadata
            {
                TotalResults = ragResponse.SourceDocuments?.Count ?? 0,
                ProcessingTimeMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds,
                Model = ragResponse.ModelUsed ?? "unknown",
                TokensUsed = ragResponse.TokensUsed
            }
        };

        _logger.LogInformation(
            "Search completed for store {StoreId}: {ResultCount} results in {TimeMs}ms",
            storeId, response.Metadata.TotalResults, response.Metadata.ProcessingTimeMs);

        return response;
    }

    public async Task<List<SourceDocument>> SearchDocumentsAsync(
        Guid storeId,
        string query,
        int topK = 10,
        float minRelevanceScore = 0.7f)
    {
        var ragEngine = await _storeManager.GetRAGEngineAsync(storeId);

        var ragRequest = new RAGRequest
        {
            Query = query,
            TopK = topK,
            MinRelevanceScore = minRelevanceScore,
            IncludeSourceDocuments = true,
            UseGenerativeAnswer = false
        };

        var ragResponse = await ragEngine.QueryAsync(ragRequest);

        return ragResponse.SourceDocuments?.Select(doc => new SourceDocument
        {
            DocumentId = doc.DocumentId,
            ChunkIndex = doc.ChunkIndex,
            Text = doc.Text,
            RelevanceScore = doc.RelevanceScore,
            Metadata = doc.Metadata
        }).ToList() ?? new List<SourceDocument>();
    }
}
