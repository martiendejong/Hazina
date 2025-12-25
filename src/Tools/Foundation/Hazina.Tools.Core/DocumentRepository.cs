using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HazinaStore.Core.Interfaces;
using HazinaStore.Core.Models;
using HazinaStore.Models;
using Hazina.Tools.Models;

namespace HazinaStore.Core
{
    /// <summary>
    /// Manages all documents within a project
    /// Handles both uploaded documents and generated documents
    /// </summary>
    public class DocumentRepository
    {
        private readonly string _projectPath;
        private readonly Project _project;
        private const string UploadedFilesMetadataFile = "uploadedFiles.json";

        /// <summary>
        /// Path to the project folder
        /// </summary>
        public string ProjectPath => _projectPath;

        /// <summary>
        /// Parent project
        /// </summary>
        public Project Project => _project;

        /// <summary>
        /// Initialize document repository for a project
        /// </summary>
        /// <param name="projectPath">Path to project folder</param>
        /// <param name="project">Parent project</param>
        internal DocumentRepository(string projectPath, Project project)
        {
            _projectPath = projectPath ?? throw new ArgumentNullException(nameof(projectPath));
            _project = project ?? throw new ArgumentNullException(nameof(project));
        }

        #region Uploaded Documents

        /// <summary>
        /// Upload a document to the project
        /// </summary>
        /// <param name="filename">Original filename</param>
        /// <param name="content">File content as bytes</param>
        /// <param name="extractedText">Extracted text content (for PDFs, images, etc.)</param>
        /// <returns>Uploaded document metadata</returns>
        public UploadedDocument UploadDocument(string filename, byte[] content, string extractedText = null)
        {
            if (string.IsNullOrWhiteSpace(filename))
                throw new ArgumentException("Filename cannot be empty", nameof(filename));

            if (content == null || content.Length == 0)
                throw new ArgumentException("Content cannot be empty", nameof(content));

            // Create Uploads folder if it doesn't exist
            var uploadsFolder = Path.Combine(_projectPath, "Uploads");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

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

            // Create metadata
            var document = new UploadedDocument
            {
                Id = Guid.NewGuid().ToString(),
                Name = Path.GetFileName(filePath),
                Filename = Path.GetFileName(filePath),
                FilePath = Path.Combine("Uploads", Path.GetFileName(filePath)),
                ExtractedText = extractedText ?? string.Empty,
                UploadedAt = DateTime.UtcNow,
                FileSize = content.Length,
                MimeType = GetMimeType(filename)
            };

            // NOTE: We don't save to uploadedFiles.json here anymore
            // The legacy sync service will handle that to avoid duplicates
            // The Core API just manages the physical file on disk

            return document;
        }

        /// <summary>
        /// Get all uploaded documents metadata
        /// NOTE: During transition, this returns empty list
        /// Use the legacy GetUploadedFiles() for actual file list
        /// </summary>
        /// <returns>List of uploaded documents</returns>
        public List<UploadedDocument> GetUploadedDocuments()
        {
            // During transition period, we don't maintain a separate uploadedDocuments.json
            // The legacy uploadedFiles.json is the source of truth
            // This method is only used internally for finding files to delete
            return new List<UploadedDocument>();
        }

        /// <summary>
        /// Get a specific uploaded document by ID
        /// </summary>
        /// <param name="documentId">Document ID</param>
        /// <returns>Uploaded document or null if not found</returns>
        public UploadedDocument GetUploadedDocument(string documentId)
        {
            return GetUploadedDocumentsMetadata().FirstOrDefault(d => d.Id == documentId);
        }

        /// <summary>
        /// Delete an uploaded document
        /// </summary>
        /// <param name="documentId">Document ID to delete</param>
        public void DeleteUploadedDocument(string documentId)
        {
            var docs = GetUploadedDocumentsMetadata();
            var doc = docs.FirstOrDefault(d => d.Id == documentId);

            if (doc != null)
            {
                // Delete physical file
                // Handle both new format (with FilePath) and old format (without FilePath)
                string fullPath = null;

                if (!string.IsNullOrWhiteSpace(doc.FilePath))
                {
                    fullPath = Path.Combine(_projectPath, doc.FilePath);
                }
                else if (!string.IsNullOrWhiteSpace(doc.Filename))
                {
                    // Fallback for old documents without FilePath
                    fullPath = Path.Combine(_projectPath, "Uploads", doc.Filename);
                }

                if (fullPath != null && File.Exists(fullPath))
                    File.Delete(fullPath);

                // Also delete the extracted text file if it exists
                if (!string.IsNullOrWhiteSpace(doc.Filename))
                {
                    var extension = Path.GetExtension(doc.Filename)?.TrimStart('.') ?? "txt";
                    var textFileName = $"{Path.GetFileNameWithoutExtension(doc.Filename)}.{extension}.txt";
                    var textFilePath = Path.Combine(_projectPath, "Uploads", textFileName);
                    if (File.Exists(textFilePath))
                        File.Delete(textFilePath);
                }

                // Remove from metadata
                docs.Remove(doc);
                SaveUploadedDocumentsMetadata(docs);
            }
        }

        /// <summary>
        /// Read uploaded documents metadata from disk
        /// </summary>
        private List<UploadedDocument> GetUploadedDocumentsMetadata()
        {
            var metadataPath = Path.Combine(_projectPath, UploadedFilesMetadataFile);

            if (!File.Exists(metadataPath))
                return new List<UploadedDocument>();

            try
            {
                var json = File.ReadAllText(metadataPath);
                return JsonSerializer.Deserialize<List<UploadedDocument>>(json) ?? new List<UploadedDocument>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading uploaded files metadata: {ex.Message}");
                return new List<UploadedDocument>();
            }
        }

        /// <summary>
        /// Save uploaded documents metadata to disk
        /// </summary>
        private void SaveUploadedDocumentsMetadata(List<UploadedDocument> documents)
        {
            var metadataPath = Path.Combine(_projectPath, UploadedFilesMetadataFile);
            var json = JsonSerializer.Serialize(documents, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(metadataPath, json);
        }

        #endregion

        #region Generated Documents

        /// <summary>
        /// Create or update a generated document
        /// </summary>
        /// <param name="name">Document name</param>
        /// <param name="content">Document content</param>
        /// <returns>Generated document</returns>
        public GeneratedDocument SaveGeneratedDocument(string name, string content)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Document name cannot be empty", nameof(name));

            var documentPath = Path.Combine(_projectPath, $"{name}.json");

            GeneratedDocument document;

            // Load existing or create new
            if (File.Exists(documentPath))
            {
                document = GeneratedDocument.Load(documentPath);
            }
            else
            {
                document = new GeneratedDocument
                {
                    Name = name,
                    Revisions = new List<DocumentRevision>()
                };
            }

            // Add new revision
            document.Revisions.Add(new DocumentRevision
            {
                Content = content,
                Timestamp = DateTime.UtcNow
            });

            // Save
            document.Save(documentPath);

            return document;
        }

        /// <summary>
        /// Get a generated document by name
        /// </summary>
        /// <param name="name">Document name</param>
        /// <returns>Generated document or null if not found</returns>
        public GeneratedDocument GetGeneratedDocument(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            var documentPath = Path.Combine(_projectPath, $"{name}.json");

            if (!File.Exists(documentPath))
                return null;

            try
            {
                return GeneratedDocument.Load(documentPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading generated document '{name}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get all generated documents in the project
        /// </summary>
        /// <returns>List of generated documents</returns>
        public List<GeneratedDocument> GetGeneratedDocuments()
        {
            var documents = new List<GeneratedDocument>();

            // Find all .json files in project root (excluding special files)
            var jsonFiles = Directory.GetFiles(_projectPath, "*.json", SearchOption.TopDirectoryOnly);

            var excludeFiles = new[]
            {
                UploadedFilesMetadataFile,
                $"{_project.Id}.json", // Project metadata
                "chats.json",
                "users.json",
                "contenthooks.json",
                "ConversationStarters.json",
                "published_posts.json",
                "roleprompts.json"
            };

            foreach (var jsonFile in jsonFiles)
            {
                var filename = Path.GetFileName(jsonFile);

                // Skip excluded files
                if (excludeFiles.Contains(filename, StringComparer.OrdinalIgnoreCase))
                    continue;

                // Skip prompt files
                if (filename.Contains(".prompts."))
                    continue;

                try
                {
                    var doc = GeneratedDocument.Load(jsonFile);
                    documents.Add(doc);
                }
                catch
                {
                    // Skip files that aren't valid GeneratedDocument format
                }
            }

            return documents;
        }

        /// <summary>
        /// Delete a generated document
        /// </summary>
        /// <param name="name">Document name</param>
        public void DeleteGeneratedDocument(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            var documentPath = Path.Combine(_projectPath, $"{name}.json");

            if (File.Exists(documentPath))
                File.Delete(documentPath);
        }

        /// <summary>
        /// Check if a generated document exists
        /// </summary>
        /// <param name="name">Document name</param>
        /// <returns>True if document exists</returns>
        public bool GeneratedDocumentExists(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            var documentPath = Path.Combine(_projectPath, $"{name}.json");
            return File.Exists(documentPath);
        }

        #endregion

        #region Search and Retrieval

        /// <summary>
        /// Get all documents (both uploaded and generated)
        /// </summary>
        /// <returns>List of all documents</returns>
        public List<IDocument> GetAllDocuments()
        {
            var documents = new List<IDocument>();

            // Add uploaded documents
            documents.AddRange(GetUploadedDocuments().Cast<IDocument>());

            // Add generated documents
            documents.AddRange(GetGeneratedDocuments().Cast<IDocument>());

            return documents;
        }

        /// <summary>
        /// Search documents by content or name
        /// </summary>
        /// <param name="query">Search query</param>
        /// <returns>Matching documents</returns>
        public List<IDocument> SearchDocuments(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return GetAllDocuments();

            var lowerQuery = query.ToLower();

            return GetAllDocuments()
                .Where(d =>
                    d.Name.ToLower().Contains(lowerQuery) ||
                    d.Content.ToLower().Contains(lowerQuery))
                .ToList();
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Get MIME type from filename
        /// </summary>
        private string GetMimeType(string filename)
        {
            var extension = Path.GetExtension(filename).ToLower();

            return extension switch
            {
                ".pdf" => "application/pdf",
                ".txt" => "text/plain",
                ".md" => "text/markdown",
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".csv" => "text/csv",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                _ => "application/octet-stream"
            };
        }

        #endregion
    }
}

