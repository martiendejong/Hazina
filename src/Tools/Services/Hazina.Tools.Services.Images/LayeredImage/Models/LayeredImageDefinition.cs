using System.Text.Json.Serialization;

namespace Hazina.Tools.Services.Images.LayeredImage.Models;

/// <summary>
/// Complete definition for a layered image, as received from LLM JSON.
/// </summary>
public class LayeredImageDefinition
{
    /// <summary>
    /// Output format (pdn, ora, psd). Defaults to "pdn".
    /// </summary>
    [JsonPropertyName("format")]
    public string Format { get; set; } = "pdn";

    /// <summary>
    /// Canvas dimensions for the final image.
    /// </summary>
    [JsonPropertyName("canvas")]
    public CanvasDimensions Canvas { get; set; } = new();

    /// <summary>
    /// Ordered list of layers (bottom to top).
    /// </summary>
    [JsonPropertyName("layers")]
    public List<LayerDefinition> Layers { get; set; } = new();

    /// <summary>
    /// Optional metadata for the image.
    /// </summary>
    [JsonPropertyName("metadata")]
    public ImageMetadata? Metadata { get; set; }

    /// <summary>
    /// Parses the format string to the enum value.
    /// </summary>
    public LayeredImageFormat GetFormat()
    {
        return Format?.ToLowerInvariant() switch
        {
            "pdn" => LayeredImageFormat.Pdn,
            "ora" => LayeredImageFormat.Ora,
            "psd" => LayeredImageFormat.Psd,
            _ => LayeredImageFormat.Pdn
        };
    }
}

/// <summary>
/// Canvas dimensions.
/// </summary>
public class CanvasDimensions
{
    [JsonPropertyName("width")]
    public int Width { get; set; } = 1920;

    [JsonPropertyName("height")]
    public int Height { get; set; } = 1080;
}

/// <summary>
/// Definition for a single layer.
/// </summary>
public class LayerDefinition
{
    /// <summary>
    /// Unique name for the layer.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "Layer";

    /// <summary>
    /// Layer type: Generated, Uploaded, SolidColor, Text.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "Generated";

    /// <summary>
    /// Position on the canvas.
    /// </summary>
    [JsonPropertyName("position")]
    public LayerPosition Position { get; set; } = new();

    /// <summary>
    /// Size of the layer content.
    /// </summary>
    [JsonPropertyName("size")]
    public LayerSize Size { get; set; } = new();

    /// <summary>
    /// Content meaning depends on type:
    /// - Generated: AI prompt
    /// - Uploaded: filename from uploads
    /// - SolidColor: hex color (#FF5500)
    /// - Text: the text to render
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Layer opacity (0.0 to 1.0).
    /// </summary>
    [JsonPropertyName("opacity")]
    public float Opacity { get; set; } = 1.0f;

    /// <summary>
    /// Blend mode (Normal, Multiply, Screen, Overlay, Darken, Lighten).
    /// </summary>
    [JsonPropertyName("blendMode")]
    public string BlendMode { get; set; } = "Normal";

    /// <summary>
    /// Whether the layer is visible.
    /// </summary>
    [JsonPropertyName("visible")]
    public bool Visible { get; set; } = true;

    /// <summary>
    /// Text-specific: font family.
    /// </summary>
    [JsonPropertyName("fontFamily")]
    public string? FontFamily { get; set; }

    /// <summary>
    /// Text-specific: font size in points.
    /// </summary>
    [JsonPropertyName("fontSize")]
    public int? FontSize { get; set; }

    /// <summary>
    /// Text-specific: font color (hex).
    /// </summary>
    [JsonPropertyName("fontColor")]
    public string? FontColor { get; set; }

    /// <summary>
    /// Parses the layer type string to enum.
    /// </summary>
    public LayerType GetLayerType()
    {
        return Type?.ToLowerInvariant() switch
        {
            "generated" => LayerType.Generated,
            "uploaded" => LayerType.Uploaded,
            "solidcolor" => LayerType.SolidColor,
            "text" => LayerType.Text,
            _ => LayerType.Generated
        };
    }

    /// <summary>
    /// Parses the blend mode string to enum.
    /// </summary>
    public LayerBlendMode GetBlendMode()
    {
        return BlendMode?.ToLowerInvariant() switch
        {
            "normal" => LayerBlendMode.Normal,
            "multiply" => LayerBlendMode.Multiply,
            "screen" => LayerBlendMode.Screen,
            "overlay" => LayerBlendMode.Overlay,
            "darken" => LayerBlendMode.Darken,
            "lighten" => LayerBlendMode.Lighten,
            _ => LayerBlendMode.Normal
        };
    }
}

/// <summary>
/// Layer position on the canvas.
/// </summary>
public class LayerPosition
{
    [JsonPropertyName("x")]
    public int X { get; set; } = 0;

    [JsonPropertyName("y")]
    public int Y { get; set; } = 0;
}

/// <summary>
/// Layer content size.
/// </summary>
public class LayerSize
{
    [JsonPropertyName("width")]
    public int Width { get; set; } = 1024;

    [JsonPropertyName("height")]
    public int Height { get; set; } = 1024;
}

/// <summary>
/// Optional metadata for the layered image.
/// </summary>
public class ImageMetadata
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

/// <summary>
/// Type of layer content.
/// </summary>
public enum LayerType
{
    /// <summary>
    /// AI-generated image from prompt.
    /// </summary>
    Generated,

    /// <summary>
    /// Existing file from uploads.
    /// </summary>
    Uploaded,

    /// <summary>
    /// Solid color fill.
    /// </summary>
    SolidColor,

    /// <summary>
    /// Rendered text.
    /// </summary>
    Text
}

/// <summary>
/// Layer blend modes.
/// </summary>
public enum LayerBlendMode
{
    Normal,
    Multiply,
    Screen,
    Overlay,
    Darken,
    Lighten
}
