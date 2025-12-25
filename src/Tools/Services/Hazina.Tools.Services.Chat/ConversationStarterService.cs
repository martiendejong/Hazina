using Hazina.Tools.Data;
using System.Threading;
using System.Threading.Tasks;
using Hazina.Tools.Models;

namespace Hazina.Tools.Services.Chat
{
    public class ConversationStarterService : ChatServiceBase, IConversationStarterService
    {
        public ConversationStarterService(ProjectsRepository projects, ProjectFileLocator fileLocator) : base(projects, fileLocator) { }

        public SerializableList<ConversationMessage> GetConversationStarterMessages(Project project, string chatId)
        {
            return new SerializableList<ConversationMessage>(new[]
            {
                new ConversationMessage { Role = ChatMessageRole.Assistant, Text = "Waar wil je het over hebben?" }
            });
        }

        public Task<SerializableList<ConversationMessage>> GenerateConversationStarter(Project project, string chatId, CancellationToken cancel)
        {
            return Task.FromResult(GetConversationStarterMessages(project, chatId));
        }

        public Task<ConversationStarter> GetConversationStarter(string projectId, string chatId, string userId = null)
        {
            var starter = new ConversationStarter
            {
                Name = "Start een gesprek",
                Prompt = "Stel een vraag of geef een opdracht.",
                Questions = new System.Collections.Generic.List<ConversationStarterQuestion>
                {
                    new ConversationStarterQuestion { Name = "Algemeen", Question = "Waar kan ik mee helpen?", Action = "ask" }
                }
            };
            return Task.FromResult(starter);
        }

        public Task<ChatConversation> OpenConversationStarter(string projectId, ConversationStarter starter, string chatId, string addTochatMessage, CancellationToken cancel)
        {
            var convo = new ChatConversation
            {
                MetaData = new ChatMetadata { Id = chatId, Name = starter?.Name ?? "Chat" },
                ChatMessages = new SerializableList<ConversationMessage>(GetConversationStarterMessages(null, chatId))
            };
            return Task.FromResult(convo);
        }

        public Task<ChatConversation> OpenConversationStarter(string projectId, ConversationStarter starter, string chatId, string userId, string addTochatMessage, CancellationToken cancel)
        {
            return OpenConversationStarter(projectId, starter, chatId, addTochatMessage, cancel);
        }
    }
}

