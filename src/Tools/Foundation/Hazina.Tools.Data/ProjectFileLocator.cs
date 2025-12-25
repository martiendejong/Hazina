using System;
using System.IO;

namespace Hazina.Tools.Data
{
    /// <summary>
    /// Service responsible for all project file path operations and file name constants.
    /// This centralizes path construction and file existence checks for project files.
    /// </summary>
    public class ProjectFileLocator
    {
        public string ProjectsFolder { get; }

        // File name constants
        public static readonly string RolePromptsFile = "roleprompts.json";
        public static readonly string BasisPromptFile = "basisprompt.txt";
        public static readonly string SnelAanpassenFile = "snelaanpassen.txt";
        public static readonly string DataGatheringPromptFile = "prompt.datagathering.txt";
        public static readonly string ChatDefaultPromptFile = "prompt.chat.default.txt";

        public static readonly string BlogCategoriesFile = "BlogCategories.json";
        public static readonly string ContentPlanningFile = "ContentPlanning.json";
        public static readonly string ContentScheduleFile = "ContentSchedule.json";
        public static readonly string ContentHooksFile = "contenthooks.json";
        public static readonly string ContentFile = "content.json";
        public static readonly string AcceptedPlannedContentFile = "acceptedplannedcontent.json";
        public static readonly string RejectedContentFile = "content.rejected.json";
        public static readonly string DeletedContentFile = "content.deleted.json";
        public static readonly string ContentChatFile = "content.chat.json";
        public static readonly string AcceptedContentFile = "content.accepted.json";

        public static readonly string DoelenFile = "doelen.json";
        public static readonly string UsersFile = "users.json";
        public static readonly string PublishedContentFile = "publishedposts.json";

        public ProjectFileLocator(string projectsFolder)
        {
            ProjectsFolder = projectsFolder ?? throw new ArgumentNullException(nameof(projectsFolder));
        }

        #region Project Folder Paths

        public string GetProjectFolder(string projectId)
        {
            if (string.IsNullOrWhiteSpace(projectId))
                return Path.Combine(ProjectsFolder, "__INVALID_PROJECT_ID__");
            return Path.Combine(ProjectsFolder, projectId);
        }

        public string GetProjectFolder(string projectId, string userId)
        {
            if (string.IsNullOrWhiteSpace(projectId))
                return Path.Combine(ProjectsFolder, "__INVALID_PROJECT_ID__", userId ?? "__INVALID_USER_ID__");
            if (string.IsNullOrWhiteSpace(userId))
                return Path.Combine(ProjectsFolder, projectId ?? "__INVALID_PROJECT_ID__", "__INVALID_USER_ID__");
            return Path.Combine(ProjectsFolder, projectId, userId);
        }

        #endregion

        #region Generic Path Helpers

        public string GetPath(string projectId, string relativePath)
        {
            return Path.Combine(GetProjectFolder(projectId), relativePath);
        }

        public string GetProjectFilePath(string projectId)
        {
            return GetPath(projectId, projectId + ".json");
        }

        #endregion

        #region File Existence Checks

        public bool Exists(string projectId)
        {
            return File.Exists(GetProjectFilePath(projectId));
        }

        public bool FileExists(string projectId, string file)
        {
            return File.Exists(GetPath(projectId, file));
        }

        #endregion

        #region Specific File Paths

        public string GetBlogCategoriesFile(string projectId)
        {
            return GetPath(projectId, BlogCategoriesFile);
        }

        public string GetContentPlanningFile(string projectId)
        {
            return GetPath(projectId, ContentPlanningFile);
        }

        public string GetContentScheduleFile(string projectId)
        {
            return GetPath(projectId, ContentScheduleFile);
        }

        public string GetEmbeddingsFilePath(string projectId)
        {
            return GetPath(projectId, "embeddings.json");
        }

        #endregion

        #region Chat Paths

        public string GetChatsFolder(string projectId)
        {
            var folder = Path.Combine(GetProjectFolder(projectId), "chats");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            return folder;
        }

        public string GetChatsFolder(string projectId, string userId)
        {
            var folder = Path.Combine(GetProjectFolder(projectId), userId, "chats");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            return folder;
        }

        public string GetChatFile(string projectId, string chatId)
        {
            return Path.Combine(GetChatsFolder(projectId), $"{chatId}.json");
        }

        public string GetChatFile(string projectId, string chatId, string userId)
        {
            return Path.Combine(GetChatsFolder(projectId, userId), $"{chatId}.json");
        }

        public string GetChatsFile(string projectId)
        {
            return Path.Combine(GetChatsFolder(projectId), "chats.json");
        }

        public string GetChatsFile(string projectId, string userId)
        {
            return Path.Combine(GetChatsFolder(projectId, userId), "chats.json");
        }

        public string GetChatUploadsFolder(string projectId, string chatId)
        {
            return Path.Combine(GetChatsFolder(projectId), $"{chatId}_uploads");
        }

        public string GetChatUploadsFolder(string projectId, string chatId, string userId)
        {
            return Path.Combine(GetChatsFolder(projectId, userId), $"{chatId}_uploads");
        }

        #endregion
    }
}

