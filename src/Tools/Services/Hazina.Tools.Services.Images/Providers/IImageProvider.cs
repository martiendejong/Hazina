using Hazina.Tools.Services.Images.Abstractions;
using Hazina.Tools.Services.Images.Operations;

namespace Hazina.Tools.Services.Images.Providers;

/// <summary>
/// Interface for image processing providers.
/// Each provider can handle specific types of operations.
/// </summary>
public interface IImageProvider
{
    /// <summary>
    /// The unique name of this provider.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Determines if this provider can handle the specified operation.
    /// </summary>
    /// <param name="operation">The operation to check.</param>
    /// <returns>True if this provider can handle the operation.</returns>
    bool CanHandle(ImageEditOperation operation);

    /// <summary>
    /// Applies an operation to an image.
    /// </summary>
    /// <param name="image">The source image stream.</param>
    /// <param name="operation">The operation to apply.</param>
    /// <param name="options">Provider options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A new stream containing the processed image.</returns>
    Task<Stream> ApplyAsync(
        Stream image,
        ImageEditOperation operation,
        ImageProviderOptions options,
        CancellationToken cancellationToken = default);
}
