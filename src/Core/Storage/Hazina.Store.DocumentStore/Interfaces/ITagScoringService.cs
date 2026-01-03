using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Service for scoring tags based on query relevance.
/// Can use LLM or rule-based implementations.
/// </summary>
public interface ITagScoringService
{
    /// <summary>
    /// Score a list of tags based on their relevance to a query/instruction.
    /// Returns a TagRelevanceIndex with scores for each tag.
    /// </summary>
    /// <param name="tags">List of tags to score</param>
    /// <param name="queryContext">The query or instruction to score against</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Tag relevance index with scores</returns>
    Task<TagRelevanceIndex> ScoreTagsAsync(
        IEnumerable<string> tags,
        string queryContext,
        CancellationToken ct = default);

    /// <summary>
    /// Get or compute tag scores for a query context.
    /// Uses cached scores if available, otherwise computes new ones.
    /// </summary>
    /// <param name="tags">List of tags to score</param>
    /// <param name="queryContext">The query or instruction to score against</param>
    /// <param name="maxCacheAge">Maximum age of cached scores to use</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Tag relevance index with scores</returns>
    Task<TagRelevanceIndex> GetOrComputeScoresAsync(
        IEnumerable<string> tags,
        string queryContext,
        TimeSpan? maxCacheAge = null,
        CancellationToken ct = default);

    /// <summary>
    /// Check if scores are available for a query context.
    /// </summary>
    Task<bool> HasScoresForContextAsync(string queryContext, CancellationToken ct = default);
}
