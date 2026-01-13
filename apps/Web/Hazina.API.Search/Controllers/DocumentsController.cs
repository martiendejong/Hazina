using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Hazina.API.Search.Models;

namespace Hazina.API.Search.Controllers;

[ApiController]
[Route("api/v1/documents")]
[Authorize(Policy = "ReadAccess")]
public class DocumentsController : ControllerBase
{
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(ILogger<DocumentsController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get paginated list of documents with optional filtering
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<DocumentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<DocumentResponse>>> GetDocumentsAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? tags = null,
        [FromQuery] string? mimeType = null,
        [FromQuery] DateTime? createdAfter = null)
    {
        _logger.LogInformation("Get documents: Page={Page}, PageSize={PageSize}, Tags={Tags}",
            page, pageSize, tags);

        // Validate pagination
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        // TODO: Integrate with actual DocumentStore
        var mockDocuments = new List<DocumentResponse>
        {
            new DocumentResponse
            {
                Id = "doc_001",
                Title = "Sample Document 1",
                Content = "This is sample content...",
                MimeType = "text/plain",
                Tags = new List<string> { "sample", "test" },
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                ModifiedAt = DateTime.UtcNow.AddDays(-2),
                SizeBytes = 1024
            },
            new DocumentResponse
            {
                Id = "doc_002",
                Title = "Sample Document 2",
                Content = "More sample content...",
                MimeType = "application/pdf",
                Tags = new List<string> { "research" },
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                ModifiedAt = DateTime.UtcNow.AddDays(-1),
                SizeBytes = 4096
            }
        };

        var totalItems = 2;
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var response = new PagedResponse<DocumentResponse>
        {
            Items = mockDocuments,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages
        };

        return Ok(response);
    }

    /// <summary>
    /// Get document by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentResponse>> GetDocumentByIdAsync(string id)
    {
        _logger.LogInformation("Get document by ID: {Id}", id);

        // TODO: Integrate with actual DocumentStore
        var document = new DocumentResponse
        {
            Id = id,
            Title = $"Document {id}",
            Content = "Full document content goes here...",
            MimeType = "text/plain",
            Tags = new List<string> { "sample" },
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            ModifiedAt = DateTime.UtcNow.AddDays(-1),
            SizeBytes = 2048
        };

        return Ok(document);
    }

    /// <summary>
    /// Upload a new document
    /// </summary>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting("document_upload")]
    [RequestSizeLimit(100_000_000)] // 100 MB
    [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<DocumentResponse>> UploadDocumentAsync([FromForm] UploadDocumentRequest request)
    {
        if (request.File == null || request.File.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid File",
                Detail = "No file was uploaded or file is empty"
            });
        }

        _logger.LogInformation("Upload document: {FileName}, Size: {Size} bytes",
            request.File.FileName, request.File.Length);

        // TODO: Integrate with actual DocumentStore
        using var stream = new MemoryStream();
        await request.File.CopyToAsync(stream);
        var content = stream.ToArray();

        var documentId = Guid.NewGuid().ToString("N");

        var document = new DocumentResponse
        {
            Id = documentId,
            Title = request.File.FileName,
            Content = "Content extracted from uploaded file...",
            MimeType = request.File.ContentType,
            Tags = request.Tags?.Split(',').Select(t => t.Trim()).ToList() ?? new List<string>(),
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            SizeBytes = request.File.Length
        };

        return CreatedAtAction(
            nameof(GetDocumentByIdAsync),
            new { id = documentId },
            document);
    }

    /// <summary>
    /// Update document metadata
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<DocumentResponse>> UpdateDocumentAsync(
        string id,
        [FromBody] UpdateDocumentRequest request)
    {
        _logger.LogInformation("Update document: {Id}", id);

        // TODO: Integrate with actual DocumentStore
        var document = new DocumentResponse
        {
            Id = id,
            Title = request.Title ?? $"Document {id}",
            Content = "Existing content...",
            MimeType = "text/plain",
            Tags = request.Tags?.Split(',').Select(t => t.Trim()).ToList() ?? new List<string>(),
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            ModifiedAt = DateTime.UtcNow,
            SizeBytes = 2048
        };

        return Ok(document);
    }

    /// <summary>
    /// Delete document (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> DeleteDocumentAsync(string id)
    {
        _logger.LogInformation("Delete document: {Id}", id);

        // TODO: Integrate with actual DocumentStore
        // Perform soft delete

        return NoContent();
    }

    /// <summary>
    /// Find similar documents
    /// </summary>
    [HttpGet("{id}/similar")]
    [ProducesResponseType(typeof(List<DocumentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<DocumentResponse>>> GetSimilarDocumentsAsync(
        string id,
        [FromQuery] int topK = 5)
    {
        _logger.LogInformation("Get similar documents for: {Id}, TopK: {TopK}", id, topK);

        // TODO: Integrate with actual EmbeddingStore
        var similarDocuments = new List<DocumentResponse>
        {
            new DocumentResponse
            {
                Id = "doc_similar_1",
                Title = "Similar Document 1",
                Content = "Content similar to the original...",
                MimeType = "text/plain",
                Tags = new List<string> { "related" },
                CreatedAt = DateTime.UtcNow.AddDays(-4),
                ModifiedAt = DateTime.UtcNow.AddDays(-1),
                SizeBytes = 1500
            }
        };

        return Ok(similarDocuments);
    }
}
