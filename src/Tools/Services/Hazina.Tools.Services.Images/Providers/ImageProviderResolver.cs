using Hazina.Tools.Services.Images.Abstractions;
using Hazina.Tools.Services.Images.Operations;

namespace Hazina.Tools.Services.Images.Providers;

/// <summary>
/// Default implementation of IImageProviderResolver.
/// Resolves providers based on operation type and options.
/// </summary>
public class ImageProviderResolver : IImageProviderResolver
{
    private readonly IReadOnlyList<IImageProvider> _providers;

    public ImageProviderResolver(IEnumerable<IImageProvider> providers)
    {
        _providers = providers.ToList();
    }

    /// <inheritdoc />
    public IImageProvider Resolve(ImageEditOperation operation, ImageProviderOptions options)
    {
        // If a preferred provider is specified, try to use it
        if (!string.IsNullOrEmpty(options.PreferredProvider))
        {
            var preferred = _providers.FirstOrDefault(p =>
                p.Name.Equals(options.PreferredProvider, StringComparison.OrdinalIgnoreCase) &&
                p.CanHandle(operation));

            if (preferred != null)
            {
                return preferred;
            }
        }

        // Find the first provider that can handle this operation
        var provider = _providers.FirstOrDefault(p => p.CanHandle(operation));

        if (provider == null)
        {
            throw new InvalidOperationException(
                $"No provider found that can handle operation of type '{operation.GetType().Name}'. " +
                $"Available providers: {string.Join(", ", _providers.Select(p => p.Name))}");
        }

        return provider;
    }

    /// <inheritdoc />
    public IReadOnlyList<IImageProvider> GetAllProviders() => _providers;
}
