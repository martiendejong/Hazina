namespace Hazina.Neurochain.Core;

/// <summary>
/// Result from a reasoning layer
/// </summary>
public class ReasoningResult
{
    /// <summary>
    /// The reasoning output
    /// </summary>
    public string Response { get; set; } = string.Empty;

    /// <summary>
    /// Confidence score (0-1)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Reasoning chain - step-by-step thought process
    /// </summary>
    public List<string> ReasoningChain { get; set; } = new();

    /// <summary>
    /// Supporting evidence
    /// </summary>
    public List<string> Evidence { get; set; } = new();

    /// <summary>
    /// Identified assumptions
    /// </summary>
    public List<string> Assumptions { get; set; } = new();

    /// <summary>
    /// Potential flaws or weaknesses
    /// </summary>
    public List<string> Weaknesses { get; set; } = new();

    /// <summary>
    /// Time taken to reason (milliseconds)
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Provider used for reasoning
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Cost of reasoning
    /// </summary>
    public decimal Cost { get; set; }

    /// <summary>
    /// Whether this result passed validation
    /// </summary>
    public bool IsValid { get; set; } = true;

    /// <summary>
    /// Validation issues if any
    /// </summary>
    public List<string> ValidationIssues { get; set; } = new();
}
