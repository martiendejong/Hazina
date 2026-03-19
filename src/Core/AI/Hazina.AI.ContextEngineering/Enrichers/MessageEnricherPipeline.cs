using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Hazina.Observability.Core.Correlation;

namespace Hazina.AI.ContextEngineering.Enrichers;

/// <summary>
/// Orchestrates the execution of message enrichers in priority order
/// </summary>
public class MessageEnricherPipeline
{
    private readonly IEnumerable<IMessageEnricher> _enrichers;
    private readonly ILogger<MessageEnricherPipeline> _logger;

    public MessageEnricherPipeline(
        IEnumerable<IMessageEnricher> enrichers,
        ILogger<MessageEnricherPipeline> logger)
    {
        _enrichers = enrichers.OrderBy(e => e.Priority).ToList();
        _logger = logger;
    }

    /// <summary>
    /// Executes all enrichers in priority order
    /// </summary>
    public async Task<MessageContext> EnrichAsync(
        MessageContext context,
        CancellationToken cancellationToken = default)
    {
        var correlationId = CorrelationContext.GetOrCreateCorrelationId();
        context.CorrelationId ??= correlationId;

        _logger.LogInformation(
            "[ENRICHER-PIPELINE] Starting enrichment pipeline | CorrelationId: {CorrelationId} | Enrichers: {EnricherCount}",
            correlationId,
            _enrichers.Count());

        var enrichedContext = context;

        foreach (var enricher in _enrichers)
        {
            try
            {
                _logger.LogDebug(
                    "[ENRICHER-PIPELINE] Executing enricher: {EnricherName} | Priority: {Priority} | CorrelationId: {CorrelationId}",
                    enricher.Name,
                    enricher.Priority,
                    correlationId);

                var startTime = DateTime.UtcNow;
                enrichedContext = await enricher.EnrichAsync(enrichedContext, cancellationToken);
                var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

                _logger.LogDebug(
                    "[ENRICHER-PIPELINE] Enricher completed: {EnricherName} | Duration: {DurationMs}ms | Tokens: {Tokens} | CorrelationId: {CorrelationId}",
                    enricher.Name,
                    duration,
                    enrichedContext.EstimatedTokens,
                    correlationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[ENRICHER-PIPELINE] Enricher failed: {EnricherName} | Error: {ErrorMessage} | CorrelationId: {CorrelationId}",
                    enricher.Name,
                    ex.Message,
                    correlationId);

                // Continue with remaining enrichers even if one fails
            }
        }

        _logger.LogInformation(
            "[ENRICHER-PIPELINE] Pipeline completed | Total Tokens: {Tokens} | Documents: {DocCount} | History: {HistoryCount} | CorrelationId: {CorrelationId}",
            enrichedContext.EstimatedTokens,
            enrichedContext.Documents.Count,
            enrichedContext.History.Count,
            correlationId);

        return enrichedContext;
    }

    /// <summary>
    /// Gets registered enrichers in priority order
    /// </summary>
    public IEnumerable<IMessageEnricher> GetEnrichers() => _enrichers;
}
