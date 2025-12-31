using System.Text.Json;
using Hazina.Tools.Models;
using Hazina.Tools.Services.DataGathering.Abstractions;
using Hazina.Tools.Services.DataGathering.Models;
using Hazina.Tools.Services.Store;

namespace Hazina.Tools.Services.DataGathering.ToolsContexts;

/// <summary>
/// Tools context for the background data gathering process.
/// Provides tools for storing structured information extracted from chat conversations.
/// </summary>
/// <remarks>
/// This context is used by the parallel data extraction service, not by the main chat.
/// It contains only data gathering tools to keep concerns separated.
/// </remarks>
public sealed class DataGatheringToolsContext : ToolsContextBase
{
    private readonly IGatheredDataProvider _dataProvider;
    private readonly string _projectId;
    private readonly string _chatId;
    private readonly IAnalysisFieldsProvider? _analysisProvider;

    /// <summary>
    /// Gets the items that were gathered during tool execution.
    /// </summary>
    public IReadOnlyList<GatheredDataItem> GatheredItems => _gatheredItems.AsReadOnly();
    private readonly List<GatheredDataItem> _gatheredItems = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="DataGatheringToolsContext"/> class.
    /// </summary>
    /// <param name="dataProvider">The provider for storing gathered data.</param>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="chatId">The chat identifier where data is being gathered.</param>
    /// <param name="analysisProvider">Optional analysis provider to enable analysis tools.</param>
    public DataGatheringToolsContext(
        IGatheredDataProvider dataProvider,
        string projectId,
        string chatId,
        IAnalysisFieldsProvider? analysisProvider = null)
    {
        _dataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
        _projectId = projectId ?? throw new ArgumentNullException(nameof(projectId));
        _chatId = chatId ?? throw new ArgumentNullException(nameof(chatId));
        _analysisProvider = analysisProvider;

        RegisterGatheringTools();
        RegisterAnalysisTools();
    }

    private void RegisterGatheringTools()
    {
        // Main tool for storing gathered data with full structured format
        Add("StoreGatheredData",
            "Store structured information gathered from the conversation. Use this to capture important facts, preferences, or data points mentioned by the user.",
            [
                CreateParameter("key", "A unique identifier for this data (e.g., 'brand-name', 'target-audience'). Use lowercase with hyphens.", "string", required: true),
                CreateParameter("title", "A human-readable title for this data item (e.g., 'Brand Name', 'Target Audience')", "string", required: true),
                CreateParameter("value", "The actual data value to store", "string", required: true),
                CreateParameter("value_type", "The type of value: 'string', 'number', 'large_text', or 'list'. Default is 'string'.", "string", required: false)
            ],
            StoreGatheredDataAsync);

        // Convenience tool for storing simple key-value pairs
        Add("StoreSimpleData",
            "Store a simple piece of information as a string. Shorthand for common use cases.",
            [
                CreateParameter("key", "A unique identifier for this data", "string", required: true),
                CreateParameter("value", "The value to store", "string", required: true)
            ],
            StoreSimpleDataAsync);

        // Tool for storing a list of items
        Add("StoreListData",
            "Store a list of related items. Use when the user provides multiple items of the same type.",
            [
                CreateParameter("key", "A unique identifier for this list (e.g., 'competitors', 'features')", "string", required: true),
                CreateParameter("title", "A human-readable title for this list", "string", required: true),
                CreateParameter("items", "JSON array of items. Each item should have 'key', 'title', and 'value' fields.", "string", required: true)
            ],
            StoreListDataAsync);

        // Tool for storing large text content
        Add("StoreLargeText",
            "Store large text content like descriptions, stories, or documentation.",
            [
                CreateParameter("key", "A unique identifier for this content", "string", required: true),
                CreateParameter("title", "A human-readable title", "string", required: true),
                CreateParameter("content", "The large text content to store", "string", required: true)
            ],
            StoreLargeTextAsync);

        // Tool to check if data already exists
        Add("CheckDataExists",
            "Check if a data item with the given key already exists.",
            [
                CreateParameter("key", "The key to check", "string", required: true)
            ],
            CheckDataExistsAsync);

        // Tool to get existing data
        Add("GetExistingData",
            "Retrieve previously stored data by key.",
            [
                CreateParameter("key", "The key of the data to retrieve", "string", required: true)
            ],
            GetExistingDataAsync);
    }

    private void RegisterAnalysisTools()
    {
        if (_analysisProvider == null) return;

        // Allow the model to populate analysis fields when enough context is available
        Add("UpdateAnalysisField",
            "Store generated content for an analysis field when you have enough information. Only call this when confident.",
            [
                CreateParameter("key", "Analysis field key (e.g., 'topic-synopsis', 'central-thesis')", "string", required: true),
                CreateParameter("content", "Generated content for this analysis field", "string", required: true),
                CreateParameter("feedback", "Optional feedback or reasoning you used", "string", required: false)
            ],
            UpdateAnalysisFieldAsync);
    }

    private async Task<string> StoreGatheredDataAsync(
        List<HazinaChatMessage> messages,
        HazinaChatToolCall toolCall,
        CancellationToken cancellationToken)
    {
        var key = GetStringParameter(toolCall, "key");
        var title = GetStringParameter(toolCall, "title");
        var value = GetStringParameter(toolCall, "value");
        var valueType = GetStringParameter(toolCall, "value_type") ?? "string";

        if (string.IsNullOrWhiteSpace(key))
            return JsonResult(false, "Parameter 'key' is required.");
        if (string.IsNullOrWhiteSpace(title))
            return JsonResult(false, "Parameter 'title' is required.");
        if (string.IsNullOrWhiteSpace(value))
            return JsonResult(false, "Parameter 'value' is required.");

        var dataValue = valueType.ToLowerInvariant() switch
        {
            "number" when decimal.TryParse(value, out var num) => GatheredDataValue.FromNumber(num),
            "large_text" => GatheredDataValue.FromLargeText(value),
            _ => GatheredDataValue.FromString(value)
        };

        var item = new GatheredDataItem
        {
            Key = key,
            Title = title,
            Data = dataValue,
            Source = $"chat:{_chatId}"
        };

        return await SaveItemAsync(item, cancellationToken);
    }

    private async Task<string> StoreSimpleDataAsync(
        List<HazinaChatMessage> messages,
        HazinaChatToolCall toolCall,
        CancellationToken cancellationToken)
    {
        var key = GetStringParameter(toolCall, "key");
        var value = GetStringParameter(toolCall, "value");

        if (string.IsNullOrWhiteSpace(key))
            return JsonResult(false, "Parameter 'key' is required.");
        if (string.IsNullOrWhiteSpace(value))
            return JsonResult(false, "Parameter 'value' is required.");

        // Generate title from key
        var title = GenerateTitleFromKey(key);

        var item = new GatheredDataItem
        {
            Key = key,
            Title = title,
            Data = GatheredDataValue.FromString(value),
            Source = $"chat:{_chatId}"
        };

        return await SaveItemAsync(item, cancellationToken);
    }

    private async Task<string> StoreListDataAsync(
        List<HazinaChatMessage> messages,
        HazinaChatToolCall toolCall,
        CancellationToken cancellationToken)
    {
        var key = GetStringParameter(toolCall, "key");
        var title = GetStringParameter(toolCall, "title");
        var itemsJson = GetStringParameter(toolCall, "items");

        if (string.IsNullOrWhiteSpace(key))
            return JsonResult(false, "Parameter 'key' is required.");
        if (string.IsNullOrWhiteSpace(title))
            return JsonResult(false, "Parameter 'title' is required.");
        if (string.IsNullOrWhiteSpace(itemsJson))
            return JsonResult(false, "Parameter 'items' is required.");

        List<GatheredDataItem> listItems;
        try
        {
            listItems = ParseListItems(itemsJson);
        }
        catch (JsonException ex)
        {
            return JsonResult(false, $"Invalid JSON in 'items': {ex.Message}");
        }

        var item = new GatheredDataItem
        {
            Key = key,
            Title = title,
            Data = GatheredDataValue.FromList(listItems),
            Source = $"chat:{_chatId}"
        };

        return await SaveItemAsync(item, cancellationToken);
    }

    private async Task<string> StoreLargeTextAsync(
        List<HazinaChatMessage> messages,
        HazinaChatToolCall toolCall,
        CancellationToken cancellationToken)
    {
        var key = GetStringParameter(toolCall, "key");
        var title = GetStringParameter(toolCall, "title");
        var content = GetStringParameter(toolCall, "content");

        if (string.IsNullOrWhiteSpace(key))
            return JsonResult(false, "Parameter 'key' is required.");
        if (string.IsNullOrWhiteSpace(title))
            return JsonResult(false, "Parameter 'title' is required.");
        if (string.IsNullOrWhiteSpace(content))
            return JsonResult(false, "Parameter 'content' is required.");

        var item = new GatheredDataItem
        {
            Key = key,
            Title = title,
            Data = GatheredDataValue.FromLargeText(content),
            Source = $"chat:{_chatId}"
        };

        return await SaveItemAsync(item, cancellationToken);
    }

    private async Task<string> CheckDataExistsAsync(
        List<HazinaChatMessage> messages,
        HazinaChatToolCall toolCall,
        CancellationToken cancellationToken)
    {
        var key = GetStringParameter(toolCall, "key");
        if (string.IsNullOrWhiteSpace(key))
            return JsonResult(false, "Parameter 'key' is required.");

        var exists = await _dataProvider.ExistsAsync(_projectId, key, cancellationToken);
        return JsonSerializer.Serialize(new { exists, key });
    }

    private async Task<string> GetExistingDataAsync(
        List<HazinaChatMessage> messages,
        HazinaChatToolCall toolCall,
        CancellationToken cancellationToken)
    {
        var key = GetStringParameter(toolCall, "key");
        if (string.IsNullOrWhiteSpace(key))
            return JsonResult(false, "Parameter 'key' is required.");

        var item = await _dataProvider.GetAsync(_projectId, key, cancellationToken);
        if (item == null)
            return JsonSerializer.Serialize(new { found = false, key });

        return JsonSerializer.Serialize(new
        {
            found = true,
            key = item.Key,
            title = item.Title,
            value = item.Data.DisplayValue,
            type = item.Data.Type.ToString().ToLowerInvariant()
        });
    }

    private async Task<string> SaveItemAsync(GatheredDataItem item, CancellationToken cancellationToken)
    {
        if (IsIrrelevantProfileNoise(item))
        {
            return JsonResult(false, $"Skipped irrelevant data for key '{item.Key}'.");
        }

        // Check if data already exists
        var existingItem = await _dataProvider.GetAsync(_projectId, item.Key, cancellationToken);
        if (existingItem != null)
        {
            // Compare values - only save if changed
            var existingValue = existingItem.Data?.DisplayValue ?? string.Empty;
            var newValue = item.Data?.DisplayValue ?? string.Empty;

            if (string.Equals(existingValue, newValue, StringComparison.OrdinalIgnoreCase))
            {
                // Value hasn't changed - skip saving
                return JsonSerializer.Serialize(new
                {
                    ok = false,
                    key = item.Key,
                    message = $"Data '{item.Title}' already exists with the same value. Skipped duplicate."
                });
            }
        }

        var success = await _dataProvider.SaveAsync(_projectId, item, cancellationToken);
        if (success)
        {
            _gatheredItems.Add(item);
            return JsonSerializer.Serialize(new
            {
                ok = true,
                key = item.Key,
                title = item.Title,
                message = existingItem != null
                    ? $"Data '{item.Title}' updated successfully."
                    : $"Data '{item.Title}' stored successfully."
            });
        }

        return JsonResult(false, $"Failed to store data with key '{item.Key}'.");
    }

    private static bool IsIrrelevantProfileNoise(GatheredDataItem item)
    {
        // Block obvious non-profile keys and mirrored chat content
        var key = item.Key?.ToLowerInvariant() ?? string.Empty;
        var title = item.Title?.ToLowerInvariant() ?? string.Empty;
        var value = item.Data?.DisplayValue?.ToLowerInvariant() ?? string.Empty;

        // Block metadata/conversational noise
        string[] bannedKeyFragments = new[]
        {
            "timestamp", "time", "date", "message", "chat", "conversation", "user", "assistant", "utterance", "prompt"
        };

        if (bannedKeyFragments.Any(f => key.Contains(f) || title.Contains(f)))
        {
            return true;
        }

        // Block brand/company/business NAME fields from gathered data (these belong in analysis fields only)
        // IMPORTANT: Use EXACT match only, not Contains, to avoid blocking valid fields like "business-plan", "existing-business"
        string[] analysisOnlyKeys = new[]
        {
            "brand-name", "brandname", "brand_name",
            "company-name", "companyname", "company_name",
            "business-name", "businessname", "business_name",
            "project-name", "projectname", "project_name"
        };

        // Only block if key EXACTLY matches an analysis-only field
        if (analysisOnlyKeys.Any(k => key == k))
        {
            return true;
        }

        // Treat pure echoed conversation as noise
        if (value.StartsWith("user:") || value.StartsWith("assistant:") || value.StartsWith("system:"))
        {
            return true;
        }

        // If the value looks like a raw timestamp-only content
        if (DateTime.TryParse(value, out _))
        {
            return true;
        }

        return false;
    }

    private static List<GatheredDataItem> ParseListItems(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var result = new List<GatheredDataItem>();

        foreach (var element in doc.RootElement.EnumerateArray())
        {
            var key = element.GetProperty("key").GetString() ?? string.Empty;
            var title = element.TryGetProperty("title", out var t) ? t.GetString() ?? key : key;
            var value = element.GetProperty("value").GetString() ?? string.Empty;

            result.Add(new GatheredDataItem
            {
                Key = key,
                Title = title,
                Data = GatheredDataValue.FromString(value)
            });
        }

        return result;
    }

    private static string GenerateTitleFromKey(string key)
    {
        // Convert 'brand-name' to 'Brand Name'
        var words = key.Replace('-', ' ').Replace('_', ' ').Split(' ');
        return string.Join(' ', words.Select(w =>
            string.IsNullOrEmpty(w) ? w : char.ToUpper(w[0]) + w[1..].ToLower()));
    }

    #region Parameter Helpers

    private static ChatToolParameter CreateParameter(string name, string description, string type, bool required)
        => new() { Name = name, Description = description, Type = type, Required = required };

    private static string? GetStringParameter(HazinaChatToolCall toolCall, string name)
    {
        using var doc = JsonDocument.Parse(toolCall.FunctionArguments);
        return doc.RootElement.TryGetProperty(name, out var el) ? el.GetString() : null;
    }

    private static string JsonResult(bool ok, string message)
        => JsonSerializer.Serialize(new { ok, message });

    #endregion

    private async Task<string> UpdateAnalysisFieldAsync(
        List<HazinaChatMessage> messages,
        HazinaChatToolCall toolCall,
        CancellationToken cancellationToken)
    {
        if (_analysisProvider == null)
            return JsonResult(false, "Analysis provider not available.");

        using var doc = JsonDocument.Parse(toolCall.FunctionArguments);
        var key = doc.RootElement.TryGetProperty("key", out var k) ? k.GetString() : null;
        var content = doc.RootElement.TryGetProperty("content", out var c) ? c.GetString() : null;
        var feedback = doc.RootElement.TryGetProperty("feedback", out var f) ? f.GetString() : null;

        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(content))
            return JsonResult(false, "Parameters 'key' and 'content' are required.");

        var ok = await _analysisProvider.SaveFieldAsync(_projectId, key, content, feedback, _chatId);
        return JsonSerializer.Serialize(new { ok, key });
    }
}
