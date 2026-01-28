using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Social.Abstractions;

namespace Hazina.Tools.Services.Social.Providers;

/// <summary>
/// Facebook Pages messaging provider implementation.
/// Supports reading inbox conversations, sending messages, and marking as read.
/// Requires: pages_messaging, read_page_mailboxes, pages_manage_metadata permissions.
/// </summary>
public class FacebookMessagingProvider : ISocialMessagingProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FacebookMessagingProvider> _logger;
    private readonly string _appId;
    private readonly string _appSecret;

    private const string ApiBaseUrl = "https://graph.facebook.com/v18.0";

    public string ProviderId => "facebook";
    public string DisplayName => "Facebook";

    public FacebookMessagingProvider(
        HttpClient httpClient,
        ILogger<FacebookMessagingProvider> logger,
        string appId,
        string appSecret)
    {
        _httpClient = httpClient;
        _logger = logger;
        _appId = appId;
        _appSecret = appSecret;
    }

    public async Task<bool> ValidateMessagingAccessAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Verify access token by attempting to get pages
            var url = $"{ApiBaseUrl}/me/accounts?access_token={accessToken}";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Facebook messaging access validation failed: {StatusCode}", response.StatusCode);
                return false;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var pagesResponse = JsonSerializer.Deserialize<FacebookPagesResponse>(json);

            if (pagesResponse?.data == null || !pagesResponse.data.Any())
            {
                _logger.LogWarning("No Facebook pages found for this access token");
                return false;
            }

            // Check if at least one page has messaging capability
            // (In production, you'd verify specific permissions via /me/permissions endpoint)
            _logger.LogInformation("Facebook messaging access validated for {PageCount} pages", pagesResponse.data.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating Facebook messaging access");
            return false;
        }
    }

    public async Task<MessagingResult<List<Conversation>>> GetConversationsAsync(
        string accessToken,
        MessagingOptions options,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // First, get user's pages (Facebook Pages messaging, not personal Messenger)
            var pages = await GetUserPagesAsync(accessToken, cancellationToken);

            if (!pages.Any())
            {
                return new MessagingResult<List<Conversation>>
                {
                    Success = false,
                    Error = "No Facebook Pages found for this account",
                    ErrorCode = "no_pages"
                };
            }

            // Aggregate conversations from all pages
            var allConversations = new List<Conversation>();

            foreach (var page in pages)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var pageConversations = await GetPageConversationsAsync(
                    page.access_token ?? accessToken,
                    page.id,
                    options,
                    cancellationToken);

                allConversations.AddRange(pageConversations);
            }

            // Apply filters and pagination
            if (options.UnreadOnly)
            {
                allConversations = allConversations.Where(c => c.IsUnread).ToList();
            }

            allConversations = allConversations
                .OrderByDescending(c => c.LastMessageAt)
                .Take(options.MaxItems)
                .ToList();

            return new MessagingResult<List<Conversation>>
            {
                Success = true,
                Data = allConversations,
                TotalCount = allConversations.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Facebook conversations");
            return new MessagingResult<List<Conversation>>
            {
                Success = false,
                Error = ex.Message,
                ErrorCode = "fetch_failed"
            };
        }
    }

    public async Task<MessagingResult<List<Message>>> GetMessagesAsync(
        string accessToken,
        string conversationId,
        MessagingOptions options,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var limit = Math.Min(options.MaxItems, 100);
            var url = $"{ApiBaseUrl}/{conversationId}/messages?fields=id,created_time,from,message,attachments&limit={limit}&access_token={accessToken}";

            if (options.Since.HasValue)
            {
                var sinceTimestamp = new DateTimeOffset(options.Since.Value).ToUnixTimeSeconds();
                url += $"&since={sinceTimestamp}";
            }

            var response = await _httpClient.GetAsync(url, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Facebook messages fetch failed for conversation {ConversationId}: {Response}",
                    conversationId, json);
                return new MessagingResult<List<Message>>
                {
                    Success = false,
                    Error = $"API request failed: {response.StatusCode}",
                    ErrorCode = "api_error"
                };
            }

            var messagesResponse = JsonSerializer.Deserialize<FacebookMessagesResponse>(json);
            if (messagesResponse?.data == null)
            {
                return new MessagingResult<List<Message>>
                {
                    Success = true,
                    Data = new List<Message>()
                };
            }

            var messages = messagesResponse.data.Select(msg => new Message
            {
                Id = msg.id ?? "",
                ConversationId = conversationId,
                SenderId = msg.from?.id ?? "",
                SenderName = msg.from?.name ?? "",
                Content = msg.message ?? "",
                SentAt = DateTime.TryParse(msg.created_time, out var sentAt)
                    ? sentAt.ToUniversalTime()
                    : DateTime.UtcNow,
                IsFromMe = false, // Will be determined by comparing with page ID
                IsRead = true, // Assume read when fetching
                Attachments = msg.attachments?.data?.Select(a => new MessageAttachment
                {
                    Type = a.type ?? "unknown",
                    Url = a.image_data?.url ?? a.video_data?.url ?? a.file_url ?? "",
                    MimeType = a.mime_type
                }).ToList() ?? new List<MessageAttachment>()
            }).ToList();

            return new MessagingResult<List<Message>>
            {
                Success = true,
                Data = messages,
                TotalCount = messages.Count,
                ContinuationToken = messagesResponse.paging?.next
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Facebook messages for conversation {ConversationId}", conversationId);
            return new MessagingResult<List<Message>>
            {
                Success = false,
                Error = ex.Message,
                ErrorCode = "fetch_failed"
            };
        }
    }

    public async Task<MessagingResult<Message>> SendMessageAsync(
        string accessToken,
        SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(request.RecipientId) && string.IsNullOrEmpty(request.ConversationId))
            {
                return new MessagingResult<Message>
                {
                    Success = false,
                    Error = "Either RecipientId or ConversationId must be provided",
                    ErrorCode = "invalid_request"
                };
            }

            // Facebook Messenger Send API endpoint
            var url = $"{ApiBaseUrl}/me/messages?access_token={accessToken}";

            var payload = new
            {
                recipient = new { id = request.RecipientId },
                message = new
                {
                    text = request.Content,
                    // attachment support can be added here if needed
                },
                messaging_type = request.Options.GetValueOrDefault("messaging_type", "RESPONSE")
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Facebook send message failed: {Response}", json);
                return new MessagingResult<Message>
                {
                    Success = false,
                    Error = $"API request failed: {response.StatusCode}",
                    ErrorCode = "send_failed"
                };
            }

            var sendResponse = JsonSerializer.Deserialize<FacebookSendMessageResponse>(json);

            var sentMessage = new Message
            {
                Id = sendResponse?.message_id ?? "",
                ConversationId = request.ConversationId ?? "",
                SenderId = "me",
                SenderName = "Me",
                Content = request.Content,
                SentAt = DateTime.UtcNow,
                IsFromMe = true,
                IsRead = true
            };

            return new MessagingResult<Message>
            {
                Success = true,
                Data = sentMessage
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Facebook message");
            return new MessagingResult<Message>
            {
                Success = false,
                Error = ex.Message,
                ErrorCode = "send_failed"
            };
        }
    }

    public async Task<bool> MarkAsReadAsync(
        string accessToken,
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Mark conversation as read via Graph API
            var url = $"{ApiBaseUrl}/{conversationId}?mark_read=true&access_token={accessToken}";

            var response = await _httpClient.PostAsync(url, null, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Facebook mark as read failed for conversation {ConversationId}: {Response}",
                    conversationId, json);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking Facebook conversation {ConversationId} as read", conversationId);
            return false;
        }
    }

    #region Private Helper Methods

    private async Task<List<FacebookPage>> GetUserPagesAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        try
        {
            var url = $"{ApiBaseUrl}/me/accounts?access_token={accessToken}";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Facebook pages fetch failed: {Response}", json);
                return new List<FacebookPage>();
            }

            var pagesResponse = JsonSerializer.Deserialize<FacebookPagesResponse>(json);
            return pagesResponse?.data ?? new List<FacebookPage>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Facebook pages");
            return new List<FacebookPage>();
        }
    }

    private async Task<List<Conversation>> GetPageConversationsAsync(
        string pageAccessToken,
        string? pageId,
        MessagingOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            var limit = Math.Min(options.MaxItems, 100);
            var url = $"{ApiBaseUrl}/{pageId}/conversations?fields=id,link,updated_time,participants,messages{{message,created_time,from}}&limit={limit}&access_token={pageAccessToken}";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Facebook conversations fetch failed for page {PageId}: {Response}",
                    pageId, json);
                return new List<Conversation>();
            }

            var conversationsResponse = JsonSerializer.Deserialize<FacebookConversationsResponse>(json);
            if (conversationsResponse?.data == null)
            {
                return new List<Conversation>();
            }

            return conversationsResponse.data.Select(conv => new Conversation
            {
                Id = conv.id ?? "",
                Participants = conv.participants?.data?.Select(p => new Participant
                {
                    Id = p.id ?? "",
                    Name = p.name ?? "",
                    ProfileUrl = p.id != null ? $"https://www.facebook.com/{p.id}" : null
                }).ToList() ?? new List<Participant>(),
                LastMessagePreview = conv.messages?.data?.FirstOrDefault()?.message,
                LastMessageAt = DateTime.TryParse(conv.updated_time, out var updatedAt)
                    ? updatedAt.ToUniversalTime()
                    : null,
                IsUnread = conv.unread_count > 0,
                UnreadCount = conv.unread_count,
                Url = conv.link
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching conversations for Facebook page {PageId}", pageId);
            return new List<Conversation>();
        }
    }

    #endregion

    #region Response DTOs

    private class FacebookPagesResponse
    {
        public List<FacebookPage> data { get; set; } = new();
    }

    private class FacebookPage
    {
        public string? id { get; set; }
        public string? name { get; set; }
        public string? access_token { get; set; }
    }

    private class FacebookConversationsResponse
    {
        public List<FacebookConversation> data { get; set; } = new();
        public FacebookPaging? paging { get; set; }
    }

    private class FacebookConversation
    {
        public string? id { get; set; }
        public string? link { get; set; }
        public string? updated_time { get; set; }
        public int unread_count { get; set; }
        public FacebookParticipants? participants { get; set; }
        public FacebookMessages? messages { get; set; }
    }

    private class FacebookParticipants
    {
        public List<FacebookParticipant> data { get; set; } = new();
    }

    private class FacebookParticipant
    {
        public string? id { get; set; }
        public string? name { get; set; }
    }

    private class FacebookMessages
    {
        public List<FacebookMessage> data { get; set; } = new();
    }

    private class FacebookMessagesResponse
    {
        public List<FacebookMessage> data { get; set; } = new();
        public FacebookPaging? paging { get; set; }
    }

    private class FacebookMessage
    {
        public string? id { get; set; }
        public string? created_time { get; set; }
        public FacebookFrom? from { get; set; }
        public string? message { get; set; }
        public FacebookAttachments? attachments { get; set; }
    }

    private class FacebookFrom
    {
        public string? id { get; set; }
        public string? name { get; set; }
    }

    private class FacebookAttachments
    {
        public List<FacebookAttachment> data { get; set; } = new();
    }

    private class FacebookAttachment
    {
        public string? type { get; set; }
        public string? mime_type { get; set; }
        public string? file_url { get; set; }
        public FacebookImageData? image_data { get; set; }
        public FacebookVideoData? video_data { get; set; }
    }

    private class FacebookImageData
    {
        public string? url { get; set; }
    }

    private class FacebookVideoData
    {
        public string? url { get; set; }
    }

    private class FacebookPaging
    {
        public string? next { get; set; }
        public string? previous { get; set; }
    }

    private class FacebookSendMessageResponse
    {
        public string? recipient_id { get; set; }
        public string? message_id { get; set; }
    }

    #endregion
}
