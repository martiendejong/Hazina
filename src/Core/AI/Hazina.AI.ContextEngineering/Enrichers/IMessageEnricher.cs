using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.AI.ContextEngineering.Enrichers;

/// <summary>
/// Interface for message enrichers that add context to LLM requests
/// </summary>
public interface IMessageEnricher
{
    /// <summary>
    /// Priority order for enricher execution (lower numbers execute first)
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Unique name for this enricher
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Enriches the message context with additional information
    /// </summary>
    /// <param name="context">Current message context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Enriched context</returns>
    Task<MessageContext> EnrichAsync(MessageContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Context object that flows through the enricher pipeline
/// </summary>
public class MessageContext
{
    /// <summary>
    /// User's input message
    /// </summary>
    public string UserMessage { get; set; } = string.Empty;

    /// <summary>
    /// System prompt
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// Conversation history messages
    /// </summary>
    public List<ChatMessage> History { get; set; } = new();

    /// <summary>
    /// Retrieved documents from RAG
    /// </summary>
    public List<Document> Documents { get; set; } = new();

    /// <summary>
    /// File list context
    /// </summary>
    public List<string> Files { get; set; } = new();

    /// <summary>
    /// Additional stores/databases context
    /// </summary>
    public Dictionary<string, object> Stores { get; set; } = new();

    /// <summary>
    /// Custom metadata that enrichers can add
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Project or session identifier
    /// </summary>
    public string? ProjectId { get; set; }

    /// <summary>
    /// Correlation ID for tracing
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Estimated token count
    /// </summary>
    public int EstimatedTokens { get; set; }

    /// <summary>
    /// Maximum token budget
    /// </summary>
    public int? MaxTokens { get; set; }
}

/// <summary>
/// Represents a chat message in history
/// </summary>
public class ChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Represents a document from RAG or stores
/// </summary>
public class Document
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
    public double Score { get; set; }
}
