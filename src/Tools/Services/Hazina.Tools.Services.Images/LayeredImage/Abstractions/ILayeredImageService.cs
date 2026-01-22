using Hazina.Tools.Services.Images.LayeredImage.Models;

namespace Hazina.Tools.Services.Images.LayeredImage.Abstractions;

/// <summary>
/// Main service for generating layered images.
/// Orchestrates layer generation, composition, and export.
/// </summary>
public interface ILayeredImageService
{
    /// <summary>
    /// Generates a layered image from the given definition.
    /// </summary>
    /// <param name="projectId">Project ID for storage.</param>
    /// <param name="userId">Optional user ID.</param>
    /// <param name="definition">Layer definitions from LLM.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing file URL and layer info.</returns>
    Task<LayeredImageResult> GenerateAsync(
        string projectId,
        string? userId,
        LayeredImageDefinition definition,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a layered image from JSON definition string.
    /// </summary>
    /// <param name="projectId">Project ID for storage.</param>
    /// <param name="userId">Optional user ID.</param>
    /// <param name="jsonDefinition">JSON string representing LayeredImageDefinition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing file URL and layer info.</returns>
    Task<LayeredImageResult> GenerateFromJsonAsync(
        string projectId,
        string? userId,
        string jsonDefinition,
        CancellationToken cancellationToken = default);
}
