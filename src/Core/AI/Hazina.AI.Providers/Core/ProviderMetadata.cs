namespace Hazina.AI.Providers.Core;

/// <summary>
/// Describes an LLM provider registered with the <see cref="ProviderOrchestrator"/>.
/// </summary>
/// <remarks>
/// Pass a <see cref="ProviderMetadata"/> instance when calling
/// <see cref="ProviderOrchestrator.RegisterProvider"/> to configure routing priority, capabilities,
/// and cost tracking for the provider.
/// </remarks>
/// <example>
/// <code>
/// var metadata = new ProviderMetadata
/// {
///     Name        = "openai-gpt4o",
///     DisplayName = "OpenAI GPT-4o",
///     Type        = ProviderType.OpenAI,
///     Priority    = 10,
///     Capabilities = new ProviderCapabilities
///     {
///         SupportsTools    = true,
///         SupportsVision   = true,
///         SupportsStreaming = true,
///         MaxContextWindow = 128_000
///     },
///     Pricing = new ProviderPricing
///     {
///         InputCostPer1KTokens  = 0.005m,
///         OutputCostPer1KTokens = 0.015m,
///         Currency              = "USD"
///     }
/// };
/// orchestrator.RegisterProvider("openai-gpt4o", openAiClient, metadata);
/// </code>
/// </example>
public class ProviderMetadata
{
    /// <summary>Gets or sets the unique internal identifier for this provider (e.g., <c>"openai-gpt4o"</c>).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the human-readable display name (e.g., <c>"OpenAI GPT-4o"</c>).</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Gets or sets the provider type used for routing and capability inference.</summary>
    public ProviderType Type { get; set; }

    /// <summary>Gets or sets the feature capabilities declared by this provider.</summary>
    public ProviderCapabilities Capabilities { get; set; } = new();

    /// <summary>Gets or sets the pricing model used for cost tracking and budget enforcement.</summary>
    public ProviderPricing Pricing { get; set; } = new();

    /// <summary>
    /// Gets or sets the routing priority. Lower values are preferred by the provider selector.
    /// Default: <c>100</c>.
    /// </summary>
    public int Priority { get; set; } = 100;

    /// <summary>
    /// Gets or sets a value indicating whether this provider is available for routing.
    /// Disabled providers are skipped during selection. Default: <see langword="true"/>.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Gets or sets arbitrary provider-specific configuration key/value pairs (e.g., model name, region).</summary>
    public Dictionary<string, string> Configuration { get; set; } = new();
}

/// <summary>
/// Declares the feature set supported by a specific LLM provider or model.
/// </summary>
public enum ProviderType
{
    /// <summary>OpenAI API (GPT-4, GPT-4o, o1, etc.).</summary>
    OpenAI,
    /// <summary>Anthropic API (Claude 3, Claude 3.5, etc.).</summary>
    Anthropic,
    /// <summary>Google Gemini API.</summary>
    GoogleGemini,
    /// <summary>Google Agent Development Kit.</summary>
    GoogleADK,
    /// <summary>HuggingFace Inference API or local models.</summary>
    HuggingFace,
    /// <summary>Mistral AI API.</summary>
    Mistral,
    /// <summary>Microsoft Semantic Kernel abstraction layer.</summary>
    SemanticKernel,
    /// <summary>Azure OpenAI Service.</summary>
    AzureOpenAI,
    /// <summary>Locally hosted model (Ollama, LM Studio, etc.).</summary>
    Local,
    /// <summary>Any other provider not covered by the above types.</summary>
    Custom
}

/// <summary>
/// Feature flags and limits for an LLM provider, used to select the right provider for a request.
/// </summary>
public class ProviderCapabilities
{
    /// <summary>Indicates whether the provider supports chat completions. Default: <see langword="true"/>.</summary>
    public bool SupportsChat { get; set; } = true;

    /// <summary>Indicates whether the provider supports streaming responses. Default: <see langword="true"/>.</summary>
    public bool SupportsStreaming { get; set; } = true;

    /// <summary>Indicates whether the provider can generate vector embeddings. Default: <see langword="false"/>.</summary>
    public bool SupportsEmbeddings { get; set; } = false;

    /// <summary>Indicates whether the provider can generate images. Default: <see langword="false"/>.</summary>
    public bool SupportsImages { get; set; } = false;

    /// <summary>Indicates whether the provider supports text-to-speech. Default: <see langword="false"/>.</summary>
    public bool SupportsTTS { get; set; } = false;

    /// <summary>Indicates whether the provider supports function/tool calling. Default: <see langword="false"/>.</summary>
    public bool SupportsTools { get; set; } = false;

    /// <summary>Indicates whether the provider supports multimodal (image) input. Default: <see langword="false"/>.</summary>
    public bool SupportsVision { get; set; } = false;

    /// <summary>Maximum output tokens per response. Default: <c>4096</c>.</summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>Maximum combined input + output context window in tokens. Default: <c>4096</c>.</summary>
    public int MaxContextWindow { get; set; } = 4096;
}

/// <summary>
/// Per-operation pricing for an LLM provider, used to track and enforce cost budgets.
/// </summary>
public class ProviderPricing
{
    /// <summary>Gets or sets the cost per 1,000 input tokens in <see cref="Currency"/>.</summary>
    public decimal InputCostPer1KTokens { get; set; }

    /// <summary>Gets or sets the cost per 1,000 output tokens in <see cref="Currency"/>.</summary>
    public decimal OutputCostPer1KTokens { get; set; }

    /// <summary>Gets or sets the cost per 1,000 tokens for embedding operations in <see cref="Currency"/>.</summary>
    public decimal EmbeddingCostPer1KTokens { get; set; }

    /// <summary>Gets or sets the cost per image generation in <see cref="Currency"/>.</summary>
    public decimal ImageCostPerGeneration { get; set; }

    /// <summary>Gets or sets the ISO 4217 currency code for all prices. Default: <c>"USD"</c>.</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>Calculates the input cost for the given token count.</summary>
    /// <param name="tokens">Number of input tokens consumed.</param>
    /// <returns>Total cost in <see cref="Currency"/>.</returns>
    public decimal CalculateInputCost(int tokens)
    {
        return (tokens / 1000m) * InputCostPer1KTokens;
    }

    /// <summary>Calculates the output cost for the given token count.</summary>
    /// <param name="tokens">Number of output tokens generated.</param>
    /// <returns>Total cost in <see cref="Currency"/>.</returns>
    public decimal CalculateOutputCost(int tokens)
    {
        return (tokens / 1000m) * OutputCostPer1KTokens;
    }

    /// <summary>Calculates the embedding cost for the given token count.</summary>
    /// <param name="tokens">Number of tokens used in the embedding operation.</param>
    /// <returns>Total cost in <see cref="Currency"/>.</returns>
    public decimal CalculateEmbeddingCost(int tokens)
    {
        return (tokens / 1000m) * EmbeddingCostPer1KTokens;
    }
}
