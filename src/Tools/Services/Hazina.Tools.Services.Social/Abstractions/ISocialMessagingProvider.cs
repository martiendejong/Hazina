namespace Hazina.Tools.Services.Social.Abstractions;

/// <summary>
/// Interface for social media messaging providers.
/// Supports fetching conversations, messages, and sending messages.
/// </summary>
public interface ISocialMessagingProvider
{
    /// <summary>
    /// Provider identifier (e.g., "facebook", "instagram").
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Human-readable provider name.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets conversations for the authenticated user.
    /// </summary>
    Task<ProviderResult<List<PlatformConversation>>> GetConversationsAsync(
        string accessToken,
        MessagingOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets messages for a specific conversation.
    /// </summary>
    Task<ProviderResult<List<PlatformMessage>>> GetMessagesAsync(
        string accessToken,
        string conversationId,
        MessagingOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message in a conversation.
    /// </summary>
    Task<ProviderResult<PlatformMessage>> SendMessageAsync(
        string accessToken,
        SendMessageRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Options for messaging operations.
/// </summary>
public class MessagingOptions
{
    /// <summary>
    /// Maximum number of items to retrieve.
    /// </summary>
    public int MaxItems { get; set; } = 50;

    /// <summary>
    /// Retrieve items from after this date.
    /// </summary>
    public DateTime? Since { get; set; }

    /// <summary>
    /// Only retrieve unread items.
    /// </summary>
    public bool UnreadOnly { get; set; }
}

/// <summary>
/// Generic result wrapper for provider operations.
/// </summary>
public class ProviderResult<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }
    public string? ContinuationToken { get; set; }
}

/// <summary>
/// Represents a conversation from a social platform.
/// </summary>
public class PlatformConversation
{
    public string Id { get; set; } = "";
    public List<ConversationParticipant> Participants { get; set; } = new();
    public string? LastMessagePreview { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public bool IsUnread { get; set; }
    public int UnreadCount { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Represents a participant in a conversation.
/// </summary>
public class ConversationParticipant
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public bool IsMe { get; set; }
}

/// <summary>
/// Represents a message from a social platform.
/// </summary>
public class PlatformMessage
{
    public string Id { get; set; } = "";
    public string ConversationId { get; set; } = "";
    public string SenderId { get; set; } = "";
    public string SenderName { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime SentAt { get; set; }
    public bool IsFromMe { get; set; }
    public bool IsRead { get; set; }
    public List<MessageAttachment> Attachments { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Represents an attachment in a message.
/// </summary>
public class MessageAttachment
{
    public string Type { get; set; } = ""; // "image", "video", "file", "link"
    public string Url { get; set; } = "";
    public string? Name { get; set; }
    public string? MimeType { get; set; }
    public long? SizeBytes { get; set; }
}

/// <summary>
/// Request to send a message.
/// </summary>
public class SendMessageRequest
{
    public string ConversationId { get; set; } = "";
    public string Content { get; set; } = "";
    public List<string> AttachmentUrls { get; set; } = new();
}
