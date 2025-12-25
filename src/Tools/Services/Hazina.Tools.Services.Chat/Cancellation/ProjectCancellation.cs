using System.Collections.Generic;

namespace Hazina.Tools.Services.Chat
{
    public class ProjectCancellation
    {
        public string ProjectId { get; set; }
        public List<ProjectChatCancellation> Chats { get; set; } = new List<ProjectChatCancellation>();
    }
}

