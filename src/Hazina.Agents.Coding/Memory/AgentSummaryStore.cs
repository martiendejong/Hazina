namespace Hazina.Agents.Coding.Memory;

/// <summary>
/// Stores agent summaries only (not full logs) for memory continuity.
/// File-based storage in .hazina/agent-memory/ directory.
/// </summary>
public class AgentSummaryStore
{
    private readonly string _storePath;
    private const int MaxTokensPerSummary = 500;

    public AgentSummaryStore(string workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            throw new ArgumentNullException(nameof(workingDirectory));
        }

        _storePath = Path.Combine(workingDirectory, ".hazina", "agent-memory");

        if (!Directory.Exists(_storePath))
        {
            Directory.CreateDirectory(_storePath);
        }
    }

    /// <summary>
    /// Store iteration summary
    /// </summary>
    public async Task StoreSummaryAsync(string taskId, int iteration, string summary)
    {
        var filePath = GetSummaryFilePath(taskId);

        var entry = $@"Iteration {iteration}:
{TruncateSummary(summary, MaxTokensPerSummary)}

";

        await File.AppendAllTextAsync(filePath, entry);
    }

    /// <summary>
    /// Load all summaries for a task
    /// </summary>
    public async Task<string?> LoadSummariesAsync(string taskId)
    {
        var filePath = GetSummaryFilePath(taskId);

        if (!File.Exists(filePath))
        {
            return null;
        }

        return await File.ReadAllTextAsync(filePath);
    }

    /// <summary>
    /// Clear summaries for a task
    /// </summary>
    public void ClearSummaries(string taskId)
    {
        var filePath = GetSummaryFilePath(taskId);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    /// <summary>
    /// Get path for task summary file
    /// </summary>
    private string GetSummaryFilePath(string taskId)
    {
        var safeTaskId = SanitizeTaskId(taskId);
        return Path.Combine(_storePath, $"{safeTaskId}.txt");
    }

    /// <summary>
    /// Sanitize task ID for file name
    /// </summary>
    private static string SanitizeTaskId(string taskId)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(taskId.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());

        if (sanitized.Length > 50)
        {
            sanitized = sanitized.Substring(0, 50);
        }

        return sanitized;
    }

    /// <summary>
    /// Truncate summary to maximum tokens (approximate)
    /// </summary>
    private static string TruncateSummary(string summary, int maxTokens)
    {
        var estimatedChars = maxTokens * 4;

        if (summary.Length <= estimatedChars)
        {
            return summary;
        }

        return summary.Substring(0, estimatedChars) + "... [truncated]";
    }

    /// <summary>
    /// Generate task ID from description
    /// </summary>
    public static string GenerateTaskId(string description)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var descPrefix = new string(description.Take(20).ToArray());
        return $"{timestamp}_{descPrefix}";
    }
}
