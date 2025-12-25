using System.Text.Json.Serialization;

namespace Hazina.Tools.Services.DataGathering.Models;

/// <summary>
/// Represents a single piece of gathered data with a key, human-readable title, and flexible value.
/// This is the primary unit of data storage for information extracted from chat conversations.
/// </summary>
public sealed class GatheredDataItem
{
    /// <summary>
    /// The unique key identifying this data item within a project.
    /// Used as the filename (sanitized) and for lookups.
    /// Examples: "brand-name", "company-website", "target-audience"
    /// </summary>
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// A human-readable title for display in the UI.
    /// Examples: "Brand Name", "Company Website", "Target Audience"
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The actual data value, which can be a string, number, large text, or list of items.
    /// </summary>
    [JsonPropertyName("data")]
    public GatheredDataValue Data { get; set; } = new();

    /// <summary>
    /// The UTC timestamp when this data was gathered.
    /// </summary>
    [JsonPropertyName("gatheredAt")]
    public DateTime GatheredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional reference to the source of this data (e.g., message index or context).
    /// </summary>
    [JsonPropertyName("source")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Source { get; set; }

    /// <summary>
    /// Creates a new <see cref="GatheredDataItem"/> with a simple string value.
    /// </summary>
    public static GatheredDataItem Create(string key, string title, string value, string? source = null) => new()
    {
        Key = key,
        Title = title,
        Data = GatheredDataValue.FromString(value),
        GatheredAt = DateTime.UtcNow,
        Source = source
    };

    /// <summary>
    /// Creates a new <see cref="GatheredDataItem"/> with a numeric value.
    /// </summary>
    public static GatheredDataItem Create(string key, string title, decimal value, string? source = null) => new()
    {
        Key = key,
        Title = title,
        Data = GatheredDataValue.FromNumber(value),
        GatheredAt = DateTime.UtcNow,
        Source = source
    };

    /// <summary>
    /// Creates a new <see cref="GatheredDataItem"/> with a large text value.
    /// </summary>
    public static GatheredDataItem CreateLargeText(string key, string title, string value, string? source = null) => new()
    {
        Key = key,
        Title = title,
        Data = GatheredDataValue.FromLargeText(value),
        GatheredAt = DateTime.UtcNow,
        Source = source
    };

    /// <summary>
    /// Creates a new <see cref="GatheredDataItem"/> with a list of nested items.
    /// </summary>
    public static GatheredDataItem CreateList(string key, string title, IReadOnlyList<GatheredDataItem> items, string? source = null) => new()
    {
        Key = key,
        Title = title,
        Data = GatheredDataValue.FromList(items),
        GatheredAt = DateTime.UtcNow,
        Source = source
    };
}
