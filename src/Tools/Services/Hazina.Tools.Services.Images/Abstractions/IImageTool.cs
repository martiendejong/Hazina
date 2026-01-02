namespace Hazina.Tools.Services.Images.Abstractions;

/// <summary>
/// Main entry point for image editing operations.
/// Orchestrates multiple operations using appropriate providers.
/// </summary>
public interface IImageTool
{
    /// <summary>
    /// Applies a sequence of edit operations to an image.
    /// </summary>
    /// <param name="request">The edit request containing the source image and operations to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result containing the edited image stream.</returns>
    Task<ImageEditResult> EditAsync(
        ImageEditRequest request,
        CancellationToken cancellationToken = default);
}
