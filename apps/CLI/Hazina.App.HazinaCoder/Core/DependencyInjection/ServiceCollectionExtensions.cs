using Microsoft.Extensions.DependencyInjection;
using Hazina.App.HazinaCoder.Core.Configuration;
using Hazina.App.HazinaCoder.Core.Events;
using Hazina.App.HazinaCoder.Core.Streaming;
using Hazina.App.HazinaCoder.Core.Learning;
using Hazina.App.HazinaCoder.Core.Memory;
using Hazina.App.HazinaCoder.Core.Security;
using Hazina.App.HazinaCoder.Core.Providers;
using Hazina.App.HazinaCoder.Core.Vision;
using Hazina.App.HazinaCoder.Core.Skills;
using Hazina.App.HazinaCoder.Core.Tools;
using Hazina.App.HazinaCoder.Core.Identity;
using Hazina.App.HazinaCoder.Core.Infrastructure;

namespace Hazina.App.HazinaCoder.Core.DependencyInjection;

/// <summary>
/// Extension methods for configuring HazinaCoder services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add all HazinaCoder core services to the service collection
    /// </summary>
    public static IServiceCollection AddHazinaCoder(
        this IServiceCollection services,
        HazinaCoderConfiguration config)
    {
        // Configuration
        services.AddSingleton(config);
        services.AddSingleton(config.Features);

        // Core Infrastructure
        services.AddSingleton<AgentEventBus>();
        services.AddSingleton<CircuitBreakerService>();
        services.AddSingleton<GracefulDegradationService>();

        // Providers
        services.AddSingleton<ProviderRouter>();
        services.AddTransient<ILLMProvider, OpenAIProvider>();
        services.AddTransient<ILLMProvider, AnthropicProvider>();

        // Streaming
        services.AddSingleton<StreamingOrchestrator>();
        services.AddTransient<ProgressReporter>();
        services.AddTransient<InterruptHandler>();

        // Learning & Memory
        services.AddSingleton<LearningSystem>();
        services.AddSingleton<MemorySystems>();
        services.AddSingleton<ExperienceCapture>();
        services.AddSingleton<ExperienceRetrieval>();
        services.AddSingleton<ExperienceStorage>();
        services.AddSingleton<RelationshipMemory>();
        services.AddSingleton<SuccessPatternRecognizer>();
        services.AddSingleton<FailureAnalyzer>();
        services.AddSingleton<MistakePrevention>();

        // Identity & Consciousness
        services.AddSingleton<AgentIdentity>();
        services.AddSingleton<AssumptionTracker>();

        // Security
        services.AddSingleton<SecretScanner>();

        // Vision
        if (config.Features.VisionAnalysis)
        {
            services.AddSingleton<VisionAnalyzer>();
            services.AddSingleton<VisionCache>();
        }

        // Skills & Tools
        services.AddSingleton<SkillDiscovery>();
        services.AddSingleton<SkillComposer>();
        services.AddSingleton<DynamicToolRegistry>();
        services.AddSingleton<ToolPerformanceProfiler>();
        services.AddSingleton<ToolEffectivenessTracker>();

        // State Management
        services.AddSingleton<StateManager>();

        return services;
    }

    /// <summary>
    /// Add provider-specific services based on configuration
    /// </summary>
    public static IServiceCollection AddProviders(
        this IServiceCollection services,
        HazinaCoderConfiguration config)
    {
        if (!string.IsNullOrEmpty(config.Provider.OpenAI?.ApiKey))
        {
            services.AddTransient<OpenAIProvider>();
        }

        if (!string.IsNullOrEmpty(config.Provider.Anthropic?.ApiKey))
        {
            services.AddTransient<AnthropicProvider>();
        }

        if (!string.IsNullOrEmpty(config.Provider.Ollama?.Endpoint))
        {
            // Ollama provider registration
        }

        return services;
    }

    /// <summary>
    /// Add feature-flagged services
    /// </summary>
    public static IServiceCollection AddFeaturesIf(
        this IServiceCollection services,
        FeatureFlags flags)
    {
        if (flags.MultiAgentCoordination)
        {
            services.AddSingleton<CoordinationDatabase>();
            services.AddSingleton<HeartbeatManager>();
            services.AddSingleton<ConflictDetector>();
        }

        if (flags.SmartContextCaching)
        {
            services.AddSingleton<SmartContextCache>();
        }

        if (flags.IncrementalEmbedding)
        {
            services.AddSingleton<IncrementalEmbeddingCache>();
        }

        if (flags.ParallelToolExecution)
        {
            services.AddSingleton<ParallelToolExecutor>();
        }

        if (flags.CrashRecovery)
        {
            services.AddSingleton<CrashRecoverySystem>();
        }

        if (flags.NaturalLanguageGit)
        {
            services.AddSingleton<NaturalGitCommands>();
        }

        return services;
    }

    /// <summary>
    /// Configure logging services
    /// </summary>
    public static IServiceCollection AddHazinaLogging(
        this IServiceCollection services,
        LoggingConfiguration loggingConfig)
    {
        services.AddSingleton<StructuredLogger>();
        services.AddSingleton(loggingConfig);

        return services;
    }
}

/// <summary>
/// Builder pattern for HazinaCoder configuration
/// </summary>
public class HazinaCoderBuilder
{
    private readonly IServiceCollection _services;
    private readonly HazinaCoderConfiguration _config;

    public HazinaCoderBuilder(IServiceCollection services, HazinaCoderConfiguration config)
    {
        _services = services;
        _config = config;
    }

    /// <summary>
    /// Add core services
    /// </summary>
    public HazinaCoderBuilder AddCoreServices()
    {
        _services.AddHazinaCoder(_config);
        return this;
    }

    /// <summary>
    /// Add providers
    /// </summary>
    public HazinaCoderBuilder AddProviders()
    {
        _services.AddProviders(_config);
        return this;
    }

    /// <summary>
    /// Add feature-flagged services
    /// </summary>
    public HazinaCoderBuilder AddFeatures()
    {
        _services.AddFeaturesIf(_config.Features);
        return this;
    }

    /// <summary>
    /// Add logging
    /// </summary>
    public HazinaCoderBuilder AddLogging()
    {
        _services.AddHazinaLogging(_config.Logging);
        return this;
    }

    /// <summary>
    /// Build the service provider
    /// </summary>
    public IServiceProvider Build()
    {
        return _services.BuildServiceProvider();
    }
}

/// <summary>
/// Startup extension for easy configuration
/// </summary>
public static class HazinaCoderStartup
{
    /// <summary>
    /// Configure HazinaCoder with default settings
    /// </summary>
    public static IServiceProvider ConfigureHazinaCoder(HazinaCoderConfiguration config)
    {
        var services = new ServiceCollection();

        return new HazinaCoderBuilder(services, config)
            .AddCoreServices()
            .AddProviders()
            .AddFeatures()
            .AddLogging()
            .Build();
    }

    /// <summary>
    /// Configure with minimal services (fast startup)
    /// </summary>
    public static IServiceProvider ConfigureMinimal(HazinaCoderConfiguration config)
    {
        var services = new ServiceCollection();

        services.AddSingleton(config);
        services.AddSingleton(config.Features);
        services.AddSingleton<AgentEventBus>();
        services.AddSingleton<ProviderRouter>();
        services.AddSingleton<StreamingOrchestrator>();

        return services.BuildServiceProvider();
    }
}
