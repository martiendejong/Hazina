namespace Hazina.Store.EmbeddingStore;

/// <summary>
/// Progress information for batch indexing operations.
/// </summary>
public class BatchIndexProgress
{
    /// <summary>
    /// Number of items processed so far.
    /// </summary>
    public int ProcessedCount { get; set; }

    /// <summary>
    /// Total number of items to process.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Number of items successfully indexed.
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Number of items that failed.
    /// </summary>
    public int FailureCount { get; set; }

    /// <summary>
    /// Current operation being performed.
    /// </summary>
    public string CurrentOperation { get; set; } = "";

    /// <summary>
    /// Percentage complete (0-100).
    /// </summary>
    public double PercentComplete => TotalCount > 0 ? (ProcessedCount * 100.0) / TotalCount : 0;

    /// <summary>
    /// Optional error message for the last failed item.
    /// </summary>
    public string? LastError { get; set; }

    public override string ToString()
    {
        return $"{ProcessedCount}/{TotalCount} ({PercentComplete:F1}%) - {SuccessCount} succeeded, {FailureCount} failed";
    }
}
