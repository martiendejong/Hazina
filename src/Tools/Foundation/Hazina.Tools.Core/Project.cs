using System;
using System.IO;
using HazinaStore.Core.Models;

namespace HazinaStore.Core
{
    /// <summary>
    /// Represents a single project - a folder with documents, chats, and settings
    /// Core entity that provides access to all project-related functionality
    /// </summary>
    public class Project
    {
        private readonly string _projectPath;
        private readonly Workspace _workspace;
        private ProjectMetadata _metadata;
        private DocumentRepository _documents;
        private ChatRepository _chats;
        private ProjectSettings _settings;

        /// <summary>
        /// Path to the project folder
        /// </summary>
        public string ProjectPath => _projectPath;

        /// <summary>
        /// Project identifier (folder name)
        /// </summary>
        public string Id => _metadata.Id;

        /// <summary>
        /// Project display name
        /// </summary>
        public string Name => _metadata.Name;

        /// <summary>
        /// Project metadata
        /// </summary>
        public ProjectMetadata Metadata => _metadata;

        /// <summary>
        /// Document repository for this project
        /// </summary>
        public DocumentRepository Documents => _documents ??= new DocumentRepository(_projectPath, this);

        /// <summary>
        /// Chat repository for this project
        /// </summary>
        public ChatRepository Chats => _chats ??= new ChatRepository(_projectPath, this);

        /// <summary>
        /// Project-specific settings
        /// </summary>
        public ProjectSettings Settings => _settings ??= new ProjectSettings(_projectPath, this);

        /// <summary>
        /// Parent workspace
        /// </summary>
        public Workspace Workspace => _workspace;

        /// <summary>
        /// Initialize a project from its folder path
        /// </summary>
        /// <param name="projectPath">Path to the project folder</param>
        /// <param name="workspace">Parent workspace</param>
        internal Project(string projectPath, Workspace workspace)
        {
            if (string.IsNullOrWhiteSpace(projectPath))
                throw new ArgumentException("Project path cannot be empty", nameof(projectPath));

            if (!Directory.Exists(projectPath))
                throw new DirectoryNotFoundException($"Project folder not found: {projectPath}");

            _projectPath = projectPath;
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));

            LoadMetadata();
        }

        /// <summary>
        /// Load project metadata from disk
        /// </summary>
        private void LoadMetadata()
        {
            var projectId = Path.GetFileName(_projectPath);
            var metadataPath = Path.Combine(_projectPath, $"{projectId}.json");

            if (!File.Exists(metadataPath))
                throw new FileNotFoundException($"Project metadata file not found: {metadataPath}");

            _metadata = ProjectMetadata.Load(metadataPath);
        }

        /// <summary>
        /// Save project metadata to disk
        /// </summary>
        private void SaveMetadata()
        {
            var projectId = Path.GetFileName(_projectPath);
            var metadataPath = Path.Combine(_projectPath, $"{projectId}.json");
            _metadata.Save(metadataPath);
        }

        /// <summary>
        /// Update project metadata
        /// </summary>
        /// <param name="metadata">New metadata</param>
        public void UpdateMetadata(ProjectMetadata metadata)
        {
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));

            // Ensure ID doesn't change
            metadata.Id = _metadata.Id;

            _metadata = metadata;
            SaveMetadata();
        }

        /// <summary>
        /// Update project name
        /// </summary>
        /// <param name="name">New project name</param>
        public void UpdateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Project name cannot be empty", nameof(name));

            _metadata.Name = name;
            SaveMetadata();
        }

        /// <summary>
        /// Update project description
        /// </summary>
        /// <param name="description">New description</param>
        public void UpdateDescription(string description)
        {
            _metadata.Description = description ?? string.Empty;
            SaveMetadata();
        }

        /// <summary>
        /// Archive this project (soft delete)
        /// </summary>
        public void Archive()
        {
            _metadata.Archived = true;
            SaveMetadata();
        }

        /// <summary>
        /// Unarchive this project
        /// </summary>
        public void Unarchive()
        {
            _metadata.Archived = false;
            SaveMetadata();
        }

        /// <summary>
        /// Pin this project to the top
        /// </summary>
        public void Pin()
        {
            _metadata.IsPinned = true;
            SaveMetadata();
        }

        /// <summary>
        /// Unpin this project
        /// </summary>
        public void Unpin()
        {
            _metadata.IsPinned = false;
            SaveMetadata();
        }

        /// <summary>
        /// Update project status
        /// </summary>
        /// <param name="status">New status</param>
        public void UpdateStatus(string status)
        {
            _metadata.Status = status ?? "INITIAL";
            SaveMetadata();
        }

        /// <summary>
        /// Get embeddings file path for this project
        /// </summary>
        /// <returns>Path to embeddings file</returns>
        public string GetEmbeddingsFilePath()
        {
            return Path.Combine(_projectPath, "embeddings");
        }

        /// <summary>
        /// Check if embeddings exist for this project
        /// </summary>
        /// <returns>True if embeddings file exists</returns>
        public bool HasEmbeddings()
        {
            return File.Exists(GetEmbeddingsFilePath());
        }

        /// <summary>
        /// Get the uploads folder path
        /// </summary>
        /// <returns>Path to Uploads folder</returns>
        public string GetUploadsFolder()
        {
            var uploadsPath = Path.Combine(_projectPath, "Uploads");
            if (!Directory.Exists(uploadsPath))
                Directory.CreateDirectory(uploadsPath);
            return uploadsPath;
        }

        /// <summary>
        /// Get the chats folder path
        /// </summary>
        /// <returns>Path to chats folder</returns>
        public string GetChatsFolder()
        {
            var chatsPath = Path.Combine(_projectPath, "chats");
            if (!Directory.Exists(chatsPath))
                Directory.CreateDirectory(chatsPath);
            return chatsPath;
        }

        public override string ToString()
        {
            return $"Project: {Name} ({Id})";
        }
    }
}
