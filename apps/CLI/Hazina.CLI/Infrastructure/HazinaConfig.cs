using Microsoft.Extensions.Configuration;
using Hazina.LLMs.OpenAI;
using Hazina.LLMs;
using Spectre.Console;

namespace Hazina.CLI.Infrastructure;

/// <summary>
/// Shared configuration and provider setup for Hazina CLI
/// </summary>
public static class HazinaConfig
{
    private static IConfiguration? _configuration;
    private static ILLMClient? _llmClient;

    /// <summary>
    /// Load configuration from appsettings.json or environment
    /// </summary>
    public static IConfiguration GetConfiguration()
    {
        if (_configuration != null) return _configuration;

        var builder = new ConfigurationBuilder();

        // Try multiple locations for appsettings.json
        var locations = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
            Path.Combine(Environment.CurrentDirectory, "appsettings.json"),
            @"C:\Projects\client-manager\ClientManagerAPI\appsettings.Secrets.json",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".hazina", "config.json")
        };

        foreach (var location in locations)
        {
            if (File.Exists(location))
            {
                builder.AddJsonFile(location, optional: true);
            }
        }

        builder.AddEnvironmentVariables("HAZINA_");

        _configuration = builder.Build();
        return _configuration;
    }

    /// <summary>
    /// Get the default LLM client (OpenAI)
    /// </summary>
    public static ILLMClient GetLLMClient(string? provider = null)
    {
        if (_llmClient != null) return _llmClient;

        var config = GetConfiguration();

        // Try to configure OpenAI - check multiple config paths
        var openAiKey = config["OpenAI:ApiKey"]
            ?? config["ApiSettings:OpenApiKey"]
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrEmpty(openAiKey))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] No OpenAI API key configured.");
            AnsiConsole.MarkupLine("[yellow]Set OPENAI_API_KEY environment variable.[/]");
            throw new InvalidOperationException("No OpenAI API key configured");
        }

        var openAiConfig = new OpenAIConfig
        {
            ApiKey = openAiKey,
            Model = config["OpenAI:Model"] ?? "gpt-4o-mini",
            EmbeddingModel = config["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small"
        };

        _llmClient = new OpenAIClientWrapper(openAiConfig);
        return _llmClient;
    }

    /// <summary>
    /// Get store configuration path
    /// </summary>
    public static string GetStoresConfigPath()
    {
        var config = GetConfiguration();
        return config["Hazina:StoresConfig"]
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".hazina", "stores.json");
    }
}
