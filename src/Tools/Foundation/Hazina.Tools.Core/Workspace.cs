using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HazinaStore.Core.Models;

namespace HazinaStore.Core
{
    /// <summary>
    /// Represents the entire workspace - the projects folder
    /// Entry point for accessing all projects, global chats, and workspace settings
    /// </summary>
    public class Workspace
    {
        private readonly string _basePath;
        private readonly WorkspaceSettings _settings;

        /// <summary>
        /// Path to the projects folder (workspace root)
        /// </summary>
        public string BasePath => _basePath;

        /// <summary>
        /// Workspace-wide settings and configuration
        /// </summary>
        public WorkspaceSettings Settings => _settings;

        /// <summary>
        /// Initialize a workspace from the projects folder path
        /// </summary>
        /// <param name="basePath">Path to the projects folder</param>
        public Workspace(string basePath)
        {
            if (string.IsNullOrWhiteSpace(basePath))
                throw new ArgumentException("Base path cannot be empty", nameof(basePath));

            if (!Directory.Exists(basePath))
                throw new DirectoryNotFoundException($"Workspace path does not exist: {basePath}");

            _basePath = basePath;
            _settings = new WorkspaceSettings(_basePath);
        }

        /// <summary>
        /// Get a specific project by ID (folder name)
        /// </summary>
        /// <param name="projectId">Project identifier (folder name)</param>
        /// <returns>Project instance</returns>
        public Project GetProject(string projectId)
        {
            if (string.IsNullOrWhiteSpace(projectId))
                throw new ArgumentException("Project ID cannot be empty", nameof(projectId));

            var projectPath = Path.Combine(_basePath, projectId);
            if (!Directory.Exists(projectPath))
                throw new DirectoryNotFoundException($"Project not found: {projectId}");

            var projectFilePath = Path.Combine(projectPath, $"{projectId}.json");
            if (!File.Exists(projectFilePath))
                throw new FileNotFoundException($"Project metadata file not found: {projectFilePath}");

            return new Project(projectPath, this);
        }

        /// <summary>
        /// Get all non-archived projects
        /// </summary>
        /// <returns>List of all active projects</returns>
        public List<Project> GetAllProjects()
        {
            return GetAllProjects(includeArchived: false);
        }

        /// <summary>
        /// Get all projects, optionally including archived ones
        /// </summary>
        /// <param name="includeArchived">Whether to include archived projects</param>
        /// <returns>List of projects</returns>
        public List<Project> GetAllProjects(bool includeArchived = false)
        {
            var projects = new List<Project>();

            // System directories that should be excluded from project list
            var systemDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "uploads", ".chats", "temp", "cache", "__GLOBAL__", "__INVALID_PROJECT_ID__"
            };

            var directories = Directory.GetDirectories(_basePath)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name) &&
                              !name.StartsWith(".") &&
                              !systemDirectories.Contains(name))
                .ToList();

            foreach (var projectId in directories)
            {
                try
                {
                    // Skip folders without a matching metadata file to avoid noisy errors
                    var metaPath = Path.Combine(_basePath, projectId, $"{projectId}.json");
                    if (!File.Exists(metaPath))
                    {
                        Console.WriteLine($"Warning: Skipping project folder '{projectId}' because metadata file is missing.");
                        continue;
                    }

                    var project = GetProject(projectId);

                    if (!includeArchived && project.Metadata.Archived)
                        continue;

                    projects.Add(project);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not load project '{projectId}': {ex.Message}");
                }
            }

            return projects.OrderByDescending(p => p.Metadata.Created).ToList();
        }

        /// <summary>
        /// Create a new project
        /// </summary>
        /// <param name="projectId">Unique project identifier (will be folder name)</param>
        /// <param name="name">Display name for the project</param>
        /// <param name="description">Optional project description</param>
        /// <returns>Newly created project</returns>
        public Project CreateProject(string projectId, string name, string description = null)
        {
            if (string.IsNullOrWhiteSpace(projectId))
                throw new ArgumentException("Project ID cannot be empty", nameof(projectId));

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Project name cannot be empty", nameof(name));

            var projectPath = Path.Combine(_basePath, projectId);
            if (Directory.Exists(projectPath))
                throw new InvalidOperationException($"Project already exists: {projectId}");

            // Create project directory
            Directory.CreateDirectory(projectPath);

            // Create subdirectories
            Directory.CreateDirectory(Path.Combine(projectPath, "Uploads"));
            Directory.CreateDirectory(Path.Combine(projectPath, "chats"));

            // Create project metadata
            var metadata = new ProjectMetadata
            {
                Id = projectId,
                Name = name,
                Description = description ?? string.Empty,
                Created = DateTime.UtcNow,
                Status = "INITIAL"
            };

            var metadataPath = Path.Combine(projectPath, $"{projectId}.json");
            metadata.Save(metadataPath);

            return new Project(projectPath, this);
        }

        /// <summary>
        /// Delete a project (archives it instead of hard delete)
        /// </summary>
        /// <param name="projectId">Project to delete</param>
        public void DeleteProject(string projectId)
        {
            var project = GetProject(projectId);
            project.Archive();
        }

        /// <summary>
        /// Get the global chat system (chats accessible across all projects)
        /// </summary>
        /// <returns>GlobalChat instance</returns>
        public GlobalChat GetGlobalChat()
        {
            var globalChatPath = Path.Combine(_basePath, ".chats");

            // Create .chats folder if it doesn't exist
            if (!Directory.Exists(globalChatPath))
                Directory.CreateDirectory(globalChatPath);

            return new GlobalChat(globalChatPath, this);
        }

        /// <summary>
        /// Check if a project exists
        /// </summary>
        /// <param name="projectId">Project identifier</param>
        /// <returns>True if project exists</returns>
        public bool ProjectExists(string projectId)
        {
            if (string.IsNullOrWhiteSpace(projectId))
                return false;

            var projectPath = Path.Combine(_basePath, projectId);
            var projectFilePath = Path.Combine(projectPath, $"{projectId}.json");

            return Directory.Exists(projectPath) && File.Exists(projectFilePath);
        }

        /// <summary>
        /// Search for projects by name or description
        /// </summary>
        /// <param name="query">Search query</param>
        /// <returns>Matching projects</returns>
        public List<Project> SearchProjects(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return GetAllProjects();

            var lowerQuery = query.ToLower();

            return GetAllProjects()
                .Where(p =>
                    p.Metadata.Name.ToLower().Contains(lowerQuery) ||
                    (p.Metadata.Description?.ToLower().Contains(lowerQuery) ?? false))
                .ToList();
        }
    }
}
