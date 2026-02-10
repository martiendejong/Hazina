using Hazina.AI.CognitivePipeline.Core;

namespace Hazina.AI.CognitivePipeline.Stages;

/// <summary>
/// Represents a single stage in the SCP (Sensory-Cognitive-Process) pipeline.
/// Stages execute sequentially: S → O → L → F → B → M
/// </summary>
public interface ISCPStage
{
    /// <summary>
    /// The type of this stage, determining execution order.
    /// </summary>
    SCPStageType StageType { get; }

    /// <summary>
    /// Execution order within the pipeline. Lower values execute first.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Human-readable name for logging and metrics.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Execute this stage, reading from and writing to the shared context.
    /// </summary>
    Task<SCPStageResult> ExecuteAsync(SCPContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// The six stages of the SCP cognitive pipeline.
/// </summary>
public enum SCPStageType
{
    /// <summary>Sensory Input - Load raw data, check GroundTruth cache</summary>
    Sensory = 0,

    /// <summary>Organization - Structure entities, relationships, timeline</summary>
    Organization = 1,

    /// <summary>Noise Filter (Limbic) - Filter unreliable/biased content</summary>
    NoiseFilter = 2,

    /// <summary>Reflective (Frontal) - Cross-validate claims with multi-layer reasoning</summary>
    Reflective = 3,

    /// <summary>Decision (Binding) - Weighted confidence synthesis</summary>
    Decision = 4,

    /// <summary>Memory - Store results, promote facts to GroundTruth</summary>
    Memory = 5
}
