using Hazina.AI.CognitivePipeline.Stages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hazina.AI.CognitivePipeline.Core;

/// <summary>
/// Fluent builder for constructing and configuring a <see cref="CognitivePipeline"/>.
/// Allows C# developers to add stages, set timeouts and tuning parameters, then
/// call <see cref="Build"/> to produce a ready-to-run pipeline instance.
/// </summary>
/// <example>
/// <code>
/// var pipeline = new CognitivePipelineBuilder()
///     .WithStep&lt;SensoryStage&gt;()
///     .WithStep&lt;OrganizationStage&gt;()
///     .WithStep&lt;NoiseFilterStage&gt;()
///     .WithStep&lt;ReflectiveStage&gt;()
///     .WithStep&lt;DecisionStage&gt;()
///     .WithStep&lt;MemoryStage&gt;()
///     .WithTimeout(TimeSpan.FromMinutes(5))
///     .WithStageTimeout(TimeSpan.FromSeconds(30))
///     .WithGroundTruthShortCircuit(true)
///     .Build(loggerFactory);
/// </code>
/// </example>
public class CognitivePipelineBuilder
{
    private readonly List<ISCPStage> _stages = new();
    private readonly CognitivePipelineConfig _config = new();

    // ─── Stage registration ────────────────────────────────────────────────────

    /// <summary>
    /// Add a pre-constructed stage instance to the pipeline.
    /// Stages execute in insertion order unless their <see cref="ISCPStage.Order"/>
    /// values differ.
    /// </summary>
    public CognitivePipelineBuilder WithStep(ISCPStage stage)
    {
        ArgumentNullException.ThrowIfNull(stage);
        _stages.Add(stage);
        return this;
    }

    /// <summary>
    /// Add a stage by type using the default parameterless constructor.
    /// The type must implement <see cref="ISCPStage"/> and expose a public
    /// no-arg constructor.
    /// </summary>
    public CognitivePipelineBuilder WithStep<T>() where T : ISCPStage, new()
    {
        _stages.Add(new T());
        return this;
    }

    // ─── Timing ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Maximum wall-clock time allowed for the entire pipeline before it is
    /// cancelled and a timeout error is returned.
    /// </summary>
    public CognitivePipelineBuilder WithTimeout(TimeSpan timeout)
    {
        _config.PipelineTimeoutMs = (int)timeout.TotalMilliseconds;
        return this;
    }

    /// <summary>
    /// Maximum time allowed for any single stage before it is cancelled and a
    /// <see cref="SCPStageResult"/> with <c>Success = false</c> is recorded.
    /// </summary>
    public CognitivePipelineBuilder WithStageTimeout(TimeSpan timeout)
    {
        _config.StageTimeoutMs = (int)timeout.TotalMilliseconds;
        return this;
    }

    // ─── GroundTruth / short-circuit ──────────────────────────────────────────

    /// <summary>
    /// Enable or disable the GroundTruth short-circuit optimisation.
    /// When enabled (default), a high-confidence GroundTruth hit in the
    /// Sensory stage skips all intermediate stages and jumps straight to
    /// Decision and Memory.
    /// </summary>
    public CognitivePipelineBuilder WithGroundTruthShortCircuit(bool enable = true)
    {
        _config.EnableGroundTruthShortCircuit = enable;
        return this;
    }

    /// <summary>
    /// Set the minimum GroundTruth confidence (0.0–1.0) that triggers the
    /// short-circuit.  Defaults to 0.9.
    /// </summary>
    public CognitivePipelineBuilder WithGroundTruthThreshold(double threshold)
    {
        if (threshold is < 0.0 or > 1.0)
            throw new ArgumentOutOfRangeException(nameof(threshold), "Threshold must be between 0.0 and 1.0.");
        _config.GroundTruthShortCircuitThreshold = threshold;
        return this;
    }

    // ─── Confidence / quality ─────────────────────────────────────────────────

    /// <summary>
    /// Minimum final confidence threshold.  Results with a lower confidence
    /// are flagged in <see cref="CognitivePipelineResult.Warnings"/>.
    /// Defaults to 0.6.
    /// </summary>
    public CognitivePipelineBuilder WithMinConfidence(double minConfidence)
    {
        if (minConfidence is < 0.0 or > 1.0)
            throw new ArgumentOutOfRangeException(nameof(minConfidence), "Confidence must be between 0.0 and 1.0.");
        _config.MinConfidence = minConfidence;
        return this;
    }

    // ─── Error handling ───────────────────────────────────────────────────────

    /// <summary>
    /// When <c>true</c> (default), a failing stage does not abort the remaining
    /// stages.  When <c>false</c>, the pipeline halts at the first failed stage.
    /// </summary>
    public CognitivePipelineBuilder ContinueOnStageFailure(bool continueOnFailure = true)
    {
        _config.ContinueOnStageFailure = continueOnFailure;
        return this;
    }

    // ─── Learning ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Enable or disable recording this execution for the learning sub-system.
    /// Defaults to <c>true</c>.
    /// </summary>
    public CognitivePipelineBuilder WithLearning(bool enable = true)
    {
        _config.EnableLearning = enable;
        return this;
    }

    // ─── Stage weights ────────────────────────────────────────────────────────

    /// <summary>
    /// Override the per-stage confidence weights used by the Decision stage
    /// to synthesise a final score.  Keys are <see cref="SCPStageType"/> names
    /// (e.g. "NoiseFilter"); values are weights that should collectively sum
    /// to 1.0.
    /// </summary>
    public CognitivePipelineBuilder WithStageWeights(Dictionary<string, double> weights)
    {
        ArgumentNullException.ThrowIfNull(weights);
        _config.StageWeights = weights;
        return this;
    }

    // ─── Build ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Validate the configuration and produce a <see cref="CognitivePipeline"/>
    /// ready for execution.
    /// </summary>
    /// <param name="loggerFactory">
    /// Optional logger factory.  When <c>null</c>, <see cref="NullLoggerFactory.Instance"/>
    /// is used (no log output).
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no stages have been added.
    /// </exception>
    public CognitivePipeline Build(ILoggerFactory? loggerFactory = null)
    {
        if (_stages.Count == 0)
            throw new InvalidOperationException(
                "At least one stage must be added before calling Build(). " +
                "Use WithStep<T>() or WithStep(instance) to register stages.");

        var factory = loggerFactory ?? NullLoggerFactory.Instance;
        var logger = factory.CreateLogger<CognitivePipeline>();

        // Capture the config onto each stage context by storing it in the
        // pipeline — stages read it via SCPContext.Config at runtime.
        return new CognitivePipeline(_stages, logger, _config);
    }
}
