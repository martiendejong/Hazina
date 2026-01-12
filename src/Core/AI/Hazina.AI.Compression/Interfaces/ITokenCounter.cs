namespace Hazina.AI.Compression.Interfaces;

/// <summary>
/// Provider-specific token counting interface
/// </summary>
public interface ITokenCounter
{
    /// <summary>
    /// Get the name of the provider this counter supports
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Count tokens in text for a specific model
    /// </summary>
    int CountTokens(string text, string? modelName = null);

    /// <summary>
    /// Count tokens across multiple text segments
    /// </summary>
    int CountTokens(IEnumerable<string> texts, string? modelName = null);

    /// <summary>
    /// Check if this counter supports the given model
    /// </summary>
    bool SupportsModel(string modelName);

    /// <summary>
    /// Truncate text to fit within token limit
    /// </summary>
    string TruncateToTokenLimit(string text, int maxTokens, string? modelName = null);
}
