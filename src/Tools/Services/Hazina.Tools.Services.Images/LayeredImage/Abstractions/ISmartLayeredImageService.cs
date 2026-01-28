namespace Hazina.Tools.Services.Images.LayeredImage.Abstractions;

/// <summary>
/// Smart layered image service that takes a natural language prompt
/// and automatically plans, generates, and composites a multi-layer image.
/// </summary>
public interface ISmartLayeredImageService
{
    /// <summary>
    /// Generates a layered image from a natural language description.
    /// The LLM plans the layer structure, generates each layer, and composites them.
    /// </summary>
    /// <param name="prompt">Natural language description of the desired image</param>
    /// <param name="width">Canvas width (default 1024)</param>
    /// <param name="height">Canvas height (default 1024)</param>
    /// <param name="format">Output format: pdn, ora, psd (default pdn)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the layered image data and metadata</returns>
    Task<SmartLayeredImageResult> GenerateFromPromptAsync(
        string prompt,
        int width = 1024,
        int height = 1024,
        string format = "pdn",
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of smart layered image generation.
/// </summary>
public class SmartLayeredImageResult
{
    /// <summary>
    /// Whether generation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if generation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// The generated layered image file data.
    /// </summary>
    public byte[]? LayeredImageData { get; set; }

    /// <summary>
    /// The flattened composite PNG for preview.
    /// </summary>
    public byte[]? CompositePreview { get; set; }

    /// <summary>
    /// Individual layer images (for debugging/preview).
    /// </summary>
    public List<LayerPreview> Layers { get; set; } = new();

    /// <summary>
    /// The LLM's reasoning for the composition.
    /// </summary>
    public string? CompositionReasoning { get; set; }

    /// <summary>
    /// Output format used (pdn, ora, psd).
    /// </summary>
    public string Format { get; set; } = "pdn";
}

/// <summary>
/// Preview of an individual layer.
/// </summary>
public class LayerPreview
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public byte[]? ImageData { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }
}
