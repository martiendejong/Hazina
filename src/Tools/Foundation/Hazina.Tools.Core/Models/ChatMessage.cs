using System;
using DevGPT.GenerationTools.Models;
using DevGPT.GenerationTools.Models.WordPress.Blogs;

namespace DevGPTStore.Core.Models
{
    /// <summary>
    /// Represents a single message in a chat conversation
    /// </summary>
    public class ChatMessage : Serializer<ChatMessage>
    {
        /// <summary>
        /// Role of the message sender (user, assistant, system)
        /// </summary>
        public string Role { get; set; }

        /// <summary>
        /// Text content of the message
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Timestamp when the message was created
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Optional metadata (e.g., file uploads, canvas edits)
        /// </summary>
        public string Metadata { get; set; }
    }
}
