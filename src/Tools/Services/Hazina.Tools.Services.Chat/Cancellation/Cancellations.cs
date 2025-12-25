using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Hazina.Tools.Services.Chat
{
    public class Cancellations
    {
        public List<ProjectCancellation> ProjectCancellations { get; set; } = new List<ProjectCancellation>();

        public void Add(string projectId, string chatId, CancellationTokenSource cancel)
        {
            var projectCancellation = ProjectCancellations.FirstOrDefault(p => p.ProjectId == projectId);
            if (projectCancellation == null)
            {
                projectCancellation = new ProjectCancellation() { ProjectId = projectId };
                ProjectCancellations.Add(projectCancellation);
            }
            var chat = projectCancellation.Chats.FirstOrDefault();
            if (chat == null)
            {
                chat = new ProjectChatCancellation() { ChatId = chatId };
                projectCancellation.Chats.Add(chat);
            }
            chat.Cancels.Add(cancel);
        }

        public void Remove(string projectId, string chatId, CancellationTokenSource cancel)
        {
            var projectCancellation = ProjectCancellations.FirstOrDefault(p => p.ProjectId == projectId);
            if (projectCancellation == null) return;
            var chat = projectCancellation.Chats.FirstOrDefault();
            if (chat == null) return;
            if (chat.Cancels.Contains(cancel)) chat.Cancels.Remove(cancel);
        }

        public bool Cancel(string projectId, string chatId)
        {
            var projectCancellation = ProjectCancellations.FirstOrDefault(p => p.ProjectId == projectId);
            if (projectCancellation == null) return false;
            var chat = projectCancellation.Chats.FirstOrDefault();
            if (chat == null) return false;
            chat.Cancel();
            projectCancellation.Chats.Remove(chat);
            return true;
        }
    }
}

