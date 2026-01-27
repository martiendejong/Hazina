namespace Hazina.Tools.Services.Social.Abstractions;

/// <summary>
/// Interface for managing social media messaging (inbox, direct messages).
/// Separate from ISocialProvider (import) and ISocialPublisher (posting) to keep concerns separated.
/// </summary>
public interface ISocialMessagingProvider
{
    /// <summary>
    /// Provider identifier (e.g., "facebook", "instagram").
    /// Must match the corresponding ISocialProvider.ProviderId.
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Human-readable provider name.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Validates that the access token has required messaging permissions.
    /// </summary>
    /// <param name="accessToken">OAuth access token</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if token has messaging permissions</returns>
    Task<bool> ValidateMessagingAccessAsync(
        string accessToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves conversations (inbox threads) from the platform.
    /// </summary>
    /// <param name="accessToken">OAuth access token</param>
    /// <param name="options">Options for filtering and pagination</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing conversations</returns>
    Task<MessagingResult<List<Conversation>>> GetConversationsAsync(
        string accessToken,
        MessagingOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves messages for a specific conversation.
    /// </summary>
    /// <param name="accessToken">OAuth access token</param>
    /// <param name="conversationId">Platform-specific conversation ID</param>
    /// <param name="options">Options for filtering and pagination</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing messages</returns>
    Task<MessagingResult<List<Message>>> GetMessagesAsync(
        string accessToken,
        string conversationId,
        MessagingOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message in a conversation.
    /// </summary>
    /// <param name="accessToken">OAuth access token</param>
    /// <param name="request">Message content and options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing sent message details</returns>
    Task<MessagingResult<Message>> SendMessageAsync(
        string accessToken,
        SendMessageRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a conversation as read.
    /// </summary>
    /// <param name="accessToken">OAuth access token</param>
    /// <param name="conversationId">Platform-specific conversation ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success status</returns>
    Task<bool> MarkAsReadAsync(
        string accessToken,
        string conversationId,
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
    /// Pagination token for fetching next page.
    /// </summary>
    public string? ContinuationToken { get; set; }

    /// <summary>
    /// Filter for unread conversations/messages only.
    /// </summary>
    public bool UnreadOnly { get; set; } = false;
}

/// <summary>
/// Generic result wrapper for messaging operations.
/// </summary>
public class MessagingResult<T>
{
    /// <summary>
    /// Whether the operation succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Result data (conversations, messages, etc.).
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// Error message if operation failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Error code for programmatic handling.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Pagination token for fetching next page.
    /// </summary>
    public string? ContinuationToken { get; set; }

    /// <summary>
    /// Total count (if available).
    /// </summary>
    public int? TotalCount { get; set; }
}

/// <summary>
/// Represents a messaging conversation (inbox thread).
/// </summary>
public class Conversation
{
    /// <summary>
    /// Platform-specific conversation ID.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// Participants in the conversation.
    /// </summary>
    public List<Participant> Participants { get; set; } = new();

    /// <summary>
    /// Preview of the last message.
    /// </summary>
    public string? LastMessagePreview { get; set; }

    /// <summary>
    /// When the last message was sent.
    /// </summary>
    public DateTime? LastMessageAt { get; set; }

    /// <summary>
    /// Whether the conversation has unread messages.
    /// </summary>
    public bool IsUnread { get; set; }

    /// <summary>
    /// Number of unread messages.
    /// </summary>
    public int UnreadCount { get; set; }

    /// <summary>
    /// Public URL to view the conversation (if available).
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Additional platform-specific metadata.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Represents a participant in a conversation.
/// </summary>
public class Participant
{
    /// <summary>
    /// Platform-specific user/page ID.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// Display name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Profile URL (if available).
    /// </summary>
    public string? ProfileUrl { get; set; }

    /// <summary>
    /// Avatar/profile picture URL (if available).
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Whether this participant is the authenticated user/page.
    /// </summary>
    public bool IsMe { get; set; }
}

/// <summary>
/// Represents a message in a conversation.
/// </summary>
public class Message
{
    /// <summary>
    /// Platform-specific message ID.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// Conversation this message belongs to.
    /// </summary>
    public string ConversationId { get; set; } = "";

    /// <summary>
    /// Platform-specific sender ID.
    /// </summary>
    public string SenderId { get; set; } = "";

    /// <summary>
    /// Sender display name.
    /// </summary>
    public string SenderName { get; set; } = "";

    /// <summary>
    /// Message content (text).
    /// </summary>
    public string Content { get; set; } = "";

    /// <summary>
    /// When the message was sent.
    /// </summary>
    public DateTime SentAt { get; set; }

    /// <summary>
    /// Whether the message was sent by the authenticated user/page.
    /// </summary>
    public bool IsFromMe { get; set; }

    /// <summary>
    /// Whether the message has been read.
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// Attachments (images, files, etc.).
    /// </summary>
    public List<MessageAttachment> Attachments { get; set; } = new();

    /// <summary>
    /// Additional platform-specific metadata.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Represents a file/media attachment in a message.
/// </summary>
public class MessageAttachment
{
    /// <summary>
    /// Attachment type (image, video, file, audio, etc.).
    /// </summary>
    public string Type { get; set; } = "";

    /// <summary>
    /// URL to access the attachment.
    /// </summary>
    public string Url { get; set; } = "";

    /// <summary>
    /// File name (if available).
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// MIME type (if available).
    /// </summary>
    public string? MimeType { get; set; }

    /// <summary>
    /// File size in bytes (if available).
    /// </summary>
    public long? SizeBytes { get; set; }
}

/// <summary>
/// Request to send a message in a conversation.
/// </summary>
public class SendMessageRequest
{
    /// <summary>
    /// Platform-specific conversation ID (for replies).
    /// </summary>
    public string? ConversationId { get; set; }

    /// <summary>
    /// Platform-specific recipient ID (for new conversations).
    /// </summary>
    public string? RecipientId { get; set; }

    /// <summary>
    /// Message text content.
    /// </summary>
    public string Content { get; set; } = "";

    /// <summary>
    /// Optional attachment URLs.
    /// </summary>
    public List<string> AttachmentUrls { get; set; } = new();

    /// <summary>
    /// Platform-specific options (e.g., messaging_type for Facebook).
    /// </summary>
    public Dictionary<string, string> Options { get; set; } = new();
}
