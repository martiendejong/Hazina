using Hazina.AI.CognitivePipeline.GroundTruth;
using Hazina.AI.CognitivePipeline.Stages;

namespace Hazina.AI.CognitivePipeline.Core;

/// <summary>
/// Shared context flowing through all SCP pipeline stages.
/// Each stage reads from previous stages' output and writes its own results.
/// </summary>
public class SCPContext
{
    /// <summary>
    /// Unique identifier for this pipeline execution.
    /// </summary>
    public string ExecutionId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// The topic or subject being analyzed.
    /// </summary>
    public string TopicId { get; set; } = string.Empty;

    /// <summary>
    /// Domain context (e.g., "art-history", "medical", "legal").
    /// </summary>
    public string? Domain { get; set; }

    /// <summary>
    /// Raw input data loaded by the Sensory stage.
    /// </summary>
    public Dictionary<string, object> RawData { get; set; } = new();

    /// <summary>
    /// Structured data produced by the Organization stage.
    /// Contains entities, relationships, and timeline data.
    /// </summary>
    public Dictionary<string, object> OrganizedData { get; set; } = new();

    /// <summary>
    /// Data that passed the noise filter. Unreliable/biased content removed.
    /// </summary>
    public Dictionary<string, object> FilteredData { get; set; } = new();

    /// <summary>
    /// Ground truth facts retrieved from the store during Sensory stage.
    /// Key = fact identifier, Value = the lookup result with confidence.
    /// </summary>
    public Dictionary<string, GroundTruthLookupResult> GroundTruthHits { get; set; } = new();

    /// <summary>
    /// Findings accumulated across all stages.
    /// </summary>
    public List<string> Findings { get; set; } = new();

    /// <summary>
    /// Warnings accumulated across all stages.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Per-stage results, keyed by stage type.
    /// </summary>
    public Dictionary<SCPStageType, SCPStageResult> StageResults { get; set; } = new();

    /// <summary>
    /// Additional metadata for domain-specific use.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Pipeline configuration in effect.
    /// </summary>
    public CognitivePipelineConfig Config { get; set; } = new();

    /// <summary>
    /// Whether a GroundTruth short-circuit was triggered (skip remaining stages).
    /// </summary>
    public bool GroundTruthShortCircuit { get; set; }

    /// <summary>
    /// Timestamp when pipeline execution started.
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
}
