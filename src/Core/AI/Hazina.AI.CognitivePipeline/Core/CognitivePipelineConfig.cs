namespace Hazina.AI.CognitivePipeline.Core;

/// <summary>
/// Configuration for the SCP cognitive pipeline.
/// </summary>
public class CognitivePipelineConfig
{
    /// <summary>
    /// When true, if the Sensory stage finds a high-confidence GroundTruth match,
    /// skip Organization/Filter/Reflective stages and go straight to Decision.
    /// </summary>
    public bool EnableGroundTruthShortCircuit { get; set; } = true;

    /// <summary>
    /// Minimum confidence threshold for the final result.
    /// Results below this are flagged as low-confidence.
    /// </summary>
    public double MinConfidence { get; set; } = 0.6;

    /// <summary>
    /// Minimum GroundTruth confidence to trigger a short-circuit (0.0 to 1.0).
    /// </summary>
    public double GroundTruthShortCircuitThreshold { get; set; } = 0.9;

    /// <summary>
    /// Maximum time in milliseconds for any single stage before timeout.
    /// </summary>
    public int StageTimeoutMs { get; set; } = 60_000;

    /// <summary>
    /// Maximum total pipeline execution time in milliseconds.
    /// </summary>
    public int PipelineTimeoutMs { get; set; } = 300_000;

    /// <summary>
    /// Whether to continue executing subsequent stages if one fails.
    /// </summary>
    public bool ContinueOnStageFailure { get; set; } = true;

    /// <summary>
    /// Whether to record this execution for learning purposes.
    /// </summary>
    public bool EnableLearning { get; set; } = true;

    /// <summary>
    /// Confidence weights for the Decision stage synthesis.
    /// Keys are SCPStageType names, values are weights (should sum to 1.0).
    /// </summary>
    public Dictionary<string, double> StageWeights { get; set; } = new()
    {
        ["NoiseFilter"] = 0.4,
        ["Reflective"] = 0.3,
        ["Organization"] = 0.2,
        ["Sensory"] = 0.1
    };
}
