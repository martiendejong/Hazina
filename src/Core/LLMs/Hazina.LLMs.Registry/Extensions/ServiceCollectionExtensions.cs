using Hazina.LLMs.Registry.Factory;
using Hazina.LLMs.Registry.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hazina.LLMs.Registry.Extensions;

/// <summary>
/// Extension methods for registering the provider registry with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the provider registry and factory to the service collection.
    /// Configuration is loaded from IConfiguration.
    /// </summary>
    public static IServiceCollection AddProviderRegistry(this IServiceCollection services)
    {
        services.AddSingleton<ProviderRegistry>(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            return new ProviderRegistry(configuration);
        });

        services.AddSingleton<ProviderFactory>();

        return services;
    }

    /// <summary>
    /// Adds the provider registry with a custom configuration action.
    /// </summary>
    public static IServiceCollection AddProviderRegistry(
        this IServiceCollection services,
        Action<ProviderRegistryConfig> configure)
    {
        var config = new ProviderRegistryConfig();
        configure(config);

        services.AddSingleton(new ProviderRegistry(config));
        services.AddSingleton<ProviderFactory>();

        return services;
    }

    /// <summary>
    /// Adds the provider registry with factory customization.
    /// </summary>
    public static IServiceCollection AddProviderRegistry(
        this IServiceCollection services,
        Action<ProviderFactory> configureFactory)
    {
        services.AddSingleton<ProviderRegistry>(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            return new ProviderRegistry(configuration);
        });

        services.AddSingleton(sp =>
        {
            var registry = sp.GetRequiredService<ProviderRegistry>();
            var factory = new ProviderFactory(registry);
            configureFactory(factory);
            return factory;
        });

        return services;
    }

    /// <summary>
    /// Registers an ILLMClient for the default provider.
    /// </summary>
    public static IServiceCollection AddDefaultLLMClient(this IServiceCollection services)
    {
        services.AddScoped<ILLMClient>(sp =>
        {
            var factory = sp.GetRequiredService<ProviderFactory>();
            return factory.CreateDefaultClient();
        });

        return services;
    }

    /// <summary>
    /// Registers an ILLMClient for a specific provider.
    /// </summary>
    public static IServiceCollection AddLLMClient(this IServiceCollection services, string providerId)
    {
        services.AddScoped<ILLMClient>(sp =>
        {
            var factory = sp.GetRequiredService<ProviderFactory>();
            return factory.CreateClient(providerId);
        });

        return services;
    }

    /// <summary>
    /// Registers a keyed ILLMClient for a specific provider (useful for multi-provider scenarios).
    /// </summary>
    public static IServiceCollection AddKeyedLLMClient(
        this IServiceCollection services,
        string serviceKey,
        string providerId)
    {
        services.AddKeyedScoped<ILLMClient>(serviceKey, (sp, _) =>
        {
            var factory = sp.GetRequiredService<ProviderFactory>();
            return factory.CreateClient(providerId);
        });

        return services;
    }
}
