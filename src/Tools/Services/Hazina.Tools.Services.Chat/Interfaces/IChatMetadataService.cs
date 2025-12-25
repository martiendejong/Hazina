using Hazina.Tools.Models;

namespace Hazina.Tools.Services.Chat
{
    public interface IChatMetadataService
    {
        SerializableList<ChatConversation> GetChats(string projectId, string userId = null);
        ChatMetadata GetChatMetaData(string projectId, string chatId);
        ChatMetadata GetChatMetaDataUser(string projectId, string chatId, string userId);
        SerializableList<ChatMetadata> GetChatMetaData(string projectId);
        SerializableList<ChatMetadata> GetChatMetaDataUser(string projectId, string userId);
        void SaveChatMetaData(string projectId, SerializableList<ChatMetadata> metas, string userId = null);
        ChatMetadata UpdateChatName(string projectId, string chatId, string name, string userId = null);
        ChatMetadata UpdateChatPinState(string projectId, string chatId, bool isPinned, string userId = null);
        ChatMetadata UpdateChatMetadataModified(string projectId, string chatId, string name, string userId = null);
        SerializableList<ChatConversation> GetAllChats();
    }
}
