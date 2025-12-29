using Hazina.AI.Providers.Core;

namespace Hazina.AI.Orchestration.Context;

/// <summary>
/// Manages conversation contexts
/// </summary>
public class ContextManager : IContextManager
{
    private readonly Dictionary<string, ConversationContext> _contexts = new();
    private readonly IProviderOrchestrator? _orchestrator;
    private readonly object _lock = new();

    public ContextManager(IProviderOrchestrator? orchestrator = null)
    {
        _orchestrator = orchestrator;
    }

    /// <summary>
    /// Create new conversation context
    /// </summary>
    public ConversationContext CreateContext(int maxTokens = 128000)
    {
        var context = new ConversationContext
        {
            MaxTokens = maxTokens
        };

        lock (_lock)
        {
            _contexts[context.Id] = context;
        }

        return context;
    }

    /// <summary>
    /// Get existing context
    /// </summary>
    public ConversationContext? GetContext(string contextId)
    {
        lock (_lock)
        {
            return _contexts.TryGetValue(contextId, out var context) ? context : null;
        }
    }

    /// <summary>
    /// Delete context
    /// </summary>
    public void DeleteContext(string contextId)
    {
        lock (_lock)
        {
            _contexts.Remove(contextId);
        }
    }

    /// <summary>
    /// Add message to context
    /// </summary>
    public void AddMessage(string contextId, HazinaChatMessage message)
    {
        var context = GetContext(contextId);
        if (context != null)
        {
            context.AddMessage(message);
        }
    }

    /// <summary>
    /// Get messages from context
    /// </summary>
    public List<HazinaChatMessage> GetMessages(string contextId, int? maxTokens = null)
    {
        var context = GetContext(contextId);
        if (context == null)
            return new List<HazinaChatMessage>();

        return maxTokens.HasValue
            ? context.GetMessages(maxTokens.Value)
            : context.Messages;
    }

    /// <summary>
    /// Summarize old messages to save tokens
    /// </summary>
    public async Task<string> SummarizeContextAsync(string contextId, CancellationToken cancellationToken = default)
    {
        var context = GetContext(contextId);
        if (context == null || _orchestrator == null)
            return string.Empty;

        // Get all messages except the most recent ones
        var messagesToSummarize = context.Messages.Take(context.Messages.Count - 5).ToList();
        if (!messagesToSummarize.Any())
            return string.Empty;

        // Create summarization prompt
        var conversationText = string.Join("\n\n", messagesToSummarize.Select(m =>
            $"{m.Role?.Role}: {m.Text}"));

        var summaryPrompt = new List<HazinaChatMessage>
        {
            new()
            {
                Role = HazinaMessageRole.System,
                Text = "Summarize the following conversation, preserving key context and facts."
            },
            new()
            {
                Role = HazinaMessageRole.User,
                Text = conversationText
            }
        };

        // Get summary
        var response = await _orchestrator.GetResponse(
            summaryPrompt,
            HazinaChatResponseFormat.Text,
            null,
            null,
            cancellationToken
        );

        // Replace old messages with summary
        context.Messages = new List<HazinaChatMessage>
        {
            new()
            {
                Role = HazinaMessageRole.System,
                Text = $"Previous conversation summary: {response.Result}"
            }
        };

        // Add back the recent messages
        foreach (var message in context.Messages.Skip(context.Messages.Count - 5))
        {
            context.Messages.Add(message);
        }

        return response.Result;
    }

    /// <summary>
    /// Clear old messages from context
    /// </summary>
    public void ClearOldMessages(string contextId, int keepLast)
    {
        var context = GetContext(contextId);
        context?.ClearOldMessages(keepLast);
    }
}
