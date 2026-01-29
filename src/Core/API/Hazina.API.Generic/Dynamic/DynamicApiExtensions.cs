using Hazina.API.Generic.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Hazina.API.Generic.Dynamic;

/// <summary>
/// Extension methods for configuring the dynamic API.
/// </summary>
public static class DynamicApiExtensions
{
    /// <summary>
    /// Adds dynamic API services.
    /// Entities are defined in YAML - no C# code needed!
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="yamlPath">Path to the entities.yaml file</param>
    /// <param name="databasePath">Path to SQLite database (created if not exists)</param>
    /// <example>
    /// <code>
    /// // In Program.cs - THIS IS ALL YOU NEED:
    /// builder.Services.AddHazinaDynamicApi("entities.yaml", "data.db");
    /// </code>
    /// </example>
    public static IServiceCollection AddHazinaDynamicApi(
        this IServiceCollection services,
        string yamlPath,
        string? databasePath = null)
    {
        // Load entity definitions from YAML
        var loader = new EntityConfigurationLoader();
        var definitions = loader.LoadFromFileAsync(yamlPath).GetAwaiter().GetResult();

        // Validate definitions
        var errors = loader.ValidateDefinitions(definitions);
        if (errors.Any())
        {
            throw new InvalidOperationException(
                $"Invalid entity definitions in {yamlPath}:\n" +
                string.Join("\n", errors.Select(e => $"  - {e}")));
        }

        // Default database path
        databasePath ??= Path.Combine(
            Path.GetDirectoryName(yamlPath) ?? ".",
            "hazina-dynamic.db");

        // Register store as singleton (thread-safe)
        var store = new DynamicEntityStore(databasePath, definitions);
        store.InitializeAsync().GetAwaiter().GetResult();
        services.AddSingleton(store);

        return services;
    }

    /// <summary>
    /// Adds dynamic API services from a directory of YAML files.
    /// Each file can contain multiple entity definitions.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="directoryPath">Directory containing entity YAML files</param>
    /// <param name="pattern">File pattern (default: *.yaml)</param>
    /// <param name="databasePath">Path to SQLite database</param>
    public static IServiceCollection AddHazinaDynamicApiFromDirectory(
        this IServiceCollection services,
        string directoryPath,
        string pattern = "*.yaml",
        string? databasePath = null)
    {
        var loader = new EntityConfigurationLoader();
        var definitions = loader.LoadFromDirectoryAsync(directoryPath, pattern).GetAwaiter().GetResult();

        var errors = loader.ValidateDefinitions(definitions);
        if (errors.Any())
        {
            throw new InvalidOperationException(
                $"Invalid entity definitions:\n" +
                string.Join("\n", errors.Select(e => $"  - {e}")));
        }

        databasePath ??= Path.Combine(directoryPath, "hazina-dynamic.db");

        var store = new DynamicEntityStore(databasePath, definitions);
        store.InitializeAsync().GetAwaiter().GetResult();
        services.AddSingleton(store);

        return services;
    }

    /// <summary>
    /// Adds dynamic API with inline entity definitions (for testing/demos).
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="yamlContent">YAML content as string</param>
    /// <param name="databasePath">Path to SQLite database</param>
    public static IServiceCollection AddHazinaDynamicApiFromYaml(
        this IServiceCollection services,
        string yamlContent,
        string databasePath = "hazina-dynamic.db")
    {
        var loader = new EntityConfigurationLoader();
        var definitions = loader.LoadFromYaml(yamlContent);

        var errors = loader.ValidateDefinitions(definitions);
        if (errors.Any())
        {
            throw new InvalidOperationException(
                $"Invalid entity definitions:\n" +
                string.Join("\n", errors.Select(e => $"  - {e}")));
        }

        var store = new DynamicEntityStore(databasePath, definitions);
        store.InitializeAsync().GetAwaiter().GetResult();
        services.AddSingleton(store);

        return services;
    }

    /// <summary>
    /// Maps the dynamic API endpoints.
    /// </summary>
    /// <param name="app">Application builder</param>
    /// <example>
    /// <code>
    /// // In Program.cs:
    /// app.MapHazinaDynamicApi();
    /// </code>
    /// </example>
    public static IApplicationBuilder UseHazinaDynamicApi(this IApplicationBuilder app)
    {
        // The DynamicApiController handles routing via attribute routing
        // This method is here for clarity/discoverability
        return app;
    }
}
