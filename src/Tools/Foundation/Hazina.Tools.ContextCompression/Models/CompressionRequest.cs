namespace Hazina.Tools.ContextCompression.Models;

/// <summary>
/// Request for context compression
/// </summary>
public class CompressionRequest
{
    /// <summary>
    /// List of file paths to compress
    /// </summary>
    public List<string> FilePaths { get; set; } = new();

    /// <summary>
    /// User query or task description for relevance filtering
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Preferred compression strategy
    /// </summary>
    public CompressionStrategy Strategy { get; set; } = CompressionStrategy.Auto;

    /// <summary>
    /// Maximum number of tokens allowed in compressed output
    /// </summary>
    public int MaxTokens { get; set; } = 8000;

    /// <summary>
    /// Repository root path for git operations
    /// </summary>
    public string? RepositoryPath { get; set; }

    /// <summary>
    /// Additional metadata for compression
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}
