using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hazina.Tools.Data;
using OpenAI.Chat;

namespace Hazina.Tools.Services.FileOps
{
    /// <summary>
    /// File operations for project/chat files. Refactored to avoid direct LLM calls.
    /// </summary>
    public class FileOperationsService
    {
        private readonly IDocumentStore _store;
        private readonly ProjectsRepository _projects;
        private readonly ProjectFileLocator _fileLocator;
        private readonly string _projectId;
        private readonly string _chatId;
        private readonly string _userId;

        public FileOperationsService(IDocumentStore store, ProjectsRepository projects, string projectId, string chatId, string userId, string apiKey)
        {
            _store = store;
            _projects = projects;
            _fileLocator = new ProjectFileLocator(projects.ProjectsFolder);
            _projectId = projectId;
            _chatId = chatId;
            _userId = userId;
        }

        public Task<string> GetProjectFilesListAsync()
        {
            var keys = _store.EmbeddingStore.Embeddings.Select(e => e.Key).ToList();
            var r = string.Join("\n", keys);
            return Task.FromResult(r);
        }

        public Task<string> ReadProjectFileAsync(string file)
        {
            return Task.FromResult(File.ReadAllText(_store.GetPath(file)));
        }

        public async Task<string> AnalyzeProjectPdfFileAsync(string file)
        {
            try
            {
                var fullPath = Path.Combine(_fileLocator.GetProjectFolder(_projectId), file);
                var txt = fullPath + ".txt";
                var text = File.Exists(txt) ? File.ReadAllText(txt) : string.Empty;
                if (!string.IsNullOrWhiteSpace(text) && !text.StartsWith("Page 1"))
                    return text;

                var extractor = new TextFileExtractor(api: null);
                await extractor.ExtractTextFromPdf(fullPath, txt);
                return File.ReadAllText(txt);
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        public async Task<string> AnalyzeChatPdfFileAsync(string file)
        {
            try
            {
                var baseFolder = string.IsNullOrEmpty(_userId)
                    ? _fileLocator.GetProjectFolder(_projectId)
                    : _fileLocator.GetProjectFolder(_projectId, _userId);
                var fullPath = Path.Combine(baseFolder, "chats", _chatId + "_uploads", file);
                var txt = fullPath + ".txt";
                var text = File.Exists(txt) ? File.ReadAllText(txt) : string.Empty;
                if (!string.IsNullOrWhiteSpace(text) && !text.StartsWith("Page 1"))
                    return text;

                var extractor = new TextFileExtractor(api: null);
                await extractor.ExtractTextFromPdf(fullPath, txt);
                return File.ReadAllText(txt);
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        public async Task<string> AnalyzeChatDocumentAsync(string file)
        {
            try
            {
                var baseFolder = string.IsNullOrEmpty(_userId)
                    ? _fileLocator.GetProjectFolder(_projectId)
                    : _fileLocator.GetProjectFolder(_projectId, _userId);
                var fullPath = Path.Combine(baseFolder, "chats", _chatId + "_uploads", file);

                // Check if text file already exists
                var txt = fullPath + ".txt";
                var text = File.Exists(txt) ? File.ReadAllText(txt) : string.Empty;
                if (!string.IsNullOrWhiteSpace(text))
                    return text;

                // Extract text from document using generic extractor (supports PDF, DOCX, XLSX, etc.)
                var extractor = new TextFileExtractor(api: null);
                await extractor.ExtractTextFromDocument(fullPath, txt);

                return File.Exists(txt) ? File.ReadAllText(txt) : "Document extraction failed.";
            }
            catch (Exception e)
            {
                return $"Error analyzing document: {e.Message}";
            }
        }

        public Task<string> PerformReasoningAsync(string problemStatement, System.Collections.IEnumerable messages)
        {
            if (string.IsNullOrWhiteSpace(problemStatement))
                return Task.FromResult("No problem statement provided for reasoning.");
            return Task.FromResult("Reasoning is not enabled in this environment.");
        }
    }
}
