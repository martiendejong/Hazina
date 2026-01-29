using System.Text.Json;
using System.Text.RegularExpressions;
using Hazina.LLMs;
using Hazina.Tools.Services.Images.LayeredImage.Abstractions;
using Hazina.Tools.Services.Images.LayeredImage.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.Fonts;
using Microsoft.Extensions.Logging;

namespace Hazina.Tools.Services.Images.LayeredImage.Services;

/// <summary>
/// Smart layered image service that takes a natural language prompt
/// and automatically plans, generates, and composites a multi-layer image.
/// </summary>
public class SmartLayeredImageService : ISmartLayeredImageService
{
    private readonly ILLMClient _llmClient;
    private readonly ILayeredImageService _layeredImageService;
    private readonly ILogger<SmartLayeredImageService> _logger;

    public SmartLayeredImageService(
        ILLMClient llmClient,
        ILayeredImageService layeredImageService,
        ILogger<SmartLayeredImageService> logger)
    {
        _llmClient = llmClient;
        _layeredImageService = layeredImageService;
        _logger = logger;
    }

    public async Task<SmartLayeredImageResult> GenerateFromPromptAsync(
        string prompt,
        int width = 1024,
        int height = 1024,
        string format = "pdn",
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("SmartLayeredImageService: Starting generation from prompt");

            // Step 1: Plan the layers
            _logger.LogInformation("Step 1: Planning layer structure...");
            var layerPlan = await PlanLayersAsync(prompt, width, height, cancellationToken);

            if (layerPlan == null || layerPlan.Layers.Count == 0)
            {
                return new SmartLayeredImageResult
                {
                    Success = false,
                    ErrorMessage = "Failed to plan layer structure from prompt"
                };
            }

            _logger.LogInformation("Planned {LayerCount} layers: {LayerNames}",
                layerPlan.Layers.Count,
                string.Join(", ", layerPlan.Layers.Select(l => l.Name)));

            // Step 2: Convert plan to LayeredImageDefinition
            var definition = ConvertPlanToDefinition(layerPlan, width, height, format);

            // Step 3: Use existing LayeredImageService to generate
            _logger.LogInformation("Step 3: Generating layered image...");
            var result = await _layeredImageService.GenerateAsync(
                "smart-layered",
                null,
                definition,
                cancellationToken);

            if (!result.Success)
            {
                return new SmartLayeredImageResult
                {
                    Success = false,
                    ErrorMessage = result.ErrorMessage ?? "Failed to generate layered image"
                };
            }

            // Build response
            return new SmartLayeredImageResult
            {
                Success = true,
                LayeredImageData = result.FileData,
                CompositePreview = result.CompositePreview,
                CompositionReasoning = layerPlan.CompositionReasoning,
                Format = format,
                Layers = result.Layers.Select(l => new LayerPreview
                {
                    Name = l.Name,
                    Type = l.Type,
                    Description = l.Description ?? "",
                    ImageData = l.ImageData,
                    Width = l.Width,
                    Height = l.Height,
                    PositionX = l.PositionX,
                    PositionY = l.PositionY
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SmartLayeredImageService: Error generating from prompt");
            return new SmartLayeredImageResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<LayerPlanResult?> PlanLayersAsync(
        string userPrompt, int canvasWidth, int canvasHeight, CancellationToken cancellationToken)
    {
        var systemPrompt = $@"You are an expert art director for layered image composition.

Given a user's description, you must plan a multi-layer image composition.

CRITICAL RULES:
1. Always start with a Background layer (type: ""Background"")
2. The background should INCLUDE static scene elements like buildings, streets, landscapes
3. Only foreground/dynamic elements should be separate layers with transparent backgrounds
4. For text, use type ""Text"" - these are rendered locally
5. Create prompts that generate images that work TOGETHER as a cohesive scene
6. Consider perspective, lighting direction, and scale consistency
7. Each foreground element MUST have transparent background

Canvas size: {canvasWidth}x{canvasHeight} pixels

IMPORTANT: When creating prompts for non-background layers, always emphasize:
- ""CRITICAL: Volledig transparante achtergrond""
- ""GEEN straat, GEEN omgeving, GEEN reflecties op de grond""
- ""ALLEEN het object zelf""

Respond with JSON:
{{
  ""compositionReasoning"": ""<your artistic vision>"",
  ""layers"": [
    {{
      ""name"": ""<short name>"",
      ""type"": ""Background"" | ""Element"" | ""Text"",
      ""description"": ""<what this layer shows>"",
      ""generationPrompt"": ""<detailed prompt for AI image generation>"",
      ""placementGuidance"": ""<where to place this layer>"",
      ""suggestedPosition"": {{ ""x"": 0, ""y"": 0, ""width"": {canvasWidth}, ""height"": {canvasHeight} }},
      ""textContent"": ""<only for Text: actual text>"",
      ""fontFamily"": ""<only for Text: font>"",
      ""fontColor"": ""<only for Text: hex color>""
    }}
  ]
}}";

        var messages = new List<HazinaChatMessage>
        {
            new() { Role = HazinaMessageRole.System, Text = systemPrompt },
            new() { Role = HazinaMessageRole.User, Text = userPrompt }
        };

        var response = await _llmClient.GetResponse(
            messages,
            HazinaChatResponseFormat.Json,
            null,
            null,
            cancellationToken);

        if (string.IsNullOrEmpty(response.Result))
        {
            return null;
        }

        try
        {
            // Extract JSON from response
            var jsonMatch = Regex.Match(response.Result, @"\{[\s\S]*\}", RegexOptions.Singleline);
            if (!jsonMatch.Success)
            {
                _logger.LogWarning("Could not extract JSON from response: {Response}", response.Result);
                return null;
            }

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<LayerPlanResult>(jsonMatch.Value, options);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse layer plan JSON");
            return null;
        }
    }

    private LayeredImageDefinition ConvertPlanToDefinition(
        LayerPlanResult plan,
        int canvasWidth,
        int canvasHeight,
        string format)
    {
        var definition = new LayeredImageDefinition
        {
            Format = format,
            Canvas = new CanvasDimensions { Width = canvasWidth, Height = canvasHeight },
            UseVisionContext = true,
            Layers = new List<LayerDefinition>()
        };

        foreach (var plannedLayer in plan.Layers)
        {
            var layerDef = new LayerDefinition
            {
                Name = plannedLayer.Name,
                Type = plannedLayer.Type,
                Content = plannedLayer.Type == "Text"
                    ? plannedLayer.TextContent ?? plannedLayer.Description
                    : plannedLayer.GenerationPrompt,
                ShouldGenerate = plannedLayer.Type != "Text",
                Size = new LayerSize
                {
                    Width = plannedLayer.SuggestedPosition?.Width ?? canvasWidth,
                    Height = plannedLayer.SuggestedPosition?.Height ?? canvasHeight
                },
                Position = new LayerPosition
                {
                    X = plannedLayer.SuggestedPosition?.X ?? 0,
                    Y = plannedLayer.SuggestedPosition?.Y ?? 0
                },
                PlacementGuidance = plannedLayer.PlacementGuidance
            };

            // Text-specific properties
            if (plannedLayer.Type == "Text")
            {
                layerDef.FontFamily = plannedLayer.FontFamily ?? "Segoe UI";
                layerDef.FontColor = plannedLayer.FontColor ?? "#FFFFFF";
                layerDef.FontSize = 48;
            }

            definition.Layers.Add(layerDef);
        }

        return definition;
    }
}

/// <summary>
/// Internal class for deserializing LLM layer plan response.
/// </summary>
internal class LayerPlanResult
{
    public string CompositionReasoning { get; set; } = string.Empty;
    public List<PlannedLayerItem> Layers { get; set; } = new();
}

internal class PlannedLayerItem
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string GenerationPrompt { get; set; } = string.Empty;
    public string PlacementGuidance { get; set; } = string.Empty;
    public SuggestedPositionData? SuggestedPosition { get; set; }
    public string? TextContent { get; set; }
    public string? FontFamily { get; set; }
    public string? FontColor { get; set; }
}

internal class SuggestedPositionData
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}
