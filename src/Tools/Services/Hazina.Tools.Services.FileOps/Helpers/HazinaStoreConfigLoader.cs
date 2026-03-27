using HazinaStore.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace Hazina.Tools.Services.FileOps.Helpers
{
    public class HazinaStoreConfigLoader
    {
        public static HazinaStoreConfig LoadHazinaStoreConfig()
        {
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            // Load secrets file if it exists (gitignored)
            var secretsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.Secrets.json");
            if (File.Exists(secretsPath))
            {
                configBuilder.AddJsonFile("appsettings.Secrets.json", optional: true, reloadOnChange: true);
            }

            // Load environment variables with prefix (for production/Docker)
            configBuilder.AddEnvironmentVariables(prefix: "ARTREVISIONIST_");

            var configuration = configBuilder.Build();

            var apiSettings = configuration.GetSection("ApiSettings").Get<ApiSettings>();
            var projectSettings = configuration.GetSection("ProjectSettings").Get<ProjectSettings>();
            var googleOAuthSettings = configuration.GetSection("GoogleOAuth").Get<GoogleOAuthSettings>();

            // Guard against null ProjectSettings - can happen when config binding fails on IIS
            if (projectSettings == null)
            {
                System.Console.WriteLine($"[HazinaStoreConfigLoader] WARNING: ProjectSettings binding returned null. BaseDirectory={AppContext.BaseDirectory}");
                var fallbackFolder = Path.Combine(AppContext.BaseDirectory, "projects");
                projectSettings = new ProjectSettings
                {
                    ProjectsFolder = fallbackFolder,
                    FrontendUrl = "https://app.brand2boost.com"
                };
                System.Console.WriteLine($"[HazinaStoreConfigLoader] Using fallback ProjectsFolder: {fallbackFolder}");
            }
            var openAIConfig = Hazina.LLMs.OpenAI.OpenAIConfig.FromConfiguration(configuration);

            System.Console.WriteLine($"[HazinaStoreConfigLoader] OpenAI config loaded: Model='{openAIConfig.Model}', ApiKey={(string.IsNullOrEmpty(openAIConfig.ApiKey) ? "(empty)" : "(set)")}");

            // Resolve "configuration:" references in OpenAI config
            if (!string.IsNullOrEmpty(openAIConfig.ApiKey) && openAIConfig.ApiKey.StartsWith("configuration:"))
            {
                var configPath = openAIConfig.ApiKey.Substring("configuration:".Length);
                var resolvedValue = configuration[configPath];
                if (!string.IsNullOrEmpty(resolvedValue))
                {
                    openAIConfig.ApiKey = resolvedValue;
                    System.Console.WriteLine($"[HazinaStoreConfigLoader] Resolved ApiKey from configuration path: {configPath}");
                }
            }

            System.Console.WriteLine($"[HazinaStoreConfigLoader] Final OpenAI config: Model='{openAIConfig.Model}', ApiKey={(string.IsNullOrEmpty(openAIConfig.ApiKey) ? "(empty)" : "(set)")}");

            var config = new HazinaStoreConfig
            {
                ProjectSettings = projectSettings,
                ApiSettings = apiSettings!,
                GoogleOAuthSettings = googleOAuthSettings!,
                OpenAI = openAIConfig
            };
            return config;
        }
    }
}

