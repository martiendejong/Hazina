namespace Hazina.Terminal.ChatAgent;

/// <summary>
/// Manages conversation context with a rolling window of recent messages.
/// </summary>
public class ContextManager
{
    private readonly List<HazinaChatMessage> _messages;
    private readonly int _maxMessages;
    private readonly string _systemPrompt;

    /// <summary>
    /// Creates a new context manager with the specified maximum message count.
    /// </summary>
    /// <param name="maxMessages">Maximum number of messages to retain (default: 10)</param>
    /// <param name="systemPrompt">System prompt to prepend to conversations</param>
    public ContextManager(int maxMessages = 10, string? systemPrompt = null)
    {
        _maxMessages = maxMessages;
        _systemPrompt = systemPrompt ?? "You are a helpful assistant for Hazina orchestration tasks. " +
                                        "You can help users understand and work with the Hazina framework.";
        _messages = new List<HazinaChatMessage>();
    }

    /// <summary>
    /// Adds a user message to the context.
    /// </summary>
    public void AddUserMessage(string text)
    {
        _messages.Add(new HazinaChatMessage(HazinaMessageRole.User, text));
        TrimContext();
    }

    /// <summary>
    /// Adds an assistant message to the context.
    /// </summary>
    public void AddAssistantMessage(string text)
    {
        _messages.Add(new HazinaChatMessage(HazinaMessageRole.Assistant, text));
        TrimContext();
    }

    /// <summary>
    /// Gets all messages including the system prompt.
    /// </summary>
    public List<HazinaChatMessage> GetMessages()
    {
        var messages = new List<HazinaChatMessage>
        {
            new HazinaChatMessage(HazinaMessageRole.System, _systemPrompt)
        };
        messages.AddRange(_messages);
        return messages;
    }

    /// <summary>
    /// Clears all messages from the context.
    /// </summary>
    public void Clear()
    {
        _messages.Clear();
    }

    /// <summary>
    /// Gets the current message count (excluding system prompt).
    /// </summary>
    public int Count => _messages.Count;

    /// <summary>
    /// Trims the context to the maximum message count, keeping the most recent messages.
    /// </summary>
    private void TrimContext()
    {
        while (_messages.Count > _maxMessages)
        {
            _messages.RemoveAt(0);
        }
    }
}
