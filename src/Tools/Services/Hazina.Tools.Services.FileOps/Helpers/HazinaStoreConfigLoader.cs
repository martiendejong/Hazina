using HazinaStore.Models;
using Microsoft.Extensions.Configuration;
using System;

namespace Hazina.Tools.Services.FileOps.Helpers
{
    public class HazinaStoreConfigLoader
    {
        public static HazinaStoreConfig LoadHazinaStoreConfig()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

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

