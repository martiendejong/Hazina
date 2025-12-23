using System.Text.Json.Serialization;

namespace DevGPT.GenerationTools.Services.DataGathering.Models;

/// <summary>
/// Payload structure for chat messages that contain gathered data.
/// This is used in <see cref="ConversationMessage.Payload"/> to display
/// gathered data in a custom chat component.
/// </summary>
public sealed class GatheredDataMessagePayload
{
    /// <summary>
    /// The discriminator value used to identify this payload type in the frontend.
    /// </summary>
    public const string PayloadType = "gathered-data";

    /// <summary>
    /// The component path for rendering this payload in the frontend.
    /// </summary>
    public const string ComponentPath = "view/analysis/GatheredData";

    /// <summary>
    /// The type identifier for this payload. Always returns <see cref="PayloadType"/>.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type => PayloadType;

    /// <summary>
    /// The component name/path for rendering this payload.
    /// </summary>
    [JsonPropertyName("componentName")]
    public string ComponentName => ComponentPath;

    /// <summary>
    /// The collection of gathered data items to display.
    /// </summary>
    [JsonPropertyName("items")]
    public IReadOnlyList<GatheredDataItem> Items { get; set; } = Array.Empty<GatheredDataItem>();

    /// <summary>
    /// Optional summary text to display above or below the items.
    /// </summary>
    [JsonPropertyName("summary")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Summary { get; set; }

    /// <summary>
    /// The UTC timestamp when this batch of data was gathered.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Creates an empty payload.
    /// </summary>
    public GatheredDataMessagePayload() { }

    /// <summary>
    /// Creates a payload with the specified items.
    /// </summary>
    public GatheredDataMessagePayload(IEnumerable<GatheredDataItem> items, string? summary = null)
    {
        Items = items.ToList().AsReadOnly();
        Summary = summary;
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a payload containing a single item.
    /// </summary>
    public static GatheredDataMessagePayload FromSingleItem(GatheredDataItem item, string? summary = null)
        => new(new[] { item }, summary);

    /// <summary>
    /// Creates a payload containing multiple items.
    /// </summary>
    public static GatheredDataMessagePayload FromItems(IEnumerable<GatheredDataItem> items, string? summary = null)
        => new(items, summary);
}
