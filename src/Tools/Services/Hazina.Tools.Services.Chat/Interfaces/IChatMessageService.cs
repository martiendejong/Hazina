using Hazina.Tools.Models;
using System.Threading.Tasks;

namespace Hazina.Tools.Services.Chat
{
    public interface IChatMessageService
    {
        SerializableList<ConversationMessage> GetChatMessages(string projectId, string chatId, string userId = null);
        void StoreChatMessages(string projectId, string chatId, SerializableList<ConversationMessage> messages, string userId = null);
        Task Delete(string projectId, string chatId, string userId = null);
        void RemoveMessage(string projectId, string chatId, int index, string userId = null);
        void UpdateMessage(string projectId, string chatId, int index, string message, string userId = null);
        ConversationMessage AddFileMessage(string projectId, string chatId, string filePath, bool includeInProject, string userId = null);
        void CreateChat(string projectId, string chatId);
    }
}
