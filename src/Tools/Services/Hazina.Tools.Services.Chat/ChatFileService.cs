using DevGPT.GenerationTools.Data;
using DevGPT.GenerationTools.Models;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace DevGPT.GenerationTools.Services.Chat
{
    /// <summary>
    /// Service for managing chat file operations and project file inclusion
    /// </summary>
    public class ChatFileService
    {
        private readonly ProjectsRepository _projects;

        public ChatFileService(ProjectsRepository projects)
        {
            _projects = projects ?? throw new ArgumentNullException(nameof(projects));
        }

        /// <summary>
        /// Handles file operations after a chat message is deleted
        /// </summary>
        /// <param name="messageText">The deleted message text</param>
        /// <param name="projectId">The project ID</param>
        /// <param name="chatId">The chat ID</param>
        /// <param name="userId">Optional user ID for personal projects</param>
        /// <param name="onRemoveFile">Callback for removing file from project</param>
        public async Task HandleMessageDeleted(
            string messageText,
            string projectId,
            string chatId,
            string userId,
            Func<string, string, string, Task> onRemoveFile)
        {
            if (string.IsNullOrWhiteSpace(messageText) || !messageText.StartsWith("{"))
                return;

            try
            {
                var file = JsonSerializer.Deserialize<DevGPTStoreChatFile>(messageText);
                if (file != null && file.IncludeInProject)
                {
                    var fileNameWithoutTxt = RemoveTxtExtension(file.File);
                    await onRemoveFile(projectId, chatId, fileNameWithoutTxt);
                }
            }
            catch (JsonException)
            {
                // Not a valid DevGPTStoreChatFile JSON, ignore
            }
        }

        /// <summary>
        /// Handles file operations after a chat message is updated
        /// </summary>
        /// <param name="messageText">The updated message text</param>
        /// <param name="projectId">The project ID</param>
        /// <param name="chatId">The chat ID</param>
        /// <param name="userId">Optional user ID for personal projects</param>
        /// <param name="onAddFile">Callback for adding file to project</param>
        /// <param name="onRemoveFile">Callback for removing file from project</param>
        public async Task HandleMessageUpdated(
            string messageText,
            string projectId,
            string chatId,
            string userId,
            Func<string, string, string, string, Task> onAddFile,
            Func<string, string, string, Task> onRemoveFile)
        {
            if (string.IsNullOrWhiteSpace(messageText) || !messageText.StartsWith("{"))
                return;

            try
            {
                var file = JsonSerializer.Deserialize<DevGPTStoreChatFile>(messageText);
                if (file == null)
                    return;

                if (file.IncludeInProject)
                {
                    await onAddFile(projectId, chatId, file.File, userId);
                }
                else
                {
                    var fileNameWithoutTxt = RemoveTxtExtension(file.File);
                    await onRemoveFile(projectId, chatId, fileNameWithoutTxt);
                }
            }
            catch (JsonException)
            {
                // Not a valid DevGPTStoreChatFile JSON, ignore
            }
        }

        /// <summary>
        /// Extracts DevGPTStoreChatFile from message text if valid JSON
        /// </summary>
        public DevGPTStoreChatFile TryParseChatFile(string messageText)
        {
            if (string.IsNullOrWhiteSpace(messageText) || !messageText.StartsWith("{"))
                return null;

            try
            {
                return JsonSerializer.Deserialize<DevGPTStoreChatFile>(messageText);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// Removes .txt extension from filename if present
        /// </summary>
        private string RemoveTxtExtension(string fileName)
        {
            return fileName?.EndsWith(".txt") == true
                ? fileName.Substring(0, fileName.Length - 4)
                : fileName;
        }
    }
}
