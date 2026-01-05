using Hazina.Security.Core.Secrets;
using Hazina.Security.Core.Validation;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hazina.Security.Core;

/// <summary>
/// Dependency injection extensions for Hazina security services
/// </summary>
public static class SecurityExtensions
{
    /// <summary>
    /// Adds Hazina security services to the service collection
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configureSecrets">Optional secret manager configuration</param>
    /// <param name="configureValidator">Optional input validator configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddHazinaSecurity(
        this IServiceCollection services,
        Action<SecretManagerOptions>? configureSecrets = null,
        Action<InputValidatorOptions>? configureValidator = null)
    {
        // Register Data Protection with persistent key storage
        services.AddDataProtection()
            .SetApplicationName("Hazina")
            .PersistKeysToFileSystem(new DirectoryInfo(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Hazina",
                    "DataProtection-Keys")));

        // Configure options
        var secretOptions = new SecretManagerOptions();
        configureSecrets?.Invoke(secretOptions);

        var validatorOptions = new InputValidatorOptions();
        configureValidator?.Invoke(validatorOptions);

        // Register services
        services.AddSingleton(secretOptions);
        services.AddSingleton(validatorOptions);
        services.AddSingleton<ISecretManager, SecretManager>();
        services.AddSingleton<IInputValidator, InputValidator>();

        return services;
    }

    /// <summary>
    /// Adds Hazina secret management only
    /// </summary>
    public static IServiceCollection AddHazinaSecrets(
        this IServiceCollection services,
        Action<SecretManagerOptions>? configure = null)
    {
        services.AddDataProtection()
            .SetApplicationName("Hazina")
            .PersistKeysToFileSystem(new DirectoryInfo(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Hazina",
                    "DataProtection-Keys")));

        var options = new SecretManagerOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<ISecretManager, SecretManager>();

        return services;
    }

    /// <summary>
    /// Adds Hazina input validation only
    /// </summary>
    public static IServiceCollection AddHazinaInputValidation(
        this IServiceCollection services,
        Action<InputValidatorOptions>? configure = null)
    {
        var options = new InputValidatorOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IInputValidator, InputValidator>();

        return services;
    }
}
