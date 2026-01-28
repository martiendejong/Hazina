using Hazina.LLMs.Configuration;

namespace Hazina.AI.LocalLLM.Configuration;

/// <summary>
/// Configuration for local LLM inference using LLamaSharp.
/// </summary>
public class LocalLLMConfig : IProviderConfig
{
    /// <summary>
    /// Path to the GGUF model file.
    /// </summary>
    public required string ModelPath { get; set; }

    /// <summary>
    /// Number of layers to offload to GPU.
    /// Set to -1 to offload all layers, 0 for CPU-only.
    /// </summary>
    public int GpuLayerCount { get; set; } = -1;

    /// <summary>
    /// Context size (maximum tokens in conversation).
    /// </summary>
    public uint ContextSize { get; set; } = 4096;

    /// <summary>
    /// Batch size for prompt processing.
    /// </summary>
    public uint BatchSize { get; set; } = 512;

    /// <summary>
    /// Number of threads to use for CPU inference.
    /// Set to 0 to use all available cores.
    /// </summary>
    public uint ThreadCount { get; set; } = 0;

    /// <summary>
    /// Seed for random number generation. Set to 0 for random seed.
    /// </summary>
    public uint Seed { get; set; } = 0;

    /// <summary>
    /// Use memory-mapped file for model loading (reduces RAM usage).
    /// </summary>
    public bool UseMemoryMap { get; set; } = true;

    /// <summary>
    /// Use memory locking to prevent swapping.
    /// </summary>
    public bool UseMemoryLock { get; set; } = false;

    /// <summary>
    /// GPU device ID to use (for multi-GPU systems).
    /// </summary>
    public int MainGpuId { get; set; } = 0;

    /// <summary>
    /// Maximum tokens to generate per response.
    /// </summary>
    public int MaxTokens { get; set; } = 2048;

    /// <summary>
    /// Temperature for sampling (0.0 = deterministic, 1.0 = creative).
    /// </summary>
    public float Temperature { get; set; } = 0.7f;

    /// <summary>
    /// Top-p (nucleus) sampling threshold.
    /// </summary>
    public float TopP { get; set; } = 0.9f;

    /// <summary>
    /// Top-k sampling (0 = disabled).
    /// </summary>
    public int TopK { get; set; } = 40;

    /// <summary>
    /// Repetition penalty (1.0 = no penalty).
    /// </summary>
    public float RepetitionPenalty { get; set; } = 1.1f;

    /// <summary>
    /// Stop sequences to end generation.
    /// </summary>
    public List<string> StopSequences { get; set; } = new();

    /// <summary>
    /// Chat template to use (auto-detected if null).
    /// Options: "llama2", "llama3", "chatml", "phi3", "mistral", "custom".
    /// </summary>
    public string? ChatTemplate { get; set; }

    /// <summary>
    /// Custom system prompt prefix (if ChatTemplate is "custom").
    /// </summary>
    public string? SystemPromptPrefix { get; set; }

    /// <summary>
    /// Enable verbose logging from llama.cpp.
    /// </summary>
    public bool Verbose { get; set; } = false;

    // IProviderConfig implementation
    string IProviderConfig.ApiKey { get => string.Empty; set { } } // Not used for local models
    string? IProviderConfig.Endpoint { get => ModelPath; set { ModelPath = value ?? ModelPath; } }
    string IProviderConfig.Model { get => Path.GetFileNameWithoutExtension(ModelPath); set { } }
    string? IProviderConfig.LogPath { get; set; }

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrEmpty(ModelPath))
            yield return "ModelPath is required";
        else if (!File.Exists(ModelPath))
            yield return $"Model file not found: {ModelPath}";

        if (ContextSize < 128)
            yield return "ContextSize must be at least 128";
    }
}

/// <summary>
/// Preset configurations for common models.
/// </summary>
public static class LocalLLMPresets
{
    /// <summary>
    /// Creates a configuration for Llama 3.2 models.
    /// </summary>
    public static LocalLLMConfig Llama32(string modelPath, int gpuLayers = -1) => new()
    {
        ModelPath = modelPath,
        GpuLayerCount = gpuLayers,
        ContextSize = 8192,
        ChatTemplate = "llama3",
        Temperature = 0.7f,
        TopP = 0.9f,
        RepetitionPenalty = 1.1f
    };

    /// <summary>
    /// Creates a configuration for Phi-4 models.
    /// </summary>
    public static LocalLLMConfig Phi4(string modelPath, int gpuLayers = -1) => new()
    {
        ModelPath = modelPath,
        GpuLayerCount = gpuLayers,
        ContextSize = 16384,
        ChatTemplate = "phi3",
        Temperature = 0.7f,
        TopP = 0.95f
    };

    /// <summary>
    /// Creates a configuration for DeepSeek models.
    /// </summary>
    public static LocalLLMConfig DeepSeek(string modelPath, int gpuLayers = -1) => new()
    {
        ModelPath = modelPath,
        GpuLayerCount = gpuLayers,
        ContextSize = 32768,
        ChatTemplate = "chatml",
        Temperature = 0.7f
    };

    /// <summary>
    /// Creates a configuration for Qwen models.
    /// </summary>
    public static LocalLLMConfig Qwen(string modelPath, int gpuLayers = -1) => new()
    {
        ModelPath = modelPath,
        GpuLayerCount = gpuLayers,
        ContextSize = 32768,
        ChatTemplate = "chatml",
        Temperature = 0.7f
    };

    /// <summary>
    /// Creates a configuration for Mistral models.
    /// </summary>
    public static LocalLLMConfig Mistral(string modelPath, int gpuLayers = -1) => new()
    {
        ModelPath = modelPath,
        GpuLayerCount = gpuLayers,
        ContextSize = 8192,
        ChatTemplate = "mistral",
        Temperature = 0.7f
    };

    /// <summary>
    /// Creates a configuration optimized for CPU-only inference.
    /// </summary>
    public static LocalLLMConfig CpuOptimized(string modelPath) => new()
    {
        ModelPath = modelPath,
        GpuLayerCount = 0,
        ContextSize = 2048,
        BatchSize = 256,
        ThreadCount = 0, // Use all cores
        UseMemoryMap = true
    };
}
