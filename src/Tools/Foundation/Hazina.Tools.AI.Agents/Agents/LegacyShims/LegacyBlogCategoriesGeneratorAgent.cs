using DevGPT.GenerationTools.AI.Agents;
using System.Threading.Tasks;
using DevGPT.GenerationTools.Data;
using DevGPT.GenerationTools.Models;

namespace DevGPT.GenerationTools.AI.Agents
{
    public class LegacyBlogCategoriesGeneratorAgent : GeneratorAgentBase
    {
        public LegacyBlogCategoriesGeneratorAgent(Microsoft.Extensions.Configuration.IConfiguration configuration, ProjectsRepository projects, string basisPrompt) : base(configuration, basisPrompt) { }
        public Task InternalRegenerate(Project project) => Task.CompletedTask;
        public Task InternalRegenerateWithFeedback(Project project, string feedback) => Task.CompletedTask;
        public Task InternalRegenerateCategoryWithFeedback(Project project, int index, string feedback) => Task.CompletedTask;
    }
}

