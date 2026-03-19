using System.Diagnostics.CodeAnalysis;

namespace Hazina.AI.Learning;

/// <summary>
/// Extracts patterns from outcomes and updates system behavior
/// </summary>
/// <remarks>
/// This is an experimental API and may change in future versions without notice.
/// </remarks>
[Experimental("HAZ004")]
public interface IPatternExtractor
{
    /// <summary>
    /// Extract patterns from a set of outcomes
    /// </summary>
    /// <param name="outcomes">Outcome data</param>
    /// <returns>Discovered patterns</returns>
    Task<List<Pattern>> Extract(List<Outcome> outcomes);

    /// <summary>
    /// Check if a pattern should be promoted to permanent memory
    /// </summary>
    /// <param name="pattern">Pattern to evaluate</param>
    /// <returns>True if pattern is validated and should be promoted</returns>
    Task<bool> ShouldPromoteToMemory(Pattern pattern);

    /// <summary>
    /// Validate a pattern against new outcomes
    /// </summary>
    /// <param name="pattern">Pattern to validate</param>
    /// <param name="newOutcomes">New outcomes to test against</param>
    /// <returns>Updated confidence score</returns>
    Task<ConfidenceScore> ValidatePattern(Pattern pattern, List<Outcome> newOutcomes);

    /// <summary>
    /// Get all active patterns
    /// </summary>
    /// <returns>List of patterns</returns>
    Task<List<Pattern>> GetActivePatterns();
}

/// <summary>
/// Discovered pattern from outcomes
/// </summary>
public class Pattern
{
    /// <summary>Pattern unique identifier</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Pattern description</summary>
    public required string Description { get; set; }

    /// <summary>Condition that triggers pattern</summary>
    public required string Condition { get; set; }

    /// <summary>Recommended action</summary>
    public required string Action { get; set; }

    /// <summary>Confidence score (0-1)</summary>
    public double Confidence { get; set; }

    /// <summary>Number of times pattern was observed</summary>
    public int Occurrences { get; set; }

    /// <summary>Success rate when pattern followed</summary>
    public double SuccessRate { get; set; }

    /// <summary>When pattern was discovered</summary>
    public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;

    /// <summary>Last validation timestamp</summary>
    public DateTime LastValidatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Generic outcome data
/// </summary>
public class Outcome
{
    /// <summary>Outcome identifier</summary>
    public required string Id { get; set; }

    /// <summary>Type of operation</summary>
    public required string Type { get; set; }

    /// <summary>Was operation successful?</summary>
    public bool Success { get; set; }

    /// <summary>Factors that influenced outcome</summary>
    public Dictionary<string, object> Factors { get; set; } = [];

    /// <summary>Result data</summary>
    public Dictionary<string, object> Result { get; set; } = [];

    /// <summary>When outcome occurred</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Confidence score for pattern
/// </summary>
public class ConfidenceScore
{
    /// <summary>Overall confidence (0-1)</summary>
    public double Overall { get; set; }

    /// <summary>Statistical significance</summary>
    public double StatisticalSignificance { get; set; }

    /// <summary>Sample size used for validation</summary>
    public int SampleSize { get; set; }
}
