using System;
using System.Collections.Generic;

/// <summary>
/// Explains why a document received its search ranking score.
/// Provides transparency into the scoring algorithm for debugging and user trust.
/// </summary>
public class SearchResultExplanation
{
    /// <summary>
    /// Human-readable summary of why this result ranked where it did.
    /// </summary>
    public string Summary { get; set; } = "";

    /// <summary>
    /// Detailed breakdown of each scoring component.
    /// </summary>
    public ScoreBreakdownDetail Breakdown { get; set; } = new();

    /// <summary>
    /// Factors that boosted this result's score.
    /// </summary>
    public List<string> Boosts { get; set; } = new();

    /// <summary>
    /// Factors that penalized this result's score.
    /// </summary>
    public List<string> Penalties { get; set; } = new();

    /// <summary>
    /// Generate a human-readable summary from the breakdown.
    /// </summary>
    public static string GenerateSummary(ScoreBreakdownDetail breakdown)
    {
        var factors = new List<string>();

        if (breakdown.TagScore.Contribution > 0.2)
        {
            var tags = string.Join(", ", breakdown.TagScore.MatchedTags);
            factors.Add($"strong tag match ({tags})");
        }

        if (breakdown.Similarity.Score > 0.8)
        {
            factors.Add("high semantic similarity");
        }
        else if (breakdown.Similarity.Score > 0.6)
        {
            factors.Add("moderate semantic similarity");
        }

        if (breakdown.Recency.Score > 0.8)
        {
            factors.Add($"recent document ({breakdown.Recency.AgeDescription})");
        }

        if (breakdown.Position.Score > 0.8)
        {
            factors.Add("high initial ranking");
        }

        if (factors.Count == 0)
        {
            return "Moderate relevance across multiple factors";
        }

        return $"Ranked due to {string.Join(" and ", factors)}";
    }
}

/// <summary>
/// Detailed breakdown of scoring components.
/// </summary>
public class ScoreBreakdownDetail
{
    public SimilarityDetail Similarity { get; set; } = new();
    public TagScoreDetail TagScore { get; set; } = new();
    public RecencyDetail Recency { get; set; } = new();
    public PositionDetail Position { get; set; } = new();
}

/// <summary>
/// Similarity score details.
/// </summary>
public class SimilarityDetail
{
    public double Score { get; set; }
    public double Weight { get; set; }
    public double Contribution { get; set; }
    public string Method { get; set; } = "cosine"; // cosine, token_overlap, hybrid
}

/// <summary>
/// Tag score details.
/// </summary>
public class TagScoreDetail
{
    public double Score { get; set; }
    public double Weight { get; set; }
    public double Contribution { get; set; }
    public List<string> MatchedTags { get; set; } = new();
    public Dictionary<string, double> TagScores { get; set; } = new();
    public string AggregationMethod { get; set; } = "maximum";
}

/// <summary>
/// Recency score details.
/// </summary>
public class RecencyDetail
{
    public double Score { get; set; }
    public double Weight { get; set; }
    public double Contribution { get; set; }
    public DateTime? DocumentDate { get; set; }
    public string AgeDescription { get; set; } = "";
    public double HalfLifeDays { get; set; }
}

/// <summary>
/// Position score details.
/// </summary>
public class PositionDetail
{
    public double Score { get; set; }
    public double Weight { get; set; }
    public double Contribution { get; set; }
    public int OriginalPosition { get; set; }
    public int TotalResults { get; set; }
}
