namespace Hazina.AI.Providers.Core;

/// <summary>
/// Metadata about an LLM provider
/// </summary>
public class ProviderMetadata
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public ProviderType Type { get; set; }
    public ProviderCapabilities Capabilities { get; set; } = new();
    public ProviderPricing Pricing { get; set; } = new();
    public int Priority { get; set; } = 100; // Lower = higher priority
    public bool IsEnabled { get; set; } = true;
    public Dictionary<string, string> Configuration { get; set; } = new();
}

/// <summary>
/// Type of LLM provider
/// </summary>
public enum ProviderType
{
    OpenAI,
    Anthropic,
    GoogleGemini,
    GoogleADK,
    HuggingFace,
    Mistral,
    SemanticKernel,
    AzureOpenAI,
    Local,
    Custom
}

/// <summary>
/// Capabilities supported by a provider
/// </summary>
public class ProviderCapabilities
{
    public bool SupportsChat { get; set; } = true;
    public bool SupportsStreaming { get; set; } = true;
    public bool SupportsEmbeddings { get; set; } = false;
    public bool SupportsImages { get; set; } = false;
    public bool SupportsTTS { get; set; } = false;
    public bool SupportsTools { get; set; } = false;
    public bool SupportsVision { get; set; } = false;
    public int MaxTokens { get; set; } = 4096;
    public int MaxContextWindow { get; set; } = 4096;
}

/// <summary>
/// Pricing information for a provider
/// </summary>
public class ProviderPricing
{
    public decimal InputCostPer1KTokens { get; set; }
    public decimal OutputCostPer1KTokens { get; set; }
    public decimal EmbeddingCostPer1KTokens { get; set; }
    public decimal ImageCostPerGeneration { get; set; }
    public string Currency { get; set; } = "USD";

    public decimal CalculateInputCost(int tokens)
    {
        return (tokens / 1000m) * InputCostPer1KTokens;
    }

    public decimal CalculateOutputCost(int tokens)
    {
        return (tokens / 1000m) * OutputCostPer1KTokens;
    }

    public decimal CalculateEmbeddingCost(int tokens)
    {
        return (tokens / 1000m) * EmbeddingCostPer1KTokens;
    }
}
