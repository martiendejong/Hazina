using Microsoft.Extensions.Configuration;

namespace DevGPT.LLMs.Gemini;

public class GeminiConfig
{
    public GeminiConfig(string apiKey = "", string model = "gemini-1.5-pro", string endpoint = "https://generativelanguage.googleapis.com/v1beta", string logPath = "c:\\projects\\devgptlogs.txt", string imageModel = "imagegeneration", string? ttsApiKey = null, string ttsLanguageCode = "en-US", string ttsVoiceName = "en-US-Neural2-C", string ttsAudioEncoding = "MP3")
    {
        ApiKey = apiKey;
        Model = model;
        Endpoint = endpoint.TrimEnd('/');
        LogPath = logPath;
        ImageModel = imageModel;
        TtsApiKey = ttsApiKey ?? apiKey;
        TtsLanguageCode = ttsLanguageCode;
        TtsVoiceName = ttsVoiceName;
        TtsAudioEncoding = ttsAudioEncoding;
    }

    public string ApiKey { get; set; }
    public string Model { get; set; }
    public string Endpoint { get; set; }
    public string LogPath { get; set; }
    public string ImageModel { get; set; }
    public string? TtsApiKey { get; set; }
    public string TtsLanguageCode { get; set; }
    public string TtsVoiceName { get; set; }
    public string TtsAudioEncoding { get; set; }

    public static GeminiConfig Load()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var settings = new GeminiConfig();
        config.GetSection("Gemini").Bind(settings);
        return settings;
    }
}
