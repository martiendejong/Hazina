using Hazina.Tools.Models;
using Hazina.Tools.Models.WordPress.Blogs;
using System.Text.Json;

public class ConversationMessage : Serializer<ConversationMessage>
{
    public ChatMessageRole Role { get; set; }
    public string? Text { get; set; }
    public dynamic? Payload { get; set; }
    public HazinaChatMessage ToChatMessage()
    {
        var isPayloadNull = Payload is null || (Payload is JsonElement je && je.ValueKind == JsonValueKind.Undefined);

        var content = isPayloadNull ? Text : Payload.ToString();

        if (Role == ChatMessageRole.User) return new HazinaChatMessage(HazinaMessageRole.User, content);
        if (Role == ChatMessageRole.Assistant) return new HazinaChatMessage(HazinaMessageRole.Assistant, content);
        if (Role == ChatMessageRole.System) return new HazinaChatMessage(HazinaMessageRole.System, content);
        return new HazinaChatMessage(HazinaMessageRole.Assistant, content);
    }
}
