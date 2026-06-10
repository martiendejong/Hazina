using Hazina.API.DocumentStore.Data;
using Hazina.API.DocumentStore.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hazina.API.DocumentStore.Services;

/// <summary>
/// Background service for processing OCR queue
/// </summary>
public class OCRQueueService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OCRQueueService> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _retryDelay = TimeSpan.FromMinutes(2);

    public OCRQueueService(
        IServiceProvider serviceProvider,
        ILogger<OCRQueueService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OCR Queue Service starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessQueueAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing OCR queue");
            }

            // Wait before next poll
            await Task.Delay(_pollInterval, stoppingToken);
        }

        _logger.LogInformation("OCR Queue Service stopping");
    }

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DocumentStoreDbContext>();
        var ocrService = scope.ServiceProvider.GetRequiredService<OCRService>();
        var storeManager = scope.ServiceProvider.GetRequiredService<RAGStoreManager>();

        // Get next pending item (highest priority, oldest first)
        var queueItem = await dbContext.OCRQueues
            .Where(q => q.Status == OCRQueueStatus.Pending)
            .OrderByDescending(q => q.Priority)
            .ThenBy(q => q.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (queueItem == null)
        {
            // Check for failed items that can be retried
            var failedItem = await dbContext.OCRQueues
                .Where(q => q.Status == OCRQueueStatus.Failed &&
                           q.RetryCount < q.MaxRetries &&
                           q.ProcessedAt.HasValue &&
                           q.ProcessedAt.Value.Add(_retryDelay) < DateTime.UtcNow)
                .OrderByDescending(q => q.Priority)
                .ThenBy(q => q.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (failedItem != null)
            {
                queueItem = failedItem;
                queueItem.Status = OCRQueueStatus.Pending;
                queueItem.RetryCount++;
                queueItem.ErrorMessage = null;
                await dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Retrying failed OCR job {JobId} (attempt {Attempt}/{MaxAttempts})",
                    queueItem.Id, queueItem.RetryCount + 1, queueItem.MaxRetries);
            }
        }

        if (queueItem == null)
        {
            // No work to do
            return;
        }

        // Mark as processing
        queueItem.Status = OCRQueueStatus.Processing;
        queueItem.ProcessedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Processing OCR job {JobId} for document {DocumentId}",
            queueItem.Id, queueItem.DocumentId);

        try
        {
            // Get the document from store
            var document = await storeManager.GetDocumentAsync(queueItem.RAGStoreId, queueItem.DocumentId.ToString());
            if (document == null)
            {
                throw new Exception($"Document {queueItem.DocumentId} not found in store {queueItem.RAGStoreId}");
            }

            // Check if it's an image
            if (!IsImageMimeType(document.MimeType))
            {
                throw new Exception($"Document is not an image (MIME: {document.MimeType})");
            }

            // Get document stream (this would need to be implemented in RAGStoreManager)
            // For now, we'll assume we can get the file path from metadata
            var filePath = GetDocumentFilePath(document);
            if (!File.Exists(filePath))
            {
                throw new Exception($"Document file not found: {filePath}");
            }

            // Process with OCR
            using var stream = File.OpenRead(filePath);
            var language = queueItem.Language ?? "eng";
            var result = await ocrService.ProcessImageAsync(stream, language);

            if (!result.Success)
            {
                throw new Exception(result.ErrorMessage ?? "OCR processing failed");
            }

            // Update queue item with results
            queueItem.Status = OCRQueueStatus.Completed;
            queueItem.CompletedAt = DateTime.UtcNow;
            queueItem.ExtractedText = result.ExtractedText;
            queueItem.ConfidenceScore = result.ConfidenceScore;
            queueItem.ExtractedMetadata = result.Metadata;
            queueItem.ErrorMessage = null;

            // Update document metadata in store
            // This would update the document with OCR results
            await UpdateDocumentWithOCRResults(storeManager, queueItem, result);

            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Completed OCR job {JobId} with confidence {Confidence:F2}",
                queueItem.Id, result.ConfidenceScore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OCR job {JobId} failed", queueItem.Id);

            queueItem.Status = queueItem.RetryCount >= queueItem.MaxRetries
                ? OCRQueueStatus.Failed
                : OCRQueueStatus.Pending;
            queueItem.ErrorMessage = ex.Message;
            queueItem.ProcessedAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private bool IsImageMimeType(string mimeType)
    {
        return mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    private string GetDocumentFilePath(DocumentInfo document)
    {
        // This is a simplified implementation
        // In a real scenario, you'd get the actual file path from the document store
        // For now, we'll construct it from metadata
        if (document.Metadata.TryGetValue("filePath", out var path))
        {
            return path;
        }

        // Fallback: construct from document ID
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HazinaDocumentStore",
            document.StoreId.ToString(),
            document.DocumentId);
    }

    private async Task UpdateDocumentWithOCRResults(
        RAGStoreManager storeManager,
        OCRQueue queueItem,
        OCRResult result)
    {
        // This would update the document's metadata with OCR results
        // Implementation depends on how RAGStoreManager handles metadata updates
        _logger.LogInformation(
            "Updating document {DocumentId} with OCR results",
            queueItem.DocumentId);

        // For now, we'll just log this
        // In a real implementation, you'd call:
        // await storeManager.UpdateDocumentMetadataAsync(queueItem.RAGStoreId, queueItem.DocumentId, metadata);
    }
}
