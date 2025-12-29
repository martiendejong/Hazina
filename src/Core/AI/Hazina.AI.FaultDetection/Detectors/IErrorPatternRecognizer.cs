using Hazina.AI.FaultDetection.Core;

namespace Hazina.AI.FaultDetection.Detectors;

/// <summary>
/// Interface for recognizing error patterns in LLM responses
/// </summary>
public interface IErrorPatternRecognizer
{
    /// <summary>
    /// Recognize error patterns in a response
    /// </summary>
    Task<ErrorPatternResult> RecognizeAsync(
        string response,
        ValidationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Learn from a new error pattern
    /// </summary>
    Task LearnPatternAsync(ErrorPattern pattern);

    /// <summary>
    /// Get all known error patterns
    /// </summary>
    IEnumerable<ErrorPattern> GetKnownPatterns();
}

/// <summary>
/// Result of error pattern recognition
/// </summary>
public class ErrorPatternResult
{
    public bool ContainsErrorPattern { get; set; }
    public List<MatchedErrorPattern> MatchedPatterns { get; set; } = new();
}

/// <summary>
/// Matched error pattern
/// </summary>
public class MatchedErrorPattern
{
    public ErrorPattern Pattern { get; set; } = new();
    public double MatchConfidence { get; set; }
    public string MatchedText { get; set; } = string.Empty;
    public int Position { get; set; }
}

/// <summary>
/// Error pattern definition
/// </summary>
public class ErrorPattern
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PatternType Type { get; set; }
    public string Pattern { get; set; } = string.Empty; // Regex or text pattern
    public string? SuggestedFix { get; set; }
    public IssueSeverity Severity { get; set; } = IssueSeverity.Error;
    public int OccurrenceCount { get; set; } = 0;
    public DateTime FirstSeen { get; set; } = DateTime.UtcNow;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Type of error pattern
/// </summary>
public enum PatternType
{
    Regex,
    TextMatch,
    Semantic,
    Structural
}
