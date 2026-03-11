namespace Hazina.AI.CognitivePipeline.GroundTruth;

/// <summary>
/// Persistent store for verified facts (ground truth).
/// Used by the Sensory stage to short-circuit expensive LLM calls
/// and by the Memory stage to promote high-confidence findings.
/// </summary>
public interface IGroundTruthStore
{
    /// <summary>
    /// Look up a fact by domain and key.
    /// </summary>
    Task<GroundTruthLookupResult?> LookupAsync(string domain, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all known facts for a domain.
    /// </summary>
    Task<IReadOnlyList<GroundTruthFact>> GetFactsAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary>
    /// Add or update a fact in the store.
    /// </summary>
    Task AddFactAsync(GroundTruthFact fact, CancellationToken cancellationToken = default);

    /// <summary>
    /// Increment the verification count for a fact.
    /// Called when a pipeline run independently confirms an existing fact.
    /// </summary>
    Task IncrementVerificationAsync(string domain, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get facts that have been verified at least the specified number of times
    /// and meet the minimum confidence threshold.
    /// </summary>
    Task<IReadOnlyList<GroundTruthFact>> GetPromotableFacts(
        string domain,
        int minVerifications = 3,
        double minConfidence = 0.9,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A verified fact stored in the ground truth system.
/// </summary>
public class GroundTruthFact
{
    /// <summary>
    /// Domain this fact belongs to (e.g., "art-history", "bronze-casting").
    /// </summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// Unique key within the domain (e.g., "rodin-birth-year", "lost-wax-process-origin").
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// The factual value (e.g., "1840", "Ancient Egypt, circa 3000 BCE").
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Confidence in this fact (0.0 to 1.0).
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Source of the fact (e.g., "SourceVerification channel", "Manual seed", "Pipeline promotion").
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Number of independent pipeline runs that have confirmed this fact.
    /// </summary>
    public int VerificationCount { get; set; }

    /// <summary>
    /// When this fact was first recorded.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this fact was last verified or updated.
    /// </summary>
    public DateTime LastVerifiedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Result of looking up a fact in the ground truth store.
/// </summary>
public class GroundTruthLookupResult
{
    /// <summary>
    /// Whether a matching fact was found.
    /// </summary>
    public bool Found { get; set; }

    /// <summary>
    /// The matched fact, if any.
    /// </summary>
    public GroundTruthFact? Fact { get; set; }

    /// <summary>
    /// Match quality (0.0 to 1.0). Exact key match = 1.0, partial = lower.
    /// </summary>
    public double MatchQuality { get; set; }
}
