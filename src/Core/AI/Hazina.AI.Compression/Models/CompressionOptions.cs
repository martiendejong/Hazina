namespace Hazina.AI.Compression.Models;

/// <summary>
/// Compression strategy configuration
/// </summary>
public class CompressionOptions
{
    /// <summary>
    /// Maximum tokens for the context
    /// </summary>
    public int MaxTokens { get; set; } = 100_000;

    /// <summary>
    /// Reserved tokens for system prompt and tools
    /// </summary>
    public int ReservedSystemTokens { get; set; } = 5_000;

    /// <summary>
    /// Reserved tokens for response generation
    /// </summary>
    public int ReservedResponseTokens { get; set; } = 4_000;

    /// <summary>
    /// Compression strategy to use
    /// </summary>
    public CompressionStrategy Strategy { get; set; } = CompressionStrategy.Balanced;

    /// <summary>
    /// Enable message relevance scoring
    /// </summary>
    public bool EnableRelevanceScoring { get; set; } = true;

    /// <summary>
    /// Enable conversation summarization
    /// </summary>
    public bool EnableSummarization { get; set; } = true;

    /// <summary>
    /// Number of recent messages to always keep
    /// </summary>
    public int KeepRecentMessages { get; set; } = 10;

    /// <summary>
    /// Provider or model name for token counting
    /// </summary>
    public string? ProviderOrModel { get; set; }

    /// <summary>
    /// Get available tokens for content (after reserves)
    /// </summary>
    public int AvailableTokens => MaxTokens - ReservedSystemTokens - ReservedResponseTokens;

    /// <summary>
    /// Create default options for a specific model
    /// </summary>
    public static CompressionOptions ForModel(string modelName, int contextWindow)
    {
        return new CompressionOptions
        {
            MaxTokens = contextWindow,
            ProviderOrModel = modelName,
            ReservedSystemTokens = (int)(contextWindow * 0.05), // 5%
            ReservedResponseTokens = (int)(contextWindow * 0.20), // 20%
        };
    }

    /// <summary>
    /// Aggressive compression preset
    /// </summary>
    public static CompressionOptions Aggressive => new()
    {
        Strategy = CompressionStrategy.Aggressive,
        EnableRelevanceScoring = true,
        EnableSummarization = true,
        KeepRecentMessages = 5,
    };

    /// <summary>
    /// Balanced compression preset (default)
    /// </summary>
    public static CompressionOptions Balanced => new()
    {
        Strategy = CompressionStrategy.Balanced,
        EnableRelevanceScoring = true,
        EnableSummarization = true,
        KeepRecentMessages = 10,
    };

    /// <summary>
    /// Conservative compression preset
    /// </summary>
    public static CompressionOptions Conservative => new()
    {
        Strategy = CompressionStrategy.Conservative,
        EnableRelevanceScoring = false,
        EnableSummarization = false,
        KeepRecentMessages = 20,
    };
}

/// <summary>
/// Compression strategy enum
/// </summary>
public enum CompressionStrategy
{
    /// <summary>
    /// Minimal compression - keep most content
    /// </summary>
    Conservative,

    /// <summary>
    /// Balanced approach - moderate compression
    /// </summary>
    Balanced,

    /// <summary>
    /// Aggressive compression - maximize token savings
    /// </summary>
    Aggressive,

    /// <summary>
    /// No compression - passthrough
    /// </summary>
    None
}
