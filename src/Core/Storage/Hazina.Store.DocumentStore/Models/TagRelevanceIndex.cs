using System;
using System.Collections.Generic;

/// <summary>
/// Stores tag relevance scores for a specific query context.
/// Used for query-adaptive document ranking.
/// </summary>
public class TagRelevanceIndex
{
    /// <summary>
    /// Unique identifier for this index.
    /// </summary>
    public string IndexId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The query or instruction that was used to generate these scores.
    /// </summary>
    public string QueryContext { get; set; } = "";

    /// <summary>
    /// MD5 checksum of the query context for cache lookup.
    /// </summary>
    public string ContextChecksum { get; set; } = "";

    /// <summary>
    /// When this index was created.
    /// </summary>
    public DateTime Created { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this index was last updated.
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Tag name → Relevance score (0.0 to 1.0).
    /// 1.0 = Highly relevant to the query.
    /// 0.5 = Neutral/uncertain relevance.
    /// 0.0 = Not relevant at all.
    /// </summary>
    public Dictionary<string, double> TagScores { get; set; } = new();

    /// <summary>
    /// Get the relevance score for a specific tag.
    /// Returns 0.5 (neutral) if tag not found.
    /// </summary>
    public double GetScore(string tag)
    {
        if (TagScores.TryGetValue(tag, out var score))
        {
            return score;
        }
        return 0.5; // Neutral default
    }

    /// <summary>
    /// Calculate aggregate score for a list of tags.
    /// Uses the maximum score among all tags (optimistic matching).
    /// </summary>
    public double GetAggregateScore(IEnumerable<string> tags)
    {
        double maxScore = 0.0;
        foreach (var tag in tags)
        {
            var score = GetScore(tag);
            if (score > maxScore)
            {
                maxScore = score;
            }
        }
        return maxScore;
    }

    /// <summary>
    /// Calculate weighted aggregate score for a list of tags.
    /// Uses average of all tag scores.
    /// </summary>
    public double GetAverageScore(IEnumerable<string> tags)
    {
        var tagList = new List<string>(tags);
        if (tagList.Count == 0) return 0.5;

        double total = 0.0;
        foreach (var tag in tagList)
        {
            total += GetScore(tag);
        }
        return total / tagList.Count;
    }
}
