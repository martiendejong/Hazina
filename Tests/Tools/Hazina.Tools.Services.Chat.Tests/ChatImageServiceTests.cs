using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevGPT.GenerationTools.Data;
using DevGPT.GenerationTools.Models;
using DevGPT.GenerationTools.Services.Chat;
using DevGPTStore.Models;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DevGPT.GenerationTools.Services.Chat.Tests;

public class ChatImageServiceTests
{
    [Fact]
    public async Task GenerateImage_ReturnsAssistantImageForOpenAi()
    {
        var service = CreateService(
            openAi: (_, _, _) => Task.FromResult("https://example.com/generated.png"));

        var conversation = await service.GenerateImage(
            "project-a",
            "chat-1",
            new Project { Id = "project-a", ImageModel = ImageModel.DallE3 },
            new GeneratorMessage { Message = "Prompt" },
            CancellationToken.None);

        conversation.MetaData.Id.Should().Be("chat-1");
        conversation.ChatMessages.Should().HaveCount(2);
        conversation.ChatMessages[1].Text.Should()
            .Contain("![Generated Image](https://example.com/generated.png)")
            .And.Contain("*Generated with DALL-E 3*");
    }

    [Fact]
    public async Task GenerateImage_UsesNanoBananaModelInfo()
    {
        var service = CreateService(
            openAi: (_, _, _) => Task.FromResult("unused"),
            nanoBanana: (_, _) => Task.FromResult("data:image/png;base64,AAAA"));

        var conversation = await service.GenerateImage(
            "project-b",
            "chat-2",
            new Project { Id = "project-b", ImageModel = ImageModel.NanoBanana },
            new GeneratorMessage { Message = "Nano prompt" },
            CancellationToken.None);

        conversation.ChatMessages.Should().HaveCount(2);
        conversation.ChatMessages[1].Text.Should()
            .Contain("data:image/png;base64,AAAA")
            .And.Contain("*Generated with Nano Banana (Gemini 2.5 Flash Image)*");
    }

    [Fact]
    public async Task GenerateImage_ReturnsErrorMessageOnFailure()
    {
        var service = CreateService(
            openAi: (_, _, _) => throw new InvalidOperationException("boom"));

        var conversation = await service.GenerateImage(
            "project-c",
            "chat-3",
            new Project { Id = "project-c", ImageModel = ImageModel.DallE3 },
            new GeneratorMessage { Message = "oops" },
            CancellationToken.None);

        conversation.ChatMessages.Should().HaveCount(2);
        conversation.ChatMessages[1].Text.Should().Contain("Error generating image: boom");
    }

    [Fact]
    public async Task GenerateImage_ReturnsOpenAiImageWhenApiKeyConfigured()
    {
        var settings = LoadIntegrationSettings();
        if (settings == null)
        {
            Console.WriteLine("Integration configuration not available.");
            return;
        }
        var apiKey = settings.ApiSettings.OpenApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            var reason = "Provide OpenApiKey in Tests/DevGPT.GenerationTools.Services.Chat.Tests/appsettings.integration.json to run this integration test.";
            Console.WriteLine(reason);
            return;
        }

        var service = CreateIntegrationService(apiKey, null, settings.ProjectSettings);
        var conversation = await service.GenerateImage(
            "integration",
            "openai-image",
            new Project { Id = "integration", ImageModel = ImageModel.DallE3 },
            new GeneratorMessage { Message = "Integration prompt" },
            CancellationToken.None);

        conversation.ChatMessages.Should().HaveCount(2);
        conversation.ChatMessages[1].Text.Should()
            .Contain("![Generated Image](")
            .And.Contain("Generated with DALL-E");
    }

    [Fact]
    public async Task GenerateImage_ReturnsNanoBananaImageWhenApiKeyConfigured()
    {
        var settings = LoadIntegrationSettings();
        if (settings == null)
        {
            Console.WriteLine("Integration configuration not available.");
            return;
        }
        var apiKey = settings.ApiSettings.GeminiApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            var reason = "Provide GeminiApiKey in Tests/DevGPT.GenerationTools.Services.Chat.Tests/appsettings.integration.json to run this integration test.";
            Console.WriteLine(reason);
            return;
        }

        var service = CreateIntegrationService(null, apiKey, settings.ProjectSettings);
        var conversation = await service.GenerateImage(
            "integration",
            "nano-image",
            new Project { Id = "integration", ImageModel = ImageModel.NanoBanana },
            new GeneratorMessage { Message = "Nano integration prompt" },
            CancellationToken.None);

        conversation.ChatMessages.Should().HaveCount(2);
        conversation.ChatMessages[1].Text.Should()
            .Contain("data:")
            .And.Contain("Generated with Nano Banana");
    }

    private static ChatImageService CreateIntegrationService(string? openAiKey, string? geminiKey, ProjectSettings? projectSettings)
    {
        var projectsFolder = projectSettings?.ProjectsFolder;
        if (string.IsNullOrWhiteSpace(projectsFolder))
        {
            projectsFolder = Path.Combine(Path.GetTempPath(), "DevGPTTools", "ChatImageIntegration", Guid.NewGuid().ToString("N"));
        }
        Directory.CreateDirectory(projectsFolder);

        var config = new DevGPTStoreConfig
        {
            ApiSettings = new ApiSettings
            {
                OpenApiKey = openAiKey,
                GeminiApiKey = geminiKey
            },
            GoogleOAuthSettings = new GoogleOAuthSettings(),
            ProjectSettings = new ProjectSettings
            {
                ProjectsFolder = projectsFolder
            }
        };
        if (projectSettings != null)
        {
            projectSettings.ProjectsFolder = projectsFolder;
        }

        var builder = new ConfigurationBuilder().Build();
        var projectsRepository = new ProjectsRepository(config, builder);
        var intakeRepository = new IntakeRepository(config, builder);
        var fileLocator = new ProjectFileLocator(projectsRepository.ProjectsFolder);
        var generatedImageRepository = new GeneratedImageRepository(projectsRepository, fileLocator);
        return new ChatImageService(projectsRepository, fileLocator, intakeRepository, openAiKey, geminiKey, generatedImageRepository);
    }

    [Fact]
    public async Task GenerateImage_PersistsImageWhenIntegrationConfigured()
    {
        var settings = LoadIntegrationSettings();
        if (settings == null)
        {
            Console.WriteLine("Integration configuration not available.");
            return;
        }

        var apiKey = settings.ApiSettings.OpenApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.WriteLine("Set ApiSettings.OpenApiKey in the integration config.");
            return;
        }

        var projectsFolder = settings.ProjectSettings.ProjectsFolder;
        if (string.IsNullOrWhiteSpace(projectsFolder))
        {
            Console.WriteLine("Set ProjectSettings.ProjectsFolder in the integration config.");
            return;
        }

        var projectId = $"integration-store-{Guid.NewGuid():N}";
        var projectPath = Path.Combine(projectsFolder, projectId);
        if (Directory.Exists(projectPath))
        {
            Directory.Delete(projectPath, true);
        }

        var service = CreateIntegrationService(apiKey, null, settings.ProjectSettings);
        var prompt = "Integration persistence test";

        try
        {
            await service.GenerateImage(
                projectId,
                "chat-store",
                new Project { Id = projectId, ImageModel = ImageModel.DallE3 },
                new GeneratorMessage { Message = prompt },
                CancellationToken.None);

            var generatedFolder = Path.Combine(projectPath, "generatedimages");
            Directory.Exists(generatedFolder).Should().BeTrue();

            var storedFiles = Directory.EnumerateFiles(generatedFolder)
                .Where(f => !f.EndsWith("generatedimages.json", StringComparison.OrdinalIgnoreCase)).ToList();
            storedFiles.Should().NotBeEmpty();

            var metadataPath = Path.Combine(generatedFolder, "generatedimages.json");
            File.Exists(metadataPath).Should().BeTrue();

            var metadata = SerializableList<GeneratedImageInfo>.Load(metadataPath);
            metadata.Should().Contain(entry =>
                File.Exists(Path.Combine(generatedFolder, entry.FileName)) &&
                entry.ProjectId == projectId &&
                entry.Prompt == prompt &&
                entry.Model.Contains("DALL-E"));
        }
        finally
        {
            if (Directory.Exists(projectPath))
            {
                Directory.Delete(projectPath, true);
            }
        }
    }

    private static TestChatImageService CreateService(
        Func<string, ImageModel, CancellationToken, Task<string>> openAi,
        Func<string, CancellationToken, Task<string>>? nanoBanana = null)
    {
        return CreateServiceWithRepository(openAi, nanoBanana ?? ((_, _) => Task.FromException<string>(new InvalidOperationException("Nano Banana stub missing")))).Service;
    }

    private static (TestChatImageService Service, string ProjectsFolder) CreateServiceWithRepository(
        Func<string, ImageModel, CancellationToken, Task<string>> openAi,
        Func<string, CancellationToken, Task<string>> nanoBanana)
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "DevGPTTools", "ChatImageTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        var config = new DevGPTStoreConfig
        {
            ApiSettings = new ApiSettings
            {
                OpenApiKey = "sk-test",
                GeminiApiKey = "gemini-test"
            },
            GoogleOAuthSettings = new GoogleOAuthSettings(),
            ProjectSettings = new ProjectSettings
            {
                ProjectsFolder = tempFolder
            }
        };

        var projectsRepository = new ProjectsRepository(config, new ConfigurationBuilder().Build());
        var intakeRepository = new IntakeRepository(config, new ConfigurationBuilder().Build());
        var fileLocator = new ProjectFileLocator(projectsRepository.ProjectsFolder);
        var generatedImageRepository = new GeneratedImageRepository(projectsRepository, fileLocator);

        var service = new TestChatImageService(
            projectsRepository,
            intakeRepository,
            generatedImageRepository,
            openAi,
            nanoBanana);

        return (service, tempFolder);
    }

    private static IntegrationSettings? LoadIntegrationSettings()
    {
        var baseDir = AppContext.BaseDirectory;
        string? configPath = null;
        foreach (var fileName in new[] { "appsettings.integration.json", "appsettings.json" })
        {
            var candidate = Path.Combine(baseDir, fileName);
            if (File.Exists(candidate))
            {
                configPath = candidate;
                break;
            }
        }

        if (configPath == null)
        {
            var reason = "Copy Tests/DevGPT.GenerationTools.Services.Chat.Tests/appsettings.integration.json (or appsettings.json) from the repository root and add API keys before running these integration tests.";
            Console.WriteLine(reason);
            return null;
        }

        var config = new ConfigurationBuilder()
            .AddJsonFile(configPath, optional: false, reloadOnChange: false)
            .Build();

        var settings = config.Get<IntegrationSettings>();
        if (settings == null)
        {
            var reason = "Integration configuration is empty.";
            Console.WriteLine(reason);
            return null;
        }

        return settings;
    }

    private sealed class IntegrationSettings
    {
        public ApiSettings ApiSettings { get; set; } = new();
        public ProjectSettings ProjectSettings { get; set; } = new();
    }

    private sealed class TestChatImageService : ChatImageService
    {
        private readonly Func<string, ImageModel, CancellationToken, Task<string>> _openAi;
        private readonly Func<string, CancellationToken, Task<string>> _nanoBanana;

        public TestChatImageService(
            ProjectsRepository projects,
            IntakeRepository intake,
            GeneratedImageRepository generatedImageRepository,
            Func<string, ImageModel, CancellationToken, Task<string>> openAi,
            Func<string, CancellationToken, Task<string>> nanoBanana)
            : base(projects, new ProjectFileLocator(projects.ProjectsFolder), intake, "open-key", "gemini-key", generatedImageRepository)
        {
            _openAi = openAi;
            _nanoBanana = nanoBanana;
        }

        protected override Task<string> GenerateOpenAIImage(string prompt, ImageModel model, CancellationToken cancel)
            => _openAi(prompt, model, cancel);

        protected override Task<string> GenerateNanoBananaImage(string prompt, CancellationToken cancel)
            => _nanoBanana(prompt, cancel);
    }
}
