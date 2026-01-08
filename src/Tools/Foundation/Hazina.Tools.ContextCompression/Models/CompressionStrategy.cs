namespace Hazina.Tools.ContextCompression.Models;

/// <summary>
/// Defines the different compression strategies available
/// </summary>
public enum CompressionStrategy
{
    /// <summary>
    /// Automatically select the best strategy based on context
    /// </summary>
    Auto,

    /// <summary>
    /// Extract code structure using Abstract Syntax Tree parsing
    /// </summary>
    Ast,

    /// <summary>
    /// Extract only changed code with minimal context
    /// </summary>
    Diff,

    /// <summary>
    /// Create symbol index with references instead of full code
    /// </summary>
    SymbolIndex,

    /// <summary>
    /// Split into semantic chunks and filter by relevance
    /// </summary>
    Semantic,

    /// <summary>
    /// Reduce repeated patterns and boilerplate
    /// </summary>
    Template,

    /// <summary>
    /// Provide hierarchical context from high-level to detailed
    /// </summary>
    Hierarchical,

    /// <summary>
    /// Combine AST with symbol indexing
    /// </summary>
    AstWithSymbols
}
