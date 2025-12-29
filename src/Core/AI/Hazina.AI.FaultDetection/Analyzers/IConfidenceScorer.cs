using Hazina.AI.FaultDetection.Core;

namespace Hazina.AI.FaultDetection.Analyzers;

/// <summary>
/// Interface for scoring confidence of LLM responses
/// </summary>
public interface IConfidenceScorer
{
    /// <summary>
    /// Calculate confidence score for a response
    /// </summary>
    Task<ConfidenceScore> ScoreAsync(
        string response,
        ValidationContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Confidence score result
/// </summary>
public class ConfidenceScore
{
    public double Score { get; set; } = 1.0; // 0-1, higher is better
    public Dictionary<string, double> ComponentScores { get; set; } = new();
    public List<ConfidenceFactor> Factors { get; set; } = new();
    public string Reasoning { get; set; } = string.Empty;

    public bool IsHighConfidence => Score >= 0.8;
    public bool IsMediumConfidence => Score >= 0.5 && Score < 0.8;
    public bool IsLowConfidence => Score < 0.5;
}

/// <summary>
/// Factor affecting confidence
/// </summary>
public class ConfidenceFactor
{
    public string Name { get; set; } = string.Empty;
    public double Impact { get; set; } // Positive or negative impact on confidence
    public string Description { get; set; } = string.Empty;
}
