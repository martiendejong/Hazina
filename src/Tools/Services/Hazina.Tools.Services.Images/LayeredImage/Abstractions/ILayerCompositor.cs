using Hazina.Tools.Services.Images.LayeredImage.Models;

namespace Hazina.Tools.Services.Images.LayeredImage.Abstractions;

/// <summary>
/// Interface for compositing multiple layers into a single image.
/// </summary>
public interface ILayerCompositor
{
    /// <summary>
    /// Composites all layers into a single flattened image.
    /// </summary>
    /// <param name="canvasWidth">Width of the canvas.</param>
    /// <param name="canvasHeight">Height of the canvas.</param>
    /// <param name="layers">Layers to composite (bottom to top order).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>PNG byte array of the composited image.</returns>
    Task<byte[]> CompositeAsync(
        int canvasWidth,
        int canvasHeight,
        IReadOnlyList<LayerGenerationResult> layers,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a thumbnail of the composited image.
    /// </summary>
    /// <param name="canvasWidth">Width of the canvas.</param>
    /// <param name="canvasHeight">Height of the canvas.</param>
    /// <param name="layers">Layers to composite.</param>
    /// <param name="maxDimension">Maximum dimension (width or height) for the thumbnail.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>PNG byte array of the thumbnail.</returns>
    Task<byte[]> GenerateThumbnailAsync(
        int canvasWidth,
        int canvasHeight,
        IReadOnlyList<LayerGenerationResult> layers,
        int maxDimension = 256,
        CancellationToken cancellationToken = default);
}
