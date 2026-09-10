using System.ComponentModel.DataAnnotations;

namespace Hazina.API.DocumentStore.Models;

/// <summary>
/// OCR processing queue status
/// </summary>
public enum OCRQueueStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}

/// <summary>
/// OCR queue entity for background processing
/// </summary>
public class OCRQueue
{
    public Guid Id { get; set; }

    [Required]
    public Guid DocumentId { get; set; }

    [Required]
    public Guid RAGStoreId { get; set; }

    [Required]
    public OCRQueueStatus Status { get; set; } = OCRQueueStatus.Pending;

    public int Priority { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ProcessedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? ErrorMessage { get; set; }

    public int RetryCount { get; set; } = 0;

    public int MaxRetries { get; set; } = 3;

    public double? ConfidenceScore { get; set; }

    public string? ExtractedText { get; set; }

    public string? OriginalFilename { get; set; }

    public string? Language { get; set; } = "eng";

    /// <summary>
    /// Extracted metadata (e.g., VAT amount, invoice number)
    /// </summary>
    public Dictionary<string, object>? ExtractedMetadata { get; set; }
}

/// <summary>
/// OCR queue response model
/// </summary>
public class OCRQueueResponse
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Guid RAGStoreId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; }
    public double? ConfidenceScore { get; set; }
    public string? OriginalFilename { get; set; }
    public string? Language { get; set; }
    public Dictionary<string, object>? ExtractedMetadata { get; set; }
}

/// <summary>
/// OCR queue statistics
/// </summary>
public class OCRQueueStats
{
    public int TotalPending { get; set; }
    public int TotalProcessing { get; set; }
    public int TotalCompleted { get; set; }
    public int TotalFailed { get; set; }
    public double AverageProcessingTimeSeconds { get; set; }
    public double AverageConfidenceScore { get; set; }
}

/// <summary>
/// Request to reprocess failed OCR
/// </summary>
public class ReprocessOCRRequest
{
    public string? Language { get; set; }
    public int Priority { get; set; } = 0;
}
