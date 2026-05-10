using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.Enrichment;

/// <summary>
/// Pipeline for running message enrichers in sequence
/// </summary>
public class MessageEnricherPipeline
{
    private readonly List<IMessageEnricher> _enrichers;
    private readonly ILogger? _logger;

    public MessageEnricherPipeline(
        IEnumerable<IMessageEnricher> enrichers,
        ILogger? logger = null)
    {
        _enrichers = enrichers
            .OrderBy(e => e.Priority)
            .ToList();
        _logger = logger;
    }

    /// <summary>
    /// Run all enrichers on the messages
    /// </summary>
    public async Task<List<HazinaChatMessage>> EnrichAsync(
        List<HazinaChatMessage> messages,
        EnrichmentContext context,
        CancellationToken cancellationToken = default)
    {
        var currentMessages = new List<HazinaChatMessage>(messages);

        foreach (var enricher in _enrichers)
        {
            if (context.SkipEnrichers.Contains(enricher.Name))
            {
                _logger?.LogDebug("Skipping enricher: {EnricherName}", enricher.Name);
                continue;
            }

            try
            {
                _logger?.LogDebug("Running enricher: {EnricherName}", enricher.Name);

                var enrichedMessages = await enricher.EnrichAsync(
                    currentMessages,
                    context,
                    cancellationToken);

                currentMessages = enrichedMessages;

                _logger?.LogDebug(
                    "Enricher {EnricherName} completed. Message count: {Before} -> {After}",
                    enricher.Name,
                    messages.Count,
                    currentMessages.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(
                    ex,
                    "Enricher {EnricherName} failed: {Error}",
                    enricher.Name,
                    ex.Message);

                // Continue with other enrichers
            }
        }

        return currentMessages;
    }

    /// <summary>
    /// Add an enricher to the pipeline
    /// </summary>
    public void AddEnricher(IMessageEnricher enricher)
    {
        _enrichers.Add(enricher);
        _enrichers.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }

    /// <summary>
    /// Remove an enricher by name
    /// </summary>
    public bool RemoveEnricher(string name)
    {
        return _enrichers.RemoveAll(e => e.Name == name) > 0;
    }

    /// <summary>
    /// Get all registered enrichers
    /// </summary>
    public IReadOnlyList<IMessageEnricher> GetEnrichers()
    {
        return _enrichers.AsReadOnly();
    }
}

/// <summary>
/// Base class for message enrichers
/// </summary>
public abstract class MessageEnricherBase : IMessageEnricher
{
    public abstract string Name { get; }
    public virtual int Priority => 100;

    public abstract Task<List<HazinaChatMessage>> EnrichAsync(
        List<HazinaChatMessage> messages,
        EnrichmentContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Helper to clone messages list
    /// </summary>
    protected List<HazinaChatMessage> CloneMessages(List<HazinaChatMessage> messages)
    {
        return new List<HazinaChatMessage>(messages);
    }
}
