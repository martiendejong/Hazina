using DevGPT.GenerationTools.Models;
using System.Threading;
using System.Threading.Tasks;

namespace DevGPT.GenerationTools.Services.Chat
{
    public interface IConversationStarterService
    {
        SerializableList<ConversationMessage> GetConversationStarterMessages(Project project, string chatId);
        Task<SerializableList<ConversationMessage>> GenerateConversationStarter(Project project, string chatId, CancellationToken cancel);
        Task<ConversationStarter> GetConversationStarter(string projectId, string chatId, string userId = null);
        Task<ChatConversation> OpenConversationStarter(string projectId, ConversationStarter starter, string chatId, string addTochatMessage, CancellationToken cancel);
        Task<ChatConversation> OpenConversationStarter(string projectId, ConversationStarter starter, string chatId, string userId, string addTochatMessage, CancellationToken cancel);
    }
}
