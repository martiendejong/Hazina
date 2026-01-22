namespace Hazina.Tools.Services.Images.LayeredImage.Models;

/// <summary>
/// Result of generating/loading a single layer.
/// </summary>
public class LayerGenerationResult
{
    /// <summary>
    /// Layer name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Raw image data (PNG or other format).
    /// </summary>
    public byte[] ImageData { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Actual width of the generated image.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Actual height of the generated image.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Position X on the canvas.
    /// </summary>
    public int PositionX { get; set; }

    /// <summary>
    /// Position Y on the canvas.
    /// </summary>
    public int PositionY { get; set; }

    /// <summary>
    /// Layer opacity (0-255 for internal use).
    /// </summary>
    public byte Opacity { get; set; } = 255;

    /// <summary>
    /// Blend mode for compositing.
    /// </summary>
    public LayerBlendMode BlendMode { get; set; } = LayerBlendMode.Normal;

    /// <summary>
    /// Whether the layer is visible.
    /// </summary>
    public bool Visible { get; set; } = true;

    /// <summary>
    /// Whether generation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if generation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of generating a complete layered image.
/// </summary>
public class LayeredImageResult
{
    /// <summary>
    /// Whether the overall generation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Generated filename (e.g., "a1b2c3d4.pdn").
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// URL to access the file.
    /// </summary>
    public string FileUrl { get; set; } = string.Empty;

    /// <summary>
    /// Format used for export.
    /// </summary>
    public LayeredImageFormat Format { get; set; }

    /// <summary>
    /// Results for each layer.
    /// </summary>
    public List<LayerResultInfo> Layers { get; set; } = new();

    /// <summary>
    /// Error message if generation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Summary info for a layer in the result.
/// </summary>
public class LayerResultInfo
{
    /// <summary>
    /// Layer name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Layer width.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Layer height.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Whether this layer was generated successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if layer generation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
