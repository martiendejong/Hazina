using System.Text.Json;
using Hazina.Tools.Services.Images.LayeredImage.Abstractions;
using Hazina.Tools.Services.Images.LayeredImage.Models;
using OpenAI.Chat;

namespace Hazina.Tools.Services.Images.LayeredImage;

/// <summary>
/// Provides LLM tool definitions for layered image generation.
/// </summary>
public static class LayeredImageToolsContext
{
    /// <summary>
    /// Creates the GenerateLayeredImage tool for use with LLM agents.
    /// </summary>
    /// <param name="layeredImageService">The layered image service instance.</param>
    /// <param name="projectId">The project ID for storing generated images.</param>
    /// <param name="userId">Optional user ID.</param>
    /// <returns>A HazinaChatTool for layered image generation.</returns>
    public static HazinaChatTool CreateGenerateLayeredImageTool(
        ILayeredImageService layeredImageService,
        string projectId,
        string? userId = null)
    {
        var parameters = new List<ChatToolParameter>
        {
            new ChatToolParameter
            {
                Name = "definition",
                Description = @"JSON object defining the layered image. Structure:
{
  ""format"": ""pdn"" | ""ora"" | ""psd"" (default: ""pdn""),
  ""canvas"": { ""width"": number, ""height"": number },
  ""layers"": [
    {
      ""name"": ""layer name"",
      ""type"": ""Generated"" | ""Uploaded"" | ""SolidColor"" | ""Text"",
      ""position"": { ""x"": number, ""y"": number },
      ""size"": { ""width"": number, ""height"": number },
      ""content"": ""AI prompt for Generated, filename for Uploaded, hex color for SolidColor, text for Text"",
      ""opacity"": 0.0-1.0,
      ""blendMode"": ""Normal"" | ""Multiply"" | ""Screen"" | ""Overlay"" | ""Darken"" | ""Lighten"",
      ""visible"": true | false,
      ""fontFamily"": ""Font name (for Text type)"",
      ""fontSize"": number (for Text type),
      ""fontColor"": ""#RRGGBB (for Text type)""
    }
  ],
  ""metadata"": { ""title"": ""string"", ""tags"": [""string""], ""description"": ""string"" }
}",
                Type = "string",
                Required = true
            }
        };

        return new HazinaChatTool(
            "GenerateLayeredImage",
            "Generates a multi-layer image file (PDN/ORA/PSD format) that can be edited in Paint.NET, GIMP, or Krita. " +
            "Each layer is generated separately and can be repositioned, hidden, or modified in the image editor. " +
            "Layer types: Generated (AI creates image from prompt), Uploaded (use existing file), SolidColor (fill with color), Text (render text). " +
            "Returns a downloadable layered file URL.",
            parameters,
            async (messages, toolCall, cancel) =>
            {
                try
                {
                    using var args = JsonDocument.Parse(toolCall.FunctionArguments);
                    if (!args.RootElement.TryGetProperty("definition", out var definitionEl))
                    {
                        return JsonSerializer.Serialize(new
                        {
                            success = false,
                            error = "Missing 'definition' parameter"
                        });
                    }

                    var definition = definitionEl.GetString();
                    if (string.IsNullOrWhiteSpace(definition))
                    {
                        return JsonSerializer.Serialize(new
                        {
                            success = false,
                            error = "Empty definition"
                        });
                    }

                    var result = await layeredImageService.GenerateFromJsonAsync(
                        projectId,
                        userId,
                        definition,
                        cancel);

                    return JsonSerializer.Serialize(new
                    {
                        success = result.Success,
                        fileName = result.FileName,
                        fileUrl = result.FileUrl,
                        format = result.Format.ToString().ToLowerInvariant(),
                        layers = result.Layers.Select(l => new
                        {
                            name = l.Name,
                            width = l.Width,
                            height = l.Height,
                            success = l.Success,
                            error = l.ErrorMessage
                        }),
                        error = result.ErrorMessage
                    });
                }
                catch (Exception ex)
                {
                    return JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = $"Failed to generate layered image: {ex.Message}"
                    });
                }
            });
    }

    /// <summary>
    /// Gets the list of supported layered image formats.
    /// </summary>
    public static HazinaChatTool CreateGetLayeredImageFormatsInfoTool()
    {
        return new HazinaChatTool(
            "GetLayeredImageFormats",
            "Returns information about supported layered image formats (PDN, ORA, PSD).",
            new List<ChatToolParameter>(),
            (messages, toolCall, cancel) =>
            {
                var formats = new[]
                {
                    new
                    {
                        format = "pdn",
                        name = "Paint.NET",
                        extension = ".pdn",
                        description = "Native Paint.NET format. Best for Paint.NET users.",
                        editors = new[] { "Paint.NET" }
                    },
                    new
                    {
                        format = "ora",
                        name = "OpenRaster",
                        extension = ".ora",
                        description = "Open standard format. Widely supported.",
                        editors = new[] { "GIMP", "Krita", "Paint.NET", "MyPaint" }
                    },
                    new
                    {
                        format = "psd",
                        name = "Photoshop",
                        extension = ".psd",
                        description = "Adobe Photoshop format. Limited layer support.",
                        editors = new[] { "Photoshop", "GIMP", "Krita", "Paint.NET" }
                    }
                };

                return Task.FromResult(JsonSerializer.Serialize(new { formats }));
            });
    }
}
