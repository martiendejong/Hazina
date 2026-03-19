using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Hazina.AI.ContextEngineering.Enrichers;

/// <summary>
/// Enriches context with a sliding window of conversation history
/// </summary>
public class HistoryWindowEnricher : IMessageEnricher
{
    private readonly ILogger<HistoryWindowEnricher> _logger;
    private readonly HistoryWindowOptions _options;

    public int Priority => 10; // Execute early
    public string Name => "HistoryWindow";

    public HistoryWindowEnricher(
        ILogger<HistoryWindowEnricher> logger,
        HistoryWindowOptions? options = null)
    {
        _logger = logger;
        _options = options ?? new HistoryWindowOptions();
    }

    public Task<MessageContext> EnrichAsync(MessageContext context, CancellationToken cancellationToken = default)
    {
        if (context.History == null || context.History.Count == 0)
        {
            _logger.LogDebug("[HISTORY-ENRICHER] No history to process");
            return Task.FromResult(context);
        }

        var originalCount = context.History.Count;

        // Apply sliding window
        if (context.History.Count > _options.MaxMessages)
        {
            var skip = context.History.Count - _options.MaxMessages;
            context.History = context.History.Skip(skip).ToList();

            _logger.LogDebug(
                "[HISTORY-ENRICHER] Applied sliding window | Original: {Original} | Kept: {Kept} | Window: {Window}",
                originalCount,
                context.History.Count,
                _options.MaxMessages);
        }

        // Estimate tokens (rough approximation: 4 chars per token)
        var historyTokens = context.History.Sum(m => m.Content.Length / 4);
        context.EstimatedTokens += historyTokens;

        _logger.LogDebug(
            "[HISTORY-ENRICHER] History processed | Messages: {Count} | Estimated Tokens: {Tokens}",
            context.History.Count,
            historyTokens);

        return Task.FromResult(context);
    }
}

/// <summary>
/// Configuration options for history window enricher
/// </summary>
public class HistoryWindowOptions
{
    /// <summary>
    /// Maximum number of history messages to include
    /// </summary>
    public int MaxMessages { get; set; } = 10;

    /// <summary>
    /// Whether to prioritize recent messages
    /// </summary>
    public bool PrioritizeRecent { get; set; } = true;
}
