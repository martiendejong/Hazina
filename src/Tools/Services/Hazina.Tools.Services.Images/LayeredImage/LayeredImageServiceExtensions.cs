using Hazina.Tools.Services.Images.LayeredImage.Abstractions;
using Hazina.Tools.Services.Images.LayeredImage.Exporters;
using Hazina.Tools.Services.Images.LayeredImage.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Hazina.Tools.Services.Images.LayeredImage;

/// <summary>
/// Extension methods for registering layered image services with dependency injection.
/// </summary>
public static class LayeredImageServiceExtensions
{
    /// <summary>
    /// Adds layered image generation services to the service collection.
    /// Includes all exporters (PDN, ORA, PSD) and the main service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLayeredImageServices(this IServiceCollection services)
    {
        // Register compositor
        services.AddSingleton<ILayerCompositor, LayerCompositor>();

        // Register exporters
        services.AddSingleton<OraExporter>();
        services.AddSingleton<ILayeredImageExporter>(sp => sp.GetRequiredService<OraExporter>());

        services.AddSingleton<PdnExporter>();
        services.AddSingleton<ILayeredImageExporter>(sp => sp.GetRequiredService<PdnExporter>());

        services.AddSingleton<PsdExporter>();
        services.AddSingleton<ILayeredImageExporter>(sp => sp.GetRequiredService<PsdExporter>());

        // Register main service
        services.AddScoped<ILayeredImageService, LayeredImageService>();

        return services;
    }

    /// <summary>
    /// Adds only the ORA exporter (simple, widely supported format).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLayeredImageServicesOraOnly(this IServiceCollection services)
    {
        services.AddSingleton<ILayerCompositor, LayerCompositor>();
        services.AddSingleton<OraExporter>();
        services.AddSingleton<ILayeredImageExporter>(sp => sp.GetRequiredService<OraExporter>());
        services.AddScoped<ILayeredImageService, LayeredImageService>();

        return services;
    }
}
