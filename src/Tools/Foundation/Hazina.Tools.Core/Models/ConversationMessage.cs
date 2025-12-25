using DevGPT.GenerationTools.Models;
using DevGPT.GenerationTools.Models.WordPress.Blogs;
using System.Text.Json;

public class ConversationMessage : Serializer<ConversationMessage>
{
    public ChatMessageRole Role { get; set; }
    public string? Text { get; set; }
    public dynamic? Payload { get; set; }
    public DevGPTChatMessage ToChatMessage()
    {
        var isPayloadNull = Payload is null || (Payload is JsonElement je && je.ValueKind == JsonValueKind.Undefined);

        var content = isPayloadNull ? Text : Payload.ToString();

        if (Role == ChatMessageRole.User) return new DevGPTChatMessage(DevGPTMessageRole.User, content);
        if (Role == ChatMessageRole.Assistant) return new DevGPTChatMessage(DevGPTMessageRole.Assistant, content);
        if (Role == ChatMessageRole.System) return new DevGPTChatMessage(DevGPTMessageRole.System, content);
        return new DevGPTChatMessage(DevGPTMessageRole.Assistant, content);
    }
}
