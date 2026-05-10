namespace Hazina.Terminal.ChatAgent;

/// <summary>
/// Configuration for the Terminal Chat Agent.
/// </summary>
public class ChatConfig
{
    /// <summary>
    /// LLM provider ID to use (e.g., "openai", "claude", "gemini").
    /// If null/empty, uses the default provider from Hazina configuration.
    /// </summary>
    public string? ProviderId { get; set; }

    /// <summary>
    /// Maximum number of messages to retain in context window.
    /// </summary>
    public int MaxContextMessages { get; set; } = 10;

    /// <summary>
    /// System prompt prepended to all conversations.
    /// </summary>
    public string SystemPrompt { get; set; } =
        "You are a helpful assistant for Hazina orchestration tasks. " +
        "You can help users understand and work with the Hazina framework.";

    /// <summary>
    /// Whether to display token usage information after each response.
    /// </summary>
    public bool ShowTokenUsage { get; set; } = true;
}
