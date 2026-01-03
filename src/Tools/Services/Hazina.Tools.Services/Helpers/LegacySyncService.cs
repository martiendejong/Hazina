using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hazina.Tools.Models;
using Hazina.Tools.Data;
using HazinaStore.Models;
using CoreProject = HazinaStore.Core.Project;
using LegacyProject = Hazina.Tools.Models.Project;
using Hazina.Tools.Services.Helpers;
using Hazina.Tools.Services.FileOps.Helpers;
using Hazina.Tools.AI.Agents;
using Microsoft.Extensions.Configuration;

namespace Hazina.Tools.Services.Helpers
{
    /// <summary>
    /// Service for synchronizing data between Core API and legacy file-based system
    /// Maintains backward compatibility during transition period
    /// </summary>
    public class LegacySyncService
    {
        private readonly ProjectsRepository _legacyProjects;
        private readonly HazinaStoreConfig _config;
        private readonly IConfiguration _configuration;
        private readonly ProjectGlobalSettingsRepository _globalSettings;

        public LegacySyncService(
            ProjectsRepository legacyProjects,
            HazinaStoreConfig config,
            IConfiguration configuration = null)
        {
            _legacyProjects = legacyProjects ?? throw new ArgumentNullException(nameof(legacyProjects));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _configuration = configuration; // Optional for backward compatibility
            _globalSettings = new ProjectGlobalSettingsRepository(legacyProjects.ProjectsFolder);
        }

        /// <summary>
        /// Synchronize uploaded document to legacy system
        /// Updates uploadedFiles.json with new file metadata
        /// </summary>
        public async Task SyncUploadedDocument(
            CoreProject project,
            string fileName,
            int tokenCount = 0,
            List<string> tags = null)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));

            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name is required", nameof(fileName));

            var uploadsFolder = project.GetUploadsFolder();
            var filePath = Path.Combine(uploadsFolder, fileName);
            var listFilePath = Path.Combine(project.ProjectPath, "uploadedFiles.json");

            // Create legacy UploadedFile object
            var uploadedFile = FileHelper.GetUploadedFileDetails(filePath, fileName, tokenCount);

            // Add tags if provided
            if (tags != null && tags.Any())
            {
                uploadedFile.Tags = tags;
            }

            // Update legacy uploadedFiles.json
            await FileHelper.UpdateUploadedFilesListAsync(listFilePath, uploadedFile);
        }

        /// <summary>
        /// Synchronize uploaded document to legacy system (overload for backward compatibility)
        /// </summary>
        public async Task SyncUploadedDocument(
            CoreProject project,
            string fileName,
            List<string> tags)
        {
            await SyncUploadedDocument(project, fileName, 0, tags);
        }

        /// <summary>
        /// Synchronize document metadata after text extraction
        /// Updates token counts and file parts for existing entry
        /// </summary>
        public async Task SyncDocumentMetadata(
            CoreProject project,
            string fileName,
            string extractedText,
            int tokenCount,
            List<string> splitFilePaths)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));

            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name is required", nameof(fileName));

            var uploadsFolder = project.GetUploadsFolder();
            var filePath = Path.Combine(uploadsFolder, fileName);
            var listFilePath = Path.Combine(project.ProjectPath, "uploadedFiles.json");

            // Create updated file details with token count
            var uploadedFile = FileHelper.GetUploadedFileDetails(filePath, fileName, tokenCount);

            // Add parts if file was split
            if (splitFilePaths != null && splitFilePaths.Count > 1)
            {
                uploadedFile.Parts.Clear();
                foreach (var part in splitFilePaths)
                {
                    uploadedFile.Parts.Add(new List<double>());
                }
            }

            // Update using FileHelper which handles deduplication
            // This will remove any existing entry with same TextFilename and add the updated one
            await FileHelper.UpdateUploadedFilesListAsync(listFilePath, uploadedFile);
        }

        /// <summary>
        /// Synchronize deleted document to legacy system
        /// Removes file from uploadedFiles.json
        /// </summary>
        public async Task SyncDeletedDocument(CoreProject project, string fileName)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));

            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name is required", nameof(fileName));

            var uploadsFolder = project.GetUploadsFolder();
            var filePath = Path.Combine(uploadsFolder, fileName);
            var listFilePath = Path.Combine(project.ProjectPath, "uploadedFiles.json");

            // Delete from legacy system
            await FileHelper.DeleteUploadedFileAsync(filePath, listFilePath, fileName);
        }

        /// <summary>
        /// Initialize or reinitialize the legacy vector store
        /// Triggers embedding generation for all documents
        /// </summary>
        public async Task InitializeStore(string projectId, bool force = false)
        {
            if (string.IsNullOrWhiteSpace(projectId))
                throw new ArgumentException("Project ID is required", nameof(projectId));

            // If no IConfiguration is available, skip initialization
            // This maintains backward compatibility with older code
            if (_configuration == null)
            {
                Console.WriteLine("Warning: Cannot initialize store without IConfiguration");
                return;
            }

            // Load the legacy project
            var legacyProject = _legacyProjects.Load(projectId);

            // Create agent to initialize store
            var agent = new GeneratorAgentBase(_configuration, _globalSettings.LoadBasisPrompt());

            // Initialize store with updateEmbeddings = true to trigger embedding generation
            await agent.InitStore(legacyProject, updateEmbeddings: true);
        }

        /// <summary>
        /// Split large files into smaller chunks for embedding
        /// Legacy method for handling files that exceed token limits
        /// Returns list of file paths (strings) for the split files
        /// </summary>
        public async Task<List<string>> SplitAndEmbedFile(
            string filePath,
            string extractedText,
            int tokenCount)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path is required", nameof(filePath));

            if (string.IsNullOrWhiteSpace(extractedText))
                return new List<string>();

            var files = await FileHelper.SplitFiles(
                filePath,
                extractedText,
                tokenCount,
                _legacyProjects,
                _config.ApiSettings.OpenApiKey
            );

            return files;
        }

        /// <summary>
        /// Get legacy uploadedFiles.json metadata for a project
        /// Used for backward compatibility during transition
        /// </summary>
        public async Task<List<UploadedFile>> GetLegacyUploadedFiles(CoreProject project)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));

            var listFilePath = Path.Combine(project.ProjectPath, "uploadedFiles.json");

            try
            {
                var uploadedFilesList = await FileHelper.GetUploadedFilesListAsync(listFilePath);

                // Strip embeddings and parts from response for performance
                uploadedFilesList.ForEach(file =>
                {
                    file.Parts = null;
                    file.Embedding = null;
                });

                return uploadedFilesList;
            }
            catch (Exception)
            {
                return new List<UploadedFile>();
            }
        }

        /// <summary>
        /// Update legacy file metadata (e.g., label)
        /// </summary>
        public async Task UpdateLegacyFileMetadata(
            CoreProject project,
            string fileName,
            string newLabel)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));

            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name is required", nameof(fileName));

            if (string.IsNullOrWhiteSpace(newLabel))
                throw new ArgumentException("Label is required", nameof(newLabel));

            await FileHelper.UpdateUploadedFileAsync(project.ProjectPath, fileName, newLabel);
        }

        /// <summary>
        /// Update legacy file tags
        /// </summary>
        public async Task UpdateLegacyFileTags(
            CoreProject project,
            string fileName,
            List<string> tags)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));

            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name is required", nameof(fileName));

            await FileHelper.UpdateUploadedFileTagsAsync(project.ProjectPath, fileName, tags);
        }
    }
}
