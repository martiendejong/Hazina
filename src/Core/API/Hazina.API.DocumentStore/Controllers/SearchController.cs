using Hazina.API.DocumentStore.Models;
using Hazina.API.DocumentStore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Hazina.API.DocumentStore.Controllers;

/// <summary>
/// Controller for performing RAG-powered searches
/// </summary>
[ApiController]
[Route("api/v1/stores/{storeId}/search")]
[Authorize]
public class SearchController : ControllerBase
{
    private readonly SearchService _searchService;
    private readonly RAGStoreManager _storeManager;
    private readonly ILogger<SearchController> _logger;

    public SearchController(
        SearchService searchService,
        RAGStoreManager storeManager,
        ILogger<SearchController> logger)
    {
        _searchService = searchService;
        _storeManager = storeManager;
        _logger = logger;
    }

    /// <summary>
    /// Perform a RAG-powered search with optional answer generation
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SearchResponse>> Search(
        Guid storeId,
        [FromBody] SearchRequest request)
    {
        try
        {
            // Verify store exists
            var store = await _storeManager.GetStoreAsync(storeId);
            if (store == null)
            {
                return NotFound(new { error = $"RAG store {storeId} not found" });
            }

            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest(new { error = "Query cannot be empty" });
            }

            var response = await _searchService.SearchAsync(storeId, request);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing search in store {StoreId}", storeId);
            return BadRequest(new { error = "Search failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Search for similar documents without answer generation
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<SourceDocument>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<SourceDocument>>> SearchDocuments(
        Guid storeId,
        [FromQuery] string query,
        [FromQuery] int topK = 10,
        [FromQuery] float minRelevanceScore = 0.7f)
    {
        try
        {
            // Verify store exists
            var store = await _storeManager.GetStoreAsync(storeId);
            if (store == null)
            {
                return NotFound(new { error = $"RAG store {storeId} not found" });
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest(new { error = "Query cannot be empty" });
            }

            var documents = await _searchService.SearchDocumentsAsync(
                storeId, query, topK, minRelevanceScore);

            return Ok(documents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching documents in store {StoreId}", storeId);
            return BadRequest(new { error = "Document search failed", details = ex.Message });
        }
    }
}
