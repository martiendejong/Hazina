using Microsoft.Extensions.Configuration;

public class OpenAIConfig
{
    public OpenAIConfig(string apiKey = "", string embeddingModel = "text-embedding-ada-002", string model = "gpt-4.1", string imageModel = "gpt-image-1", string logPath = "c:\\projects\\hazinalogs.txt", string ttsModel = "gpt-4o-mini-tts")
    {
        ApiKey = apiKey;
        Model = model;
        ImageModel = imageModel;
        EmbeddingModel = embeddingModel;
        LogPath = logPath;
        TtsModel = ttsModel;
    }

    public string ApiKey { get; set; }
    public string Model { get; set; }
    public string ImageModel { get; set; }
    public string EmbeddingModel { get; set; }
    public string LogPath { get; set; }
    public string TtsModel { get; set; }

    public static OpenAIConfig Load()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory) // current directory
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // Bind the OpenAI section to a strongly-typed object
        var openAISettings = new OpenAIConfig();
        config.GetSection("OpenAI").Bind(openAISettings);
        return openAISettings;
    }
}
