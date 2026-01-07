namespace Hazina.Tools.ContextCompression.Models;

/// <summary>
/// Options for configuring compression behavior
/// </summary>
public class CompressionOptions
{
    /// <summary>
    /// Maximum tokens allowed in output
    /// </summary>
    public int MaxTokens { get; set; } = 8000;

    /// <summary>
    /// Preferred compression strategy
    /// </summary>
    public CompressionStrategy PreferredStrategy { get; set; } = CompressionStrategy.Auto;

    /// <summary>
    /// Include line numbers in output
    /// </summary>
    public bool IncludeLineNumbers { get; set; } = true;

    /// <summary>
    /// Include file paths in output
    /// </summary>
    public bool IncludeFilePaths { get; set; } = true;

    /// <summary>
    /// Minimum relevance score for semantic filtering (0.0 - 1.0)
    /// </summary>
    public double MinRelevanceScore { get; set; } = 0.5;

    /// <summary>
    /// Number of context lines to include around changes (for diff strategy)
    /// </summary>
    public int ContextLines { get; set; } = 3;

    /// <summary>
    /// Include comments and documentation
    /// </summary>
    public bool IncludeComments { get; set; } = true;

    /// <summary>
    /// Include private members in AST extraction
    /// </summary>
    public bool IncludePrivateMembers { get; set; } = false;

    /// <summary>
    /// Model name for token counting (e.g., "gpt-4", "claude-3")
    /// </summary>
    public string TokenizerModel { get; set; } = "gpt-4";
}
