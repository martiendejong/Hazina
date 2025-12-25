using DevGPT.GenerationTools.Data;
using DevGPT.GenerationTools.Models;
using DevGPT.GenerationTools.Models.WordPress.Blogs;
using DevGPTStore.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DevGPT.GenerationTools.Extensions
{
    /// <summary>
    /// Extension methods for ProjectsRepository to add missing methods
    /// </summary>
    public static class ProjectsRepositoryExtensions
    {
        private const string BasePromptFile = "baseprompt.txt";
        private const string QuickUpdatePromptFile = "quickupdate.json";

        /// <summary>
        /// Gets a ProjectFileLocator for the repository
        /// </summary>
        public static ProjectFileLocator GetFileLocator(this ProjectsRepository repository)
        {
            return new ProjectFileLocator(repository.ProjectsFolder);
        }

        // Constants for file names - accessed via ProjectsRepository.FileNames
        public static class FileNames
        {
            public const string ContentFile = "content.json";
            public const string ContentChatFile = "content.chat.json";
            public const string ContentHooksFile = "contenthooks.json";
            public const string AcceptedContentFile = "accepted.content.json";
            public const string AcceptedPlannedContentFile = "accepted.planned.content.json";
            public const string RejectedContentFile = "rejected.content.json";
            public const string DeletedContentFile = "deleted.content.json";
            public const string RolePromptsFile = "role.prompts.json";
            public const string GLOBAL_PROJECT_ID = "__global__";
        }

        /// <summary>
        /// Loads a project by ID
        /// </summary>
        public static Project Load(this ProjectsRepository repository, string projectId)
        {
            var projectPath = Path.Combine(repository.ProjectsFolder, projectId, "project.json");
            if (File.Exists(projectPath))
            {
                try
                {
                    var json = File.ReadAllText(projectPath);
                    var project = JsonSerializer.Deserialize<Project>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (project != null)
                    {
                        project.Id = projectId;
                        return project;
                    }
                }
                catch
                {
                    // Fall through to create default project
                }
            }

            // Return a default project if file doesn't exist
            return new Project
            {
                Id = projectId,
                Name = projectId,
                Created = DateTime.Now
            };
        }

        /// <summary>
        /// Saves a project
        /// </summary>
        public static void Save(this ProjectsRepository repository, Project project)
        {
            var projectFolder = Path.Combine(repository.ProjectsFolder, project.Id);
            if (!Directory.Exists(projectFolder))
            {
                Directory.CreateDirectory(projectFolder);
            }

            var projectPath = Path.Combine(projectFolder, "project.json");
            var json = JsonSerializer.Serialize(project, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(projectPath, json);
        }

        /// <summary>
        /// Gets the project folder path for a given project ID
        /// </summary>
        public static string GetProjectFolder(this ProjectsRepository repository, string projectId)
        {
            return Path.Combine(repository.ProjectsFolder, projectId);
        }

        /// <summary>
        /// Gets the project folder path for a chat project with user ID
        /// </summary>
        public static string GetProjectFolder(this ProjectsRepository repository, string projectId, string userId)
        {
            return Path.Combine(repository.ProjectsFolder, projectId, userId);
        }

        /// <summary>
        /// Gets the full path to a file within a project
        /// </summary>
        public static string GetPath(this ProjectsRepository repository, string projectId, string fileName)
        {
            return Path.Combine(repository.GetProjectFolder(projectId), fileName);
        }

        /// <summary>
        /// Gets the full path to a file (overload for Project object)
        /// </summary>
        public static string GetPath(this ProjectsRepository repository, Project project, string fileName)
        {
            return Path.Combine(repository.GetProjectFolder(project.Id), fileName);
        }

        /// <summary>
        /// Checks if a file exists in a project
        /// </summary>
        public static bool FileExists(this ProjectsRepository repository, string projectId, string fileName)
        {
            var path = repository.GetPath(projectId, fileName);
            return File.Exists(path);
        }

        /// <summary>
        /// Checks if a generated document exists
        /// </summary>
        public static bool GeneratedDocumentExists(this ProjectsRepository repository, string projectId, string fileName)
        {
            return repository.FileExists(projectId, fileName);
        }

        /// <summary>
        /// Loads a stored list from a project file
        /// </summary>
        public static SerializableList<T> LoadStoredList<T>(this ProjectsRepository repository, string projectId, string fileName) where T : class
        {
            var path = repository.GetPath(projectId, fileName);
            if (!File.Exists(path))
            {
                return new SerializableList<T>();
            }

            try
            {
                var json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json) || json.Trim() == "[]")
                {
                    return new SerializableList<T>();
                }

                var items = JsonSerializer.Deserialize<List<T>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return new SerializableList<T>(items ?? new List<T>());
            }
            catch
            {
                return new SerializableList<T>();
            }
        }

        /// <summary>
        /// Saves a stored list to a project file
        /// </summary>
        public static void SaveStoredList<T>(this ProjectsRepository repository, string projectId, string fileName, SerializableList<T> list) where T : class
        {
            var path = repository.GetPath(projectId, fileName);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = list.Serialize();
            File.WriteAllText(path, json);
        }

        /// <summary>
        /// Loads a stored object from a project file
        /// </summary>
        public static T LoadStoredObject<T>(this ProjectsRepository repository, string projectId, string fileName) where T : class
        {
            var path = repository.GetPath(projectId, fileName);
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Stores file content to a project file
        /// </summary>
        public static void StoreFile(this ProjectsRepository repository, string projectId, string fileName, string content)
        {
            var path = repository.GetPath(projectId, fileName);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, content);
        }

        /// <summary>
        /// Loads the basis prompt from the projects folder
        /// </summary>
        public static string LoadBasisPrompt(this ProjectsRepository repository)
        {
            var filePath = Path.Combine(repository.ProjectsFolder, BasePromptFile);
            if (File.Exists(filePath))
            {
                return File.ReadAllText(filePath);
            }
            return string.Empty;
        }

        /// <summary>
        /// Saves the basis prompt to the projects folder
        /// </summary>
        public static void SaveBasisPrompt(this ProjectsRepository repository, string prompt)
        {
            var filePath = Path.Combine(repository.ProjectsFolder, BasePromptFile);
            File.WriteAllText(filePath, prompt);
        }

        /// <summary>
        /// Loads snel aanpassen (quick adjustments) from the projects folder
        /// </summary>
        public static List<KeyValuePair<string, string>> LoadSnelAanpassen(this ProjectsRepository repository)
        {
            var filePath = Path.Combine(repository.ProjectsFolder, QuickUpdatePromptFile);
            if (File.Exists(filePath))
            {
                try
                {
                    var json = File.ReadAllText(filePath);
                    var items = JsonSerializer.Deserialize<List<KeyValuePair<string, string>>>(json);
                    return items ?? new List<KeyValuePair<string, string>>();
                }
                catch
                {
                    return new List<KeyValuePair<string, string>>();
                }
            }
            return new List<KeyValuePair<string, string>>();
        }

        /// <summary>
        /// Saves snel aanpassen (quick adjustments) to the projects folder
        /// </summary>
        public static void SaveSnelAanpassen(this ProjectsRepository repository, List<KeyValuePair<string, string>> items)
        {
            var filePath = Path.Combine(repository.ProjectsFolder, QuickUpdatePromptFile);
            var json = JsonSerializer.Serialize(items);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Gets the blog categories file name for a project
        /// </summary>
        public static string GetBlogCategoriesFile(this ProjectsRepository repository, string projectId)
        {
            return "blog.categories.json";
        }

        /// <summary>
        /// Gets the content planning file name
        /// </summary>
        public static string GetContentPlanningFile(this ProjectsRepository repository, string projectId)
        {
            return "content.planning.json";
        }

        /// <summary>
        /// Gets the content schedule file name
        /// </summary>
        public static string GetContentScheduleFile(this ProjectsRepository repository, string projectId)
        {
            return "content.schedule.json";
        }

        /// <summary>
        /// Loads blog categories for a project
        /// </summary>
        public static BlogCategoriesClass LoadBlogCategories(this ProjectsRepository repository, string projectId)
        {
            var project = repository.Load(projectId);
            return LoadBlogCategories(repository, project);
        }

        /// <summary>
        /// Loads blog categories for a project
        /// </summary>
        public static BlogCategoriesClass LoadBlogCategories(this ProjectsRepository repository, Project project)
        {
            var fileName = repository.GetBlogCategoriesFile(project.Id);
            var path = repository.GetPath(project.Id, fileName);
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    return JsonSerializer.Deserialize<BlogCategoriesClass>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new BlogCategoriesClass();
                }
                catch
                {
                    return new BlogCategoriesClass();
                }
            }
            return new BlogCategoriesClass();
        }

        /// <summary>
        /// Gets WordPress credentials for a project
        /// </summary>
        public static WordPressCredentials GetWordPressCredentials(this ProjectsRepository repository, string projectId)
        {
            var project = repository.Load(projectId);
            return GetWordPressCredentials(repository, project);
        }

        /// <summary>
        /// Gets WordPress credentials for a project
        /// </summary>
        public static WordPressCredentials GetWordPressCredentials(this ProjectsRepository repository, Project project)
        {
            // This would typically load from project configuration or a credentials file
            // For now, return null - this may need to be implemented based on actual storage location
            return null;
        }

        /// <summary>
        /// Loads user infos from the users file
        /// </summary>
        public static List<DevGPTStoreUserInfo> LoadUserInfos(this ProjectsRepository repository)
        {
            var filePath = Path.Combine(repository.ProjectsFolder, "users.json");
            if (File.Exists(filePath))
            {
                try
                {
                    var json = File.ReadAllText(filePath);
                    return JsonSerializer.Deserialize<List<DevGPTStoreUserInfo>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<DevGPTStoreUserInfo>();
                }
                catch
                {
                    return new List<DevGPTStoreUserInfo>();
                }
            }
            return new List<DevGPTStoreUserInfo>();
        }

        /// <summary>
        /// Saves user infos to the users file
        /// </summary>
        public static void SaveUserInfos(this ProjectsRepository repository, List<DevGPTStoreUserInfo> userInfos)
        {
            var filePath = Path.Combine(repository.ProjectsFolder, "users.json");
            var json = JsonSerializer.Serialize(userInfos, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Gets the project file path for a project
        /// </summary>
        public static string GetProjectFilePath(this ProjectsRepository repository, string projectId)
        {
            return repository.GetPath(projectId, projectId + ".json");
        }

        /// <summary>
        /// Gets the chats folder path for a project
        /// </summary>
        public static string GetChatsFolder(this ProjectsRepository repository, string projectId)
        {
            var folder = Path.Combine(repository.GetProjectFolder(projectId), "chats");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            return folder;
        }

        /// <summary>
        /// Gets the chats folder path for a project with user ID
        /// </summary>
        public static string GetChatsFolder(this ProjectsRepository repository, string projectId, string userId)
        {
            var folder = Path.Combine(repository.GetProjectFolder(projectId), userId, "chats");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            return folder;
        }

        /// <summary>
        /// Gets the chat uploads folder path
        /// </summary>
        public static string GetChatUploadsFolder(this ProjectsRepository repository, string projectId, string chatId)
        {
            return Path.Combine(repository.GetChatsFolder(projectId), $"{chatId}_uploads");
        }

        /// <summary>
        /// Gets the chat uploads folder path with user ID
        /// </summary>
        public static string GetChatUploadsFolder(this ProjectsRepository repository, string projectId, string chatId, string userId)
        {
            return Path.Combine(repository.GetChatsFolder(projectId, userId), $"{chatId}_uploads");
        }

        /// <summary>
        /// Gets the list of files to be embedded for a project
        /// </summary>
        public static async Task<List<string>> GetEmbeddingsFileList(this ProjectsRepository repository, Project project)
        {
            var list = new List<string>();

            // Skip for global project
            if (project.Id == "__global__")
            {
                return list;
            }

            // Add project metadata file
            var projectFile = project.Id + ".json";
            if (repository.FileExists(project.Id, projectFile))
            {
                list.Add(projectFile);
            }

            // Add uploaded files if they exist
            try
            {
                var uploadsJsonPath = Path.Combine(repository.GetProjectFolder(project.Id), "uploadedFiles.json");
                if (File.Exists(uploadsJsonPath))
                {
                    var json = await File.ReadAllTextAsync(uploadsJsonPath);
                    var uploadedFiles = JsonSerializer.Deserialize<List<UploadedFile>>(json) ?? new List<UploadedFile>();
                    foreach (var uploadedFile in uploadedFiles)
                    {
                        if (!string.IsNullOrWhiteSpace(uploadedFile?.TextFilename))
                        {
                            list.Add(uploadedFile.TextFilename);
                        }
                    }
                }
            }
            catch { /* ignore and continue */ }

            // Add action files
            var projectFolder = repository.GetProjectFolder(project.Id);
            var actionFiles = Directory.GetFiles(projectFolder, "*.actions", SearchOption.TopDirectoryOnly);
            foreach (var file in actionFiles)
            {
                list.Add(Path.GetFileName(file));
            }

            return list;
        }

        // Helper class for uploaded files
        private class UploadedFile
        {
            public string Filename { get; set; }
            public string TextFilename { get; set; }
        }
    }
}

