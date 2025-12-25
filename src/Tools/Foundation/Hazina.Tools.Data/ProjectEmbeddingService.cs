using Hazina.Tools.Data;
using Hazina.Tools.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HazinaStore.Models;

namespace Hazina.Tools.Data
{
    /// <summary>
    /// Service responsible for embedding-related file operations.
    /// Handles file splitting, text file detection, and embeddings file list generation.
    /// </summary>
    public class ProjectEmbeddingService
    {
        private readonly ProjectFileLocator _fileLocator;
        private readonly ProjectChatRepository _chatRepository;

        public class EmbeddingsInclusion
        {
            public bool IncludeUploadedDocuments { get; set; } = true;
            public bool IncludeChats { get; set; } = true;
            public bool IncludeAnalysisFields { get; set; } = true;
        }

        public ProjectEmbeddingService(ProjectFileLocator fileLocator, ProjectChatRepository chatRepository)
        {
            _fileLocator = fileLocator ?? throw new ArgumentNullException(nameof(fileLocator));
            _chatRepository = chatRepository ?? throw new ArgumentNullException(nameof(chatRepository));
        }

        private bool IsTextFile(string filePath)
        {
            // List of text-based extensions that can be embedded
            var textExtensions = new[] { ".txt", ".json", ".xml", ".csv", ".md", ".html", ".css", ".js", ".ts", ".cs", ".java", ".py", ".actions" };
            var extension = Path.GetExtension(filePath).ToLower();

            // Check if it's a known text extension
            if (textExtensions.Contains(extension))
                return true;

            // For files without extension or unknown extensions, try to detect if it's text
            try
            {
                using (var stream = File.OpenRead(filePath))
                {
                    var buffer = new byte[512];
                    var bytesRead = stream.Read(buffer, 0, buffer.Length);

                    // Check for null bytes (common in binary files)
                    for (int i = 0; i < bytesRead; i++)
                    {
                        if (buffer[i] == 0)
                            return false;
                    }

                    // If no null bytes found in first 512 bytes, assume it's text
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public List<string> GetSplitFiles(Project project, string file, string split = ",")
        {
            var fullPath = _fileLocator.GetPath(project.Id, file);
            if (File.Exists(fullPath))
            {
                // Only process text files
                if (!IsTextFile(fullPath))
                {
                    Console.WriteLine($"Skipping binary file for embedding: {file}");
                    return new List<string>();
                }

                var content = File.ReadAllText(fullPath);

                var splitter = new DocumentSplitter();
                var files = new List<string>();
                var parts = splitter.SplitDocument(content, split);
                for (var i = 0; i < parts.Count; ++i)
                {
                    var relPath = file + "." + i;
                    var path = _fileLocator.GetPath(project.Id, relPath);
                    File.WriteAllText(path, parts[i]);
                    files.Add(relPath);
                }
                return files;
            }
            return new List<string>();
        }

        public async Task<List<string>> GetEmbeddingsFileList(Project project, EmbeddingsInclusion inclusion = null)
        {
            inclusion ??= new EmbeddingsInclusion();
            var list = new List<string>();

            // Skip for global project - it's virtual and has no files
            if (project.Id == ProjectsRepository.GLOBAL_PROJECT_ID)
            {
                return list;
            }

            // 1. Add project metadata (always split if needed)
            list.AddRange(GetSplitFiles(project, project.Id + ".json"));

            // 2. Add uploaded documents (always split if needed)
            if (inclusion.IncludeUploadedDocuments)
            {
                try
                {
                    var uploadsJsonPath = Path.Combine(_fileLocator.GetProjectFolder(project.Id), "uploadedFiles.json");
                    if (File.Exists(uploadsJsonPath))
                    {
                        var json = await File.ReadAllTextAsync(uploadsJsonPath);
                        var uploadedfiles = JsonSerializer.Deserialize<List<UploadedFile>>(json) ?? new List<UploadedFile>();
                        foreach (var uploadedfile in uploadedfiles)
                        {
                            if (!string.IsNullOrWhiteSpace(uploadedfile?.TextFilename))
                            {
                                list.AddRange(GetUploadedTextFileParts(project, uploadedfile.TextFilename));
                            }
                        }
                    }
                }
                catch { /* ignore and continue */ }
            }

            // 3. Add *.actions files from project root (always split if needed)
            var projectFolder = _fileLocator.GetProjectFolder(project.Id);
            var actionFiles = Directory.GetFiles(projectFolder, "*.actions", SearchOption.TopDirectoryOnly);
            foreach (var actionFile in actionFiles)
            {
                var fileName = Path.GetFileName(actionFile);
                list.AddRange(GetSplitFiles(project, fileName));
            }

            // 3b. Add analysis field files (topic-synopsis, narrative-stance, etc.) if present
            if (inclusion.IncludeAnalysisFields)
            {
                var analysisFiles = new[]
                {
                    IntakeRepository.TopicSynopsisFile,
                    IntakeRepository.NarrativeStanceFile,
                    IntakeRepository.TargetGroupFile,
                    IntakeRepository.PhilosophicalCommitmentsFile,
                    IntakeRepository.RevisionistClaimsFile,
                    IntakeRepository.CentralThesisFile,
                    IntakeRepository.EvidenceBaseFile,
                    IntakeRepository.CounterNarrativeStructureFile,
                    IntakeRepository.AestheticDirectionFile,
                    IntakeRepository.ProofStrategyFile,
                    IntakeRepository.IntendedImpactFile,
                };
                foreach (var af in analysisFiles)
                {
                    if (File.Exists(_fileLocator.GetPath(project.Id, af)))
                    {
                        // For structured JSON analysis files, split on newlines to keep tokens readable
                        list.AddRange(GetSplitFiles(project, af, "\n"));
                    }
                }
            }

            if (inclusion.IncludeChats)
            {
                // 4. Add chat metadata file (chats/chats.json)
                var chatsMetaFile = Path.Combine("chats", "chats.json");
                if (File.Exists(_fileLocator.GetPath(project.Id, chatsMetaFile)))
                {
                    list.AddRange(GetSplitFiles(project, chatsMetaFile));
                }

                // 5. Add individual chat JSON files (chats/{guid}.json)
                var chatMetas = _chatRepository.GetChatMetaData(project.Id);
                foreach (var chatMeta in chatMetas)
                {
                    var chatFile = Path.Combine("chats", $"{chatMeta.Id}.json");
                    if (File.Exists(_fileLocator.GetPath(project.Id, chatFile)))
                    {
                        list.AddRange(GetSplitFiles(project, chatFile));
                    }
                }

                // 6. Add chat uploaded files marked with IncludeInProject
                var chatsFiles = chatMetas.SelectMany(chat =>
                {
                    var chatFile = _fileLocator.GetChatFile(project.Id, chat.Id);
                    var messages = _chatRepository.LoadListFileOrDefault<ConversationMessage>(chatFile);
                    var chatUploadsFolder = Path.Combine("chats", chat.Id + "_uploads");
                    var m2 = messages.Where(message => message.Payload is HazinaStoreChatFile).Select(message => Path.Combine(chatUploadsFolder, (message.Payload as HazinaStoreChatFile).File)).ToList();
                    return m2;
                }).ToList();

                // Always split chat uploaded files
                foreach (var chatFile in chatsFiles)
                {
                    list.AddRange(GetSplitFiles(project, chatFile));
                }
            }

            return list;
        }

        private IEnumerable<string> GetUploadedTextFileParts(Project project, string file)
        {
            file = Path.Combine("Uploads", file);
            try
            {
                var projectfolder = _fileLocator.GetProjectFolder(project.Id);
                var length = projectfolder.Length;
                var folder = Directory.GetParent(_fileLocator.GetPath(project.Id, file)).FullName;
                var separatorIndex = file.LastIndexOf(Path.DirectorySeparatorChar);
                var dotIndex = file.IndexOf(".");
                string name = "";
                if (dotIndex >= 0)
                {
                    name = file.Substring(separatorIndex + 1, dotIndex - (separatorIndex + 1));
                }
                else
                {
                    name = file.Substring(separatorIndex + 1);
                }
                var allFiles = Directory.GetFiles(folder);
                var files = allFiles.Where(filePath => IsTextFilePart(filePath, name)).ToList();
                if (files.Count == 0) return [file];

                files = files.Select(f => f.Substring(length + 1)).ToList();

                return files;
            }
            catch (Exception e)
            {
                return new List<string>();
            }
        }

        private static bool IsTextFilePart(string filePath, string name)
        {
            var indexOfName = filePath.IndexOf(name);
            if (indexOfName < 0 || filePath.Length <= indexOfName + 1 + name.Length) return false;
            var afterName = filePath.Substring(indexOfName + 1 + name.Length);
            var partNr = afterName.Split(".")[0];
            return partNr.StartsWith("p") && int.TryParse(partNr.Substring(1), out var x);
        }

        /// <summary>
        /// Get list of all embeddings files across all projects for global chat
        /// This aggregates embeddings from all non-archived projects
        /// </summary>
        public async Task<List<string>> GetAllProjectEmbeddingsFiles(ProjectsRepository projectsRepository)
        {
            var allFiles = new List<string>();

            // Get all project directories
            var projectDirs = Directory.GetDirectories(_fileLocator.ProjectsFolder)
                .Select(Path.GetFileName)
                .Where(projectName =>
                    projectName != null &&
                    !projectName.ToLower().StartsWith(".config") &&
                    projectName != ProjectsRepository.GLOBAL_PROJECT_ID &&
                    _fileLocator.Exists(projectName))
                .ToList();

            foreach (var projectName in projectDirs)
            {
                try
                {
                    var project = projectsRepository.Load(projectName);
                    if (project.Archived) continue;

                    var embeddingsFile = _fileLocator.GetEmbeddingsFilePath(projectName);
                    if (File.Exists(embeddingsFile))
                    {
                        allFiles.Add(embeddingsFile);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading project {projectName} for global embeddings: {ex.Message}");
                }
            }

            return allFiles;
        }
    }
}

