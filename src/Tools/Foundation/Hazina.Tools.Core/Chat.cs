using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HazinaStore.Core.Models;
using Hazina.Tools.Models;

namespace HazinaStore.Core
{
    /// <summary>
    /// Represents a single chat conversation
    /// Provides access to messages, uploads, and chat metadata
    /// </summary>
    public class Chat
    {
        private readonly string _chatId;
        private readonly string _chatsPath;
        private readonly ChatRepository _repository;
        private ChatMetadata _metadata;

        /// <summary>
        /// Unique identifier for this chat
        /// </summary>
        public string Id => _chatId;

        /// <summary>
        /// Display name of the chat
        /// </summary>
        public string Name => Metadata.Name;

        /// <summary>
        /// When the chat was created
        /// </summary>
        public DateTime Created => Metadata.Created;

        /// <summary>
        /// When the chat was last updated
        /// </summary>
        public DateTime LastUpdated => Metadata.LastUpdated;

        /// <summary>
        /// Whether the chat is pinned to the top
        /// </summary>
        public bool IsPinned => Metadata.IsPinned;

        /// <summary>
        /// Chat metadata
        /// </summary>
        public ChatMetadata Metadata
        {
            get
            {
                // Refresh metadata from repository
                _metadata = _repository.GetChatMetadata(_chatId);
                return _metadata;
            }
        }

        /// <summary>
        /// Parent repository
        /// </summary>
        public ChatRepository Repository => _repository;

        /// <summary>
        /// Initialize a chat instance
        /// </summary>
        /// <param name="chatId">Chat identifier</param>
        /// <param name="chatsPath">Path to chats folder</param>
        /// <param name="repository">Parent repository</param>
        internal Chat(string chatId, string chatsPath, ChatRepository repository)
        {
            _chatId = chatId ?? throw new ArgumentNullException(nameof(chatId));
            _chatsPath = chatsPath ?? throw new ArgumentNullException(nameof(chatsPath));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        #region Messages

        /// <summary>
        /// Get all messages in this chat
        /// </summary>
        /// <returns>List of chat messages</returns>
        public List<ChatMessage> GetMessages()
        {
            var messages = _repository.GetMessages(_chatId);
            return messages.ToList();
        }

        /// <summary>
        /// Add a message to the chat
        /// </summary>
        /// <param name="message">Message to add</param>
        public void AddMessage(ChatMessage message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            var messages = _repository.GetMessages(_chatId);
            messages.Add(message);
            _repository.SaveMessages(_chatId, messages);

            // Update last updated timestamp
            UpdateLastUpdated();

            // Update last message preview
            UpdateLastMessagePreview(message.Text);
        }

        /// <summary>
        /// Add a user message to the chat
        /// </summary>
        /// <param name="text">Message text</param>
        public void AddUserMessage(string text)
        {
            AddMessage(new ChatMessage
            {
                Role = "REGULAR",
                Text = text,
                Timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Add an assistant message to the chat
        /// </summary>
        /// <param name="text">Message text</param>
        public void AddAssistantMessage(string text)
        {
            AddMessage(new ChatMessage
            {
                Role = "assistant",
                Text = text,
                Timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Update a message at a specific index
        /// </summary>
        /// <param name="index">Message index (0-based)</param>
        /// <param name="message">Updated message</param>
        public void UpdateMessage(int index, ChatMessage message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            var messages = _repository.GetMessages(_chatId);

            if (index < 0 || index >= messages.Count)
                throw new ArgumentOutOfRangeException(nameof(index), "Message index out of range");

            messages[index] = message;
            _repository.SaveMessages(_chatId, messages);

            UpdateLastUpdated();
        }

        /// <summary>
        /// Delete a message at a specific index
        /// </summary>
        /// <param name="index">Message index to delete (0-based)</param>
        public void DeleteMessage(int index)
        {
            var messages = _repository.GetMessages(_chatId);

            if (index < 0 || index >= messages.Count)
                throw new ArgumentOutOfRangeException(nameof(index), "Message index out of range");

            messages.RemoveAt(index);
            _repository.SaveMessages(_chatId, messages);

            UpdateLastUpdated();
        }

        /// <summary>
        /// Delete all messages in the chat
        /// </summary>
        public void ClearMessages()
        {
            var emptyMessages = new SerializableList<ChatMessage>();
            _repository.SaveMessages(_chatId, emptyMessages);

            UpdateLastUpdated();

            // Clear last message preview
            var metadata = Metadata;
            metadata.LastMessagePreview = null;
            _repository.UpdateChatMetadata(metadata);
        }

        #endregion

        #region Uploads

        /// <summary>
        /// Upload a file to this chat
        /// </summary>
        /// <param name="filename">Filename</param>
        /// <param name="content">File content</param>
        /// <param name="includeInProject">Whether to include in project embeddings</param>
        /// <returns>Path to uploaded file</returns>
        public string UploadFile(string filename, byte[] content, bool includeInProject = false)
        {
            if (string.IsNullOrWhiteSpace(filename))
                throw new ArgumentException("Filename cannot be empty", nameof(filename));

            if (content == null || content.Length == 0)
                throw new ArgumentException("Content cannot be empty", nameof(content));

            var uploadsFolder = _repository.GetChatUploadsFolder(_chatId);

            // Generate unique filename if file already exists
            var filePath = Path.Combine(uploadsFolder, filename);
            var counter = 1;
            while (File.Exists(filePath))
            {
                var nameWithoutExt = Path.GetFileNameWithoutExtension(filename);
                var extension = Path.GetExtension(filename);
                filePath = Path.Combine(uploadsFolder, $"{nameWithoutExt}_{counter}{extension}");
                counter++;
            }

            // Save file
            File.WriteAllBytes(filePath, content);

            return Path.GetFileName(filePath);
        }

        /// <summary>
        /// Get list of uploaded files in this chat
        /// </summary>
        /// <returns>List of filenames</returns>
        public List<string> GetUploadedFiles()
        {
            var uploadsFolder = _repository.GetChatUploadsFolder(_chatId);

            if (!Directory.Exists(uploadsFolder))
                return new List<string>();

            return Directory.GetFiles(uploadsFolder)
                .Select(Path.GetFileName)
                .ToList();
        }

        /// <summary>
        /// Get path to an uploaded file
        /// </summary>
        /// <param name="filename">Filename</param>
        /// <returns>Full path to file or null if not found</returns>
        public string GetUploadedFilePath(string filename)
        {
            var uploadsFolder = _repository.GetChatUploadsFolder(_chatId);
            var filePath = Path.Combine(uploadsFolder, filename);

            return File.Exists(filePath) ? filePath : null;
        }

        /// <summary>
        /// Delete an uploaded file
        /// </summary>
        /// <param name="filename">Filename to delete</param>
        public void DeleteUploadedFile(string filename)
        {
            var filePath = GetUploadedFilePath(filename);
            if (filePath != null && File.Exists(filePath))
                File.Delete(filePath);
        }

        #endregion

        #region Metadata Management

        /// <summary>
        /// Update chat name
        /// </summary>
        /// <param name="name">New name</param>
        public void UpdateName(string name)
        {
            _repository.UpdateChatName(_chatId, name);
        }

        /// <summary>
        /// Pin this chat to the top
        /// </summary>
        public void Pin()
        {
            _repository.PinChat(_chatId);
        }

        /// <summary>
        /// Unpin this chat
        /// </summary>
        public void Unpin()
        {
            _repository.UnpinChat(_chatId);
        }

        /// <summary>
        /// Update the last updated timestamp
        /// </summary>
        private void UpdateLastUpdated()
        {
            var metadata = Metadata;
            metadata.LastUpdated = DateTime.UtcNow;
            _repository.UpdateChatMetadata(metadata);
        }

        /// <summary>
        /// Update the last message preview
        /// </summary>
        private void UpdateLastMessagePreview(string messageText)
        {
            var metadata = Metadata;

            // Truncate to first 100 characters
            if (!string.IsNullOrWhiteSpace(messageText))
            {
                metadata.LastMessagePreview = messageText.Length > 100
                    ? messageText.Substring(0, 100) + "..."
                    : messageText;
            }
            else
            {
                metadata.LastMessagePreview = null;
            }

            _repository.UpdateChatMetadata(metadata);
        }

        #endregion

        public override string ToString()
        {
            return $"Chat: {Name} ({Id})";
        }
    }
}
