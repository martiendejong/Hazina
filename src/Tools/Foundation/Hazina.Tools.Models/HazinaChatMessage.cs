namespace Hazina.Tools.Models
{
    /// <summary>
    /// Represents the role of a message sender in a chat conversation
    /// </summary>
    public enum HazinaMessageRole
    {
        System,
        User,
        Assistant
    }

    /// <summary>
    /// Represents a chat message with role and content
    /// </summary>
    public class HazinaChatMessage
    {
        public HazinaMessageRole Role { get; set; }
        public string Content { get; set; }

        public HazinaChatMessage()
        {
        }

        public HazinaChatMessage(HazinaMessageRole role, string content)
        {
            Role = role;
            Content = content;
        }
    }
}
