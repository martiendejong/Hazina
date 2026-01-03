using System;

/// <summary>
/// Configuration options for composite document scoring.
/// Weights should sum to 1.0 for normalized results.
/// </summary>
public class ScoringOptions
{
    /// <summary>
    /// Weight for cosine similarity score (embedding-based).
    /// Default: 0.5 (50% weight)
    /// </summary>
    public double CosineSimilarityWeight { get; set; } = 0.5;

    /// <summary>
    /// Weight for tag-based relevance score.
    /// Default: 0.3 (30% weight)
    /// </summary>
    public double TagScoreWeight { get; set; } = 0.3;

    /// <summary>
    /// Weight for recency boost (newer documents score higher).
    /// Default: 0.1 (10% weight)
    /// </summary>
    public double RecencyWeight { get; set; } = 0.1;

    /// <summary>
    /// Weight for position/order boost (earlier results from initial retrieval).
    /// Default: 0.1 (10% weight)
    /// </summary>
    public double PositionWeight { get; set; } = 0.1;

    /// <summary>
    /// Number of days until recency score halves.
    /// Older documents decay exponentially.
    /// Default: 30 days
    /// </summary>
    public double RecencyHalfLifeDays { get; set; } = 30.0;

    /// <summary>
    /// Aggregation method for multiple tags.
    /// </summary>
    public TagAggregationMethod TagAggregation { get; set; } = TagAggregationMethod.Maximum;

    /// <summary>
    /// Minimum score threshold. Documents below this are filtered out.
    /// Default: 0.0 (no filtering)
    /// </summary>
    public double MinimumScore { get; set; } = 0.0;

    /// <summary>
    /// Default scoring options (balanced weights).
    /// </summary>
    public static ScoringOptions Default => new();

    /// <summary>
    /// Scoring options that prioritize embedding similarity.
    /// </summary>
    public static ScoringOptions EmbeddingFocused => new()
    {
        CosineSimilarityWeight = 0.7,
        TagScoreWeight = 0.2,
        RecencyWeight = 0.05,
        PositionWeight = 0.05
    };

    /// <summary>
    /// Scoring options that prioritize tag relevance.
    /// </summary>
    public static ScoringOptions TagFocused => new()
    {
        CosineSimilarityWeight = 0.3,
        TagScoreWeight = 0.5,
        RecencyWeight = 0.1,
        PositionWeight = 0.1
    };

    /// <summary>
    /// Scoring options for backwards compatibility (similarity only).
    /// </summary>
    public static ScoringOptions SimilarityOnly => new()
    {
        CosineSimilarityWeight = 1.0,
        TagScoreWeight = 0.0,
        RecencyWeight = 0.0,
        PositionWeight = 0.0
    };
}

/// <summary>
/// Method for aggregating scores from multiple tags.
/// </summary>
public enum TagAggregationMethod
{
    /// <summary>
    /// Use the highest score among all tags (optimistic).
    /// </summary>
    Maximum,

    /// <summary>
    /// Use the average score of all tags.
    /// </summary>
    Average,

    /// <summary>
    /// Use the sum of all scores (capped at 1.0).
    /// </summary>
    Sum
}
