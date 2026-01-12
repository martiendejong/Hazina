namespace Hazina.Brain.Options;

/// <summary>
/// Configuration options for Hazina.Brain module.
/// </summary>
public sealed class BrainOptions
{
    /// <summary>
    /// Database provider to use
    /// </summary>
    public BrainProvider Provider { get; set; } = BrainProvider.Sqlite;

    /// <summary>
    /// Database connection string
    /// </summary>
    public string ConnectionString { get; set; } = "Data Source=brain.db";

    /// <summary>
    /// Whether to distill facts immediately after observing episodes
    /// </summary>
    public bool DistillOnObserve { get; set; } = true;

    /// <summary>
    /// Half-life for fact weight decay (default: 30 days)
    /// </summary>
    public TimeSpan HalfLife { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Maximum episodes per store (triggers pruning)
    /// </summary>
    public int MaxEpisodesPerStore { get; set; } = 5000;

    /// <summary>
    /// Maximum facts per store (triggers pruning)
    /// </summary>
    public int MaxFactsPerStore { get; set; } = 2000;

    /// <summary>
    /// Default topK for episode recall
    /// </summary>
    public int EpisodesTopKDefault { get; set; } = 8;

    /// <summary>
    /// Default topK for fact recall
    /// </summary>
    public int FactsTopKDefault { get; set; } = 16;
}

/// <summary>
/// Supported database providers
/// </summary>
public enum BrainProvider
{
    /// <summary>
    /// SQLite (good for development and small deployments)
    /// </summary>
    Sqlite,

    /// <summary>
    /// PostgreSQL (recommended for production with pgvector extension)
    /// </summary>
    Postgres
}
