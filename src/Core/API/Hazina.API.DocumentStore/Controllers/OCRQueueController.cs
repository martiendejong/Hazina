using Hazina.API.DocumentStore.Data;
using Hazina.API.DocumentStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Hazina.API.DocumentStore.Controllers;

/// <summary>
/// Controller for managing OCR processing queue
/// </summary>
[ApiController]
[Route("api/v1/ocr-queue")]
[Authorize]
public class OCRQueueController : ControllerBase
{
    private readonly DocumentStoreDbContext _dbContext;
    private readonly ILogger<OCRQueueController> _logger;

    public OCRQueueController(
        DocumentStoreDbContext dbContext,
        ILogger<OCRQueueController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Get all OCR queue items
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<OCRQueueResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<OCRQueueResponse>>> GetQueue(
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _dbContext.OCRQueues.AsQueryable();

        // Filter by status
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<OCRQueueStatus>(status, true, out var statusEnum))
        {
            query = query.Where(q => q.Status == statusEnum);
        }

        // Search by filename
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(q => q.OriginalFilename != null && q.OriginalFilename.Contains(search));
        }

        // Order by priority (desc) then created date
        query = query.OrderByDescending(q => q.Priority)
                    .ThenByDescending(q => q.CreatedAt);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var response = new PagedResponse<OCRQueueResponse>
        {
            Items = items.Select(MapToResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        };

        return Ok(response);
    }

    /// <summary>
    /// Get OCR queue item by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(OCRQueueResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OCRQueueResponse>> GetQueueItem(Guid id)
    {
        var item = await _dbContext.OCRQueues.FindAsync(id);
        if (item == null)
        {
            return NotFound(new { error = $"OCR queue item {id} not found" });
        }

        return Ok(MapToResponse(item));
    }

    /// <summary>
    /// Get OCR queue status for a specific document
    /// </summary>
    [HttpGet("document/{documentId}")]
    [ProducesResponseType(typeof(OCRQueueResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OCRQueueResponse>> GetDocumentOCRStatus(Guid documentId)
    {
        var item = await _dbContext.OCRQueues
            .Where(q => q.DocumentId == documentId)
            .OrderByDescending(q => q.CreatedAt)
            .FirstOrDefaultAsync();

        if (item == null)
        {
            return NotFound(new { error = $"No OCR queue entry found for document {documentId}" });
        }

        return Ok(MapToResponse(item));
    }

    /// <summary>
    /// Get OCR queue statistics
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(OCRQueueStats), StatusCodes.Status200OK)]
    public async Task<ActionResult<OCRQueueStats>> GetStats()
    {
        var stats = new OCRQueueStats
        {
            TotalPending = await _dbContext.OCRQueues.CountAsync(q => q.Status == OCRQueueStatus.Pending),
            TotalProcessing = await _dbContext.OCRQueues.CountAsync(q => q.Status == OCRQueueStatus.Processing),
            TotalCompleted = await _dbContext.OCRQueues.CountAsync(q => q.Status == OCRQueueStatus.Completed),
            TotalFailed = await _dbContext.OCRQueues.CountAsync(q => q.Status == OCRQueueStatus.Failed)
        };

        // Calculate average processing time
        var completedItems = await _dbContext.OCRQueues
            .Where(q => q.Status == OCRQueueStatus.Completed &&
                       q.ProcessedAt.HasValue &&
                       q.CompletedAt.HasValue)
            .Select(q => new { q.ProcessedAt, q.CompletedAt })
            .ToListAsync();

        if (completedItems.Any())
        {
            var avgTime = completedItems
                .Select(i => (i.CompletedAt!.Value - i.ProcessedAt!.Value).TotalSeconds)
                .Average();
            stats.AverageProcessingTimeSeconds = avgTime;
        }

        // Calculate average confidence score
        var confidenceScores = await _dbContext.OCRQueues
            .Where(q => q.Status == OCRQueueStatus.Completed && q.ConfidenceScore.HasValue)
            .Select(q => q.ConfidenceScore!.Value)
            .ToListAsync();

        if (confidenceScores.Any())
        {
            stats.AverageConfidenceScore = confidenceScores.Average();
        }

        return Ok(stats);
    }

    /// <summary>
    /// Reprocess failed OCR for a document
    /// </summary>
    [HttpPost("document/{documentId}/reprocess")]
    [ProducesResponseType(typeof(OCRQueueResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OCRQueueResponse>> ReprocessDocument(
        Guid documentId,
        [FromBody] ReprocessOCRRequest? request = null)
    {
        // Find the latest OCR queue entry for this document
        var existingItem = await _dbContext.OCRQueues
            .Where(q => q.DocumentId == documentId)
            .OrderByDescending(q => q.CreatedAt)
            .FirstOrDefaultAsync();

        if (existingItem == null)
        {
            return NotFound(new { error = $"No OCR queue entry found for document {documentId}" });
        }

        // Check if already processing or pending
        if (existingItem.Status == OCRQueueStatus.Processing)
        {
            return BadRequest(new { error = "Document is currently being processed" });
        }

        if (existingItem.Status == OCRQueueStatus.Pending)
        {
            return BadRequest(new { error = "Document is already queued for processing" });
        }

        // Create new queue entry for reprocessing
        var newItem = new OCRQueue
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            RAGStoreId = existingItem.RAGStoreId,
            Status = OCRQueueStatus.Pending,
            Priority = request?.Priority ?? existingItem.Priority,
            CreatedAt = DateTime.UtcNow,
            Language = request?.Language ?? existingItem.Language ?? "eng",
            OriginalFilename = existingItem.OriginalFilename,
            MaxRetries = 3,
            RetryCount = 0
        };

        _dbContext.OCRQueues.Add(newItem);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Queued document {DocumentId} for OCR reprocessing", documentId);

        return Ok(MapToResponse(newItem));
    }

    /// <summary>
    /// Delete OCR queue item
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> DeleteQueueItem(Guid id)
    {
        var item = await _dbContext.OCRQueues.FindAsync(id);
        if (item == null)
        {
            return NotFound(new { error = $"OCR queue item {id} not found" });
        }

        // Don't allow deletion of items currently being processed
        if (item.Status == OCRQueueStatus.Processing)
        {
            return BadRequest(new { error = "Cannot delete item currently being processed" });
        }

        _dbContext.OCRQueues.Remove(item);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Deleted OCR queue item {ItemId}", id);

        return NoContent();
    }

    private static OCRQueueResponse MapToResponse(OCRQueue item)
    {
        return new OCRQueueResponse
        {
            Id = item.Id,
            DocumentId = item.DocumentId,
            RAGStoreId = item.RAGStoreId,
            Status = item.Status.ToString(),
            Priority = item.Priority,
            CreatedAt = item.CreatedAt,
            ProcessedAt = item.ProcessedAt,
            CompletedAt = item.CompletedAt,
            ErrorMessage = item.ErrorMessage,
            RetryCount = item.RetryCount,
            MaxRetries = item.MaxRetries,
            ConfidenceScore = item.ConfidenceScore,
            OriginalFilename = item.OriginalFilename,
            Language = item.Language,
            ExtractedMetadata = item.ExtractedMetadata
        };
    }
}
