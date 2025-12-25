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
    /// Manages global chats that have access to all projects
    /// Global chats are stored in the .chats folder at the workspace level
    /// </summary>
    public class GlobalChat
    {
        private readonly string _globalChatsPath;
        private readonly Workspace _workspace;
        private const string ChatsMetadataFile = "chats.json";

        /// <summary>
        /// Path to the global chats folder (.chats)
        /// </summary>
        public string GlobalChatsPath => _globalChatsPath;

        /// <summary>
        /// Parent workspace
        /// </summary>
        public Workspace Workspace => _workspace;

        /// <summary>
        /// Initialize global chat system
        /// </summary>
        /// <param name="globalChatsPath">Path to .chats folder</param>
        /// <param name="workspace">Parent workspace</param>
        internal GlobalChat(string globalChatsPath, Workspace workspace)
        {
            _globalChatsPath = globalChatsPath ?? throw new ArgumentNullException(nameof(globalChatsPath));
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));

            // Ensure .chats folder exists
            if (!Directory.Exists(_globalChatsPath))
                Directory.CreateDirectory(_globalChatsPath);
        }

        /// <summary>
        /// Create a new global chat
        /// </summary>
        /// <param name="name">Optional chat name</param>
        /// <returns>Newly created chat</returns>
        public Chat CreateChat(string name = null)
        {
            var chatId = Guid.NewGuid().ToString();

            var metadata = new ChatMetadata
            {
                Id = chatId,
                Name = name ?? "Global Chat",
                Created = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow,
                IsPinned = false
            };

            // Add to metadata list
            var allChats = GetAllChatsMetadata();
            allChats.Insert(0, metadata);
            SaveChatsMetadata(allChats);

            // Create empty messages file
            var messagesFile = GetChatMessagesFile(chatId);
            var emptyMessages = new SerializableList<ChatMessage>();
            SaveMessagesToFile(messagesFile, emptyMessages);

            // Create a temporary ChatRepository for this global chat
            // We reuse the ChatRepository logic but point it to .chats folder
            var tempRepo = new GlobalChatRepository(_globalChatsPath, this);
            return new Chat(chatId, _globalChatsPath, tempRepo);
        }

        /// <summary>
        /// Get a specific global chat by ID
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

            var tempRepo = new GlobalChatRepository(_globalChatsPath, this);
            return new Chat(chatId, _globalChatsPath, tempRepo);
        }

        /// <summary>
        /// Get metadata for all global chats
        /// </summary>
        /// <returns>List of chat metadata</returns>
        public List<ChatMetadata> GetAllChats()
        {
            return GetAllChatsMetadata();
        }

        /// <summary>
        /// Delete a global chat
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
        /// Get all project embeddings files (for RAG across all projects)
        /// </summary>
        /// <returns>List of paths to embeddings files</returns>
        public List<string> GetAllProjectEmbeddingsFiles()
        {
            var embeddingsFiles = new List<string>();

            var projects = _workspace.GetAllProjects();

            foreach (var project in projects)
            {
                var embeddingsFile = project.GetEmbeddingsFilePath();
                if (File.Exists(embeddingsFile))
                {
                    embeddingsFiles.Add(embeddingsFile);
                }
            }

            // Also include global embeddings if it exists
            var globalEmbeddingsFile = Path.Combine(_globalChatsPath, "embeddings");
            if (File.Exists(globalEmbeddingsFile))
            {
                embeddingsFiles.Add(globalEmbeddingsFile);
            }

            return embeddingsFiles;
        }

        #region Private Methods

        /// <summary>
        /// Get chat metadata by ID
        /// </summary>
        private ChatMetadata GetChatMetadata(string chatId)
        {
            return GetAllChatsMetadata().FirstOrDefault(c => c.Id == chatId);
        }

        /// <summary>
        /// Get path to chat messages file
        /// </summary>
        private string GetChatMessagesFile(string chatId)
        {
            return Path.Combine(_globalChatsPath, $"{chatId}.json");
        }

        /// <summary>
        /// Get chat uploads folder
        /// </summary>
        private string GetChatUploadsFolder(string chatId)
        {
            var uploadsFolder = Path.Combine(_globalChatsPath, $"{chatId}_uploads");

            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            return uploadsFolder;
        }

        /// <summary>
        /// Get path to chats metadata file
        /// </summary>
        private string GetChatsMetadataFilePath()
        {
            return Path.Combine(_globalChatsPath, ChatsMetadataFile);
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
                Console.WriteLine($"Error loading global chats metadata: {ex.Message}");
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

        /// <summary>
        /// Internal repository class for global chats
        /// Wraps ChatRepository functionality for global chats
        /// </summary>
        private class GlobalChatRepository : ChatRepository
        {
            private readonly GlobalChat _globalChat;

            public GlobalChatRepository(string globalChatsPath, GlobalChat globalChat)
                : base(Path.GetDirectoryName(globalChatsPath), null)
            {
                _globalChat = globalChat;
            }

            // This class inherits all ChatRepository functionality
            // but operates on the .chats folder instead of a project's chats folder
        }
    }
}
