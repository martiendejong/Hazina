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
        /// <summary>
        /// Gets or sets the role of the message sender.
        /// </summary>
        public HazinaMessageRole Role { get; set; }

        /// <summary>
        /// Gets or sets the content of the message.
        /// </summary>
        public string Content { get; set; } = "";

        /// <summary>
        /// Initializes a new instance of the HazinaChatMessage class.
        /// </summary>
        public HazinaChatMessage()
        {
        }

        /// <summary>
        /// Initializes a new instance of the HazinaChatMessage class with the specified role and content.
        /// </summary>
        /// <param name="role">The role of the message sender.</param>
        /// <param name="content">The content of the message.</param>
        public HazinaChatMessage(HazinaMessageRole role, string content)
        {
            Role = role;
            Content = content;
        }
    }
}
