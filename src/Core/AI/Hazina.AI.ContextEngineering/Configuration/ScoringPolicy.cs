using Hazina.AI.ContextEngineering.Fusion;
using System.ComponentModel.DataAnnotations;

namespace Hazina.AI.ContextEngineering.Configuration;

/// <summary>
/// Policy for configuring scoring and fusion behavior
/// </summary>
public class ScoringPolicy
{
    /// <summary>
    /// Fusion strategy to use
    /// </summary>
    public FusionStrategy Strategy { get; set; } = FusionStrategy.WeightedSum;

    /// <summary>
    /// Minimum score threshold for final results (0.0-1.0)
    /// Results below this threshold will be filtered out
    /// </summary>
    [Range(0.0, 1.0)]
    public double MinimumScore { get; set; } = 0.0;

    /// <summary>
    /// Whether to boost scores based on tag matches
    /// </summary>
    public bool UseTagBoost { get; set; } = true;

    /// <summary>
    /// Power factor for tag boost (higher = stronger boost)
    /// </summary>
    [Range(1.0, 3.0)]
    public double TagBoostPower { get; set; } = 1.2;

    /// <summary>
    /// Whether to apply recency boost (newer results ranked higher)
    /// </summary>
    public bool UseRecencyBoost { get; set; } = false;

    /// <summary>
    /// Decay factor for recency (0.0-1.0, higher = less decay)
    /// </summary>
    [Range(0.0, 1.0)]
    public double RecencyDecayFactor { get; set; } = 0.9;

    /// <summary>
    /// Maximum age in days for recency boost (results older than this get no boost)
    /// </summary>
    [Range(1, 365)]
    public int RecencyMaxAgeDays { get; set; } = 30;

    /// <summary>
    /// Whether to normalize scores to 0-1 range after fusion
    /// </summary>
    public bool NormalizeScores { get; set; } = true;

    /// <summary>
    /// Default scoring policy (weighted sum with minimal boosting)
    /// </summary>
    public static ScoringPolicy Default => new();

    /// <summary>
    /// RRF-based policy (good for combining incompatible scorers)
    /// </summary>
    public static ScoringPolicy ReciprocalRankFusion => new()
    {
        Strategy = FusionStrategy.ReciprocalRankFusion,
        UseTagBoost = false,
        UseRecencyBoost = false,
        NormalizeScores = true
    };

    /// <summary>
    /// Tag-focused policy (heavy emphasis on tag matching)
    /// </summary>
    public static ScoringPolicy TagFocused => new()
    {
        Strategy = FusionStrategy.WeightedSum,
        UseTagBoost = true,
        TagBoostPower = 2.0,
        UseRecencyBoost = false
    };

    /// <summary>
    /// Recency-focused policy (heavy emphasis on recent content)
    /// </summary>
    public static ScoringPolicy RecencyFocused => new()
    {
        Strategy = FusionStrategy.WeightedSum,
        UseTagBoost = false,
        UseRecencyBoost = true,
        RecencyDecayFactor = 0.95,
        RecencyMaxAgeDays = 7
    };

    /// <summary>
    /// Validate the policy configuration
    /// </summary>
    public List<string> Validate()
    {
        var errors = new List<string>();

        if (MinimumScore < 0 || MinimumScore > 1)
        {
            errors.Add("MinimumScore must be between 0.0 and 1.0");
        }

        if (UseTagBoost && (TagBoostPower < 1.0 || TagBoostPower > 3.0))
        {
            errors.Add("TagBoostPower must be between 1.0 and 3.0");
        }

        if (UseRecencyBoost && (RecencyDecayFactor < 0 || RecencyDecayFactor > 1))
        {
            errors.Add("RecencyDecayFactor must be between 0.0 and 1.0");
        }

        return errors;
    }
}
