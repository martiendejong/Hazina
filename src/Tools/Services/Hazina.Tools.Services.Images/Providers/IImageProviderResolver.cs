using Hazina.Tools.Services.Images.Abstractions;
using Hazina.Tools.Services.Images.Operations;

namespace Hazina.Tools.Services.Images.Providers;

/// <summary>
/// Resolves the appropriate image provider for a given operation.
/// </summary>
public interface IImageProviderResolver
{
    /// <summary>
    /// Resolves the most appropriate provider for the given operation.
    /// </summary>
    /// <param name="operation">The operation to resolve a provider for.</param>
    /// <param name="options">Provider options that may influence selection.</param>
    /// <returns>The resolved provider.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no provider can handle the operation.</exception>
    IImageProvider Resolve(ImageEditOperation operation, ImageProviderOptions options);

    /// <summary>
    /// Gets all registered providers.
    /// </summary>
    IReadOnlyList<IImageProvider> GetAllProviders();
}
