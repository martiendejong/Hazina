using Hazina.Tools.Data;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using HazinaStore;

namespace Hazina.Tools.Services.Prompts
{
    public class RolePromptService
    {
        public ProjectsRepository Projects;
        private readonly ProjectFileLocator _fileLocator;
        public List<RolePrompt> Prompts;
        public RolePromptService(ProjectsRepository projects)
        {
            Projects = projects;
            _fileLocator = new ProjectFileLocator(Projects.ProjectsFolder);

            LoadPrompts();
        }

        public async Task<RolePrompt> Update(RolePrompt prompt)
        {
            var existingPrompt = Prompts.Single(u => u.Role == prompt.Role);
            existingPrompt.Prompt = prompt.Prompt;
            SavePrompts();
            return existingPrompt;
        }

        public async Task<List<RolePrompt>> GetPrompts()
        {
            LoadPrompts();
            return Prompts;
        }

        private void LoadPrompts()
        {
            var filePath = Path.Combine(Projects.ProjectsFolder, ProjectFileLocator.RolePromptsFile);
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                Prompts = JsonSerializer.Deserialize<List<RolePrompt>>(json);
            }
            else
            {
                var bpFilePath = Path.Combine(Projects.ProjectsFolder, ProjectFileLocator.BasisPromptFile);
                var basis = File.ReadAllText(bpFilePath);
                Prompts = new List<RolePrompt>() { 
                    new RolePrompt { Role = "OAS", Prompt = basis }, 
                    new RolePrompt { Role = "SMM", Prompt = basis }, 
                    new RolePrompt { Role = "Admin", Prompt = basis }, 
                    new RolePrompt { Role = "Extern", Prompt = basis } 
                };
            }
        }

        private void SavePrompts()
        {
            var filePath = Path.Combine(Projects.ProjectsFolder, ProjectFileLocator.RolePromptsFile);
            var json = JsonSerializer.Serialize(Prompts);
            File.WriteAllText(filePath, json);
        }
    }
}

