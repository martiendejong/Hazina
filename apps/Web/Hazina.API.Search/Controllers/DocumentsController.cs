using Hazina.API.Search.Models;
using Hazina.API.Search.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hazina.API.Search.Controllers;

/// <summary>
/// Controller for managing documents in RAG stores
/// </summary>
[ApiController]
[Route("api/v1/stores/{storeId}/documents")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly RAGStoreManager _storeManager;
    private readonly ILogger<DocumentsController> _logger;
    private readonly IConfiguration _configuration;

    public DocumentsController(
        RAGStoreManager storeManager,
        ILogger<DocumentsController> logger,
        IConfiguration configuration)
    {
        _storeManager = storeManager;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Upload a document (txt, docx, pdf, image)
    /// </summary>
    [HttpPost("upload")]
    [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<DocumentResponse>> UploadDocument(
        Guid storeId,
        [FromForm] UploadDocumentRequest request)
    {
        try
        {
            // Verify store exists
            var store = await _storeManager.GetStoreAsync(storeId);
            if (store == null)
            {
                return NotFound(new { error = $"RAG store {storeId} not found" });
            }

            if (request.File == null || request.File.Length == 0)
            {
                return BadRequest(new { error = "No file provided" });
            }

            // Check file size
            var maxSizeMB = _configuration.GetValue<int>("Hazina:MaxUploadSizeMB", 100);
            var maxSizeBytes = maxSizeMB * 1024 * 1024;
            if (request.File.Length > maxSizeBytes)
            {
                return StatusCode(StatusCodes.Status413PayloadTooLarge,
                    new { error = $"File size exceeds maximum of {maxSizeMB}MB" });
            }

            // Upload and process the document
            using var stream = request.File.OpenReadStream();
            var documentId = await _storeManager.AddDocumentAsync(
                storeId,
                stream,
                request.File.FileName,
                request.File.ContentType);

            var response = new DocumentResponse
            {
                DocumentId = documentId,
                RAGStoreId = storeId,
                Filename = request.File.FileName,
                MimeType = request.File.ContentType,
                SizeBytes = request.File.Length,
                UploadedAt = DateTime.UtcNow,
                Status = "processed"
            };

            _logger.LogInformation("Uploaded document {DocumentId} to store {StoreId}",
                documentId, storeId);

            return CreatedAtAction(
                nameof(GetDocument),
                new { storeId, documentId },
                response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document to store {StoreId}", storeId);
            return BadRequest(new { error = "Document upload failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Add plain text as a document
    /// </summary>
    [HttpPost("text")]
    [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentResponse>> AddTextDocument(
        Guid storeId,
        [FromBody] AddTextDocumentRequest request)
    {
        try
        {
            // Verify store exists
            var store = await _storeManager.GetStoreAsync(storeId);
            if (store == null)
            {
                return NotFound(new { error = $"RAG store {storeId} not found" });
            }

            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return BadRequest(new { error = "Text cannot be empty" });
            }

            var documentId = await _storeManager.AddTextAsync(
                storeId,
                request.Text,
                request.Metadata);

            var response = new DocumentResponse
            {
                DocumentId = documentId,
                RAGStoreId = storeId,
                Filename = request.Metadata?.GetValueOrDefault("filename")?.ToString() ?? "text-document",
                MimeType = "text/plain",
                SizeBytes = System.Text.Encoding.UTF8.GetByteCount(request.Text),
                UploadedAt = DateTime.UtcNow,
                Status = "processed",
                Metadata = request.Metadata
            };

            _logger.LogInformation("Added text document {DocumentId} to store {StoreId}",
                documentId, storeId);

            return CreatedAtAction(
                nameof(GetDocument),
                new { storeId, documentId },
                response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding text document to store {StoreId}", storeId);
            return BadRequest(new { error = "Text document add failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Get document by ID
    /// </summary>
    [HttpGet("{*documentId}")]
    [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentResponse>> GetDocument(
        Guid storeId,
        string documentId)
    {
        try
        {
            // Verify store exists
            var store = await _storeManager.GetStoreAsync(storeId);
            if (store == null)
            {
                return NotFound(new { error = $"RAG store {storeId} not found" });
            }

            var document = await _storeManager.GetDocumentAsync(storeId, documentId);
            if (document == null)
            {
                return NotFound(new { error = $"Document {documentId} not found" });
            }

            return Ok(new DocumentResponse
            {
                DocumentId = document.DocumentId,
                RAGStoreId = storeId,
                Filename = document.Filename,
                MimeType = document.MimeType,
                Status = "available",
                Metadata = document.Metadata.ToDictionary(k => k.Key, v => (object)v.Value)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document {DocumentId} from store {StoreId}",
                documentId, storeId);
            return NotFound(new { error = "Document not found" });
        }
    }

    /// <summary>
    /// List all documents in a RAG store
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<DocumentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<DocumentResponse>>> ListDocuments(
        Guid storeId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            // Verify store exists
            var store = await _storeManager.GetStoreAsync(storeId);
            if (store == null)
            {
                return NotFound(new { error = $"RAG store {storeId} not found" });
            }

            var skip = (page - 1) * pageSize;
            var documents = await _storeManager.ListDocumentsAsync(storeId, skip, pageSize);
            var totalCount = await _storeManager.GetDocumentCountAsync(storeId);

            var response = new PagedResponse<DocumentResponse>
            {
                Items = documents.Select(d => new DocumentResponse
                {
                    DocumentId = d.DocumentId,
                    RAGStoreId = storeId,
                    Filename = d.Filename,
                    MimeType = d.MimeType,
                    Status = "available",
                    Metadata = d.Metadata.ToDictionary(k => k.Key, v => (object)v.Value)
                }).ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };

            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"RAG store {storeId} not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing documents in store {StoreId}", storeId);
            return BadRequest(new { error = "Failed to list documents" });
        }
    }

    /// <summary>
    /// Delete a document from a RAG store
    /// </summary>
    [HttpDelete("{*documentId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteDocument(
        Guid storeId,
        string documentId)
    {
        try
        {
            // Verify store exists
            var store = await _storeManager.GetStoreAsync(storeId);
            if (store == null)
            {
                return NotFound(new { error = $"RAG store {storeId} not found" });
            }

            var instance = await _storeManager.GetStoreInstanceAsync(storeId);
            var removed = await instance.DocumentStore.Remove(documentId);

            if (!removed)
            {
                return NotFound(new { error = $"Document {documentId} not found" });
            }

            _logger.LogInformation("Deleted document {DocumentId} from store {StoreId}",
                documentId, storeId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document {DocumentId} from store {StoreId}",
                documentId, storeId);
            return NotFound(new { error = "Document not found" });
        }
    }
}
