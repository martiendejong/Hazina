namespace Hazina.LLMs.Enrichment;

/// <summary>
/// Interface for message enrichment middleware
/// </summary>
public interface IMessageEnricher
{
    /// <summary>
    /// Name of this enricher (for logging and debugging)
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Priority for ordering (lower values run first)
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Enrich messages before sending to LLM
    /// </summary>
    /// <param name="messages">Messages to enrich</param>
    /// <param name="context">Enrichment context with metadata</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Enriched messages</returns>
    Task<List<HazinaChatMessage>> EnrichAsync(
        List<HazinaChatMessage> messages,
        EnrichmentContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Context for message enrichment
/// </summary>
public class EnrichmentContext
{
    /// <summary>
    /// Conversation ID for tracking
    /// </summary>
    public string? ConversationId { get; init; }

    /// <summary>
    /// User ID
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// Session metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Request timestamp
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Whether to skip certain enrichers (for testing)
    /// </summary>
    public HashSet<string> SkipEnrichers { get; init; } = new();

    /// <summary>
    /// Custom data that enrichers can read/write
    /// </summary>
    public Dictionary<string, object> EnricherData { get; } = new();
}
