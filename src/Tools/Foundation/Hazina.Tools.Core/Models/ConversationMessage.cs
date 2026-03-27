using Hazina.Tools.Models;
using Hazina.Tools.Models.WordPress.Blogs;
using System.Text.Json;
using System.Collections.Generic;

public class ConversationMessage : Serializer<ConversationMessage>
{
    public ChatMessageRole Role { get; set; }
    public string? Text { get; set; }
    public dynamic? Payload { get; set; }

    // STAP 6: New optional properties for enhanced chat experience (backwards compatible)
    /// <summary>
    /// Type of message (Text, GuidanceQuestion, InputGuidance, SystemStatus, Artifact, SideEffect)
    /// </summary>
    public string? MessageType { get; set; }

    /// <summary>
    /// Classified user intent (Query, Command, Feedback, Clarification)
    /// </summary>
    public string? UserIntent { get; set; }

    /// <summary>
    /// Guidance data for interactive components (stored as JSON)
    /// </summary>
    public dynamic? Guidance { get; set; }

    /// <summary>
    /// Status information for system feedback
    /// </summary>
    public dynamic? Status { get; set; }

    /// <summary>
    /// Side effects of tool execution
    /// </summary>
    public List<dynamic>? SideEffects { get; set; }

    public HazinaChatMessage ToChatMessage()
    {
        var isPayloadNull = Payload is null || (Payload is JsonElement je && je.ValueKind == JsonValueKind.Undefined);

        var content = isPayloadNull ? Text : Payload!.ToString();

        if (Role == ChatMessageRole.User) return new HazinaChatMessage(HazinaMessageRole.User, content);
        if (Role == ChatMessageRole.System) return new HazinaChatMessage(HazinaMessageRole.System, content);
        return new HazinaChatMessage(HazinaMessageRole.Assistant, content);
    }

    public List<ChatAttachment>? Attachments { get; set; }
}

public class ChatAttachment
{
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? FileRecordId { get; set; }
    public string? Thumbnail { get; set; }
}
