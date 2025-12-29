namespace Hazina.AI.Orchestration.Context;

/// <summary>
/// Manages conversation context across multiple turns
/// </summary>
public class ConversationContext
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public List<HazinaChatMessage> Messages { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    public int TotalTokens { get; set; }
    public int MaxTokens { get; set; } = 128000;

    /// <summary>
    /// Add message to context
    /// </summary>
    public void AddMessage(HazinaChatMessage message)
    {
        Messages.Add(message);
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Get messages within token limit
    /// </summary>
    public List<HazinaChatMessage> GetMessages(int maxTokens)
    {
        // Simple implementation - take most recent messages
        // Could be enhanced with smarter selection/summarization
        var result = new List<HazinaChatMessage>();
        int estimatedTokens = 0;

        for (int i = Messages.Count - 1; i >= 0; i--)
        {
            var message = Messages[i];
            var messageTokens = EstimateTokens(message.Text);

            if (estimatedTokens + messageTokens > maxTokens)
                break;

            result.Insert(0, message);
            estimatedTokens += messageTokens;
        }

        return result;
    }

    /// <summary>
    /// Clear old messages
    /// </summary>
    public void ClearOldMessages(int keepLast)
    {
        if (Messages.Count > keepLast)
        {
            Messages = Messages.Skip(Messages.Count - keepLast).ToList();
        }
    }

    /// <summary>
    /// Simple token estimation (4 chars ≈ 1 token)
    /// </summary>
    private int EstimateTokens(string text)
    {
        return string.IsNullOrEmpty(text) ? 0 : text.Length / 4;
    }

    /// <summary>
    /// Get system messages
    /// </summary>
    public List<HazinaChatMessage> GetSystemMessages()
    {
        return Messages.Where(m => m.Role == HazinaMessageRole.System).ToList();
    }

    /// <summary>
    /// Get user messages
    /// </summary>
    public List<HazinaChatMessage> GetUserMessages()
    {
        return Messages.Where(m => m.Role == HazinaMessageRole.User).ToList();
    }

    /// <summary>
    /// Get assistant messages
    /// </summary>
    public List<HazinaChatMessage> GetAssistantMessages()
    {
        return Messages.Where(m => m.Role == HazinaMessageRole.Assistant).ToList();
    }
}
