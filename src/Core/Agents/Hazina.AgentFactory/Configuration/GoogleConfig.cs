using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

public class GoogleConfig
    {
        public GoogleConfig(string projectId = "")
        {
            ProjectId = projectId;
        }

        public string ProjectId { get; set; }

        public static GoogleConfig Load()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory) // current directory
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var settings = new GoogleConfig();
            config.GetSection("Google").Bind(settings);
            return settings;
        }
    }
