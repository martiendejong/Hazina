using Microsoft.Win32;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DevGPT.App.ExplorerIntegration
{
    public static class EmbeddingService
    {
        public static async Task GenerateEmbeddingsAsync(string folder, string filtersCsv, string openAIKey, bool saveKey)
        {
            if (saveKey)
            {
                Registry.SetValue("HKEY_CURRENT_USER\\Software\\DevGPT\\OpenAI", "ApiKey", openAIKey);
            }

            var config = new OpenAIConfig(openAIKey);
            var llm = new OpenAIClientWrapper(config);

            var paths = new StorePaths(folder);
            var creator = new QuickAgentCreator(new AgentFactory(openAIKey, config.LogPath), llm);
            var store = creator.CreateStore(paths, new DirectoryInfo(folder).Name);

            var patterns = filtersCsv.Split(',').Select(s => s.Trim().TrimStart('.')).Where(s => s.Length > 0).ToArray();
            var files = patterns
                .SelectMany(p => new DirectoryInfo(folder).GetFiles($"*.{p}", SearchOption.AllDirectories))
                .Where(fi => !IsExcluded(fi.FullName, folder))
                .ToList();

            foreach (var file in files)
            {
                var rel = Path.GetRelativePath(folder, file.FullName);
                await store.Embed(rel);
            }
        }

        private static bool IsExcluded(string fullPath, string root)
        {
            var rel = Path.GetRelativePath(root, fullPath).Replace('/', '\\');
            var parts = rel.Split('\\');
            return parts.Any(p => p.Equals("bin", System.StringComparison.OrdinalIgnoreCase)
                               || p.Equals("obj", System.StringComparison.OrdinalIgnoreCase)
                               || p.Equals("node_modules", System.StringComparison.OrdinalIgnoreCase)
                               || p.Equals("dist", System.StringComparison.OrdinalIgnoreCase));
        }
    }
}


