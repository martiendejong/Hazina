using Hazina.AI.Compression.Interfaces;
using Hazina.AI.Compression.Models;
using Hazina.AI.Compression.TokenCounting;
using Microsoft.Extensions.Logging;

namespace Hazina.AI.Compression.Optimization;

/// <summary>
/// Optimizes conversation history by applying relevance scoring, deduplication,
/// and intelligent truncation to fit within token budgets
/// </summary>
public class MessageHistoryOptimizer
{
    private readonly ILogger<MessageHistoryOptimizer>? _logger;
    private readonly ITokenCounter _tokenCounter;

    public MessageHistoryOptimizer(ILogger<MessageHistoryOptimizer>? logger = null, string? providerOrModel = null)
    {
        _logger = logger;
        _tokenCounter = TokenCounterFactory.Instance.GetCounter(providerOrModel);
    }

    /// <summary>
    /// Optimize message history to fit within token budget
    /// </summary>
    public List<HazinaChatMessage> Optimize(
        List<HazinaChatMessage> messages,
        string? currentQuery,
        CompressionOptions options)
    {
        if (messages == null || messages.Count == 0)
            return new List<HazinaChatMessage>();

        _logger?.LogInformation("Optimizing {Count} messages with {Strategy} strategy, target: {MaxTokens} tokens",
            messages.Count, options.Strategy, options.AvailableTokens);

        // Strategy: No compression - return all messages
        if (options.Strategy == CompressionStrategy.None)
            return new List<HazinaChatMessage>(messages);

        // Step 1: Separate system messages and conversation messages
        var systemMessages = messages.Where(m => m.Role == HazinaMessageRole.System).ToList();
        var conversationMessages = messages.Where(m => m.Role != HazinaMessageRole.System).ToList();

        // Step 2: Always keep recent messages
        var recentCount = Math.Min(options.KeepRecentMessages, conversationMessages.Count);
        var recentMessages = conversationMessages.TakeLast(recentCount).ToList();
        var olderMessages = conversationMessages.Take(conversationMessages.Count - recentCount).ToList();

        // Step 3: Calculate token usage
        var systemTokens = CountTokens(systemMessages);
        var recentTokens = CountTokens(recentMessages);
        var olderTokens = CountTokens(olderMessages);
        var totalTokens = systemTokens + recentTokens + olderTokens;

        _logger?.LogDebug("Token distribution: System={System}, Recent={Recent}, Older={Older}, Total={Total}",
            systemTokens, recentTokens, olderTokens, totalTokens);

        // Step 4: Check if we need compression
        if (totalTokens <= options.AvailableTokens)
        {
            _logger?.LogInformation("No compression needed ({Total} <= {Available})", totalTokens, options.AvailableTokens);
            return new List<HazinaChatMessage>(messages);
        }

        // Step 5: Apply compression strategies
        var optimized = new List<HazinaChatMessage>();
        optimized.AddRange(systemMessages);

        // Budget for older messages
        var availableForOlder = options.AvailableTokens - systemTokens - recentTokens;

        if (availableForOlder > 0 && olderMessages.Count > 0)
        {
            List<HazinaChatMessage> selectedOlder;

            if (options.EnableRelevanceScoring && !string.IsNullOrEmpty(currentQuery))
            {
                // Score and select most relevant
                selectedOlder = SelectRelevantMessages(olderMessages, currentQuery, availableForOlder, options);
            }
            else
            {
                // Simple truncation from the end
                selectedOlder = TruncateToTokenLimit(olderMessages, availableForOlder);
            }

            optimized.AddRange(selectedOlder);
            _logger?.LogDebug("Selected {Count}/{Total} older messages ({Tokens} tokens)",
                selectedOlder.Count, olderMessages.Count, CountTokens(selectedOlder));
        }
        else if (availableForOlder < 0)
        {
            // Recent messages alone exceed budget - need to truncate them
            _logger?.LogWarning("Recent messages exceed budget, truncating");
            recentMessages = TruncateToTokenLimit(recentMessages, options.AvailableTokens - systemTokens);
        }

        optimized.AddRange(recentMessages);

        var finalTokens = CountTokens(optimized);
        _logger?.LogInformation("Optimized from {Original} to {Final} messages ({OriginalTokens} -> {FinalTokens} tokens, {Savings:F1}% reduction)",
            messages.Count, optimized.Count, totalTokens, finalTokens, (1 - (double)finalTokens / totalTokens) * 100);

        return optimized;
    }

    /// <summary>
    /// Select messages based on relevance to current query
    /// </summary>
    private List<HazinaChatMessage> SelectRelevantMessages(
        List<HazinaChatMessage> messages,
        string query,
        int tokenBudget,
        CompressionOptions options)
    {
        // Score each message
        var scored = messages.Select(m => new
        {
            Message = m,
            Score = CalculateRelevanceScore(m, query, messages)
        }).OrderByDescending(x => x.Score).ToList();

        // Select messages until budget is exhausted
        var selected = new List<HazinaChatMessage>();
        var usedTokens = 0;

        foreach (var item in scored)
        {
            var messageTokens = CountTokens(new[] { item.Message });
            if (usedTokens + messageTokens <= tokenBudget)
            {
                selected.Add(item.Message);
                usedTokens += messageTokens;
            }
            else
            {
                break;
            }
        }

        // Restore chronological order
        return selected.OrderBy(m => messages.IndexOf(m)).ToList();
    }

    /// <summary>
    /// Calculate relevance score for a message
    /// </summary>
    private double CalculateRelevanceScore(HazinaChatMessage message, string query, List<HazinaChatMessage> allMessages)
    {
        double score = 0.0;

        var messageText = message.Text?.ToLowerInvariant() ?? string.Empty;
        var queryWords = query.ToLowerInvariant().Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);

        // Factor 1: Keyword overlap (40% weight)
        var matchingWords = queryWords.Count(word => messageText.Contains(word));
        score += (matchingWords / (double)Math.Max(queryWords.Length, 1)) * 0.4;

        // Factor 2: Message role importance (30% weight)
        if (message.Role == HazinaMessageRole.System)
            score += 0.30; // System messages are important
        else if (message.Role == HazinaMessageRole.Assistant)
            score += 0.15; // Assistant responses carry context
        else if (message.Role == HazinaMessageRole.User)
            score += 0.20; // User questions are relevant
        else
            score += 0.10;

        // Factor 3: Recency decay (20% weight) - more recent is more relevant
        var messageIndex = allMessages.IndexOf(message);
        var recencyScore = (double)messageIndex / Math.Max(allMessages.Count - 1, 1);
        score += recencyScore * 0.2;

        // Factor 4: Message length penalty (10% weight) - prefer concise messages
        var lengthPenalty = Math.Min(messageText.Length / 1000.0, 1.0); // Penalize messages >1000 chars
        score += (1.0 - lengthPenalty) * 0.1;

        return Math.Clamp(score, 0.0, 1.0);
    }

    /// <summary>
    /// Truncate messages to fit within token limit (from beginning, keeping most recent)
    /// </summary>
    private List<HazinaChatMessage> TruncateToTokenLimit(List<HazinaChatMessage> messages, int tokenLimit)
    {
        var result = new List<HazinaChatMessage>();
        var usedTokens = 0;

        // Work backwards from most recent
        for (int i = messages.Count - 1; i >= 0; i--)
        {
            var messageTokens = CountTokens(new[] { messages[i] });
            if (usedTokens + messageTokens <= tokenLimit)
            {
                result.Insert(0, messages[i]);
                usedTokens += messageTokens;
            }
            else
            {
                break;
            }
        }

        return result;
    }

    /// <summary>
    /// Count tokens in messages
    /// </summary>
    private int CountTokens(IEnumerable<HazinaChatMessage> messages)
    {
        return messages.Sum(m =>
        {
            var text = m.Text ?? string.Empty;
            var response = m.Response ?? string.Empty;
            return _tokenCounter.CountTokens(text) + _tokenCounter.CountTokens(response);
        });
    }

    /// <summary>
    /// Deduplicate similar consecutive messages (removes redundancy)
    /// </summary>
    public List<HazinaChatMessage> DeduplicateMessages(List<HazinaChatMessage> messages, double similarityThreshold = 0.9)
    {
        if (messages.Count <= 1)
            return messages;

        var deduplicated = new List<HazinaChatMessage> { messages[0] };

        for (int i = 1; i < messages.Count; i++)
        {
            var current = messages[i];
            var previous = messages[i - 1];

            // Skip if same role and very similar text
            if (current.Role == previous.Role)
            {
                var similarity = CalculateTextSimilarity(current.Text ?? "", previous.Text ?? "");
                if (similarity >= similarityThreshold)
                {
                    _logger?.LogDebug("Skipping duplicate message: {Text}", current.Text?.Substring(0, Math.Min(50, current.Text.Length)));
                    continue;
                }
            }

            deduplicated.Add(current);
        }

        if (deduplicated.Count < messages.Count)
        {
            _logger?.LogInformation("Deduplicated {Original} -> {Final} messages", messages.Count, deduplicated.Count);
        }

        return deduplicated;
    }

    /// <summary>
    /// Simple text similarity calculation (Jaccard index)
    /// </summary>
    private double CalculateTextSimilarity(string text1, string text2)
    {
        if (string.IsNullOrEmpty(text1) || string.IsNullOrEmpty(text2))
            return 0.0;

        var words1 = text1.ToLowerInvariant().Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var words2 = text2.ToLowerInvariant().Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        var intersection = words1.Intersect(words2).Count();
        var union = words1.Union(words2).Count();

        return union > 0 ? (double)intersection / union : 0.0;
    }
}
