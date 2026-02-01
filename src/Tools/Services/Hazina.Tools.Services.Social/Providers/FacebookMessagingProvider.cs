using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Social.Abstractions;

namespace Hazina.Tools.Services.Social.Providers;

/// <summary>
/// Facebook/Instagram messaging provider implementation.
/// Supports conversation and message retrieval via Facebook Graph API.
/// </summary>
public class FacebookMessagingProvider : ISocialMessagingProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FacebookMessagingProvider> _logger;
    private readonly string _appId;
    private readonly string _appSecret;

    private const string GraphApiBaseUrl = "https://graph.facebook.com/v18.0";

    public string ProviderId => "facebook";
    public string DisplayName => "Facebook Messenger";

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

    public async Task<ProviderResult<List<PlatformConversation>>> GetConversationsAsync(
        string accessToken,
        MessagingOptions options,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get conversations from Facebook Graph API
            var url = $"{GraphApiBaseUrl}/me/conversations?fields=id,participants,updated_time,unread_count,snippet&limit={options.MaxItems}&access_token={accessToken}";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Facebook conversations fetch failed: {Response}", json);
                return new ProviderResult<List<PlatformConversation>>
                {
                    Success = false,
                    Error = $"Failed to fetch conversations: {response.StatusCode}"
                };
            }

            var result = JsonSerializer.Deserialize<FacebookConversationsResponse>(json);
            var conversations = new List<PlatformConversation>();

            if (result?.data != null)
            {
                foreach (var conv in result.data)
                {
                    var conversation = new PlatformConversation
                    {
                        Id = conv.id ?? "",
                        LastMessagePreview = conv.snippet,
                        LastMessageAt = ParseFacebookDate(conv.updated_time),
                        UnreadCount = conv.unread_count ?? 0,
                        IsUnread = (conv.unread_count ?? 0) > 0,
                        Participants = conv.participants?.data?.Select(p => new ConversationParticipant
                        {
                            Id = p.id ?? "",
                            Name = p.name ?? "Unknown"
                        }).ToList() ?? new List<ConversationParticipant>()
                    };
                    conversations.Add(conversation);
                }
            }

            return new ProviderResult<List<PlatformConversation>>
            {
                Success = true,
                Data = conversations
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Facebook conversations");
            return new ProviderResult<List<PlatformConversation>>
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    public async Task<ProviderResult<List<PlatformMessage>>> GetMessagesAsync(
        string accessToken,
        string conversationId,
        MessagingOptions options,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{GraphApiBaseUrl}/{conversationId}/messages?fields=id,message,from,created_time,attachments&limit={options.MaxItems}&access_token={accessToken}";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Facebook messages fetch failed: {Response}", json);
                return new ProviderResult<List<PlatformMessage>>
                {
                    Success = false,
                    Error = $"Failed to fetch messages: {response.StatusCode}"
                };
            }

            var result = JsonSerializer.Deserialize<FacebookMessagesResponse>(json);
            var messages = new List<PlatformMessage>();

            if (result?.data != null)
            {
                foreach (var msg in result.data)
                {
                    var message = new PlatformMessage
                    {
                        Id = msg.id ?? "",
                        ConversationId = conversationId,
                        SenderId = msg.from?.id ?? "",
                        SenderName = msg.from?.name ?? "Unknown",
                        Content = msg.message ?? "",
                        SentAt = ParseFacebookDate(msg.created_time) ?? DateTime.UtcNow,
                        IsFromMe = false, // Would need to compare with page ID
                        IsRead = true,
                        Attachments = msg.attachments?.data?.Select(a => new MessageAttachment
                        {
                            Type = a.mime_type?.StartsWith("image") == true ? "image" : 
                                   a.mime_type?.StartsWith("video") == true ? "video" : "file",
                            Url = a.image_data?.url ?? a.file_url ?? "",
                            Name = a.name,
                            MimeType = a.mime_type
                        }).ToList() ?? new List<MessageAttachment>()
                    };
                    messages.Add(message);
                }
            }

            return new ProviderResult<List<PlatformMessage>>
            {
                Success = true,
                Data = messages
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Facebook messages for conversation {ConversationId}", conversationId);
            return new ProviderResult<List<PlatformMessage>>
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    public async Task<ProviderResult<PlatformMessage>> SendMessageAsync(
        string accessToken,
        SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{GraphApiBaseUrl}/{request.ConversationId}/messages";

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["message"] = request.Content,
                ["access_token"] = accessToken
            });

            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Facebook send message failed: {Response}", json);
                return new ProviderResult<PlatformMessage>
                {
                    Success = false,
                    Error = $"Failed to send message: {response.StatusCode}"
                };
            }

            var result = JsonSerializer.Deserialize<FacebookSendMessageResponse>(json);

            return new ProviderResult<PlatformMessage>
            {
                Success = true,
                Data = new PlatformMessage
                {
                    Id = result?.id ?? "",
                    ConversationId = request.ConversationId,
                    Content = request.Content,
                    SentAt = DateTime.UtcNow,
                    IsFromMe = true
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Facebook message");
            return new ProviderResult<PlatformMessage>
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private static DateTime? ParseFacebookDate(string? dateString)
    {
        if (string.IsNullOrEmpty(dateString)) return null;
        return DateTime.TryParse(dateString, out var date) ? date : null;
    }

    // Facebook API response classes
    private class FacebookConversationsResponse
    {
        public List<FacebookConversation>? data { get; set; }
    }

    private class FacebookConversation
    {
        public string? id { get; set; }
        public string? snippet { get; set; }
        public string? updated_time { get; set; }
        public int? unread_count { get; set; }
        public FacebookParticipantsData? participants { get; set; }
    }

    private class FacebookParticipantsData
    {
        public List<FacebookParticipant>? data { get; set; }
    }

    private class FacebookParticipant
    {
        public string? id { get; set; }
        public string? name { get; set; }
    }

    private class FacebookMessagesResponse
    {
        public List<FacebookMessage>? data { get; set; }
    }

    private class FacebookMessage
    {
        public string? id { get; set; }
        public string? message { get; set; }
        public FacebookFrom? from { get; set; }
        public string? created_time { get; set; }
        public FacebookAttachmentsData? attachments { get; set; }
    }

    private class FacebookFrom
    {
        public string? id { get; set; }
        public string? name { get; set; }
    }

    private class FacebookAttachmentsData
    {
        public List<FacebookAttachment>? data { get; set; }
    }

    private class FacebookAttachment
    {
        public string? name { get; set; }
        public string? mime_type { get; set; }
        public string? file_url { get; set; }
        public FacebookImageData? image_data { get; set; }
    }

    private class FacebookImageData
    {
        public string? url { get; set; }
    }

    private class FacebookSendMessageResponse
    {
        public string? id { get; set; }
    }
}
