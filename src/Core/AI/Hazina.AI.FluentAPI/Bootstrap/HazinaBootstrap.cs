using Hazina.AgentFactory.Core;
using Hazina.AI.FluentAPI.Configuration;
using Hazina.AI.Providers.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Hazina.AI.FluentAPI.Bootstrap;

/// <summary>
/// One-call bootstrap extension for registering the Hazina framework with ASP.NET Core DI.
/// </summary>
/// <remarks>
/// <para>
/// <c>AddHazina()</c> is the recommended entry point for ASP.NET Core applications.
/// It registers:
/// <list type="bullet">
///   <item><see cref="IProviderOrchestrator"/> — multi-provider LLM orchestration</item>
///   <item><see cref="AgentManager"/> — agent lifecycle management</item>
///   <item><see cref="IHazinaService"/> — high-level service for use in controllers / minimal APIs</item>
/// </list>
/// </para>
/// <para>
/// For console apps or non-DI scenarios, use <see cref="Configuration.QuickSetup"/> directly.
/// </para>
/// </remarks>
/// <example>
/// Minimal ASP.NET Core setup (Program.cs):
/// <code>
/// builder.Services.AddHazina(options =>
/// {
///     options.OpenAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
///     options.AgentsJson = """[{ "name": "assistant", "prompt": "You are a helpful assistant.", "stores": [], "functions": [] }]""";
/// });
/// </code>
/// </example>
public static class HazinaBootstrap
{
    /// <summary>
    /// Register all core Hazina services with the DI container.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">
    /// Optional configuration action. When null, services are registered with default options.
    /// At minimum one of <see cref="HazinaOptions.OpenAiApiKey"/> or <see cref="HazinaOptions.AnthropicApiKey"/>
    /// must be set for the service to function.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHazina(
        this IServiceCollection services,
        Action<HazinaOptions>? configure = null)
    {
        var options = new HazinaOptions();
        configure?.Invoke(options);

        // -----------------------------------------------------------------------
        // 1. Build provider orchestrator based on configured keys
        // -----------------------------------------------------------------------
        var orchestrator = BuildOrchestrator(options);
        services.AddSingleton<IProviderOrchestrator>(orchestrator);

        // -----------------------------------------------------------------------
        // 2. Register AgentManager as a singleton
        //    AgentManager initialization (LoadStoresAndAgents) happens lazily
        //    on first use via HazinaService.
        // -----------------------------------------------------------------------
        services.AddSingleton(sp =>
        {
            var primaryKey = options.OpenAiApiKey ?? options.AnthropicApiKey ?? string.Empty;

            if (options.StoresJsonPath != null && options.AgentsJsonPath != null && options.FlowsJsonPath != null)
            {
                // File-based configuration
                return new AgentManager(
                    storesJsonPath: options.StoresJsonPath,
                    agentsJsonPath: options.AgentsJsonPath,
                    flowsJsonPath: options.FlowsJsonPath,
                    openAIApiKey: primaryKey,
                    logFilePath: options.AgentLogPath);
            }

            // Inline JSON configuration
            return new AgentManager(
                storesJson: options.StoresJson,
                agentsJson: options.AgentsJson,
                flowsJson: options.FlowsJson,
                openAIApiKey: primaryKey,
                logFilePath: options.AgentLogPath,
                isContent: true);
        });

        // -----------------------------------------------------------------------
        // 3. Register IHazinaService — the developer-facing high-level service
        // -----------------------------------------------------------------------
        services.AddSingleton<IHazinaService, HazinaService>();

        return services;
    }

    // -------------------------------------------------------------------------
    // Internal helpers
    // -------------------------------------------------------------------------

    private static IProviderOrchestrator BuildOrchestrator(HazinaOptions options)
    {
        var hasOpenAi = !string.IsNullOrEmpty(options.OpenAiApiKey);
        var hasAnthropic = !string.IsNullOrEmpty(options.AnthropicApiKey);

        if (!hasOpenAi && !hasAnthropic)
        {
            throw new InvalidOperationException(
                "Hazina requires at least one LLM provider API key. " +
                "Set OpenAiApiKey or AnthropicApiKey in HazinaOptions.");
        }

        return options.DefaultProvider switch
        {
            ProviderPreference.AnthropicFirst when hasAnthropic && hasOpenAi =>
                QuickSetup.SetupWithFailover(
                    openAIKey: options.OpenAiApiKey!,
                    anthropicKey: options.AnthropicApiKey!,
                    openAIModel: options.OpenAiModel,
                    anthropicModel: options.AnthropicModel),

            _ when hasOpenAi && hasAnthropic =>
                QuickSetup.SetupWithFailover(
                    openAIKey: options.OpenAiApiKey!,
                    anthropicKey: options.AnthropicApiKey!,
                    openAIModel: options.OpenAiModel,
                    anthropicModel: options.AnthropicModel),

            _ when hasOpenAi =>
                QuickSetup.SetupOpenAI(options.OpenAiApiKey!, options.OpenAiModel),

            _ =>
                QuickSetup.SetupOpenAI(
                    apiKey: options.AnthropicApiKey!, // fallback: Anthropic only - uses key as passthrough
                    model: options.AnthropicModel),
        };
    }
}
