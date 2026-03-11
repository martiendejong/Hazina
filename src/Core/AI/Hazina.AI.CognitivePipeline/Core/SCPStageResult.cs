using Hazina.AI.CognitivePipeline.Stages;

namespace Hazina.AI.CognitivePipeline.Core;

/// <summary>
/// Result produced by a single SCP pipeline stage.
/// </summary>
public class SCPStageResult
{
    /// <summary>
    /// Which stage produced this result.
    /// </summary>
    public SCPStageType StageType { get; set; }

    /// <summary>
    /// Confidence score for this stage's output (0.0 to 1.0).
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Key findings from this stage.
    /// </summary>
    public List<string> Findings { get; set; } = new();

    /// <summary>
    /// Warnings raised by this stage.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Stage-specific output data.
    /// </summary>
    public Dictionary<string, object> Data { get; set; } = new();

    /// <summary>
    /// Execution time in milliseconds.
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Estimated cost of LLM calls made during this stage.
    /// </summary>
    public double EstimatedCost { get; set; }

    /// <summary>
    /// Number of LLM calls made during this stage.
    /// </summary>
    public int LlmCallCount { get; set; }

    /// <summary>
    /// Whether this stage was skipped (e.g., due to GroundTruth short-circuit).
    /// </summary>
    public bool Skipped { get; set; }

    /// <summary>
    /// Reason for skipping, if applicable.
    /// </summary>
    public string? SkipReason { get; set; }

    /// <summary>
    /// Whether this stage completed successfully.
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// Error message if the stage failed.
    /// </summary>
    public string? Error { get; set; }
}
