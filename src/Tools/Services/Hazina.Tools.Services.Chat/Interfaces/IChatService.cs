using Hazina.Tools.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.Tools.Services.Chat
{
    public interface IChatService
    {
        // Chat list operations
        SerializableList<ChatConversation> GetChats(string projectId);
        SerializableList<ChatConversation> GetChats(string projectId, string userId);
        SerializableList<ChatConversation> GetAllChats();

        // Chat message operations
        SerializableList<ConversationMessage> GetChatMessages(string projectId, string chatId);
        SerializableList<ConversationMessage> GetChatMessages(string projectId, string chatId, string userId);

        // Chat CRUD operations
        Task Delete(string projectId, string chatId);
        Task Delete(string projectId, string chatId, string userId);
        void CreateChat(string projectId, string chatId);

        // Message management
        void Remove(string projectId, string chatId, int index);
        void Remove(string projectId, string chatId, string userId, int index);
        void Update(string projectId, string chatId, int index, string message);
        void Update(string projectId, string chatId, string userId, int index, string message);
        ConversationMessage AddFileMessage(string projectId, string chatId, string filePath, bool includeInProject);
        ConversationMessage AddFileMessage(string projectId, string chatId, string userId, string filePath, bool includeInProject);

        // Chat metadata operations
        ChatMetadata UpdateChatName(string projectId, string chatId, string name);
        ChatMetadata UpdateChatName(string projectId, string chatId, string userId, string name);
        ChatMetadata UpdateChatPinState(string projectId, string chatId, bool isPinned);
        ChatMetadata UpdateChatPinState(string projectId, string chatId, string userId, bool isPinned);
        SerializableList<ChatMetadata> GetChatMetaData(string projectId);
        SerializableList<ChatMetadata> GetChatMetaDataUser(string projectId, string userId);
        ChatMetadata GetChatMetaData(string projectId, string chatId);
        ChatMetadata GetChatMetaDataUser(string projectId, string chatId, string userId);

        // Chat message streaming
        Task<ChatConversation> SendChatMessage(string projectId, string chatId, Project project, GeneratorMessage chatMessage, CancellationToken cancel);
        Task<ChatConversation> SendChatMessage(string projectId, string chatId, string userId, Project project, GeneratorMessage chatMessage, CancellationToken cancel);

        // Image generation
        Task<ChatConversation> GenerateImage(string projectId, string chatId, Project project, GeneratorMessage chatMessage, CancellationToken cancel, bool isImageSet);
        Task<ChatConversation> GenerateImage(string projectId, string chatId, string userId, Project project, GeneratorMessage chatMessage, CancellationToken cancel, bool isImageSet);
        Task<ChatConversation> GenerateImage(string projectId, string chatId, Project project, string chatMessage, CancellationToken cancel, bool isImageSet);
        Task<ChatConversation> GenerateImage(string projectId, string chatId, string userId, Project project, string chatMessage, CancellationToken cancel, bool isImageSet);

        // Generated images
        SerializableList<GeneratedImageInfo> GetGeneratedImages(string projectId);
        SerializableList<GeneratedImageInfo> GetGeneratedImages(string projectId, string userId);

        // Canvas operations
        Task<ChatConversation> EditCanvasMessage(string projectId, string chatId, Project project, CanvasMessage message, CancellationToken cancel);
        Task<ChatConversation> EditCanvasMessage(string projectId, string chatId, string userId, Project project, CanvasMessage message, CancellationToken cancel);
        string GetChatUploadsFolder(string projectId, string chatId);
        string GetChatUploadsFolder(string projectId, string chatId, string userId);

        // Conversation starters
        Task<ConversationStarter> GetConversationStarter(string projectId, string chatId);
        Task<ConversationStarter> GetConversationStarter(string projectId, string chatId, string userId);
        Task<ChatConversation> OpenConversationStarter(string projectId, ConversationStarter starter, string chatId, string addTochatMessage, CancellationToken cancel);
        Task<ChatConversation> OpenConversationStarter(string projectId, ConversationStarter starter, string chatId, string userId, string addTochatMessage, CancellationToken cancel);

        // Data sync
        Task SyncProjectDataToDocumentStoreAsync(string projectId);
    }
}
