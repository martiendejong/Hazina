namespace DevGPT.GenerationTools.Models
{
    /// <summary>
    /// Represents the role of a message sender in a chat conversation
    /// </summary>
    public enum DevGPTMessageRole
    {
        System,
        User,
        Assistant
    }

    /// <summary>
    /// Represents a chat message with role and content
    /// </summary>
    public class DevGPTChatMessage
    {
        public DevGPTMessageRole Role { get; set; }
        public string Content { get; set; }

        public DevGPTChatMessage()
        {
        }

        public DevGPTChatMessage(DevGPTMessageRole role, string content)
        {
            Role = role;
            Content = content;
        }
    }
}
