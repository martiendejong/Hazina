using Hazina.AI.Core;

namespace Hazina.AI.Inference.Configuration;

/// <summary>
/// Configuration for ONNX model inference.
/// </summary>
public class OnnxConfig
{
    /// <summary>
    /// Path to the ONNX model file.
    /// </summary>
    public required string ModelPath { get; set; }

    /// <summary>
    /// Execution provider to use for inference.
    /// </summary>
    public ExecutionProvider ExecutionProvider { get; set; } = ExecutionProvider.CPU;

    /// <summary>
    /// GPU device ID (for CUDA/DirectML execution).
    /// </summary>
    public int DeviceId { get; set; } = 0;

    /// <summary>
    /// Number of threads for CPU execution.
    /// </summary>
    public int ThreadCount { get; set; } = 0;

    /// <summary>
    /// Enable memory pattern optimization.
    /// </summary>
    public bool EnableMemoryPattern { get; set; } = true;

    /// <summary>
    /// Enable graph optimizations.
    /// </summary>
    public GraphOptimizationLevel OptimizationLevel { get; set; } = GraphOptimizationLevel.All;

    /// <summary>
    /// Path to save optimized model (if null, won't save).
    /// </summary>
    public string? OptimizedModelPath { get; set; }

    /// <summary>
    /// Custom session options key-value pairs.
    /// </summary>
    public Dictionary<string, string> SessionOptions { get; set; } = new();
}

/// <summary>
/// Graph optimization levels for ONNX Runtime.
/// </summary>
public enum GraphOptimizationLevel
{
    /// <summary>No optimization.</summary>
    None,
    /// <summary>Basic optimizations.</summary>
    Basic,
    /// <summary>Extended optimizations including shape inference.</summary>
    Extended,
    /// <summary>All optimizations enabled.</summary>
    All
}
