using System.Text.Json;
using OpenAI.Chat;
using OpenAI.Images;

public static class HazinaOpenAIExtensions
{
    public static HazinaGeneratedImage Hazina(this GeneratedImage image)
    {
        return new(image.ImageUri, image.ImageBytes);
    }

    public static HazinaChatToolCall Hazina(this ChatToolCall chatTool)
    {
        return new HazinaChatToolCall(chatTool.Id, chatTool.FunctionName, chatTool.FunctionArguments);
    }

    public static ChatResponseFormat OpenAI(this HazinaChatResponseFormat format) {
        // Be permissive: default null or unknown formats to text to avoid hard crashes
        if (format == null) return ChatResponseFormat.CreateTextFormat();
        if (format == HazinaChatResponseFormat.Text) return ChatResponseFormat.CreateTextFormat();
        if (format == HazinaChatResponseFormat.Json) return ChatResponseFormat.CreateJsonObjectFormat();
        // Accept custom format values like "images" by treating them as text prompts for OpenAI
        try { if (string.Equals(format.Format, "images", StringComparison.OrdinalIgnoreCase)) return ChatResponseFormat.CreateTextFormat(); } catch { }
        throw new Exception("HazinaChatResponseFormat not recognized");
    }
    public static ChatMessage OpenAI(this HazinaChatMessage message)
    {
        if (message.Role == HazinaMessageRole.User || message.Role.Role == HazinaMessageRole.User.Role) return new UserChatMessage(message.Text);
        if (message.Role == HazinaMessageRole.Assistant || message.Role.Role == HazinaMessageRole.Assistant.Role) return new AssistantChatMessage(message.Text);
        if (message.Role == HazinaMessageRole.System || message.Role.Role == HazinaMessageRole.System.Role) return new SystemChatMessage(message.Text);
        throw new Exception("HazinaMessageRole not recognized");
    }
    public static HazinaChatMessage? Hazina(this ChatMessage message)
    {
        if (message is UserChatMessage) return new HazinaChatMessage() { Role = HazinaMessageRole.User, Text = message.Content.First().Text };
        if (message is AssistantChatMessage)
        {
            if(message.Content.Any())
                return new HazinaChatMessage() { Role = HazinaMessageRole.Assistant, Text = message.Content.First().Text };
            return null; // tool calls, todo check if this is right
        }
        if (message is SystemChatMessage) return new HazinaChatMessage() { Role = HazinaMessageRole.System, Text = message.Content.First().Text };
        if (message is ToolChatMessage)
            return null;
        throw new Exception("HazinaMessageRole not recognized");
    }

    public static List<ChatMessage> OpenAI(this List<HazinaChatMessage> messages)
    {
        return messages.Select(m => m.OpenAI()).ToList();
    }

    public static List<HazinaChatMessage> Hazina(this List<ChatMessage> messages)
    {
        return messages.Select(m => m.Hazina()).Where(m => m != null).Select(m => m ?? new HazinaChatMessage()).ToList();
    }

    public static ChatTool OpenAI(this HazinaChatTool chatTool)
    {
        return CreateDefinitionOpenAI(chatTool.FunctionName, chatTool.Description, chatTool.Parameters);
    }


    public static ChatTool CreateDefinitionOpenAI(string name, string description, List<ChatToolParameter> parameters)
    {
        var d = ChatTool.CreateFunctionTool(
            functionName: name,
            functionDescription: description,
            functionParameters: GenerateFunctionParameters(parameters));
        return d;
    }

    public static BinaryData GenerateFunctionParameters(List<ChatToolParameter> parameters)
    {
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var prop in parameters)
        {
            properties[prop.Name] = new Dictionary<string, object>
            {
                ["type"] = prop.Type,
                ["description"] = prop.Description
            };

            if (string.Equals(prop.Type, "array", StringComparison.OrdinalIgnoreCase))
            {
                ((Dictionary<string, object>)properties[prop.Name])["items"] = new Dictionary<string, object>
                {
                    ["type"] = string.IsNullOrWhiteSpace(prop.ItemsType) ? "string" : prop.ItemsType
                };
            }

            if (prop.Required)
                required.Add(prop.Name);
        }

        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required
        };

        string json = JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = true });
        return BinaryData.FromString(json);
    }
}
