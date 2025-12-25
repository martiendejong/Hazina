using Hazina.Tools.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HazinaStore.Models;

namespace Hazina.Tools.Data
{
    public class IntakeRepository
    {
        // Intake-related file constants
        public static readonly string UploadsFile = "uploadedFiles.json";
        public static readonly string UploadsFolderName = "Uploads";

        // Analysis-related file constants (kept for AnalysisController)
        public static readonly string TopicSynopsisFile = "topic-synopsis.json";
        public static readonly string NarrativeStanceFile = "narrative-stance.json";
        public static readonly string TargetGroupFile = "target-group.json";
        public static readonly string PhilosophicalCommitmentsFile = "philosophical-commitments.json";
        public static readonly string RevisionistClaimsFile = "revisionist-claims.json";
        public static readonly string CentralThesisFile = "central-thesis.json";
        public static readonly string EvidenceBaseFile = "evidence-base.json";
        public static readonly string CounterNarrativeStructureFile = "counter-narrative-structure.json";
        public static readonly string AestheticDirectionFile = "aesthetic-direction.json";
        public static readonly string ProofStrategyFile = "proof-strategy.json";
        public static readonly string IntendedImpactFile = "intended-impact.json";

        // Story-related file constants
        public static readonly string StoriesFile = "stories.json";

        public string ProjectsFolder;
        public ProjectsRepository Projects; // Backward compatibility
        private readonly ProjectFileLocator _fileLocator;
        private readonly ProjectEmbeddingService _embeddingService;

        public IntakeRepository(HazinaStoreConfig configuration, IConfiguration appConfig)
        {
            ProjectsFolder = configuration.ProjectSettings.ProjectsFolder;
            Projects = new ProjectsRepository(configuration, appConfig);
            _fileLocator = new ProjectFileLocator(ProjectsFolder);
            var chatRepository = new ProjectChatRepository(_fileLocator);
            _embeddingService = new ProjectEmbeddingService(_fileLocator, chatRepository);
        }

        public string GetUploadsFolder(string project) => Path.Combine(GetProjectFolder(project), UploadsFolderName);
        public string GetUploadsFilePath(string project) => Path.Combine(GetProjectFolder(project), UploadsFile);

        public List<UploadedFile> GetUploadedFiles(string uploadsFilePath)
        {
            if (File.Exists(uploadsFilePath))
            {
                var json = File.ReadAllText(uploadsFilePath);
                return JsonSerializer.Deserialize<List<UploadedFile>>(json) ?? new List<UploadedFile>();
            }
            return new List<UploadedFile>();
        }

        public void SaveUploadedFiles(string project, List<UploadedFile> uploadedFiles)
        {
            var uploadsFile = GetUploadsFilePath(project);
            var json = JsonSerializer.Serialize(uploadedFiles);
            File.WriteAllText(uploadsFile, json);
        }

        public void StoreFile(string project, string path, string document)
        {
            File.WriteAllText(GetPath(project, path), document);
        }

        public bool FileExists(string project, string file) => File.Exists(GetPath(project, file));

        public async Task<List<string>> GetIntakeFilesForEmbeddings(Project project)
        {
            // Delegate to the unified source of truth in ProjectEmbeddingService
            return await _embeddingService.GetEmbeddingsFileList(project);
        }

        // Wrappers
        public string GetProjectFolder(string projectId) => _fileLocator.GetProjectFolder(projectId);
        public string GetPath(string projectId, string path) => _fileLocator.GetPath(projectId, path);
    }
}
