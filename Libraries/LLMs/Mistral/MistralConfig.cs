using Microsoft.Extensions.Configuration;

namespace DevGPT.LLMs.Mistral;

public class MistralConfig
{
    public MistralConfig(string apiKey = "", string model = "mistral-large-latest", string endpoint = "https://api.mistral.ai/v1", string logPath = "c:\\projects\\devgptlogs.txt")
    {
        ApiKey = apiKey;
        Model = model;
        Endpoint = endpoint.TrimEnd('/');
        LogPath = logPath;
    }

    public string ApiKey { get; set; }
    public string Model { get; set; }
    public string Endpoint { get; set; }
    public string LogPath { get; set; }

    public static MistralConfig Load()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var settings = new MistralConfig();
        config.GetSection("Mistral").Bind(settings);
        return settings;
    }
}

