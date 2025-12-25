using System.Text.Json.Serialization;

namespace Hazina.Tools.Services.DataGathering.Models;

/// <summary>
/// Represents a flexible data value that can be a string, number, large text, or a list of items.
/// Uses a discriminated union pattern to support multiple data types in a single property.
/// </summary>
public sealed class GatheredDataValue
{
    /// <summary>
    /// The type of data stored in this value.
    /// </summary>
    [JsonPropertyName("type")]
    public GatheredDataValueType Type { get; set; } = GatheredDataValueType.String;

    /// <summary>
    /// The string value when <see cref="Type"/> is <see cref="GatheredDataValueType.String"/>.
    /// </summary>
    [JsonPropertyName("stringValue")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StringValue { get; set; }

    /// <summary>
    /// The numeric value when <see cref="Type"/> is <see cref="GatheredDataValueType.Number"/>.
    /// </summary>
    [JsonPropertyName("numberValue")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? NumberValue { get; set; }

    /// <summary>
    /// The large text value when <see cref="Type"/> is <see cref="GatheredDataValueType.LargeText"/>.
    /// Suitable for display in a textarea or expandable section.
    /// </summary>
    [JsonPropertyName("largeTextValue")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LargeTextValue { get; set; }

    /// <summary>
    /// The list of nested items when <see cref="Type"/> is <see cref="GatheredDataValueType.List"/>.
    /// </summary>
    [JsonPropertyName("listValue")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<GatheredDataItem>? ListValue { get; set; }

    /// <summary>
    /// Creates a new <see cref="GatheredDataValue"/> with a string value.
    /// </summary>
    public static GatheredDataValue FromString(string value) => new()
    {
        Type = GatheredDataValueType.String,
        StringValue = value
    };

    /// <summary>
    /// Creates a new <see cref="GatheredDataValue"/> with a numeric value.
    /// </summary>
    public static GatheredDataValue FromNumber(decimal value) => new()
    {
        Type = GatheredDataValueType.Number,
        NumberValue = value
    };

    /// <summary>
    /// Creates a new <see cref="GatheredDataValue"/> with a large text value.
    /// </summary>
    public static GatheredDataValue FromLargeText(string value) => new()
    {
        Type = GatheredDataValueType.LargeText,
        LargeTextValue = value
    };

    /// <summary>
    /// Creates a new <see cref="GatheredDataValue"/> with a list of items.
    /// </summary>
    public static GatheredDataValue FromList(IReadOnlyList<GatheredDataItem> items) => new()
    {
        Type = GatheredDataValueType.List,
        ListValue = items
    };

    /// <summary>
    /// Gets the display value as a string, regardless of the underlying type.
    /// </summary>
    [JsonIgnore]
    public string DisplayValue => Type switch
    {
        GatheredDataValueType.String => StringValue ?? string.Empty,
        GatheredDataValueType.Number => NumberValue?.ToString() ?? string.Empty,
        GatheredDataValueType.LargeText => LargeTextValue ?? string.Empty,
        GatheredDataValueType.List => $"[{ListValue?.Count ?? 0} items]",
        _ => string.Empty
    };
}
