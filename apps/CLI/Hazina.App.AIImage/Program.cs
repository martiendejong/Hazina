using System.CommandLine;
using Hazina.Tools.Models;
using Spectre.Console;
using OpenAI;
using OpenAI.Images;

namespace Hazina.App.AIImage;

sealed class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("AI Image Generation Tool - Powered by Hazina");

        // Required parameters
        var promptOption = new Option<string>(
            aliases: new[] { "--prompt", "-p" },
            description: "Text description of the image to generate");
        promptOption.IsRequired = true;

        var outputOption = new Option<FileInfo>(
            aliases: new[] { "--output", "-o" },
            description: "Output file path for the generated image");
        outputOption.IsRequired = true;

        // Model selection
        var modelOption = new Option<string>(
            aliases: new[] { "--model", "-m" },
            description: "Image generation model: gpt-image, dall-e-3, dall-e-2",
            getDefaultValue: () => "gpt-image");

        // Optional parameters
        var sizeOption = new Option<string>(
            aliases: new[] { "--size", "-s" },
            description: "Image size (e.g., 1024x1024, 1792x1024, 1024x1792)",
            getDefaultValue: () => "1024x1024");

        var qualityOption = new Option<string>(
            aliases: new[] { "--quality", "-q" },
            description: "Image quality: standard, hd",
            getDefaultValue: () => "standard");

        var styleOption = new Option<string>(
            aliases: new[] { "--style" },
            description: "Image style: vivid, natural",
            getDefaultValue: () => "natural");

        var apiKeyOption = new Option<string?>(
            aliases: new[] { "--api-key", "-k" },
            description: "OpenAI API key (if not provided, loads from environment)");

        // Add options to root command
        rootCommand.AddOption(promptOption);
        rootCommand.AddOption(outputOption);
        rootCommand.AddOption(modelOption);
        rootCommand.AddOption(sizeOption);
        rootCommand.AddOption(qualityOption);
        rootCommand.AddOption(styleOption);
        rootCommand.AddOption(apiKeyOption);

        rootCommand.SetHandler(async (string prompt, FileInfo output, string model, string size, string quality, string style, string? apiKey) =>
        {
            try
            {
                await GenerateImage(prompt, output, model, size, quality, style, apiKey);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
                Environment.Exit(1);
            }
        }, promptOption, outputOption, modelOption, sizeOption, qualityOption, styleOption, apiKeyOption);

        return await rootCommand.InvokeAsync(args);
    }

    static async Task GenerateImage(string prompt, FileInfo outputFile, string modelName, string size, string quality, string style, string? apiKey)
    {
        AnsiConsole.MarkupLine("[cyan]🎨 AI Image Generation - Powered by Hazina[/]\n");

        // Parse model
        var imageModel = ParseModel(modelName);
        AnsiConsole.MarkupLine($"[gray]Model:[/] {imageModel}");
        AnsiConsole.MarkupLine($"[gray]Size:[/] {size}");
        AnsiConsole.MarkupLine($"[gray]Quality:[/] {quality}");
        AnsiConsole.MarkupLine($"[gray]Style:[/] {style}");
        AnsiConsole.MarkupLine($"[gray]Output:[/] {outputFile.FullName}\n");

        // Get API key
        var resolvedApiKey = apiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrEmpty(resolvedApiKey))
        {
            // Try loading from appsettings.Secrets.json (client-manager path)
            var secretsPath = @"C:\Projects\client-manager\ClientManagerAPI\appsettings.Secrets.json";
            if (File.Exists(secretsPath))
            {
                var json = await File.ReadAllTextAsync(secretsPath);
                var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("ApiSettings", out var apiSettings) &&
                    apiSettings.TryGetProperty("OpenApiKey", out var keyElement))
                {
                    resolvedApiKey = keyElement.GetString();
                    AnsiConsole.MarkupLine("[green]✓[/] Loaded API key from appsettings.Secrets.json");
                }
            }
        }

        if (string.IsNullOrEmpty(resolvedApiKey))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] OpenAI API key not found. Provide via --api-key or set OPENAI_API_KEY environment variable.");
            Environment.Exit(1);
        }

        // Create OpenAI client
        var client = new OpenAIClient(resolvedApiKey);
        var imageClient = client.GetImageClient(MapToOpenAIModel(imageModel));

        // Generate image
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Generating image...", async ctx =>
            {
                try
                {
                    // Parse size
                    var generatedImageSize = size switch
                    {
                        "1024x1024" => GeneratedImageSize.W1024xH1024,
                        "1792x1024" => GeneratedImageSize.W1792xH1024,
                        "1024x1792" => GeneratedImageSize.W1024xH1792,
                        _ => GeneratedImageSize.W1024xH1024
                    };

                    // Parse quality
                    var generatedImageQuality = quality switch
                    {
                        "hd" => GeneratedImageQuality.High,
                        _ => GeneratedImageQuality.Standard
                    };

                    // Parse style
                    var generatedImageStyle = style switch
                    {
                        "vivid" => GeneratedImageStyle.Vivid,
                        _ => GeneratedImageStyle.Natural
                    };

                    // Call OpenAI image generation
                    var result = await imageClient.GenerateImageAsync(
                        prompt: prompt,
                        new ImageGenerationOptions
                        {
                            Size = generatedImageSize,
                            Quality = generatedImageQuality,
                            Style = generatedImageStyle,
                            ResponseFormat = GeneratedImageFormat.Uri
                        });

                    if (result?.Value != null)
                    {
                        var imageUri = result.Value.ImageUri;

                        ctx.Status("Downloading image...");

                        // Download image
                        using var httpClient = new HttpClient();
                        var imageBytes = await httpClient.GetByteArrayAsync(imageUri).ConfigureAwait(false);

                        // Save to file
                        var directory = Path.GetDirectoryName(outputFile.FullName);
                        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        await File.WriteAllBytesAsync(outputFile.FullName, imageBytes).ConfigureAwait(false);

                        ctx.Status("Complete!");
                    }
                    else
                    {
                        throw new InvalidOperationException("No image data returned from API");
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Image generation failed: {ex.Message}", ex);
                }
            });

        var fileInfo = new FileInfo(outputFile.FullName);
        AnsiConsole.MarkupLine($"\n[green]✓ Image saved:[/] {outputFile.FullName}");
        AnsiConsole.MarkupLine($"[gray]Size:[/] {fileInfo.Length / 1024:N0} KB");
    }

    static ImageModel ParseModel(string modelName)
    {
        return modelName.ToLowerInvariant() switch
        {
            "gpt-image" => ImageModel.GptImage,
            "dall-e-3" or "dalle3" => ImageModel.DallE3,
            "dall-e-2" or "dalle2" => ImageModel.DallE2,
            _ => throw new ArgumentException($"Unknown model: {modelName}. Valid options: gpt-image, dall-e-3, dall-e-2", nameof(modelName))
        };
    }

    static string MapToOpenAIModel(ImageModel model)
    {
        return model switch
        {
            ImageModel.GptImage => "dall-e-3", // Map gpt-image to DALL-E 3
            ImageModel.DallE3 => "dall-e-3",
            ImageModel.DallE2 => "dall-e-2",
            _ => "dall-e-3"
        };
    }
}
