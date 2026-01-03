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
            var config = new HazinaStoreConfig
            {
                ProjectSettings = projectSettings,
                ApiSettings = apiSettings,
                GoogleOAuthSettings = googleOAuthSettings
            };
            return config;
        }
    }
}

