using Hazina.Tools.AI.Agents;
using Hazina.Tools.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hazina.Tools.Models;

namespace Hazina.Tools.Services.Chat
{
    public class ChatCanvasService : ChatServiceBase, IChatCanvasService
    {
        private readonly GeneratorAgentBase _agent;
        private readonly IntakeRepository _intake;
        private readonly IProjectChatNotifier _notifier;

        public ChatCanvasService(ProjectsRepository projects, ProjectFileLocator fileLocator, GeneratorAgentBase agent, IntakeRepository intake, IProjectChatNotifier notifier) : base(projects, fileLocator)
        {
            _agent = agent;
            _intake = intake;
            _notifier = notifier;
        }

        public string GetChatUploadsFolder(string projectId, string chatId) => base.GetChatUploadsFolder(projectId, chatId);
        public string GetChatUploadsFolder(string projectId, string chatId, string userId) => base.GetChatUploadsFolder(projectId, chatId, userId);

        public Task<ChatConversation> EditCanvasMessage(string projectId, string chatId, Project project, CanvasMessage message, CancellationToken cancel)
        {
            var convo = new ChatConversation
            {
                MetaData = new ChatMetadata { Id = chatId, Name = "Canvas" },
                ChatMessages = new SerializableList<ConversationMessage>(new[]
                {
                    new ConversationMessage { Role = ChatMessageRole.User, Text = message?.Text }
                })
            };
            return Task.FromResult(convo);
        }

        public Task<ChatConversation> EditCanvasMessage(string projectId, string chatId, string userId, Project project, CanvasMessage message, CancellationToken cancel)
        {
            return EditCanvasMessage(projectId, chatId, project, message, cancel);
        }
    }
}

