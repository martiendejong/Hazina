namespace Hazina.Tools.Migration;

/// <summary>
/// Represents the progress of a migration operation.
/// </summary>
public class MigrationProgress
{
    public int TotalItems { get; set; }
    public int ProcessedItems { get; set; }
    public int SuccessfulItems { get; set; }
    public int FailedItems { get; set; }
    public int SkippedItems { get; set; }

    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    public TimeSpan Duration => (EndTime ?? DateTime.UtcNow) - StartTime;
    public double PercentComplete => TotalItems > 0 ? (ProcessedItems * 100.0 / TotalItems) : 0;
    public bool IsComplete => ProcessedItems >= TotalItems;

    public string Status
    {
        get
        {
            if (!IsComplete) return "In Progress";
            if (FailedItems > 0) return "Completed with Errors";
            if (Warnings.Count > 0) return "Completed with Warnings";
            return "Completed Successfully";
        }
    }

    public override string ToString()
    {
        return $"{Status}: {ProcessedItems}/{TotalItems} items ({PercentComplete:F1}%), " +
               $"{SuccessfulItems} successful, {FailedItems} failed, {SkippedItems} skipped";
    }
}

/// <summary>
/// Event args for migration progress updates.
/// </summary>
public class MigrationProgressEventArgs : EventArgs
{
    public MigrationProgress Progress { get; }
    public string? CurrentItem { get; }
    public string? Message { get; }

    public MigrationProgressEventArgs(MigrationProgress progress, string? currentItem = null, string? message = null)
    {
        Progress = progress;
        CurrentItem = currentItem;
        Message = message;
    }
}
