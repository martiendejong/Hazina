namespace Hazina.AI.Core;

/// <summary>
/// Interface for training machine learning models.
/// </summary>
/// <typeparam name="TModel">The model type being trained.</typeparam>
public interface ITrainer<TModel> where TModel : class
{
    /// <summary>
    /// Trains the model on the provided dataset.
    /// </summary>
    /// <param name="model">The model to train.</param>
    /// <param name="dataset">The training dataset.</param>
    /// <param name="config">Training configuration.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Training result with metrics.</returns>
    Task<TrainingResult> TrainAsync(
        TModel model,
        IDataset dataset,
        TrainingConfig config,
        IProgress<TrainingProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Evaluates the model on a validation dataset.
    /// </summary>
    /// <param name="model">The model to evaluate.</param>
    /// <param name="dataset">The validation dataset.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Evaluation metrics.</returns>
    Task<EvaluationResult> EvaluateAsync(
        TModel model,
        IDataset dataset,
        CancellationToken ct = default);

    /// <summary>
    /// Saves a training checkpoint.
    /// </summary>
    /// <param name="path">Path to save the checkpoint.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SaveCheckpointAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// Loads a training checkpoint to resume training.
    /// </summary>
    /// <param name="path">Path to the checkpoint.</param>
    /// <param name="ct">Cancellation token.</param>
    Task LoadCheckpointAsync(string path, CancellationToken ct = default);
}

/// <summary>
/// Configuration for model training.
/// </summary>
public record TrainingConfig
{
    /// <summary>Learning rate for the optimizer.</summary>
    public float LearningRate { get; init; } = 1e-4f;

    /// <summary>Number of samples per batch.</summary>
    public int BatchSize { get; init; } = 32;

    /// <summary>Number of complete passes through the dataset.</summary>
    public int Epochs { get; init; } = 10;

    /// <summary>Number of batches to accumulate gradients before updating.</summary>
    public int GradientAccumulationSteps { get; init; } = 1;

    /// <summary>Maximum gradient norm for clipping.</summary>
    public float MaxGradNorm { get; init; } = 1.0f;

    /// <summary>Weight decay for regularization.</summary>
    public float WeightDecay { get; init; } = 0.01f;

    /// <summary>Number of warmup steps for learning rate scheduler.</summary>
    public int WarmupSteps { get; init; } = 100;

    /// <summary>Use mixed precision training (FP16/BF16).</summary>
    public bool MixedPrecision { get; init; } = true;

    /// <summary>Random seed for reproducibility.</summary>
    public int? Seed { get; init; }

    /// <summary>Device to train on (cpu, cuda:0, etc.).</summary>
    public string Device { get; init; } = "cuda:0";
}

/// <summary>
/// Progress information during training.
/// </summary>
public record TrainingProgress
{
    /// <summary>Current epoch (1-indexed).</summary>
    public int Epoch { get; init; }

    /// <summary>Current step within the epoch.</summary>
    public int Step { get; init; }

    /// <summary>Total steps in the epoch.</summary>
    public int TotalSteps { get; init; }

    /// <summary>Current loss value.</summary>
    public float Loss { get; init; }

    /// <summary>Current learning rate.</summary>
    public float LearningRate { get; init; }

    /// <summary>Samples processed per second.</summary>
    public float SamplesPerSecond { get; init; }

    /// <summary>Estimated time remaining.</summary>
    public TimeSpan? EstimatedTimeRemaining { get; init; }
}

/// <summary>
/// Result of a training run.
/// </summary>
public record TrainingResult
{
    /// <summary>Whether training completed successfully.</summary>
    public bool Success { get; init; }

    /// <summary>Final training loss.</summary>
    public float FinalLoss { get; init; }

    /// <summary>Total training time.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Total number of steps completed.</summary>
    public int TotalSteps { get; init; }

    /// <summary>Loss history per epoch.</summary>
    public List<float> LossHistory { get; init; } = new();

    /// <summary>Path to the final checkpoint if saved.</summary>
    public string? CheckpointPath { get; init; }

    /// <summary>Error message if training failed.</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Result of model evaluation.
/// </summary>
public record EvaluationResult
{
    /// <summary>Average loss on the evaluation set.</summary>
    public float Loss { get; init; }

    /// <summary>Accuracy if applicable.</summary>
    public float? Accuracy { get; init; }

    /// <summary>Perplexity for language models.</summary>
    public float? Perplexity { get; init; }

    /// <summary>Additional metrics.</summary>
    public Dictionary<string, float> Metrics { get; init; } = new();
}

/// <summary>
/// Interface for a dataset that can be used for training.
/// </summary>
public interface IDataset
{
    /// <summary>Total number of samples in the dataset.</summary>
    int Count { get; }

    /// <summary>Gets a batch of samples.</summary>
    /// <param name="startIndex">Starting index.</param>
    /// <param name="batchSize">Number of samples to retrieve.</param>
    /// <returns>Batch of input-output pairs.</returns>
    Task<DataBatch> GetBatchAsync(int startIndex, int batchSize);

    /// <summary>Shuffles the dataset (for training).</summary>
    void Shuffle(int? seed = null);
}

/// <summary>
/// A batch of training data.
/// </summary>
public record DataBatch
{
    /// <summary>Input tensors.</summary>
    public required float[][] Inputs { get; init; }

    /// <summary>Target/label tensors.</summary>
    public required float[][] Targets { get; init; }

    /// <summary>Optional attention masks.</summary>
    public float[][]? AttentionMasks { get; init; }
}
