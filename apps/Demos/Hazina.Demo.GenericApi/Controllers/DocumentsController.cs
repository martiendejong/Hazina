using Hazina.API.Generic.Controllers;
using Hazina.Demo.GenericApi.Entities;
using Hazina.Demo.GenericApi.Services;
using Hazina.Tools.Data.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Linq.Expressions;

namespace Hazina.Demo.GenericApi.Controllers;

/// <summary>
/// Documents API with CRUD + semantic search.
/// Demonstrates extending GenericEntityController with custom endpoints.
/// </summary>
[Route("api/[controller]")]
[ApiExplorerSettings(GroupName = "Documents")]
public class DocumentsController : GenericEntityController<Document>
{
    private readonly ISemanticSearchService _searchService;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        IRepository<Document> repository,
        ISemanticSearchService searchService,
        ILogger<DocumentsController> logger) : base(repository)
    {
        _searchService = searchService;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/documents/search
    /// Semantic search - finds documents by meaning, not just keywords.
    /// </summary>
    /// <param name="request">Search query and options</param>
    /// <returns>Documents ranked by semantic similarity</returns>
    [HttpPost("search")]
    public async Task<ActionResult<SemanticSearchResult>> SemanticSearch([FromBody] SemanticSearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid query",
                Detail = "Search query cannot be empty",
                Status = 400
            });
        }

        _logger.LogInformation("Semantic search for: {Query}", request.Query);

        var results = await _searchService.SearchAsync(
            request.Query,
            request.TopK,
            request.MinScore);

        return Ok(new SemanticSearchResult
        {
            Query = request.Query,
            Results = results,
            TotalFound = results.Count
        });
    }

    /// <summary>
    /// POST /api/documents/{id}/embed
    /// Generate embedding for a specific document.
    /// </summary>
    [HttpPost("{id}/embed")]
    public async Task<ActionResult<Document>> GenerateEmbedding(Guid id)
    {
        var document = await Repository.GetByIdAsync(id);
        if (document == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Document not found",
                Detail = $"No document found with ID {id}",
                Status = 404
            });
        }

        await _searchService.EmbedDocumentAsync(document);
        await Repository.UpdateAsync(document);
        await Repository.SaveChangesAsync();

        _logger.LogInformation("Generated embedding for document {Id}", id);
        return Ok(document);
    }

    /// <summary>
    /// POST /api/documents/embed-all
    /// Generate embeddings for all documents that don't have one.
    /// </summary>
    [HttpPost("embed-all")]
    public async Task<ActionResult<EmbeddingResult>> EmbedAllDocuments()
    {
        var documents = await Repository.FindAsync(d => d.Embedding == null && !d.IsDeleted);
        var documentList = documents.ToList();

        var embedded = 0;
        var failed = 0;

        foreach (var doc in documentList)
        {
            try
            {
                await _searchService.EmbedDocumentAsync(doc);
                await Repository.UpdateAsync(doc);
                embedded++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to embed document {Id}", doc.Id);
                failed++;
            }
        }

        await Repository.SaveChangesAsync();

        return Ok(new EmbeddingResult
        {
            Processed = documentList.Count,
            Embedded = embedded,
            Failed = failed
        });
    }

    /// <summary>
    /// GET /api/documents/by-type/{type}
    /// Get documents filtered by type.
    /// </summary>
    [HttpGet("by-type/{type}")]
    public async Task<ActionResult<List<Document>>> GetByType(string type)
    {
        var documents = await Repository.FindAsync(d =>
            d.DocumentType == type && !d.IsDeleted);
        return Ok(documents);
    }

    /// <summary>
    /// GET /api/documents/by-tag/{tag}
    /// Get documents that contain a specific tag.
    /// </summary>
    [HttpGet("by-tag/{tag}")]
    public async Task<ActionResult<List<Document>>> GetByTag(string tag)
    {
        var documents = await Repository.FindAsync(d =>
            d.Tags != null && d.Tags.Contains(tag) && !d.IsDeleted);
        return Ok(documents);
    }

    /// <summary>
    /// Override filter expression to support full-text search
    /// </summary>
    protected override Expression<Func<Document, bool>>? BuildFilterExpression(string filter)
    {
        var lowerFilter = filter.ToLowerInvariant();
        return d => d.Title.ToLower().Contains(lowerFilter) ||
                   d.Content.ToLower().Contains(lowerFilter) ||
                   (d.Tags != null && d.Tags.ToLower().Contains(lowerFilter)) ||
                   (d.Author != null && d.Author.ToLower().Contains(lowerFilter));
    }
}

/// <summary>
/// Request for semantic search
/// </summary>
public class SemanticSearchRequest
{
    /// <summary>
    /// The search query (natural language)
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of results to return
    /// </summary>
    public int TopK { get; set; } = 10;

    /// <summary>
    /// Minimum similarity score (0-1)
    /// </summary>
    public float MinScore { get; set; } = 0.5f;
}

/// <summary>
/// Result of semantic search
/// </summary>
public class SemanticSearchResult
{
    public string Query { get; set; } = string.Empty;
    public List<SearchHit> Results { get; set; } = new();
    public int TotalFound { get; set; }
}

/// <summary>
/// Result of embedding operation
/// </summary>
public class EmbeddingResult
{
    public int Processed { get; set; }
    public int Embedded { get; set; }
    public int Failed { get; set; }
}
