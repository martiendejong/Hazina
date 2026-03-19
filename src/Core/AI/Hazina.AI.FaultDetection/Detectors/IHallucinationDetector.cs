using System.Diagnostics.CodeAnalysis;
using Hazina.AI.FaultDetection.Core;

namespace Hazina.AI.FaultDetection.Detectors;

/// <summary>
/// Interface for detecting hallucinations in LLM responses
/// </summary>
/// <remarks>
/// This is an experimental API and may change in future versions without notice.
/// </remarks>
[Experimental("HAZ003")]
public interface IHallucinationDetector
{
    /// <summary>
    /// Detect potential hallucinations in a response
    /// </summary>
    Task<HallucinationDetectionResult> DetectAsync(
        string response,
        ValidationContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of hallucination detection
/// </summary>
public class HallucinationDetectionResult
{
    public bool ContainsHallucination { get; set; }
    public double ConfidenceScore { get; set; } = 1.0;
    public List<HallucinationInstance> Instances { get; set; } = new();
}

/// <summary>
/// Detected hallucination instance
/// </summary>
public class HallucinationInstance
{
    public string Content { get; set; } = string.Empty;
    public HallucinationType Type { get; set; }
    public double Confidence { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public string? SuggestedCorrection { get; set; }
}

/// <summary>
/// Types of hallucinations
/// </summary>
public enum HallucinationType
{
    FabricatedFact,        // Made-up information
    Contradiction,         // Contradicts known facts or earlier statements
    ContextMismatch,       // Doesn't match the prompt context
    UnsupportedClaim,      // Claims without basis in provided context
    AttributionError,      // Misattributes information to wrong source
    TemporalError,         // Gets timing/dates wrong
    QuantitativeError      // Gets numbers/quantities wrong
}
