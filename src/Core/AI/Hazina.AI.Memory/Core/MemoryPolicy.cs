namespace Hazina.AI.Memory.Core;

/// <summary>
/// Policy for managing memory promotion between layers.
/// Decides what moves from Working → Episodic → Semantic memory.
/// </summary>
public class MemoryPolicy
{
    private readonly MemoryPolicyOptions _options;

    public MemoryPolicy(MemoryPolicyOptions? options = null)
    {
        _options = options ?? new MemoryPolicyOptions();
    }

    /// <summary>
    /// Determine if a working memory item should be promoted to episodic memory
    /// </summary>
    public bool ShouldPromoteToEpisodic(MemoryItem item)
    {
        if (_options.PromoteAllToEpisodic)
            return true;

        if (item.Type == MemoryItemType.Decision)
            return true;

        if (item.Type == MemoryItemType.ToolResult && _options.PromoteToolResultsToEpisodic)
            return true;

        if (item.Priority.HasValue && item.Priority.Value >= _options.MinPriorityForEpisodicPromotion)
            return true;

        return false;
    }

    /// <summary>
    /// Determine if an episode should be promoted to semantic memory
    /// </summary>
    public bool ShouldPromoteToSemantic(Episode episode)
    {
        if (episode.Type == EpisodeType.TaskSummary)
            return _options.PromoteTaskSummariesToSemantic;

        if (episode.Type == EpisodeType.Success)
            return _options.PromoteSuccessesToSemantic;

        if (episode.Type == EpisodeType.Error && _options.PromoteErrorsToSemantic)
            return true;

        return false;
    }

    /// <summary>
    /// Determine if working memory should be consolidated
    /// </summary>
    public bool ShouldConsolidateWorkingMemory(WorkingMemory memory)
    {
        var utilization = (double)memory.CurrentTokenCount / memory.MaxTokens;
        return utilization >= _options.WorkingMemoryConsolidationThreshold;
    }

    /// <summary>
    /// Get priority for a memory item
    /// </summary>
    public int CalculatePriority(MemoryItemType type, Dictionary<string, object>? metadata = null)
    {
        return type switch
        {
            MemoryItemType.Instruction => 100,
            MemoryItemType.Decision => 80,
            MemoryItemType.ToolResult => 60,
            MemoryItemType.Observation => 40,
            MemoryItemType.Context => 20,
            _ => 0
        };
    }
}

/// <summary>
/// Configuration options for memory policy
/// </summary>
public class MemoryPolicyOptions
{
    /// <summary>
    /// Promote all working memory items to episodic (default: false)
    /// </summary>
    public bool PromoteAllToEpisodic { get; set; } = false;

    /// <summary>
    /// Promote tool results to episodic memory (default: true)
    /// </summary>
    public bool PromoteToolResultsToEpisodic { get; set; } = true;

    /// <summary>
    /// Promote task summaries to semantic memory (default: true)
    /// </summary>
    public bool PromoteTaskSummariesToSemantic { get; set; } = true;

    /// <summary>
    /// Promote successful outcomes to semantic memory (default: true)
    /// </summary>
    public bool PromoteSuccessesToSemantic { get; set; } = true;

    /// <summary>
    /// Promote errors to semantic memory for learning (default: false)
    /// </summary>
    public bool PromoteErrorsToSemantic { get; set; } = false;

    /// <summary>
    /// Minimum priority for promoting to episodic memory (default: 50)
    /// </summary>
    public int MinPriorityForEpisodicPromotion { get; set; } = 50;

    /// <summary>
    /// Threshold for consolidating working memory (0.0-1.0, default: 0.9)
    /// </summary>
    public double WorkingMemoryConsolidationThreshold { get; set; } = 0.9;
}

/// <summary>
/// Memory manager orchestrating all memory layers and policies
/// </summary>
public class MemoryManager
{
    private readonly WorkingMemory _workingMemory;
    private readonly EpisodicMemoryStore _episodicMemory;
    private readonly ISemanticMemoryStore? _semanticMemory;
    private readonly MemoryPolicy _policy;

    public WorkingMemory WorkingMemory => _workingMemory;
    public EpisodicMemoryStore EpisodicMemory => _episodicMemory;
    public ISemanticMemoryStore? SemanticMemory => _semanticMemory;

    public MemoryManager(
        WorkingMemory workingMemory,
        EpisodicMemoryStore episodicMemory,
        ISemanticMemoryStore? semanticMemory = null,
        MemoryPolicy? policy = null)
    {
        _workingMemory = workingMemory ?? throw new ArgumentNullException(nameof(workingMemory));
        _episodicMemory = episodicMemory ?? throw new ArgumentNullException(nameof(episodicMemory));
        _semanticMemory = semanticMemory;
        _policy = policy ?? new MemoryPolicy();
    }

    /// <summary>
    /// Add to working memory and apply promotion policies
    /// </summary>
    public void Remember(MemoryItem item)
    {
        _workingMemory.Add(item);

        if (_policy.ShouldPromoteToEpisodic(item))
        {
            var episode = new Episode
            {
                EpisodeId = item.ItemId,
                Type = MapToEpisodeType(item.Type),
                Description = item.Content,
                Timestamp = item.Timestamp,
                Metadata = item.Metadata
            };

            _episodicMemory.AddEpisode(episode);
        }
    }

    /// <summary>
    /// Consolidate working memory when threshold reached
    /// </summary>
    public void ConsolidateIfNeeded()
    {
        if (_policy.ShouldConsolidateWorkingMemory(_workingMemory))
        {
            var summary = _workingMemory.ConsolidateToText();
            _workingMemory.Clear();

            _workingMemory.AddText(
                $"[Consolidated Memory]\n{summary}",
                MemoryItemType.Context,
                priority: 100
            );
        }
    }

    private static EpisodeType MapToEpisodeType(MemoryItemType type)
    {
        return type switch
        {
            MemoryItemType.Decision => EpisodeType.Decision,
            MemoryItemType.ToolResult => EpisodeType.ToolExecution,
            MemoryItemType.Observation => EpisodeType.Observation,
            _ => EpisodeType.Observation
        };
    }
}
