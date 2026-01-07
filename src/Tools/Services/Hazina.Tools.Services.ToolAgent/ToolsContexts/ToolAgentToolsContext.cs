using System.Text.Json;
using Hazina.Tools.Models;

namespace Hazina.Tools.Services.ToolAgent.ToolsContexts;

/// <summary>
/// Tools context for the tool agent (Layer 2).
/// These tools allow the agent to orchestrate specialized operations without needing full context.
/// </summary>
public class ToolAgentToolsContext : ToolsContextBase
{
    private readonly string _projectId;
    private readonly string _chatId;
    private readonly string? _userId;

    /// <summary>
    /// Gets the list of tools that were executed during this context.
    /// </summary>
    public IReadOnlyList<string> ExecutedTools => _executedTools.AsReadOnly();
    private readonly List<string> _executedTools = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolAgentToolsContext"/> class.
    /// </summary>
    public ToolAgentToolsContext(
        string projectId,
        string chatId,
        string? userId = null)
    {
        _projectId = projectId ?? throw new ArgumentNullException(nameof(projectId));
        _chatId = chatId ?? throw new ArgumentNullException(nameof(chatId));
        _userId = userId;

        RegisterTools();
    }

    private void RegisterTools()
    {
        // Tool 1: Get available analysis fields (metadata only, no content)
        Add("GetAnalysisFields",
            "Get list of available analysis fields with their keys and descriptions. Returns metadata only, not the actual content.",
            Array.Empty<ChatToolParameter>(),
            GetAnalysisFieldsAsync);

        // Tool 2: Trigger analysis field generation
        Add("TriggerAnalysisFieldGeneration",
            "Trigger generation of a specific analysis field. The actual generation happens in Layer 3 with full context.",
            new[]
            {
                new ChatToolParameter
                {
                    Name = "field_key",
                    Description = "The analysis field key (e.g., 'brand-profile', 'mission', 'vision')",
                    Type = "string",
                    Required = true
                },
                new ChatToolParameter
                {
                    Name = "instruction",
                    Description = "Optional specific instruction for generation",
                    Type = "string",
                    Required = false
                }
            },
            TriggerAnalysisFieldGenerationAsync);

        // Tool 3: Trigger image generation
        Add("TriggerImageGeneration",
            "Trigger logo or image generation. The actual generation happens in Layer 3.",
            new[]
            {
                new ChatToolParameter
                {
                    Name = "image_type",
                    Description = "Type of image to generate (e.g., 'logo', 'banner')",
                    Type = "string",
                    Required = true
                }
            },
            TriggerImageGenerationAsync);

        // Tool 4: Store gathered data
        Add("StoreGatheredData",
            "Store a piece of gathered data from the conversation.",
            new[]
            {
                new ChatToolParameter
                {
                    Name = "key",
                    Description = "Unique key for the data (e.g., 'brand-name', 'business-type')",
                    Type = "string",
                    Required = true
                },
                new ChatToolParameter
                {
                    Name = "value",
                    Description = "The data value to store",
                    Type = "string",
                    Required = true
                },
                new ChatToolParameter
                {
                    Name = "title",
                    Description = "Human-readable title for the data",
                    Type = "string",
                    Required = false
                }
            },
            StoreGatheredDataAsync);

        // Tool 5: Show guidance card
        Add("ShowGuidanceCard",
            "Show a guidance card to the user in the chat interface.",
            new[]
            {
                new ChatToolParameter
                {
                    Name = "card_type",
                    Description = "Type of guidance card (e.g., 'question', 'file-upload', 'url-input')",
                    Type = "string",
                    Required = true
                },
                new ChatToolParameter
                {
                    Name = "message",
                    Description = "Message to show in the guidance card",
                    Type = "string",
                    Required = true
                }
            },
            ShowGuidanceCardAsync);
    }

    private async Task<string> GetAnalysisFieldsAsync(
        List<HazinaChatMessage> messages,
        HazinaChatToolCall toolCall,
        CancellationToken cancellationToken)
    {
        _executedTools.Add("GetAnalysisFields");

        // Return mock data for now - in real implementation this would call IAnalysisFieldsProvider
        var fields = new[]
        {
            new { key = "brand-profile", displayName = "Brand Profile", description = "Overall brand description" },
            new { key = "mission", displayName = "Mission Statement", description = "Brand mission" },
            new { key = "vision", displayName = "Vision Statement", description = "Brand vision" },
            new { key = "core-values", displayName = "Core Values", description = "Brand values" },
            new { key = "target-audience", displayName = "Target Audience", description = "Who the brand serves" },
            new { key = "usps", displayName = "Unique Selling Points", description = "What makes the brand unique" },
            new { key = "tone-of-voice", displayName = "Tone of Voice", description = "Brand communication style" },
            new { key = "color-scheme", displayName = "Color Scheme", description = "Brand colors" },
            new { key = "logo", displayName = "Logo", description = "Brand logo images" }
        };

        return JsonSerializer.Serialize(new
        {
            success = true,
            fields,
            count = fields.Length
        });
    }

    private async Task<string> TriggerAnalysisFieldGenerationAsync(
        List<HazinaChatMessage> messages,
        HazinaChatToolCall toolCall,
        CancellationToken cancellationToken)
    {
        var fieldKey = GetStringParameter(toolCall, "field_key");
        var instruction = GetStringParameter(toolCall, "instruction");

        if (string.IsNullOrWhiteSpace(fieldKey))
            return JsonError("field_key is required");

        _executedTools.Add($"TriggerAnalysisFieldGeneration:{fieldKey}");

        // In real implementation, this would:
        // 1. Load the analysis field service
        // 2. Trigger generation with full project context (Layer 3)
        // 3. Return when generation is queued (not when complete)

        return JsonSerializer.Serialize(new
        {
            success = true,
            message = $"Generation of '{fieldKey}' triggered",
            field_key = fieldKey,
            instruction = instruction ?? "default"
        });
    }

    private async Task<string> TriggerImageGenerationAsync(
        List<HazinaChatMessage> messages,
        HazinaChatToolCall toolCall,
        CancellationToken cancellationToken)
    {
        var imageType = GetStringParameter(toolCall, "image_type");

        if (string.IsNullOrWhiteSpace(imageType))
            return JsonError("image_type is required");

        _executedTools.Add($"TriggerImageGeneration:{imageType}");

        return JsonSerializer.Serialize(new
        {
            success = true,
            message = $"Image generation for '{imageType}' triggered"
        });
    }

    private async Task<string> StoreGatheredDataAsync(
        List<HazinaChatMessage> messages,
        HazinaChatToolCall toolCall,
        CancellationToken cancellationToken)
    {
        var key = GetStringParameter(toolCall, "key");
        var value = GetStringParameter(toolCall, "value");
        var title = GetStringParameter(toolCall, "title") ?? key;

        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            return JsonError("key and value are required");

        _executedTools.Add($"StoreGatheredData:{key}");

        return JsonSerializer.Serialize(new
        {
            success = true,
            message = $"Data '{title}' stored successfully",
            key,
            title
        });
    }

    private async Task<string> ShowGuidanceCardAsync(
        List<HazinaChatMessage> messages,
        HazinaChatToolCall toolCall,
        CancellationToken cancellationToken)
    {
        var cardType = GetStringParameter(toolCall, "card_type");
        var message = GetStringParameter(toolCall, "message");

        if (string.IsNullOrWhiteSpace(cardType) || string.IsNullOrWhiteSpace(message))
            return JsonError("card_type and message are required");

        _executedTools.Add($"ShowGuidanceCard:{cardType}");

        return JsonSerializer.Serialize(new
        {
            success = true,
            message = "Guidance card shown to user",
            card_type = cardType
        });
    }

    #region Helper Methods

    private static string? GetStringParameter(HazinaChatToolCall toolCall, string name)
    {
        using var doc = JsonDocument.Parse(toolCall.FunctionArguments);
        return doc.RootElement.TryGetProperty(name, out var el) ? el.GetString() : null;
    }

    private static string JsonError(string message)
        => JsonSerializer.Serialize(new { success = false, error = message });

    #endregion
}
