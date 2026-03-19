using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Hazina.AI.ContextEngineering.Enrichers;

/// <summary>
/// Enriches context with data from additional stores (databases, caches, etc.)
/// </summary>
public class StoreEnricher : IMessageEnricher
{
    private readonly ILogger<StoreEnricher> _logger;
    private readonly StoreOptions _options;

    public int Priority => 40; // Execute last
    public string Name => "Store";

    public StoreEnricher(
        ILogger<StoreEnricher> logger,
        StoreOptions? options = null)
    {
        _logger = logger;
        _options = options ?? new StoreOptions();
    }

    public Task<MessageContext> EnrichAsync(MessageContext context, CancellationToken cancellationToken = default)
    {
        if (context.Stores == null || context.Stores.Count == 0)
        {
            _logger.LogDebug("[STORE-ENRICHER] No stores to process");
            return Task.FromResult(context);
        }

        var storeCount = context.Stores.Count;

        // Estimate tokens from store data
        var storeTokens = 0;
        foreach (var kvp in context.Stores)
        {
            if (kvp.Value is string strValue)
            {
                storeTokens += strValue.Length / 4;
            }
            else
            {
                // Rough estimate for non-string objects
                storeTokens += 50;
            }
        }

        context.EstimatedTokens += storeTokens;

        _logger.LogInformation(
            "[STORE-ENRICHER] Store data processed | Stores: {Count} | Estimated Tokens: {Tokens}",
            storeCount,
            storeTokens);

        return Task.FromResult(context);
    }
}

/// <summary>
/// Configuration options for store enricher
/// </summary>
public class StoreOptions
{
    /// <summary>
    /// Maximum number of store entries to include
    /// </summary>
    public int MaxEntries { get; set; } = 20;

    /// <summary>
    /// Whether to include metadata
    /// </summary>
    public bool IncludeMetadata { get; set; } = true;
}
