using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevGPT.GenerationTools.Data;
using DevGPT.GenerationTools.Models;
using DevGPT.GenerationTools.Services.Store;
using DevGPTStore.Models;
using DevGPT.GenerationTools.Services.FileOps.Helpers;
using DevGPTStore;
using Microsoft.Extensions.Configuration;

namespace DevGPT.GenerationTools.Services.Embeddings
{
    public class EmbeddingsService : IEmbeddingsService
    {
        private readonly IConfiguration _configuration;
        private readonly DevGPTStoreConfig _config;
        private readonly ProjectsRepository _projects;
        private readonly ProjectFileLocator _fileLocator;
        private readonly ProjectEmbeddingService _embeddingService;

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

        public EmbeddingsService(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _config = DevGPTStoreConfigLoader.LoadDevGPTStoreConfig();
            _projects = new ProjectsRepository(_config, configuration);
            _fileLocator = new ProjectFileLocator(_projects.ProjectsFolder);
            var chatRepository = new ProjectChatRepository(_fileLocator);
            _embeddingService = new ProjectEmbeddingService(_fileLocator, chatRepository);
        }

        private SemaphoreSlim GetLock(string projectId) => _locks.GetOrAdd(projectId, _ => new SemaphoreSlim(1, 1));

        public async Task RefreshProjectEmbeddings(string projectId, bool force = false)
        {
            if (string.IsNullOrWhiteSpace(projectId)) throw new ArgumentException("projectId is required", nameof(projectId));
            var gate = GetLock(projectId);
            await gate.WaitAsync();
            try
            {
                var project = _projects.Load(projectId);
                var setup = StoreProvider.GetStoreSetup(_fileLocator.GetProjectFolder(project.Id), _config.ApiSettings.OpenApiKey);
                var store = setup.Store;

                var files = await _embeddingService.GetEmbeddingsFileList(project);
                var toRemove = store.EmbeddingStore.Embeddings.Where(e => !files.Contains(e.Key, StringComparer.InvariantCultureIgnoreCase)).Select(e => e.Key).ToList();
                foreach (var r in toRemove)
                    await store.Remove(r);
                foreach (var file in files)
                {
                    try { await store.Embed(file); }
                    catch (Exception ex) { Console.WriteLine(ex.Message); }
                }

                await RefreshGlobalEmbeddings();
            }
            finally
            {
                gate.Release();
            }
        }

        public async Task RefreshGlobalEmbeddings()
        {
            try
            {
                var globalChatsFolderPreferred = Path.Combine(_projects.ProjectsFolder, ".chats");
                var globalChatsFolder = globalChatsFolderPreferred;
                // If a file exists with the same name, fall back to alt folder name
                if (File.Exists(globalChatsFolderPreferred))
                {
                    globalChatsFolder = Path.Combine(_projects.ProjectsFolder, "_chats");
                }
                if (!Directory.Exists(globalChatsFolder)) Directory.CreateDirectory(globalChatsFolder);

                var setup = StoreProvider.GetStoreSetup(globalChatsFolder, _config.ApiSettings.OpenApiKey);
                var store = setup.Store;
                var embeddingsFolderPreferred = Path.Combine(globalChatsFolder, "embeddings");
                var embeddingsFolder = embeddingsFolderPreferred;
                if (File.Exists(embeddingsFolderPreferred))
                {
                    embeddingsFolder = Path.Combine(globalChatsFolder, "_embeddings");
                }
                if (!Directory.Exists(embeddingsFolder)) Directory.CreateDirectory(embeddingsFolder);
                var globalEmbeddingsFile = Path.Combine(embeddingsFolder, "embeddings.json");

                var aggregated = new System.Collections.Generic.List<EmbeddingEntry>();
                var projectDirs = Directory.GetDirectories(_projects.ProjectsFolder)
                    .Select(Path.GetFileName)
                    .Where(n => n != null && !n.ToLower().StartsWith(".config") && n != ProjectsRepository.GLOBAL_PROJECT_ID && _fileLocator.Exists(n))
                    .ToList();

                foreach (var pid in projectDirs)
                {
                    try
                    {
                        var projectFolder = _fileLocator.GetProjectFolder(pid);
                        var embeddingsFile = Path.Combine(projectFolder, "embeddings", "embeddings.json");
                        if (!File.Exists(embeddingsFile)) continue;
                        var json = await File.ReadAllTextAsync(embeddingsFile);
                        var entries = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<EmbeddingEntry>>(json) ?? [];
                        if (entries.Count == 0) continue;
                        foreach (var e in entries)
                        {
                            e.Key = $"..{Path.DirectorySeparatorChar}{pid}{Path.DirectorySeparatorChar}{e.Key}";
                            aggregated.Add(e);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error reading embeddings from project {pid}: {ex.Message}");
                    }
                }

                var aggregatedJson = System.Text.Json.JsonSerializer.Serialize(aggregated, new System.Text.Json.JsonSerializerOptions { WriteIndented = false });
                await File.WriteAllTextAsync(globalEmbeddingsFile, aggregatedJson);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating global embeddings: {ex.Message}");
            }
        }

        public async Task EmbedProjectFile(string projectId, string relativeFilePath)
        {
            var setup = StoreProvider.GetStoreSetup(_fileLocator.GetProjectFolder(projectId), _config.ApiSettings.OpenApiKey);
            await setup.Store.Embed(relativeFilePath);
        }

        public async Task EmbedChatUpload(string projectId, string chatId, string relativeFileName, string userId = null)
        {
            var folder = string.IsNullOrWhiteSpace(userId)
                ? _fileLocator.GetChatUploadsFolder(projectId, chatId)
                : _fileLocator.GetChatUploadsFolder(projectId, chatId, userId);
            var setup = StoreProvider.GetStoreSetup(folder, _config.ApiSettings.OpenApiKey);
            await setup.Store.Embed(relativeFileName);
        }

        public async Task PromoteChatFileToProject(string projectId, string chatId, string filePrefix, string userId = null)
        {
            var projectFolder = _fileLocator.GetProjectFolder(projectId);
            var chatUploadsFolder = string.IsNullOrWhiteSpace(userId)
                ? _fileLocator.GetChatUploadsFolder(projectId, chatId)
                : _fileLocator.GetChatUploadsFolder(projectId, chatId, userId);

            var chatSetup = StoreProvider.GetStoreSetup(chatUploadsFolder, _config.ApiSettings.OpenApiKey);
            var projectSetup = StoreProvider.GetStoreSetup(projectFolder, _config.ApiSettings.OpenApiKey);

            var matches = chatSetup.Store.EmbeddingStore.Embeddings.Where(f => f.Key.StartsWith(filePrefix)).ToList();
            foreach (var m in matches)
            {
                var src = Path.Combine(chatUploadsFolder, m.Key);
                var dst = Path.Combine(projectFolder, m.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                File.Copy(src, dst, overwrite: true);
                await projectSetup.Store.Embed(m.Key);
            }
        }

        public async Task DemoteChatFileFromProject(string projectId, string chatId, string filePrefix)
        {
            var project = _projects.Load(projectId);
            var setup = StoreProvider.GetStoreSetup(_fileLocator.GetProjectFolder(project.Id), _config.ApiSettings.OpenApiKey);
            var files = setup.Store.EmbeddingStore.Embeddings.Where(f => f.Key.StartsWith(filePrefix)).Select(f => f.Key).ToList();
            foreach (var f in files)
                await setup.Store.Remove(f);
        }

        public async Task ExtractAndEmbedChatUpload(string projectId, string chatId, string fileName, string userId = null)
        {
            if (string.IsNullOrWhiteSpace(projectId)) throw new ArgumentException(nameof(projectId));
            if (string.IsNullOrWhiteSpace(chatId)) throw new ArgumentException(nameof(chatId));
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException(nameof(fileName));

            var folder = string.IsNullOrWhiteSpace(userId)
                ? _fileLocator.GetChatUploadsFolder(projectId, chatId)
                : _fileLocator.GetChatUploadsFolder(projectId, chatId, userId);

            var filePath = Path.Combine(folder, fileName);
            var index = fileName.LastIndexOf('.')
                        ;
            var extension = index >= 0 ? fileName.Substring(index + 1) : "txt";
            var textFilePath = Path.Combine(folder, Path.GetFileNameWithoutExtension(filePath) + "." + extension + ".txt");

            var setup = StoreProvider.GetStoreSetup(folder, _config.ApiSettings.OpenApiKey);
            // TODO: Fix ILLMClient interface mismatch between DevGPT.LLMs.Client and DevGPT.LLMClient
            // Temporarily disabled - requires package version alignment
            // var extractor = new TextFileExtractor(setup.LLMClient);
            // await extractor.ExtractTextFromDocument(filePath, textFilePath);

            await setup.Store.Embed(fileName);
        }

        private class EmbeddingEntry
        {
            public string Key { get; set; }
            public string Checksum { get; set; }
            public System.Collections.Generic.List<double> Data { get; set; }
        }
    }
}
