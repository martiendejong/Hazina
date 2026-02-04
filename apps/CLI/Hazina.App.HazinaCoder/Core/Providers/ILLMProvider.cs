namespace Hazina.App.HazinaCoder.Core.Providers;

/// <summary>
/// Unified interface for all LLM providers
/// Supports text generation, vision, tool use, and embeddings
/// </summary>
public interface ILLMProvider
{
    /// <summary>
    /// Provider name (e.g., "Anthropic", "OpenAI", "Ollama")
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Provider is configured and available
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Provider capabilities
    /// </summary>
    ProviderCapabilities Capabilities { get; }

    /// <summary>
    /// Generate text completion
    /// </summary>
    Task<LLMResponse> GenerateAsync(LLMRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate streaming completion
    /// </summary>
    IAsyncEnumerable<LLMStreamChunk> GenerateStreamAsync(
        LLMRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate embeddings
    /// </summary>
    Task<EmbeddingResponse> GenerateEmbeddingsAsync(
        EmbeddingRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get provider health status
    /// </summary>
    Task<ProviderHealth> GetHealthAsync();

    /// <summary>
    /// Get pricing information
    /// </summary>
    ProviderPricing GetPricing(string model);
}

/// <summary>
/// Provider capabilities
/// </summary>
public class ProviderCapabilities
{
    public bool SupportsVision { get; set; }
    public bool SupportsToolUse { get; set; }
    public bool SupportsStreaming { get; set; }
    public bool SupportsEmbeddings { get; set; }
    public bool SupportsFunctionCalling { get; set; }
    public int MaxContextLength { get; set; }
    public int MaxOutputTokens { get; set; }
    public List<string> AvailableModels { get; set; } = new();
}

/// <summary>
/// LLM request
/// </summary>
public class LLMRequest
{
    public string Model { get; set; } = string.Empty;
    public List<LLMMessage> Messages { get; set; } = new();
    public int MaxTokens { get; set; } = 1000;
    public double Temperature { get; set; } = 0.7;
    public double TopP { get; set; } = 1.0;
    public int? TopK { get; set; }
    public List<string>? StopSequences { get; set; }
    public List<ToolDefinition>? Tools { get; set; }
    public string? SystemPrompt { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// LLM message
/// </summary>
public class LLMMessage
{
    public string Role { get; set; } = string.Empty; // user, assistant, system
    public string Content { get; set; } = string.Empty;
    public List<LLMContent>? Contents { get; set; } // For multimodal (text + images)
    public List<ToolCall>? ToolCalls { get; set; }
}

/// <summary>
/// LLM content (for multimodal)
/// </summary>
public class LLMContent
{
    public string Type { get; set; } = string.Empty; // text, image
    public string? Text { get; set; }
    public ImageSource? Image { get; set; }
}

public class ImageSource
{
    public string Type { get; set; } = string.Empty; // url, base64
    public string Data { get; set; } = string.Empty;
    public string? MimeType { get; set; }
}

/// <summary>
/// Tool definition
/// </summary>
public class ToolDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Tool call
/// </summary>
public class ToolCall
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, object> Arguments { get; set; } = new();
}

/// <summary>
/// LLM response
/// </summary>
public class LLMResponse
{
    public bool Success { get; set; }
    public string Content { get; set; } = string.Empty;
    public List<ToolCall>? ToolCalls { get; set; }
    public string? Error { get; set; }
    public LLMUsage Usage { get; set; } = new();
    public string Model { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// LLM usage statistics
/// </summary>
public class LLMUsage
{
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens => InputTokens + OutputTokens;
    public double Cost { get; set; }
}

/// <summary>
/// Streaming chunk
/// </summary>
public class LLMStreamChunk
{
    public string Content { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
    public LLMUsage? Usage { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Embedding request
/// </summary>
public class EmbeddingRequest
{
    public string Model { get; set; } = string.Empty;
    public List<string> Texts { get; set; } = new();
}

/// <summary>
/// Embedding response
/// </summary>
public class EmbeddingResponse
{
    public bool Success { get; set; }
    public List<float[]> Embeddings { get; set; } = new();
    public string? Error { get; set; }
    public int TokensUsed { get; set; }
    public double Cost { get; set; }
}

/// <summary>
/// Provider health status
/// </summary>
public class ProviderHealth
{
    public bool IsHealthy { get; set; }
    public string Status { get; set; } = string.Empty; // healthy, degraded, down
    public TimeSpan? Latency { get; set; }
    public DateTime LastCheck { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Provider pricing
/// </summary>
public class ProviderPricing
{
    public double InputTokenCost { get; set; } // Cost per 1M tokens
    public double OutputTokenCost { get; set; } // Cost per 1M tokens
    public string Currency { get; set; } = "USD";

    public double CalculateCost(int inputTokens, int outputTokens)
    {
        return (inputTokens / 1_000_000.0 * InputTokenCost) +
               (outputTokens / 1_000_000.0 * OutputTokenCost);
    }
}
