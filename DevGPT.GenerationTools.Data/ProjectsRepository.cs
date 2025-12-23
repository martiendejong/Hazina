using DevGPT.GenerationTools.Models;
using DevGPTStore.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DevGPT.GenerationTools.Data
{
    /// <summary>
    /// Repository responsible for project lifecycle operations only.
    /// Handles project creation, loading, saving, listing, and archiving.
    /// For file paths, use ProjectFileLocator.
    /// For WordPress/Social Media, use ProjectIntegrationStore.
    /// For embeddings, use ProjectEmbeddingService.
    /// For global settings, use ProjectGlobalSettingsRepository.
    /// For chat metadata, use ProjectChatRepository.
    /// </summary>
    public class ProjectsRepository
    {
        public static readonly string GLOBAL_PROJECT_ID = "__GLOBAL__";

        public string ProjectsFolder { get; }

        public IConfiguration AppConfig { get; }

        public DevGPTStoreConfig DevGPTStoreConfig { get; }

        private readonly ProjectFileLocator _fileLocator;


        public ProjectsRepository(DevGPTStoreConfig configuration, IConfiguration appConfig)
        {
            ProjectsFolder = configuration.ProjectSettings.ProjectsFolder;
            AppConfig = appConfig;
            DevGPTStoreConfig = configuration;
            _fileLocator = new ProjectFileLocator(ProjectsFolder);
        }

        private static string _globalProjectsFolder = null;
        /// <summary>
        /// Set the global projects folder path. Must be called exactly once on application start.
        /// </summary>
        public static void SetProjectsFolderStatic(string folder)
        {
            _globalProjectsFolder = folder;
        }
        /// <summary>
        /// Get the previously set global projects folder path. Throws if not previously initialized.
        /// </summary>
        public static string GetProjectsFolderStatic()
        {
            if (string.IsNullOrWhiteSpace(_globalProjectsFolder))
                throw new InvalidOperationException("Projects folder is niet geinitialiseerd. Roep SetProjectsFolderStatic aan bij applicatiestart.");
            return _globalProjectsFolder;
        }

        public List<Project> GetProjectsList(List<string> filter, string type)
        {
            var projects = Directory.GetDirectories(ProjectsFolder)
                .Select(Path.GetFileName)
                .Select(projectName =>
                {
                    if (projectName == null || projectName.ToLower().StartsWith(".config") || !_fileLocator.Exists(projectName))
                        return null;
                    Console.WriteLine($"Reading project file for {projectName}");
                    var project = Load(projectName);
                    if (project.Archived || (project.ProjectType.ToLower() != type && type != "customer"))
                        return null;
                    return project;
                })
                .OfType<Project>().ToList();
            var filtered = projects.Where(p => filter == null || filter.Contains(p.Id)).ToList();
            return filtered.OrderByDescending(project => project.Created)
                .ToList();
        }

        public List<Project> GetChatProjects(List<string> filter) => GetProjectsList(filter, "chat");
        public List<Project> GetProjectsList(List<string> filter) => GetProjectsList(filter, "customer");

        public Project Load(string projectId)
        {
            if (string.IsNullOrWhiteSpace(projectId))
            {
                return null;
            }
            // Handle virtual global project
            if (projectId == GLOBAL_PROJECT_ID)
            {
                return new Project
                {
                    Id = GLOBAL_PROJECT_ID,
                    Name = "Global Chat",
                    Description = "Chat with access to all projects",
                    ProjectType = "global"  // Special type for global chats (not user-specific)
                };
            }
            var projectPath = _fileLocator.GetProjectFilePath(projectId);
            if (!File.Exists(projectPath))
            {
                return null;
            }
            return Project.Load(projectPath);
        }

        public void Save(Project project) => project.Save(_fileLocator.GetProjectFilePath(project.Id));

        public bool Exists(string projectId) => _fileLocator.Exists(projectId);

    }
}
