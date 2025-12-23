using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

public class ImageGenerationTests
{
    [Fact]
    public void OpenAI_FormatMapping_Null_DefaultsToText_And_KnownFormatsWork()
    {
        // null should map to text
        var mappedNull = DevGPTOpenAIExtensions.OpenAI((DevGPTChatResponseFormat)null);
        Assert.NotNull(mappedNull);

        // Known formats should map without exceptions
        var mappedText = DevGPTOpenAIExtensions.OpenAI(DevGPTChatResponseFormat.Text);
        Assert.NotNull(mappedText);
        var mappedJson = DevGPTOpenAIExtensions.OpenAI(DevGPTChatResponseFormat.Json);
        Assert.NotNull(mappedJson);
    }

    [Fact]
    public async Task OpenAI_GetImage_WithTextFormat_DoesNotThrow_AndReturnsImage()
    {
        // Arrange: load API key from env var or appsettings.json (OpenAI section)
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        OpenAIConfig config;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            try { config = OpenAIConfig.Load(); }
            catch { return; } // No config available locally; skip quietly
        }
        else
        {
            config = new OpenAIConfig(apiKey: apiKey);
        }

        if (string.IsNullOrWhiteSpace(config?.ApiKey)) return; // Skip when no key is available

        // Prefer stable defaults if not provided
        if (string.IsNullOrWhiteSpace(config.ImageModel)) config.ImageModel = "gpt-image-1";

        var client = new OpenAIClientWrapper(config);

        // Act
        var resp = await client.GetImage(
            prompt: "a small cozy house with a red door, watercolor style",
            responseFormat: DevGPTChatResponseFormat.Text,
            toolsContext: null,
            images: null,
            cancel: CancellationToken.None
        );

        // Assert
        Assert.NotNull(resp);
        Assert.NotNull(resp.Result);
        Assert.True(resp.Result.Url != null || resp.Result.ImageBytes != null, "Expected either a URL or image bytes.");
    }
}
