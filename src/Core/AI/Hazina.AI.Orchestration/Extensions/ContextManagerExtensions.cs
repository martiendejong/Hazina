using Hazina.AI.Compression.Models;
using Hazina.AI.Compression.Optimization;
using Hazina.AI.Orchestration.Context;
using Microsoft.Extensions.Logging;

namespace Hazina.AI.Orchestration.Extensions;

/// <summary>
/// Backward-compatible extensions for ContextManager to add compression capabilities
/// </summary>
public static class ContextManagerExtensions
{
    /// <summary>
    /// Get optimized messages from context (compressed to fit budget)
    /// </summary>
    public static List<HazinaChatMessage> GetOptimizedMessages(
        this IContextManager contextManager,
        string contextId,
        string? currentQuery = null,
        CompressionOptions? options = null,
        ILogger? logger = null)
    {
        var context = contextManager.GetContext(contextId);
        if (context == null)
            return new List<HazinaChatMessage>();

        options ??= CompressionOptions.Balanced;
        options.ProviderOrModel ??= "gpt-4o"; // Default model for token counting

        var optimizer = new MessageHistoryOptimizer(logger as ILogger<MessageHistoryOptimizer>, options.ProviderOrModel);
        return optimizer.Optimize(context.Messages, currentQuery, options);
    }

    /// <summary>
    /// Add message and auto-optimize if over budget
    /// </summary>
    public static void AddMessageWithCompression(
        this IContextManager contextManager,
        string contextId,
        HazinaChatMessage message,
        CompressionOptions? options = null,
        ILogger? logger = null)
    {
        var context = contextManager.GetContext(contextId);
        if (context == null)
            throw new InvalidOperationException($"Context {contextId} not found");

        // Add message normally
        context.AddMessage(message);

        options ??= CompressionOptions.Balanced;

        // Check if we need compression
        if (context.TotalTokens > options.AvailableTokens)
        {
            var originalTokens = context.TotalTokens;
            var originalCount = context.Messages.Count;

            logger?.LogInformation("Context {ContextId} exceeded budget ({Tokens} > {Available}), applying compression",
                contextId, originalTokens, options.AvailableTokens);

            var optimizer = new MessageHistoryOptimizer(logger as ILogger<MessageHistoryOptimizer>, options.ProviderOrModel);
            var optimized = optimizer.Optimize(context.Messages, message.Text, options);

            // Replace messages with optimized version
            context.Messages.Clear();
            context.Messages.AddRange(optimized);

            logger?.LogInformation("Context compressed: {OriginalCount} messages ({OriginalTokens} tokens) -> {FinalCount} messages",
                originalCount, originalTokens, optimized.Count);
        }
    }

    /// <summary>
    /// Get context statistics for monitoring
    /// </summary>
    public static ContextStatistics GetStatistics(this IContextManager contextManager, string contextId)
    {
        var context = contextManager.GetContext(contextId);
        if (context == null)
            return new ContextStatistics();

        return new ContextStatistics
        {
            ContextId = contextId,
            MessageCount = context.Messages.Count,
            TotalTokens = context.TotalTokens,
            MaxTokens = context.MaxTokens,
            SystemMessageCount = context.Messages.Count(m => m.Role == HazinaMessageRole.System),
            UserMessageCount = context.Messages.Count(m => m.Role == HazinaMessageRole.User),
            AssistantMessageCount = context.Messages.Count(m => m.Role == HazinaMessageRole.Assistant),
            OldestMessageAge = context.Messages.Any() ? DateTime.UtcNow - context.CreatedAt : TimeSpan.Zero,
            UtilizationPercentage = context.MaxTokens > 0 ? (double)context.TotalTokens / context.MaxTokens * 100 : 0
        };
    }
}

/// <summary>
/// Context statistics model
/// </summary>
public class ContextStatistics
{
    public string ContextId { get; set; } = string.Empty;
    public int MessageCount { get; set; }
    public int TotalTokens { get; set; }
    public int MaxTokens { get; set; }
    public int SystemMessageCount { get; set; }
    public int UserMessageCount { get; set; }
    public int AssistantMessageCount { get; set; }
    public TimeSpan OldestMessageAge { get; set; }
    public double UtilizationPercentage { get; set; }
}
