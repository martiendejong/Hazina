using System.Collections.Generic;

namespace DevGPT.GenerationTools.Services.Chat
{
    public class ProjectCancellation
    {
        public string ProjectId { get; set; }
        public List<ProjectChatCancellation> Chats { get; set; } = new List<ProjectChatCancellation>();
    }
}

