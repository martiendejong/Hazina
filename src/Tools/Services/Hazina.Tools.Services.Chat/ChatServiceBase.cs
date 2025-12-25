using Hazina.Tools.Models.WordPress.Blogs;
using Hazina.Tools.Data;
using Hazina.Tools.Models;
using System;
using System.IO;
using System.Linq;

namespace Hazina.Tools.Services.Chat
{
    /// <summary>
    /// Base class for chat-related services providing common functionality
    /// for handling user-specific and project-level operations
    /// </summary>
    public abstract class ChatServiceBase
    {
        protected readonly ProjectsRepository Projects;
        protected readonly ProjectFileLocator FileLocator;

        protected ChatServiceBase(ProjectsRepository projects, ProjectFileLocator fileLocator)
        {
            Projects = projects ?? throw new ArgumentNullException(nameof(projects));
            FileLocator = fileLocator ?? throw new ArgumentNullException(nameof(fileLocator));
        }

        #region File Path Helpers

        protected string GetChatsFolder(string projectId)
        {
            if (projectId == ProjectsRepository.GLOBAL_PROJECT_ID)
                return GetGlobalChatsFolder();

            return FileLocator.GetChatsFolder(projectId);
        }

        protected string GetChatsFolder(string projectId, string userId)
        {
            var userChatsPath = Path.Combine(GetChatsFolder(projectId), userId);
            if (!Directory.Exists(userChatsPath))
                Directory.CreateDirectory(userChatsPath);
            return userChatsPath;
        }

        protected string GetGlobalChatsFolder()
        {
            var folder = Path.Combine(FileLocator.ProjectsFolder, ".chats");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            return folder;
        }

        protected string GetChatFile(string projectId, string chatId)
        {
            if (projectId == ProjectsRepository.GLOBAL_PROJECT_ID)
                return GetGlobalChatFile(chatId);
            return Path.Combine(GetChatsFolder(projectId), $"{chatId}.json");
        }

        protected string GetChatFile(string projectId, string chatId, string userId)
        {
            return Path.Combine(GetChatsFolder(projectId, userId), $"{chatId}.json");
        }

        protected string GetGlobalChatFile(string chatId)
        {
            return Path.Combine(GetGlobalChatsFolder(), $"{chatId}.json");
        }

        protected string GetChatsFile(string projectId)
        {
            return Path.Combine(GetChatsFolder(projectId), "chats.json");
        }

        protected string GetChatsFile(string projectId, string userId)
        {
            return Path.Combine(GetChatsFolder(projectId, userId), "chats.json");
        }

        protected string GetChatUploadsFolder(string projectId, string chatId)
        {
            return Path.Combine(GetChatsFolder(projectId), $"{chatId}_uploads");
        }

        protected string GetChatUploadsFolder(string projectId, string chatId, string userId)
        {
            return Path.Combine(GetChatsFolder(projectId, userId), $"{chatId}_uploads");
        }

        #endregion

        #region File I/O Helpers

        protected SerializableList<T> LoadListFileOrDefault<T>(string chatsFile) where T : Serializer<T>
        {
            if (File.Exists(chatsFile))
            {
                return SerializableList<T>.Deserialize(File.ReadAllText(chatsFile));
            }
            return new SerializableList<T>();
        }

        protected void SaveListFile<T>(string chatsFile, SerializableList<T> items) where T : Serializer<T>
        {
            File.WriteAllText(chatsFile, items.Serialize());
        }

        #endregion

        #region User Context Execution Pattern

        /// <summary>
        /// Executes an operation with either user-specific or project-level context
        /// </summary>
        protected TResult ExecuteWithUserContext<TResult>(
            string projectId,
            string chatId,
            string userId,
            Func<string, string, TResult> projectOperation,
            Func<string, string, string, TResult> userOperation)
        {
            return string.IsNullOrEmpty(userId)
                ? projectOperation(projectId, chatId)
                : userOperation(projectId, chatId, userId);
        }

        /// <summary>
        /// Async version of ExecuteWithUserContext
        /// </summary>
        protected async System.Threading.Tasks.Task<TResult> ExecuteWithUserContextAsync<TResult>(
            string projectId,
            string chatId,
            string userId,
            Func<string, string, System.Threading.Tasks.Task<TResult>> projectOperation,
            Func<string, string, string, System.Threading.Tasks.Task<TResult>> userOperation)
        {
            return string.IsNullOrEmpty(userId)
                ? await projectOperation(projectId, chatId)
                : await userOperation(projectId, chatId, userId);
        }

        #endregion
    }
}
