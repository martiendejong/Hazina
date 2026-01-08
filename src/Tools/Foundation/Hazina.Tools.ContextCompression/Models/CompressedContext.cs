namespace Hazina.Tools.ContextCompression.Models;

/// <summary>
/// Result of context compression
/// </summary>
public class CompressedContext
{
    /// <summary>
    /// Compressed content ready for LLM consumption
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Original token count before compression
    /// </summary>
    public int OriginalTokens { get; set; }

    /// <summary>
    /// Token count after compression
    /// </summary>
    public int CompressedTokens { get; set; }

    /// <summary>
    /// Compression ratio (compressed / original)
    /// </summary>
    public double CompressionRatio => OriginalTokens > 0
        ? (double)CompressedTokens / OriginalTokens
        : 0;

    /// <summary>
    /// Token savings percentage
    /// </summary>
    public double TokenSavingsPercent => OriginalTokens > 0
        ? (1 - CompressionRatio) * 100
        : 0;

    /// <summary>
    /// Strategy that was used for compression
    /// </summary>
    public CompressionStrategy StrategyUsed { get; set; }

    /// <summary>
    /// Additional metadata about the compression
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Files that were included in the compression
    /// </summary>
    public List<string> IncludedFiles { get; set; } = new();
}
