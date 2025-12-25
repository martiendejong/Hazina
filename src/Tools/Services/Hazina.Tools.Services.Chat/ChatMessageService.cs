using Hazina.Tools.Data;
using Hazina.Tools.Models;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hazina.Tools.Services.Chat
{
    /// <summary>
    /// Service responsible for managing chat messages (CRUD operations)
    /// </summary>
    public class ChatMessageService : ChatServiceBase, IChatMessageService
    {
        private readonly IChatMetadataService _metadataService;

        public ChatMessageService(ProjectsRepository projects, ProjectFileLocator fileLocator, IChatMetadataService metadataService)
            : base(projects, fileLocator)
        {
            _metadataService = metadataService ?? throw new ArgumentNullException(nameof(metadataService));
        }

        #region Get Messages

        public SerializableList<ConversationMessage> GetChatMessages(string projectId, string chatId, string userId = null)
        {
            var chatFile = string.IsNullOrEmpty(userId)
                ? GetChatFile(projectId, chatId)
                : GetChatFile(projectId, chatId, userId);

            return LoadListFileOrDefault<ConversationMessage>(chatFile);
        }

        #endregion

        #region Store Messages

        public void StoreChatMessages(string projectId, string chatId, SerializableList<ConversationMessage> messages, string userId = null)
        {
            var chatFile = string.IsNullOrEmpty(userId)
                ? GetChatFile(projectId, chatId)
                : GetChatFile(projectId, chatId, userId);

            if (projectId == ProjectsRepository.GLOBAL_PROJECT_ID)
                StoreGlobalChatMessages(chatId, messages);
            else
                SaveListFile(chatFile, messages);
        }

        private void StoreGlobalChatMessages(string chatId, SerializableList<ConversationMessage> messages)
        {
            var chatFile = GetGlobalChatFile(chatId);
            SaveListFile(chatFile, messages);
        }

        #endregion

        #region Delete Chat

        public async Task Delete(string projectId, string chatId, string userId = null)
        {
            if (string.IsNullOrEmpty(userId))
                await DeleteInternal(projectId, chatId);
            else
                await DeleteInternal(projectId, chatId, userId);
        }

        private async Task DeleteInternal(string projectId, string chatId)
        {
            var chats = _metadataService.GetChats(projectId);
            var chat = chats.SingleOrDefault(c => c.MetaData.Id == chatId);
            if (chat != null)
            {
                chats.Remove(chat);
                var chatFile = GetChatFile(projectId, chatId);
                if (File.Exists(chatFile))
                    File.Delete(chatFile);
                _metadataService.SaveChatMetaData(projectId, new SerializableList<ChatMetadata>(chats.Select(c => c.MetaData)));
            }
        }

        private async Task DeleteInternal(string projectId, string chatId, string userId)
        {
            var chats = _metadataService.GetChats(projectId, userId);
            var chat = chats.SingleOrDefault(c => c.MetaData.Id == chatId);
            if (chat != null)
            {
                chats.Remove(chat);
                var chatFile = GetChatFile(projectId, chatId, userId);
                if (File.Exists(chatFile))
                    File.Delete(chatFile);
                _metadataService.SaveChatMetaData(projectId, new SerializableList<ChatMetadata>(chats.Select(c => c.MetaData)), userId);
            }
        }

        #endregion

        #region Remove Message

        public void RemoveMessage(string projectId, string chatId, int index, string userId = null)
        {
            var messages = GetChatMessages(projectId, chatId, userId);
            if (index >= 0 && index < messages.Count)
            {
                var m = messages[index];
                messages.Remove(m);
                StoreChatMessages(projectId, chatId, messages, userId);
            }
        }

        #endregion

        #region Update Message

        public void UpdateMessage(string projectId, string chatId, int index, string message, string userId = null)
        {
            var messages = GetChatMessages(projectId, chatId, userId);
            if (index >= 0 && index < messages.Count)
            {
                messages[index].Text = message;
                StoreChatMessages(projectId, chatId, messages, userId);
            }
        }

        #endregion

        #region Add File Message

        public ConversationMessage AddFileMessage(string projectId, string chatId, string filePath, bool includeInProject, string userId = null)
        {
            var fileMessage = new HazinaStoreChatFile { File = filePath, IncludeInProject = includeInProject };
            var chatItem = new ConversationMessage
            {
                Role = ChatMessageRole.Assistant,
                Text = JsonSerializer.Serialize(fileMessage)
            };

            var messages = GetChatMessages(projectId, chatId, userId);
            messages.Add(chatItem);
            StoreChatMessages(projectId, chatId, messages, userId);

            return chatItem;
        }

        #endregion

        #region Create Chat

        public void CreateChat(string projectId, string chatId)
        {
            var chatFile = GetChatFile(projectId, chatId);
            if (!File.Exists(chatFile))
            {
                var emptyMessages = new SerializableList<ConversationMessage>();
                SaveListFile(chatFile, emptyMessages);
            }
        }

        #endregion

        #region Helper Methods

        public static ChatConversation MakeChat(SerializableList<ConversationMessage> messages, ChatMetadata meta)
        {
            return new ChatConversation
            {
                MetaData = meta,
                ChatMessages = messages
            };
        }

        public static SerializableList<ConversationMessage> AddInteractionToMessages(
            SerializableList<ConversationMessage> messages,
            string response)
        {
            messages.Add(new ConversationMessage
            {
                Role = ChatMessageRole.Assistant,
                Text = response
            });
            return messages;
        }

        public static SerializableList<ConversationMessage> AddMessageAndResponseToMessages(
            SerializableList<ConversationMessage> messages,
            string chatMessage,
            string response)
        {
            messages.Add(new ConversationMessage
            {
                Role = ChatMessageRole.User,
                Text = chatMessage
            });
            messages.Add(new ConversationMessage
            {
                Role = ChatMessageRole.Assistant,
                Text = response
            });
            return messages;
        }

        #endregion
    }
}
