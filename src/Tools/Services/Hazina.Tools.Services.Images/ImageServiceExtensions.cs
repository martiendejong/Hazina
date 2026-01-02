using Hazina.Tools.Services.Images.Abstractions;
using Hazina.Tools.Services.Images.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hazina.Tools.Services.Images;

/// <summary>
/// Extension methods for registering image services with dependency injection.
/// </summary>
public static class ImageServiceExtensions
{
    /// <summary>
    /// Adds the Hazina Images services with default configuration.
    /// Includes the LocalImageProvider by default.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHazinaImages(this IServiceCollection services)
    {
        return services.AddHazinaImages(_ => { });
    }

    /// <summary>
    /// Adds the Hazina Images services with configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHazinaImages(
        this IServiceCollection services,
        Action<ImageServiceOptions> configure)
    {
        var options = new ImageServiceOptions();
        configure(options);

        // Register the resolver
        services.AddSingleton<IImageProviderResolver, ImageProviderResolver>();

        // Register the image tool
        services.AddSingleton<IImageTool, ImageTool>();

        // Register local provider by default
        if (options.EnableLocalProvider)
        {
            services.AddLocalImageProvider();
        }

        return services;
    }

    /// <summary>
    /// Adds the Hazina Images services with configuration from IConfiguration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration section.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHazinaImages(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = new ImageServiceOptions();
        configuration.GetSection("Hazina:Images").Bind(options);

        return services.AddHazinaImages(o =>
        {
            o.DefaultProvider = options.DefaultProvider;
            o.EnableLocalProvider = options.EnableLocalProvider;
            o.EnableAiProvider = options.EnableAiProvider;
        });
    }

    /// <summary>
    /// Adds the local image provider using SixLabors.ImageSharp.
    /// Supports: AddText, Crop, Resize, Rotate, Brightness, Contrast, Filters.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLocalImageProvider(this IServiceCollection services)
    {
        services.AddSingleton<IImageProvider, LocalImageProvider>();
        return services;
    }

    /// <summary>
    /// Adds the AI image provider with no handler configured.
    /// Use this when you want to configure the handler later.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiImageProvider(this IServiceCollection services)
    {
        services.AddSingleton<IImageProvider, AiImageProvider>();
        return services;
    }

    /// <summary>
    /// Adds the AI image provider with a custom configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Builder configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiImageProvider(
        this IServiceCollection services,
        Action<AiImageProviderBuilder> configure)
    {
        var builder = new AiImageProviderBuilder();
        configure(builder);

        services.AddSingleton<IImageProvider>(builder.Build());
        return services;
    }

    /// <summary>
    /// Adds a custom image provider.
    /// </summary>
    /// <typeparam name="TProvider">The provider type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddImageProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, IImageProvider
    {
        services.AddSingleton<IImageProvider, TProvider>();
        return services;
    }

    /// <summary>
    /// Adds a custom image provider instance.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="provider">The provider instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddImageProvider(
        this IServiceCollection services,
        IImageProvider provider)
    {
        services.AddSingleton(provider);
        return services;
    }
}

/// <summary>
/// Configuration options for image services.
/// </summary>
public class ImageServiceOptions
{
    /// <summary>
    /// The default provider to use when no preference is specified.
    /// </summary>
    public string DefaultProvider { get; set; } = "Local";

    /// <summary>
    /// Whether to enable the local image provider (default: true).
    /// </summary>
    public bool EnableLocalProvider { get; set; } = true;

    /// <summary>
    /// Whether to enable the AI image provider (default: false).
    /// Requires additional configuration when enabled.
    /// </summary>
    public bool EnableAiProvider { get; set; } = false;
}
