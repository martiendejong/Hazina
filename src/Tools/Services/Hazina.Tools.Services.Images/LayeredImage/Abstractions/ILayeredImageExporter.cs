using Hazina.Tools.Services.Images.LayeredImage.Models;

namespace Hazina.Tools.Services.Images.LayeredImage.Abstractions;

/// <summary>
/// Interface for exporting layered images to specific formats.
/// </summary>
public interface ILayeredImageExporter
{
    /// <summary>
    /// The format this exporter handles.
    /// </summary>
    LayeredImageFormat Format { get; }

    /// <summary>
    /// File extension including the dot (e.g., ".pdn", ".ora").
    /// </summary>
    string FileExtension { get; }

    /// <summary>
    /// Exports layers to the target format.
    /// </summary>
    /// <param name="canvasWidth">Width of the canvas.</param>
    /// <param name="canvasHeight">Height of the canvas.</param>
    /// <param name="layers">List of generated layers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The exported file as a byte array.</returns>
    Task<byte[]> ExportAsync(
        int canvasWidth,
        int canvasHeight,
        IReadOnlyList<LayerGenerationResult> layers,
        CancellationToken cancellationToken = default);
}
