using Hazina.Tools.Data;
using Hazina.Tools.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HazinaStore.Models;
using HazinaStore.Core;
using HazinaStore.Core.Models;
using System.Linq;
using Hazina.Tools.Services.FileOps.Helpers;
using Hazina.Tools.Services.Helpers;
using CoreProject = HazinaStore.Core.Project;
using LegacyProject = Hazina.Tools.Models.Project;
using Microsoft.Extensions.Configuration;

namespace Hazina.Tools.Services
{
    /// <summary>
    /// Service for handling document uploads with text extraction and processing
    /// Refactored to use the new Core API
    /// </summary>
    public class UploadedDocumentsService
    {
        private readonly Action<string, string> _sendUpdate;
        private readonly TextFileExtractor _extractor;
        private readonly Workspace _workspace;
        private readonly LegacySyncService _legacySync;
        private readonly DocumentLockManager _lockManager;

        public UploadedDocumentsService(
            Action<string, string> sendUpdate,
            TextFileExtractor extractor,
            Workspace workspace,
            ProjectsRepository legacyProjects,
            HazinaStoreConfig config,
            IConfiguration configuration = null)
        {
            _sendUpdate = sendUpdate ?? throw new ArgumentNullException(nameof(sendUpdate));
            _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));

            // Initialize helper services
            _legacySync = new LegacySyncService(legacyProjects, config, configuration);
            _lockManager = new DocumentLockManager(
                lockTimeout: TimeSpan.FromMinutes(5),
                inactivityTimeout: TimeSpan.FromHours(1),
                cleanupInterval: TimeSpan.FromMinutes(10)
            );
        }

        #region Public API

        /// <summary>
        /// Upload a file from HTTP request
        /// </summary>
        public async Task<UploadedFile> UploadFile(IFormFile file, string projectId)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty", nameof(file));

            if (string.IsNullOrWhiteSpace(projectId))
                throw new ArgumentException("Project ID is required", nameof(projectId));

            // Read file content
            byte[] fileBytes;
            using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);
                fileBytes = memoryStream.ToArray();
            }

            return await UploadFileInternal(projectId, file.FileName, fileBytes, initStore: true);
        }

        /// <summary>
        /// Add or update a file with text content
        /// </summary>
        public async Task<UploadedFile> AddOrUpdateFile(string name, string content, string projectId)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("File name is required", nameof(name));

            if (string.IsNullOrWhiteSpace(projectId))
                throw new ArgumentException("Project ID is required", nameof(projectId));

            var contentBytes = System.Text.Encoding.UTF8.GetBytes(content ?? string.Empty);
            return await UploadFileInternal(projectId, name, contentBytes, updateIfExists: true, initStore: false);
        }

        /// <summary>
        /// Add a new file with text content
        /// </summary>
        public async Task<UploadedFile> AddFile(string name, string content, string projectId)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("File name is required", nameof(name));

            if (string.IsNullOrWhiteSpace(projectId))
                throw new ArgumentException("Project ID is required", nameof(projectId));

            var contentBytes = System.Text.Encoding.UTF8.GetBytes(content ?? string.Empty);
            return await UploadFileInternal(projectId, name, contentBytes, updateIfExists: false, initStore: true);
        }

        /// <summary>
        /// Get all uploaded files for a project
        /// </summary>
        public async Task<List<UploadedFile>> GetUploadedFiles(string projectId)
        {
            if (string.IsNullOrWhiteSpace(projectId))
                throw new ArgumentException("Project ID is required", nameof(projectId));

            var project = _workspace.GetProject(projectId);
            return await _legacySync.GetLegacyUploadedFiles(project);
        }

        #endregion

        #region Internal Implementation

        /// <summary>
        /// Internal upload implementation using Core API
        /// </summary>
        private async Task<UploadedFile> UploadFileInternal(
            string projectId,
            string fileName,
            byte[] fileContent,
            bool updateIfExists = false,
            bool initStore = true)
        {
            var project = _workspace.GetProject(projectId);
            UploadedFile uploadedFile = null;
            string actualFileName = fileName;

            // Phase 1: Upload file to Core API (with lock)
            using (await _lockManager.AcquireLockAsync(projectId))
            {
                // Upload using new Core API
                var uploadedDoc = project.Documents.UploadDocument(
                    filename: fileName,
                    content: fileContent,
                    extractedText: null // Will be extracted in background
                );

                actualFileName = uploadedDoc.Filename;

                // Create return object
                var uploadsFolder = project.GetUploadsFolder();
                var filePath = Path.Combine(uploadsFolder, actualFileName);
                uploadedFile = FileHelper.GetUploadedFileDetails(filePath, actualFileName, 0);

                // Initial sync to legacy system (without token count, will be updated later)
                await _legacySync.SyncUploadedDocument(project, actualFileName, tokenCount: 0);

                // Send update notification
                _sendUpdate(projectId, actualFileName);
            }

            // Phase 2: Extract text in background (async, no await)
            // This will UPDATE the legacy entry with token count and parts
            _ = Task.Run(async () => await ExtractTextInBackground(project, projectId, actualFileName, initStore));

            return uploadedFile;
        }

        /// <summary>
        /// Extract text from uploaded file in background
        /// </summary>
        private async Task ExtractTextInBackground(CoreProject project, string projectId, string fileName, bool initStore)
        {
            using (await _lockManager.AcquireLockAsync(projectId))
            {
                try
                {
                    var uploadsFolder = project.GetUploadsFolder();
                    var filePath = Path.Combine(uploadsFolder, fileName);

                    if (!File.Exists(filePath))
                        return;

                    // Extract text to .txt file
                    var textFilePath = FilePathHelper.GetTextFilePath(filePath);
                    await _extractor.ExtractTextFromDocument(filePath, textFilePath);

                    if (!File.Exists(textFilePath))
                        return;

                    var extractedText = await File.ReadAllTextAsync(textFilePath);

                    // Count tokens
                    var tokenCounter = new TokenCounter();
                    var tokenCount = tokenCounter.CountTokens(extractedText);

                    // Split files if needed for embedding
                    var fileParts = await _legacySync.SplitAndEmbedFile(filePath, extractedText, tokenCount);

                    // Sync metadata to legacy system (updates uploadedFiles.json with token count and parts)
                    await _legacySync.SyncDocumentMetadata(project, fileName, extractedText, tokenCount, fileParts);

                    // Reinitialize store if requested
                    if (initStore)
                    {
                        await _legacySync.InitializeStore(projectId, force: true);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error extracting text for {fileName}: {ex.Message}");
                    // TODO: Add proper logging and error tracking
                }
            }
        }

        #endregion

        #region Static Helpers

        /// <summary>
        /// Save an uploaded file to disk
        /// </summary>
        public static async Task SaveFileAsync(IFormFile file, string filePath)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file));

            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path is required", nameof(filePath));

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
        }

        #endregion
    }
}
