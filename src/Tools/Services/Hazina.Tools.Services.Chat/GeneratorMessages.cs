using System;
using System.Collections.Generic;

namespace Hazina.Tools.Services.Chat
{
    public class GeneratorMessage
    {
        public string Message { get; set; }

        /// <summary>
        /// Original user message text for display in chat.
        /// If set, this is saved to chat history instead of Message.
        /// Message field is used for LLM (may contain additional context).
        /// </summary>
        public string? OriginalMessage { get; set; }

        public Dictionary<string, object>? Metadata { get; set; }
        public List<ChatAttachment>? Attachments { get; set; }
    }

    public class GeneratorMessageForDate
    {
        public string Message { get; set; }
        public DateTime Date { get; set; }
    }

    public class GeneratorMessageForDateAndEvent
    {
        public string Message { get; set; }
        public DateTime Date { get; set; }
        public string Event { get; set; }
    }
}

