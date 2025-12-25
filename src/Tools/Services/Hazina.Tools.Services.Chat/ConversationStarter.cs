using System.Collections.Generic;

namespace DevGPT.GenerationTools.Services.Chat
{
    public class ConversationStarter
    {
        public string Name { get; set; }
        public List<ConversationStarterQuestion> Questions { get; set; }
        public List<ConversationStarter>? Children { get; set; }
        public ConversationStarter? Parent { get; set; }
        public string Prompt { get; set; }
        public List<string>? Roles { get; set; }
    }
}

