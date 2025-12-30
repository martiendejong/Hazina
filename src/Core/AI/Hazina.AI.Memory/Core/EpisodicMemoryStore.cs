namespace Hazina.AI.Memory.Core;

/// <summary>
/// Store for episodic memories - timestamped events, tool outcomes, and agent summaries.
/// Enables temporal reasoning and learning from past experiences.
/// </summary>
public class EpisodicMemoryStore
{
    private readonly List<Episode> _episodes = new();
    private readonly int _maxEpisodes;

    public IReadOnlyList<Episode> Episodes => _episodes.AsReadOnly();

    public EpisodicMemoryStore(int maxEpisodes = 1000)
    {
        _maxEpisodes = maxEpisodes;
    }

    /// <summary>
    /// Add an episode to the store
    /// </summary>
    public void AddEpisode(Episode episode)
    {
        _episodes.Add(episode);

        if (_episodes.Count > _maxEpisodes)
        {
            _episodes.RemoveAt(0);
        }
    }

    /// <summary>
    /// Record a tool execution
    /// </summary>
    public void RecordToolExecution(string toolName, Dictionary<string, object> arguments, string result, bool success)
    {
        var episode = new Episode
        {
            EpisodeId = Guid.NewGuid().ToString(),
            Type = EpisodeType.ToolExecution,
            Description = $"Executed {toolName}",
            Timestamp = DateTime.UtcNow,
            Metadata = new Dictionary<string, object>
            {
                ["toolName"] = toolName,
                ["arguments"] = arguments,
                ["result"] = result,
                ["success"] = success
            }
        };

        AddEpisode(episode);
    }

    /// <summary>
    /// Record a decision
    /// </summary>
    public void RecordDecision(string decision, string reasoning, Dictionary<string, object>? context = null)
    {
        var episode = new Episode
        {
            EpisodeId = Guid.NewGuid().ToString(),
            Type = EpisodeType.Decision,
            Description = decision,
            Timestamp = DateTime.UtcNow,
            Metadata = new Dictionary<string, object>
            {
                ["reasoning"] = reasoning,
                ["context"] = context ?? new Dictionary<string, object>()
            }
        };

        AddEpisode(episode);
    }

    /// <summary>
    /// Record a task completion summary
    /// </summary>
    public void RecordTaskSummary(string taskName, string summary, bool success, TimeSpan duration)
    {
        var episode = new Episode
        {
            EpisodeId = Guid.NewGuid().ToString(),
            Type = EpisodeType.TaskSummary,
            Description = summary,
            Timestamp = DateTime.UtcNow,
            Metadata = new Dictionary<string, object>
            {
                ["taskName"] = taskName,
                ["success"] = success,
                ["durationMs"] = duration.TotalMilliseconds
            }
        };

        AddEpisode(episode);
    }

    /// <summary>
    /// Get episodes within a time range
    /// </summary>
    public List<Episode> GetEpisodesInRange(DateTime start, DateTime end)
    {
        return _episodes
            .Where(e => e.Timestamp >= start && e.Timestamp <= end)
            .OrderBy(e => e.Timestamp)
            .ToList();
    }

    /// <summary>
    /// Get episodes of a specific type
    /// </summary>
    public List<Episode> GetEpisodesByType(EpisodeType type)
    {
        return _episodes.Where(e => e.Type == type).ToList();
    }

    /// <summary>
    /// Get recent episodes
    /// </summary>
    public List<Episode> GetRecentEpisodes(int count)
    {
        return _episodes
            .OrderByDescending(e => e.Timestamp)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Search episodes by description
    /// </summary>
    public List<Episode> SearchEpisodes(string query)
    {
        return _episodes
            .Where(e => e.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.Timestamp)
            .ToList();
    }

    /// <summary>
    /// Get summary statistics
    /// </summary>
    public EpisodeStatistics GetStatistics()
    {
        return new EpisodeStatistics
        {
            TotalEpisodes = _episodes.Count,
            ByType = _episodes.GroupBy(e => e.Type).ToDictionary(g => g.Key, g => g.Count()),
            EarliestTimestamp = _episodes.Count > 0 ? _episodes.Min(e => e.Timestamp) : (DateTime?)null,
            LatestTimestamp = _episodes.Count > 0 ? _episodes.Max(e => e.Timestamp) : (DateTime?)null
        };
    }

    /// <summary>
    /// Clear all episodes
    /// </summary>
    public void Clear()
    {
        _episodes.Clear();
    }
}

/// <summary>
/// Individual episode in episodic memory
/// </summary>
public class Episode
{
    public required string EpisodeId { get; init; }
    public required EpisodeType Type { get; init; }
    public required string Description { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Type of episodic memory
/// </summary>
public enum EpisodeType
{
    ToolExecution,
    Decision,
    TaskSummary,
    Observation,
    Error,
    Success
}

/// <summary>
/// Statistics about episodic memory
/// </summary>
public class EpisodeStatistics
{
    public int TotalEpisodes { get; init; }
    public Dictionary<EpisodeType, int> ByType { get; init; } = new();
    public DateTime? EarliestTimestamp { get; init; }
    public DateTime? LatestTimestamp { get; init; }
}
