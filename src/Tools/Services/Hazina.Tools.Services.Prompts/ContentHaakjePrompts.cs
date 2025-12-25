using DevGPT.GenerationTools.Models.WordPress.Blogs;
using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace DevGPTStore.Agents.Prompts
{
    public class GeneratorPrompts : Serializer<GeneratorPrompts>
    {
        public string? BasisPrompt { get; set; }
        public string? SpecificPrompt { get; set; }
        public string? SystemInstruction { get; set; }
    }

    public static class ContentHookPrompts
    {
        public const string ConfigFileName = "prompts.contenthooks.json";
        private const string LegacyConfigFileName = "legacy.prompts.contenthooks.json";
        public const string ContentHooksFileName = "contenthooks.json";

        public static GeneratorPrompts GetConfig(IConfiguration configuration)
        {
            // Try to load a global config from current directory; fall back to empty
            try
            {
                var root = AppContext.BaseDirectory;
                var path = Path.Combine(root, ConfigFileName);
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var cfg = JsonSerializer.Deserialize<GeneratorPrompts>(json);
                    return cfg ?? new GeneratorPrompts();
                }
                // Legacy fallback for previously used filename
                var legacyPath = Path.Combine(root, LegacyConfigFileName);
                if (File.Exists(legacyPath))
                {
                    var json = File.ReadAllText(legacyPath);
                    var cfg = JsonSerializer.Deserialize<GeneratorPrompts>(json);
                    return cfg ?? new GeneratorPrompts();
                }
            }
            catch { }
            return new GeneratorPrompts();
        }

        public static ContentHook[] GetContentHooks(string projectsFolder)
        {
            try
            {
                var file = Path.Combine(projectsFolder, ContentHooksFileName);
                if (File.Exists(file))
                {
                    var json = File.ReadAllText(file);
                    var data = JsonSerializer.Deserialize<ContentHook[]>(json);
                    return data ?? Array.Empty<ContentHook>();
                }
            }
            catch { }
            return Array.Empty<ContentHook>();
        }
    }
}
