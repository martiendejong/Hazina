using DevGPTStore.Models;
using DevGPT.GenerationTools.Data;
using DevGPT.GenerationTools.Models;
using DevGPT.GenerationTools.Models.Social;
using DevGPT.GenerationTools.Services.Web;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DevGPTStore.ContentRetrieval;using DevGPTStore.IntakeRegenerators;

namespace DevGPT.GenerationTools.Services.Intake
{
    // Moved to Services: orchestration for intake regeneration
    public class DevGPTStoreIntakeWorker
    {
        public ProjectsRepository Projects { get; set; }
        public IntakeRepository Intake { get; set; }
        private readonly ProjectFileLocator _fileLocator;
        private readonly ProjectGlobalSettingsRepository _globalSettings;
        public IConfiguration appConfig;
        public DevGPTStoreConfig config;

        
        
        
        
        private readonly ContentHooksRegenerator _ContentHooksRegenerator;
        public readonly ContentRetrievalService _contentRetrievalService;

        public DevGPTStoreIntakeWorker(
            IntakeRepository intake,
            IConfiguration configuration,
            DevGPTStoreConfig storeConfig
        )
        {
            Intake = intake;
            Projects = intake.Projects;
            _fileLocator = new ProjectFileLocator(Projects.ProjectsFolder);
            _globalSettings = new ProjectGlobalSettingsRepository(Projects.ProjectsFolder);
            appConfig = configuration;
            config = storeConfig;
            _ContentHooksRegenerator = new ContentHooksRegenerator(Projects, appConfig, config);
            _contentRetrievalService = new ContentRetrievalService(_fileLocator, appConfig, config.ApiSettings.OpenApiKey);
        }

        public async Task RegenerateIntakeIfProjectIsMarkedRegenerate(Project project, Action<string> log = null)
        {
            if (log == null) log = (a) => { };
            try
            {
                log("check if generate");
                if ((project.SocialMediaAddresses.Any() && project.SocialMediaAddresses.All(a => a.Status == SocialMediaAddressStatus.Imported) && project.Status == "INITIAL")
                    || project.Status == "GENERATE")
                {
                    log($"Genereer data voor {project.Id}");
                    // Placeholder: embedding initialization is handled elsewhere
                    project.Status = "GENERATING";
                    Projects.Save(project);
                    await RegeneratIntake(project, log);
                    project.Status = "GENERATED";
                    Projects.Save(project);
                }
            }
            catch (Exception ex)
            {
                log?.Invoke($"Fout bij regeneratie: {ex.Message}\n{ex.StackTrace}");
                project.Status = "GENERATED";
                Projects.Save(project);
            }
        }

        public async Task RegeneratIntake(Project project, Action<string> log = null, object retry = null, Action<string, string> onUpdate = null)
        {
            if (log == null)
                log = onUpdate == null ? (a) => { } : (a) => onUpdate(project.Id, a);

            DeleteFiles(project, ProjectFileLocator.ContentHooksFile);

            log?.Invoke($"Genereer data voor {project.Id}");
            var contentHooksPath = System.IO.Path.Combine(_fileLocator.GetProjectFolder(project.Id), ProjectFileLocator.ContentHooksFile);
            if (!System.IO.File.Exists(contentHooksPath))
                _ContentHooksRegenerator.CreateEmptyContentHooksFile(project);
        }

        private void DeleteFiles(Project project, string filename)
        {
            var p = _fileLocator.GetPath(project.Id, filename);
            var dir = new System.IO.FileInfo(p).Directory;
            var files = dir.GetFiles($"{filename}*");
            foreach (var file in files) file.Delete();
        }

        public async Task UpdateStoreEmbeddings(Project project) { await Task.CompletedTask; }

        public async Task<ProjectStatistics> GenerateProjectStatistics(Project project)
        {
            var userInfos = _globalSettings.LoadUserInfos();
            var projectUsers = userInfos.Where(ui => ui.Projects.Contains(project.Id)).ToList();
            var numUsers = projectUsers.Count;

            var path = Intake.GetUploadsFilePath(project.Id);
            var numDocs = 0;
            if (System.IO.File.Exists(path))
            {
                var files = Intake.GetUploadedFiles(path);
                numDocs = files.Count;
            }

            var hasBigQuery = false; // Placeholder until BigQuery service migrates
            var hasFacebook = false; // Placeholder until Facebook service migrates

            var samenwerking = project.SamenwerkingOptions.Count();

            var s = new ProjectStatistics
            {
                Name = project.Id,
                Users = numUsers,
                Documents = numDocs,
                BigQuery = hasBigQuery,
                Facebook = hasFacebook,
                Samenwerking = samenwerking,
                KenmerkenDoelgroep = project.KenmerkenDoelgroep
            };
            return s;
        }
    }

    public class ProjectStatistics
    {
        public string Name { get; set; }
        public int Users { get; set; }
        public int Documents { get; set; }
        public bool BigQuery { get; set; }
        public bool Facebook { get; set; }
        public int Samenwerking { get; set; }
        public string KenmerkenDoelgroep { get; set; }
    }
}



