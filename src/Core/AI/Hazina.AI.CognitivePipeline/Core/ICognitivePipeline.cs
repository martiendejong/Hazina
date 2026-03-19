using System.Diagnostics.CodeAnalysis;

namespace Hazina.AI.CognitivePipeline.Core;

/// <summary>
/// The main SCP cognitive pipeline interface. Orchestrates S→O→L→F→B→M stages.
/// </summary>
/// <remarks>
/// This is an experimental API and may change in future versions without notice.
/// </remarks>
[Experimental("HAZ001")]
public interface ICognitivePipeline
{
    /// <summary>
    /// Execute the full cognitive pipeline for a given context.
    /// </summary>
    Task<CognitivePipelineResult> ExecuteAsync(SCPContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Complete result of an SCP pipeline execution.
/// </summary>
/// <remarks>
/// This is an experimental API and may change in future versions without notice.
/// </remarks>
[Experimental("HAZ001")]
public class CognitivePipelineResult
{
    /// <summary>
    /// Unique execution identifier.
    /// </summary>
    public string ExecutionId { get; set; } = string.Empty;

    /// <summary>
    /// The topic that was analyzed.
    /// </summary>
    public string TopicId { get; set; } = string.Empty;

    /// <summary>
    /// Final synthesized confidence (0.0 to 1.0).
    /// </summary>
    public double FinalConfidence { get; set; }

    /// <summary>
    /// All findings from all stages.
    /// </summary>
    public List<string> Findings { get; set; } = new();

    /// <summary>
    /// All warnings from all stages.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Per-stage results.
    /// </summary>
    public List<SCPStageResult> StageResults { get; set; } = new();

    /// <summary>
    /// Percentage of input data filtered as noise (0.0 to 1.0).
    /// </summary>
    public double NoiseReductionRate { get; set; }

    /// <summary>
    /// Percentage of queries answered from GroundTruth cache (0.0 to 1.0).
    /// </summary>
    public double GroundTruthHitRate { get; set; }

    /// <summary>
    /// Number of LLM calls avoided due to GroundTruth cache hits.
    /// </summary>
    public int LlmCallsAvoided { get; set; }

    /// <summary>
    /// Total LLM calls made across all stages.
    /// </summary>
    public int TotalLlmCalls { get; set; }

    /// <summary>
    /// Total estimated cost across all stages.
    /// </summary>
    public double TotalEstimatedCost { get; set; }

    /// <summary>
    /// Total execution time in milliseconds.
    /// </summary>
    public long TotalDurationMs { get; set; }

    /// <summary>
    /// Whether the pipeline completed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if the pipeline failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Whether a GroundTruth short-circuit was triggered.
    /// </summary>
    public bool ShortCircuited { get; set; }

    /// <summary>
    /// Additional output data from the pipeline.
    /// </summary>
    public Dictionary<string, object> OutputData { get; set; } = new();
}
