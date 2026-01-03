using System;

/// <summary>
/// Configuration options for composite document scoring.
/// Can be loaded from appsettings.json.
/// </summary>
public class CompositeScoringConfig
{
    /// <summary>
    /// Section name in configuration file.
    /// </summary>
    public const string SectionName = "CompositeScoring";

    /// <summary>
    /// Whether composite scoring is enabled.
    /// Default: false (backwards compatible)
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Whether to use LLM-based tag scoring.
    /// If false, uses NoOpTagScoringService (neutral scores).
    /// Default: true
    /// </summary>
    public bool UseLLMTagScoring { get; set; } = true;

    /// <summary>
    /// Maximum age of cached tag scores in hours.
    /// Older scores will be recomputed.
    /// Default: 24 hours
    /// </summary>
    public int TagScoreCacheHours { get; set; } = 24;

    /// <summary>
    /// Weight for cosine similarity score (0.0 to 1.0).
    /// Default: 0.5
    /// </summary>
    public double CosineSimilarityWeight { get; set; } = 0.5;

    /// <summary>
    /// Weight for tag-based relevance score (0.0 to 1.0).
    /// Default: 0.3
    /// </summary>
    public double TagScoreWeight { get; set; } = 0.3;

    /// <summary>
    /// Weight for recency boost (0.0 to 1.0).
    /// Default: 0.1
    /// </summary>
    public double RecencyWeight { get; set; } = 0.1;

    /// <summary>
    /// Weight for position boost (0.0 to 1.0).
    /// Default: 0.1
    /// </summary>
    public double PositionWeight { get; set; } = 0.1;

    /// <summary>
    /// Number of days until recency score halves.
    /// Default: 30 days
    /// </summary>
    public double RecencyHalfLifeDays { get; set; } = 30.0;

    /// <summary>
    /// Tag aggregation method: Maximum, Average, or Sum.
    /// Default: Maximum
    /// </summary>
    public string TagAggregation { get; set; } = "Maximum";

    /// <summary>
    /// Minimum composite score threshold.
    /// Documents below this are filtered out.
    /// Default: 0.0 (no filtering)
    /// </summary>
    public double MinimumScore { get; set; } = 0.0;

    /// <summary>
    /// Convert configuration to ScoringOptions.
    /// </summary>
    public ScoringOptions ToScoringOptions()
    {
        var aggregation = TagAggregation?.ToLowerInvariant() switch
        {
            "average" => TagAggregationMethod.Average,
            "sum" => TagAggregationMethod.Sum,
            _ => TagAggregationMethod.Maximum
        };

        return new ScoringOptions
        {
            CosineSimilarityWeight = CosineSimilarityWeight,
            TagScoreWeight = TagScoreWeight,
            RecencyWeight = RecencyWeight,
            PositionWeight = PositionWeight,
            RecencyHalfLifeDays = RecencyHalfLifeDays,
            TagAggregation = aggregation,
            MinimumScore = MinimumScore
        };
    }

    /// <summary>
    /// Get cache age as TimeSpan.
    /// </summary>
    public TimeSpan GetCacheAge() => TimeSpan.FromHours(TagScoreCacheHours);
}
