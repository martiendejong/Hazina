using DevGPT.GenerationTools.Models.WordPress.Blogs;
using DevGPTStore.Models;
using DevGPT.GenerationTools.Data;
using DevGPT.GenerationTools.Models;
using System.IO;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System.Linq;
using OpenAI.Chat;
using System.Collections.Generic;
using _File = System.IO.File;
using DevGPTStore;
using DevGPT.GenerationTools.Services.Store;
using DevGPT.GenerationTools.Services.Web;
using DevGPT.GenerationTools.Services.FileOps.Helpers;
using System.Text.Json;
using System;
using System.Reflection;

namespace DevGPT.GenerationTools.AI.Agents
{
    public class GeneratorAgentBase
    {
        public IConfiguration AppConfig;
        public DevGPTStoreConfig Config;
        public ProjectsRepository Projects;
        public IntakeRepository Intake;
        public DevGPTStoreAgent DevGPTStoreAgent;

        public string LogFilePath;
        public string BasisPrompt;
        private readonly DevGPT.GenerationTools.Services.Embeddings.EmbeddingsService _embeddings;
        private readonly ProjectFileLocator _fileLocator;
        private readonly ProjectEmbeddingService _embeddingService;

        public DevGPTStoreLogger GetLogger(string project)
        {
            return new DevGPTStoreLogger(LogFilePath, project);
        }

        public GeneratorAgentBase(IConfiguration configuration, string basisPrompt)
        {
            BasisPrompt = basisPrompt;
            AppConfig = configuration;
            Config = DevGPTStoreConfigLoader.LoadDevGPTStoreConfig();
            Projects = new ProjectsRepository(Config, configuration);
            Intake = new IntakeRepository(Config, configuration);
            DevGPTStoreAgent = new DevGPTStoreAgent(Projects);
            LogFilePath = Path.Combine(Projects.ProjectsFolder, "logs.txt");
            _embeddings = new DevGPT.GenerationTools.Services.Embeddings.EmbeddingsService(configuration);
            _fileLocator = new ProjectFileLocator(Projects.ProjectsFolder);
            var chatRepository = new ProjectChatRepository(_fileLocator);
            _embeddingService = new ProjectEmbeddingService(_fileLocator, chatRepository);
        }

        // TODO: Type conflict between DevGPT.Classes and DevGPT.LLMs.Classes - requires package alignment
        /* protected List<global::DevGPT.Classes.DevGPTChatMessage> GetAssistantPrompts(string specificPrompt)
        {
            var assistantPrompts = new List<global::DevGPT.Classes.DevGPTChatMessage>()
            {
                new global::DevGPT.Classes.DevGPTChatMessage(global::DevGPT.Classes.DevGPTMessageRole.System, BasisPrompt),
                new global::DevGPT.Classes.DevGPTChatMessage(global::DevGPT.Classes.DevGPTMessageRole.System, specificPrompt),
            };
            return assistantPrompts;
        } */

        public async Task<IDocumentStore> InitStore(Project project, bool updateEmbeddings = false)
        {
            if (project.Id == ProjectsRepository.GLOBAL_PROJECT_ID)
            {
                var globalFolder = Path.Combine(Projects.ProjectsFolder, ".chats");
                if (!Directory.Exists(globalFolder))
                    Directory.CreateDirectory(globalFolder);

                var setup = StoreProvider.GetStoreSetup(globalFolder, Config.ApiSettings.OpenApiKey);
                if (updateEmbeddings)
                {
                    await _embeddings.RefreshGlobalEmbeddings();
                }
                return setup.Store;
            }

            var setup2 = StoreProvider.GetStoreSetup(_fileLocator.GetProjectFolder(project.Id), Config.ApiSettings.OpenApiKey);
            if(updateEmbeddings)
                await _embeddings.RefreshProjectEmbeddings(project.Id, true);
            return setup2.Store;
        }

        private async Task UpdateAllProjectsGlobalEmbeddings()
        {
            try
            {
                var dummyProject = new Project { Id = "dummy" };
                await UpdateGlobalChatsEmbeddings(dummyProject);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating all projects global embeddings: {ex.Message}");
            }
        }

        private async Task UpdateProjectEmbeddings(Project project, IDocumentStore store)
        {
            // Get the new list of files that should be embedded
            var files = await _embeddingService.GetEmbeddingsFileList(project);

            var toRemove = store.EmbeddingStore.Embeddings.Where(e => !files.Contains(e.Key, StringComparer.InvariantCultureIgnoreCase)).Select(rr => rr.Key).ToList();
            foreach (var remove in toRemove)
            {
                await store.Remove(remove);
            }

            // Re-embed all files in the new list
            foreach (var file in files)
            {
                try
                {
                    await store.Embed(file);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }

            // Update global chats folder with embeddings that have relative paths
            await UpdateGlobalChatsEmbeddings(project);
        }

        public async Task UpdateGlobalChatsEmbeddings(Project project)
        {
            try
            {
                var globalChatsFolder = Path.Combine(Projects.ProjectsFolder, ".chats");
                if (!Directory.Exists(globalChatsFolder))
                    Directory.CreateDirectory(globalChatsFolder);

                var globalEmbeddingsFile = Path.Combine(globalChatsFolder, "embeddings");
                var allProjects = Directory.GetDirectories(Projects.ProjectsFolder)
                    .Select(Path.GetFileName)
                    .Where(projectName => projectName != null && !projectName.ToLower().StartsWith(".") && projectName != ProjectsRepository.GLOBAL_PROJECT_ID && _fileLocator.Exists(projectName))
                    .ToList();

                var aggregatedEmbeddings = new List<EmbeddingEntry>();
                foreach (var projectId in allProjects)
                {
                    try
                    {
                        var proj = Projects.Load(projectId);
                        if (proj.Archived) continue;
                        var projectEmbeddingsFile = Path.Combine(_fileLocator.GetProjectFolder(projectId), "embeddings");
                        if (!_File.Exists(projectEmbeddingsFile)) continue;
                        var embeddingsJson = await _File.ReadAllTextAsync(projectEmbeddingsFile);
                        var projectEmbeddings = JsonSerializer.Deserialize<List<EmbeddingEntry>>(embeddingsJson);
                        if (projectEmbeddings == null || projectEmbeddings.Count == 0) continue;
                        foreach (var embedding in projectEmbeddings)
                        {
                            embedding.Key = $"..{Path.DirectorySeparatorChar}{projectId}{Path.DirectorySeparatorChar}{embedding.Key}";
                            aggregatedEmbeddings.Add(embedding);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error reading embeddings from project {projectId}: {ex.Message}");
                    }
                }

                var aggregatedJson = JsonSerializer.Serialize(aggregatedEmbeddings, new JsonSerializerOptions { WriteIndented = false });
                await _File.WriteAllTextAsync(globalEmbeddingsFile, aggregatedJson);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating global chats embeddings: {ex.Message}");
            }
        }

        private class EmbeddingEntry
        {
            public string Key { get; set; }
            public string Checksum { get; set; }
            public List<double> Data { get; set; }
        }

        public async Task<DocumentGenerator> GetGeneratorWithoutPrompt(Project project)
        {
            var store = await InitStore(project);
            var folder = _fileLocator.GetProjectFolder(project.Id);
            var setup = StoreProvider.GetStoreSetup(folder, Config.ApiSettings.OpenApiKey);
            var g = new DocumentGenerator(setup.Store, new List<DevGPTChatMessage>(), setup.LLMClient, new List<IDocumentStore>());
            return g;
        }

        public async Task<DocumentGenerator> GetGenerator(Project project, string prompt)
        {
            var assistantPrompts = new List<DevGPTChatMessage>()
            {
                new DevGPTChatMessage(DevGPTMessageRole.System, prompt),
            };
            if(!string.IsNullOrWhiteSpace(project.KlantSpecifiekePrompt))
                assistantPrompts.Add(new DevGPTChatMessage(DevGPTMessageRole.System, project.KlantSpecifiekePrompt));

            var store = await InitStore(project);
            var folder = _fileLocator.GetProjectFolder(project.Id);
            var setup = StoreProvider.GetStoreSetup(folder, Config.ApiSettings.OpenApiKey);
            var g = new DocumentGenerator(store, assistantPrompts, setup.LLMClient, new List<IDocumentStore>());
            return g;
        }

        public async Task InternalGenerate(string id, string prompt, string[] systemPrompts, string documentName, string path)
        {
            // Direct implementation for backward compatibility
            var project = Projects.Load(id);
            var store = await InitStore(project);
            DocumentGenerator g = await GetGenerator(project, systemPrompts[0]);
            g.BaseMessages.AddRange(systemPrompts.Skip(1).Select(p => new DevGPTChatMessage(DevGPTMessageRole.System, p)));
            var context = new StoreToolsContext(new OpenAIConfig().Model, Config.ApiSettings.OpenApiKey, store, Projects, Intake, id, "", this);
            var tokenSource = new CancellationTokenSource();
            
            // Call the generic method using reflection or direct call with proper type
            var method = typeof(GeneratorAgentBase).GetMethod(nameof(InternalGenerate), new[] { typeof(string), typeof(string[]), typeof(string), typeof(string), typeof(string) });
            var genericMethod = method.MakeGenericMethod(typeof(GeneratedTextResponse));
            await (Task)genericMethod.Invoke(this, new object[] { id, systemPrompts, prompt, documentName, path });
        }

        public async Task InternalGenerate(string id, string systemPrompt, string instruction, string documentName, string path)
        {
            // Direct implementation for backward compatibility
            var project = Projects.Load(id);
            var store = await InitStore(project);
            DocumentGenerator g = await GetGenerator(project, systemPrompt);
            var context = new StoreToolsContext(new OpenAIConfig().Model, Config.ApiSettings.OpenApiKey, store, Projects, Intake, id, "", this);
            var tokenSource = new CancellationTokenSource();
            
            // Call the generic method using reflection
            var method = typeof(GeneratorAgentBase).GetMethod(nameof(InternalGenerate), new[] { typeof(string), typeof(string[]), typeof(string), typeof(string), typeof(string) });
            var genericMethod = method.MakeGenericMethod(typeof(GeneratedTextResponse));
            await (Task)genericMethod.Invoke(this, new object[] { id, new[] { systemPrompt }, instruction, documentName, path });
        }

        public async Task<LLMResponse<T?>> InternalGenerate<T>(string id, string[] systemPrompts, string instruction, string documentName, string path) where T : ChatResponse<T>, new()
        {
            if (systemPrompts == null || systemPrompts.Length == 0)
                throw new ArgumentException("At least one system prompt is required", nameof(systemPrompts));

            var project = Projects.Load(id);
            var store = await InitStore(project);
            DocumentGenerator g = await GetGenerator(project, systemPrompts[0]);
            g.BaseMessages.AddRange(systemPrompts.Skip(1).Select(p => new DevGPTChatMessage(DevGPTMessageRole.System, p)));
            var context = new StoreToolsContext(new OpenAIConfig().Model, Config.ApiSettings.OpenApiKey, store, Projects, Intake, id, "", this);
            var tokenSource = new CancellationTokenSource();
            var response = await g.GetResponse<T>(instruction, tokenSource.Token, [], true, true, context);
            Store(id, response.Result, path);
            return response;
        }

        public void Store(string id, string document, string file) 
        {
            var filePath = _fileLocator.GetPath(id, file);
            _File.WriteAllText(filePath, document);
        }

        public void Store<T>(string id, T document, string file)
        {
            // Special handling for GeneratedTextResponse - store plain text instead of JSON
            if (typeof(T) == typeof(GeneratedTextResponse) && document is GeneratedTextResponse textResponse)
            {
                var filePath = _fileLocator.GetPath(id, file);
                _File.WriteAllText(filePath, textResponse.GeneratedText ?? "");
                return;
            }

            // Serialize to JSON for all other types
            var json = System.Text.Json.JsonSerializer.Serialize(document);
            var filePath2 = _fileLocator.GetPath(id, file);
            _File.WriteAllText(filePath2, json);
        }

        public void Store<T>(string id, GeneratedObject<T> document, string file) where T : Serializer<T>
        {
            var filePath = _fileLocator.GetPath(id, file);
            document.Save(filePath);
        }
    }
}



