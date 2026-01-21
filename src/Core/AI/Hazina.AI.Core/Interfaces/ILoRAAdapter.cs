namespace Hazina.AI.Core;

/// <summary>
/// Interface for Low-Rank Adaptation (LoRA) adapters.
/// LoRA enables efficient fine-tuning by training small adapter layers
/// instead of the full model weights.
/// </summary>
public interface ILoRAAdapter : IDisposable
{
    /// <summary>
    /// The rank of the low-rank decomposition.
    /// Higher rank = more capacity but larger adapter size.
    /// Typical values: 8, 16, 32, 64.
    /// </summary>
    int Rank { get; }

    /// <summary>
    /// Scaling factor for the LoRA updates.
    /// Final weight = base_weight + (alpha/rank) * lora_update.
    /// </summary>
    float Alpha { get; }

    /// <summary>
    /// Dropout probability applied to LoRA layers during training.
    /// </summary>
    float Dropout { get; }

    /// <summary>
    /// Names of the modules/layers that have LoRA adapters attached.
    /// </summary>
    IReadOnlyList<string> TargetModules { get; }

    /// <summary>
    /// Total number of trainable parameters in the adapter.
    /// </summary>
    long TrainableParameterCount { get; }

    /// <summary>
    /// Attaches the LoRA adapter to a base model.
    /// This modifies the base model's forward pass to include LoRA updates.
    /// </summary>
    /// <param name="baseModel">The base model to attach to.</param>
    void AttachTo(object baseModel);

    /// <summary>
    /// Detaches the LoRA adapter from the base model.
    /// Restores the original model behavior.
    /// </summary>
    void Detach();

    /// <summary>
    /// Saves the adapter weights to a file.
    /// Only saves the LoRA weights, not the base model.
    /// </summary>
    /// <param name="path">Path to save the adapter.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SaveAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// Loads adapter weights from a file.
    /// </summary>
    /// <param name="path">Path to the adapter file.</param>
    /// <param name="ct">Cancellation token.</param>
    Task LoadAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// Merges the LoRA weights into the base model weights.
    /// This creates a new model with the adapter permanently applied.
    /// Useful for deployment without LoRA overhead.
    /// </summary>
    /// <param name="baseModel">The base model to merge into.</param>
    /// <returns>A new model with merged weights.</returns>
    object MergeWithBase(object baseModel);

    /// <summary>
    /// Sets the adapter to training mode.
    /// </summary>
    void Train();

    /// <summary>
    /// Sets the adapter to evaluation mode.
    /// </summary>
    void Eval();
}

/// <summary>
/// Configuration for creating a LoRA adapter.
/// </summary>
public record LoRAConfig
{
    /// <summary>
    /// Rank of the low-rank decomposition.
    /// Default: 16.
    /// </summary>
    public int Rank { get; init; } = 16;

    /// <summary>
    /// Scaling factor for LoRA updates.
    /// Default: 32 (2x rank).
    /// </summary>
    public float Alpha { get; init; } = 32f;

    /// <summary>
    /// Dropout probability for LoRA layers.
    /// Default: 0.05.
    /// </summary>
    public float Dropout { get; init; } = 0.05f;

    /// <summary>
    /// Names of modules to apply LoRA to.
    /// Common choices: ["q_proj", "v_proj"] or ["q_proj", "k_proj", "v_proj", "o_proj"].
    /// Default: query and value projections.
    /// </summary>
    public List<string> TargetModules { get; init; } = new() { "q_proj", "v_proj" };

    /// <summary>
    /// Whether to use bias in LoRA layers.
    /// Default: none.
    /// </summary>
    public LoRABiasMode BiasMode { get; init; } = LoRABiasMode.None;

    /// <summary>
    /// Whether to use RSLoRA (Rank-Stabilized LoRA) scaling.
    /// Scales by 1/sqrt(rank) instead of 1/rank for better stability.
    /// </summary>
    public bool UseRSLoRA { get; init; } = false;
}

/// <summary>
/// Bias modes for LoRA layers.
/// </summary>
public enum LoRABiasMode
{
    /// <summary>No bias in LoRA layers.</summary>
    None,
    /// <summary>Only LoRA layers have bias.</summary>
    LoRAOnly,
    /// <summary>All layers (including base) have trainable bias.</summary>
    All
}
