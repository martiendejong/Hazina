namespace Hazina.AI.Orchestration.Context;

/// <summary>
/// Interface for managing conversation contexts
/// </summary>
public interface IContextManager
{
    /// <summary>
    /// Create new conversation context
    /// </summary>
    ConversationContext CreateContext(int maxTokens = 128000);

    /// <summary>
    /// Get existing context
    /// </summary>
    ConversationContext? GetContext(string contextId);

    /// <summary>
    /// Delete context
    /// </summary>
    void DeleteContext(string contextId);

    /// <summary>
    /// Add message to context
    /// </summary>
    void AddMessage(string contextId, HazinaChatMessage message);

    /// <summary>
    /// Get messages from context
    /// </summary>
    List<HazinaChatMessage> GetMessages(string contextId, int? maxTokens = null);

    /// <summary>
    /// Summarize old messages to save tokens
    /// </summary>
    Task<string> SummarizeContextAsync(string contextId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear old messages from context
    /// </summary>
    void ClearOldMessages(string contextId, int keepLast);
}
