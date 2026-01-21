namespace Hazina.AI.Core;

/// <summary>
/// Interface for running inference on pre-trained models.
/// Provides a unified API for ONNX, TorchScript, and other model formats.
/// </summary>
public interface IModelInference : IDisposable
{
    /// <summary>
    /// Gets metadata about the loaded model.
    /// </summary>
    ModelMetadata Metadata { get; }

    /// <summary>
    /// Runs inference on a single input tensor.
    /// </summary>
    /// <param name="input">Input tensor as a flattened float array.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Output tensor as a flattened float array.</returns>
    Task<float[]> InferAsync(float[] input, CancellationToken ct = default);

    /// <summary>
    /// Runs batched inference on multiple inputs.
    /// </summary>
    /// <param name="inputs">Batch of input tensors.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Batch of output tensors.</returns>
    Task<float[][]> InferBatchAsync(float[][] inputs, CancellationToken ct = default);

    /// <summary>
    /// Runs inference with named inputs and outputs for complex models.
    /// </summary>
    /// <param name="namedInputs">Dictionary mapping input names to tensors.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Dictionary mapping output names to tensors.</returns>
    Task<Dictionary<string, float[]>> InferNamedAsync(
        Dictionary<string, float[]> namedInputs,
        CancellationToken ct = default);

    /// <summary>
    /// Warms up the model by running a dummy inference.
    /// Call this at application startup to avoid cold-start latency.
    /// </summary>
    void WarmUp();
}

/// <summary>
/// Metadata about a loaded model.
/// </summary>
public record ModelMetadata
{
    /// <summary>
    /// Name or identifier of the model.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Model format (ONNX, TorchScript, GGUF, etc.).
    /// </summary>
    public required ModelFormat Format { get; init; }

    /// <summary>
    /// Input tensor shapes. Key is input name, value is shape.
    /// </summary>
    public Dictionary<string, int[]> InputShapes { get; init; } = new();

    /// <summary>
    /// Output tensor shapes. Key is output name, value is shape.
    /// </summary>
    public Dictionary<string, int[]> OutputShapes { get; init; } = new();

    /// <summary>
    /// Total number of parameters in the model.
    /// </summary>
    public long ParameterCount { get; init; }

    /// <summary>
    /// Model file size in bytes.
    /// </summary>
    public long FileSizeBytes { get; init; }

    /// <summary>
    /// Custom metadata from the model file.
    /// </summary>
    public Dictionary<string, string> CustomMetadata { get; init; } = new();
}

/// <summary>
/// Supported model formats.
/// </summary>
public enum ModelFormat
{
    /// <summary>Open Neural Network Exchange format.</summary>
    ONNX,
    /// <summary>PyTorch serialized model.</summary>
    TorchScript,
    /// <summary>GGML/GGUF format for llama.cpp.</summary>
    GGUF,
    /// <summary>TensorFlow SavedModel or Keras H5.</summary>
    TensorFlow,
    /// <summary>SafeTensors format.</summary>
    SafeTensors,
    /// <summary>Unknown or custom format.</summary>
    Unknown
}
