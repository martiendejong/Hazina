using DevGPTStore.Models;
using Microsoft.Extensions.Configuration;
using System;

namespace DevGPT.GenerationTools.Services.FileOps.Helpers
{
    public class DevGPTStoreConfigLoader
    {
        public static DevGPTStoreConfig LoadDevGPTStoreConfig()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var apiSettings = configuration.GetSection("ApiSettings").Get<ApiSettings>();
            var projectSettings = configuration.GetSection("ProjectSettings").Get<ProjectSettings>();
            var googleOAuthSettings = configuration.GetSection("GoogleOAuth").Get<GoogleOAuthSettings>();
            var config = new DevGPTStoreConfig
            {
                ProjectSettings = projectSettings,
                ApiSettings = apiSettings,
                GoogleOAuthSettings = googleOAuthSettings
            };
            return config;
        }
    }
}

