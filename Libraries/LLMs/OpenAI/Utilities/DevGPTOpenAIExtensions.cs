using System.Text.Json;
using OpenAI.Chat;
using OpenAI.Images;

public static class DevGPTOpenAIExtensions
{
    public static DevGPTGeneratedImage DevGPT(this GeneratedImage image)
    {
        return new(image.ImageUri, image.ImageBytes);
    }

    public static DevGPTChatToolCall DevGPT(this ChatToolCall chatTool)
    {
        return new DevGPTChatToolCall(chatTool.Id, chatTool.FunctionName, chatTool.FunctionArguments);
    }

    public static ChatResponseFormat OpenAI(this DevGPTChatResponseFormat format) {
        // Be permissive: default null or unknown formats to text to avoid hard crashes
        if (format == null) return ChatResponseFormat.CreateTextFormat();
        if (format == DevGPTChatResponseFormat.Text) return ChatResponseFormat.CreateTextFormat();
        if (format == DevGPTChatResponseFormat.Json) return ChatResponseFormat.CreateJsonObjectFormat();
        // Accept custom format values like "images" by treating them as text prompts for OpenAI
        try { if (string.Equals(format.Format, "images", StringComparison.OrdinalIgnoreCase)) return ChatResponseFormat.CreateTextFormat(); } catch { }
        throw new Exception("DevGPTChatResponseFormat not recognized");
    }
    public static ChatMessage OpenAI(this DevGPTChatMessage message) 
    {
        if (message.Role == DevGPTMessageRole.User || message.Role.Role == DevGPTMessageRole.User.Role) return new UserChatMessage(message.Text);
        if (message.Role == DevGPTMessageRole.Assistant || message.Role.Role == DevGPTMessageRole.Assistant.Role) return new AssistantChatMessage(message.Text);
        if (message.Role == DevGPTMessageRole.System || message.Role.Role == DevGPTMessageRole.System.Role) return new SystemChatMessage(message.Text);
        throw new Exception("DevGPTMessageRole not recognized");
    }
    public static DevGPTChatMessage? DevGPT(this ChatMessage message)
    {
        if (message is UserChatMessage) return new DevGPTChatMessage() { Role = DevGPTMessageRole.User, Text = message.Content.First().Text };
        if (message is AssistantChatMessage)
        {
            if(message.Content.Any())
                return new DevGPTChatMessage() { Role = DevGPTMessageRole.Assistant, Text = message.Content.First().Text };
            return null; // tool calls, todo check if this is right
        }
        if (message is SystemChatMessage) return new DevGPTChatMessage() { Role = DevGPTMessageRole.System, Text = message.Content.First().Text };
        if (message is ToolChatMessage)
            return null;
        throw new Exception("DevGPTMessageRole not recognized");
    }

    public static List<ChatMessage> OpenAI(this List<DevGPTChatMessage> messages)
    {
        return messages.Select(m => m.OpenAI()).ToList();
    }

    public static List<DevGPTChatMessage> DevGPT(this List<ChatMessage> messages)
    {
        return messages.Select(m => m.DevGPT()).Where(m => m != null).Select(m => m ?? new DevGPTChatMessage()).ToList();
    }

    public static ChatTool OpenAI(this DevGPTChatTool chatTool)
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
