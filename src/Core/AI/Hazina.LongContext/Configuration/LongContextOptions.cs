namespace Hazina.LongContext.Configuration;

/// <summary>
/// Configuration options for long-context query execution
/// </summary>
public class LongContextOptions
{
    /// <summary>
    /// Enable recursive query decomposition globally (default: false for backwards compatibility)
    /// </summary>
    public bool EnableRecursiveQueries { get; set; } = false;

    /// <summary>
    /// Default maximum recursion depth
    /// </summary>
    public int DefaultMaxDepth { get; set; } = 3;

    /// <summary>
    /// Default maximum total tokens across all recursive calls
    /// </summary>
    public int DefaultMaxTotalTokens { get; set; } = 100_000;

    /// <summary>
    /// Default maximum number of sub-questions per decomposition
    /// </summary>
    public int DefaultMaxBranchingFactor { get; set; } = 5;

    /// <summary>
    /// Per-store configuration overrides
    /// </summary>
    public Dictionary<string, StoreRecursiveConfig> PerStoreConfig { get; set; } = new();

    /// <summary>
    /// Validate configuration
    /// </summary>
    public List<string> Validate()
    {
        var errors = new List<string>();

        if (DefaultMaxDepth < 1 || DefaultMaxDepth > 10)
            errors.Add("DefaultMaxDepth must be between 1 and 10");

        if (DefaultMaxTotalTokens < 1000)
            errors.Add("DefaultMaxTotalTokens must be at least 1000");

        if (DefaultMaxBranchingFactor < 2 || DefaultMaxBranchingFactor > 20)
            errors.Add("DefaultMaxBranchingFactor must be between 2 and 20");

        return errors;
    }
}

/// <summary>
/// Per-store configuration for recursive queries
/// </summary>
public class StoreRecursiveConfig
{
    /// <summary>
    /// Enable recursion for this specific store
    /// </summary>
    public bool EnableRecursive { get; set; }

    /// <summary>
    /// Override max depth for this store
    /// </summary>
    public int? MaxDepth { get; set; }

    /// <summary>
    /// Override max total tokens for this store
    /// </summary>
    public int? MaxTotalTokens { get; set; }

    /// <summary>
    /// Override max branching factor for this store
    /// </summary>
    public int? MaxBranchingFactor { get; set; }
}
