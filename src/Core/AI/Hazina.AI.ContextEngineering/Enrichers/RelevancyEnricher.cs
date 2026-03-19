using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Hazina.AI.ContextEngineering.Enrichers;

/// <summary>
/// Enriches context with relevant documents based on semantic similarity
/// </summary>
public class RelevancyEnricher : IMessageEnricher
{
    private readonly ILogger<RelevancyEnricher> _logger;
    private readonly RelevancyOptions _options;

    public int Priority => 20; // Execute after history
    public string Name => "Relevancy";

    public RelevancyEnricher(
        ILogger<RelevancyEnricher> logger,
        RelevancyOptions? options = null)
    {
        _logger = logger;
        _options = options ?? new RelevancyOptions();
    }

    public Task<MessageContext> EnrichAsync(MessageContext context, CancellationToken cancellationToken = default)
    {
        if (context.Documents == null || context.Documents.Count == 0)
        {
            _logger.LogDebug("[RELEVANCY-ENRICHER] No documents to process");
            return Task.FromResult(context);
        }

        var originalCount = context.Documents.Count;

        // Filter by relevance score
        context.Documents = context.Documents
            .Where(d => d.Score >= _options.MinimumScore)
            .OrderByDescending(d => d.Score)
            .Take(_options.MaxDocuments)
            .ToList();

        // Estimate tokens
        var docTokens = context.Documents.Sum(d => d.Content.Length / 4);
        context.EstimatedTokens += docTokens;

        _logger.LogInformation(
            "[RELEVANCY-ENRICHER] Documents filtered | Original: {Original} | Kept: {Kept} | MinScore: {MinScore} | Estimated Tokens: {Tokens}",
            originalCount,
            context.Documents.Count,
            _options.MinimumScore,
            docTokens);

        return Task.FromResult(context);
    }
}

/// <summary>
/// Configuration options for relevancy enricher
/// </summary>
public class RelevancyOptions
{
    /// <summary>
    /// Maximum number of documents to include
    /// </summary>
    public int MaxDocuments { get; set; } = 5;

    /// <summary>
    /// Minimum relevance score (0.0 to 1.0)
    /// </summary>
    public double MinimumScore { get; set; } = 0.7;

    /// <summary>
    /// Whether to include metadata in context
    /// </summary>
    public bool IncludeMetadata { get; set; } = true;
}
