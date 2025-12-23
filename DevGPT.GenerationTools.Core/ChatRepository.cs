using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DevGPTStore.Core.Models;
using DevGPT.GenerationTools.Models;
using DevGPT.GenerationTools.Models.WordPress.Blogs;

namespace DevGPTStore.Core
{
    /// <summary>
    /// Manages all chats within a project
    /// Handles chat metadata, messages, and uploads
    /// </summary>
    public class ChatRepository
    {
        private readonly string _chatsPath;
        private readonly Project _project;
        private const string ChatsMetadataFile = "chats.json";

        /// <summary>
        /// Path to the chats folder
        /// </summary>
        public string ChatsPath => _chatsPath;

        /// <summary>
        /// Parent project
        /// </summary>
        public Project Project => _project;

        /// <summary>
        /// Initialize chat repository for a project
        /// </summary>
        /// <param name="projectPath">Path to project folder</param>
        /// <param name="project">Parent project</param>
        internal ChatRepository(string projectPath, Project project)
        {
            if (string.IsNullOrWhiteSpace(projectPath))
                throw new ArgumentNullException(nameof(projectPath));

            _project = project ?? throw new ArgumentNullException(nameof(project));

            _chatsPath = Path.Combine(projectPath, "chats");

            // Create chats folder if it doesn't exist
            if (!Directory.Exists(_chatsPath))
                Directory.CreateDirectory(_chatsPath);
        }

        /// <summary>
        /// Create a new chat
        /// </summary>
        /// <param name="name">Optional chat name (auto-generated if not provided)</param>
        /// <returns>Newly created chat</returns>
        public Chat CreateChat(string name = null)
        {
            var chatId = Guid.NewGuid().ToString();

            var metadata = new ChatMetadata
            {
                Id = chatId,
                Name = name ?? "New Chat",
                Created = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow,
                IsPinned = false
            };

            // Add to metadata list
            var allChats = GetAllChatsMetadata();
            allChats.Insert(0, metadata); // Add to beginning
            SaveChatsMetadata(allChats);

            // Create empty messages file
            var messagesFile = GetChatMessagesFile(chatId);
            var emptyMessages = new SerializableList<ChatMessage>();
            SaveMessagesToFile(messagesFile, emptyMessages);

            return new Chat(chatId, _chatsPath, this);
        }

        /// <summary>
        /// Get a specific chat by ID
        /// </summary>
        /// <param name="chatId">Chat identifier</param>
        /// <returns>Chat instance or null if not found</returns>
        public Chat GetChat(string chatId)
        {
            if (string.IsNullOrWhiteSpace(chatId))
                return null;

            var metadata = GetChatMetadata(chatId);
            if (metadata == null)
                return null;

            return new Chat(chatId, _chatsPath, this);
        }

        /// <summary>
        /// Get metadata for all chats
        /// </summary>
        /// <returns>List of chat metadata</returns>
        public List<ChatMetadata> GetAllChats()
        {
            return GetAllChatsMetadata();
        }

        /// <summary>
        /// Delete a chat
        /// </summary>
        /// <param name="chatId">Chat ID to delete</param>
        public void DeleteChat(string chatId)
        {
            if (string.IsNullOrWhiteSpace(chatId))
                return;

            // Remove from metadata
            var allChats = GetAllChatsMetadata();
            allChats.RemoveAll(c => c.Id == chatId);
            SaveChatsMetadata(allChats);

            // Delete messages file
            var messagesFile = GetChatMessagesFile(chatId);
            if (File.Exists(messagesFile))
                File.Delete(messagesFile);

            // Delete uploads folder
            var uploadsFolder = GetChatUploadsFolder(chatId);
            if (Directory.Exists(uploadsFolder))
                Directory.Delete(uploadsFolder, recursive: true);
        }

        /// <summary>
        /// Update chat name
        /// </summary>
        /// <param name="chatId">Chat ID</param>
        /// <param name="name">New name</param>
        public void UpdateChatName(string chatId, string name)
        {
            var metadata = GetChatMetadata(chatId);
            if (metadata == null)
                return;

            metadata.Name = name ?? "Untitled Chat";
            metadata.LastUpdated = DateTime.UtcNow;

            UpdateChatMetadata(metadata);
        }

        /// <summary>
        /// Pin a chat to the top
        /// </summary>
        /// <param name="chatId">Chat ID</param>
        public void PinChat(string chatId)
        {
            var metadata = GetChatMetadata(chatId);
            if (metadata == null)
                return;

            metadata.IsPinned = true;
            metadata.LastUpdated = DateTime.UtcNow;

            UpdateChatMetadata(metadata);
        }

        /// <summary>
        /// Unpin a chat
        /// </summary>
        /// <param name="chatId">Chat ID</param>
        public void UnpinChat(string chatId)
        {
            var metadata = GetChatMetadata(chatId);
            if (metadata == null)
                return;

            metadata.IsPinned = false;
            metadata.LastUpdated = DateTime.UtcNow;

            UpdateChatMetadata(metadata);
        }

        #region Internal Methods (used by Chat class)

        /// <summary>
        /// Get chat metadata by ID
        /// </summary>
        internal ChatMetadata GetChatMetadata(string chatId)
        {
            return GetAllChatsMetadata().FirstOrDefault(c => c.Id == chatId);
        }

        /// <summary>
        /// Update chat metadata
        /// </summary>
        internal void UpdateChatMetadata(ChatMetadata metadata)
        {
            var allChats = GetAllChatsMetadata();
            var index = allChats.FindIndex(c => c.Id == metadata.Id);

            if (index >= 0)
            {
                allChats[index] = metadata;
                SaveChatsMetadata(allChats);
            }
        }

        /// <summary>
        /// Get messages for a chat
        /// </summary>
        internal SerializableList<ChatMessage> GetMessages(string chatId)
        {
            var messagesFile = GetChatMessagesFile(chatId);

            if (!File.Exists(messagesFile))
                return new SerializableList<ChatMessage>();

            try
            {
                var messages = Serializer<ChatMessage[]>.Load(messagesFile);
                return new SerializableList<ChatMessage>(messages);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading chat messages for {chatId}: {ex.Message}");
                return new SerializableList<ChatMessage>();
            }
        }

        /// <summary>
        /// Save messages for a chat
        /// </summary>
        internal void SaveMessages(string chatId, SerializableList<ChatMessage> messages)
        {
            var messagesFile = GetChatMessagesFile(chatId);
            SaveMessagesToFile(messagesFile, messages);
        }

        /// <summary>
        /// Get chat uploads folder
        /// </summary>
        internal string GetChatUploadsFolder(string chatId)
        {
            var uploadsFolder = Path.Combine(_chatsPath, $"{chatId}_uploads");

            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            return uploadsFolder;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Get path to chat messages file
        /// </summary>
        private string GetChatMessagesFile(string chatId)
        {
            return Path.Combine(_chatsPath, $"{chatId}.json");
        }

        /// <summary>
        /// Get path to chats metadata file
        /// </summary>
        private string GetChatsMetadataFilePath()
        {
            return Path.Combine(_chatsPath, ChatsMetadataFile);
        }

        /// <summary>
        /// Load all chats metadata from disk
        /// </summary>
        private List<ChatMetadata> GetAllChatsMetadata()
        {
            var metadataFile = GetChatsMetadataFilePath();

            if (!File.Exists(metadataFile))
                return new List<ChatMetadata>();

            try
            {
                var chatMetasArray = Serializer<ChatMetadata[]>.Load(metadataFile);
                return new List<ChatMetadata>(chatMetasArray);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading chats metadata: {ex.Message}");
                return new List<ChatMetadata>();
            }
        }

        /// <summary>
        /// Save chats metadata to disk
        /// </summary>
        private void SaveChatsMetadata(List<ChatMetadata> chats)
        {
            var metadataFile = GetChatsMetadataFilePath();
            var chatMetasArray = chats.ToArray();
            Serializer<ChatMetadata[]>.Save(chatMetasArray, metadataFile);
        }

        /// <summary>
        /// Save messages to file
        /// </summary>
        private void SaveMessagesToFile(string messagesFile, SerializableList<ChatMessage> messages)
        {
            var messagesArray = messages.ToArray();
            Serializer<ChatMessage[]>.Save(messagesArray, messagesFile);
        }

        #endregion
    }
}
