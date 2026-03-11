using Microsoft.Extensions.DependencyInjection;
using Hazina.App.HazinaCoder.Core.Configuration;
using Hazina.App.HazinaCoder.Core.Events;
using Hazina.App.HazinaCoder.Core.Streaming;
using Hazina.App.HazinaCoder.Core.Learning;
using Hazina.App.HazinaCoder.Core.Memory;
using Hazina.App.HazinaCoder.Core.Security;
using Hazina.App.HazinaCoder.Core.Providers;
using Hazina.App.HazinaCoder.Core.Providers.Implementations;
using Hazina.App.HazinaCoder.Core.Vision;
using Hazina.App.HazinaCoder.Core.Skills;
using Hazina.App.HazinaCoder.Core.Tools;
using Hazina.App.HazinaCoder.Core.Identity;
using Hazina.App.HazinaCoder.Core.Infrastructure;
using Hazina.App.HazinaCoder.Core.Monitoring;
using Hazina.App.HazinaCoder.Core.Performance;
using Hazina.App.HazinaCoder.Core.Commands;
using Hazina.App.HazinaCoder.Core.AI;
using Hazina.App.HazinaCoder.Core.Caching;
using Hazina.App.HazinaCoder.Core.State;
using Hazina.LLMs;

namespace Hazina.App.HazinaCoder.Core.DependencyInjection;

/// <summary>
/// Extension methods for configuring HazinaCoder services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add HazinaCoder core services (pragmatic subset - working components only)
    /// Iteration 53: Simplified integration of tested components
    /// </summary>
    public static IServiceCollection AddHazinaCoder(
        this IServiceCollection services,
        HazinaCoderConfiguration config)
    {
        // Configuration
        services.AddSingleton(config);
        services.AddSingleton(config.Features);

        // Core Infrastructure
        services.AddSingleton<EventBus>();
        services.AddSingleton<CircuitBreakerService>();
        services.AddSingleton<GracefulDegradationService>();

        // Provider routing (existing, working)
        services.AddSingleton<ProviderRouter>();

        // Streaming (existing, working)
        services.AddSingleton<StreamingOrchestrator>();
        services.AddTransient<ProgressReporter>();
        services.AddTransient<InterruptHandler>();

        // Retry & Resilience (Iterations 21-25) - working
        services.AddSingleton<RetryPolicy>();
        services.AddSingleton<RateLimiter>();

        // Monitoring (Iterations 11-15) - working standalone
        services.AddSingleton<HealthCheckService>();
        services.AddSingleton<MetricsCollector>();
        services.AddSingleton<CostTracker>();

        // Security (Iterations 32-36) - fully working
        services.AddSingleton<InputSanitizer>();
        services.AddSingleton<SecretScanner>();

        // Performance (Iterations 46-50) - working standalone
        services.AddSingleton<OptimizationEngine>();

        // Commands (Iteration 41) - working standalone
        services.AddSingleton<CommandPaletteWithFuzzy>();
        services.AddSingleton<AutoCompleter>();

        // Session & State (Iteration 40) - working standalone
        services.AddSingleton<SessionBranching>();
        services.AddSingleton<StateManager>();

        // Learning & Memory (existing system)
        services.AddSingleton<Hazina.App.HazinaCoder.Core.Learning.LearningSystem>();
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
        // TODO: AssumptionTracker not yet implemented
        // services.AddSingleton<AssumptionTracker>();

        // Skills & Tools (existing system)
        services.AddSingleton<SkillDiscovery>();
        services.AddSingleton<SkillComposer>();
        services.AddSingleton<DynamicToolRegistry>();
        services.AddSingleton<ToolPerformanceProfiler>();
        services.AddSingleton<ToolEffectivenessTracker>();

        return services;
    }

    /// <summary>
    /// Add provider-specific services based on configuration
    /// </summary>
    public static IServiceCollection AddProviders(
        this IServiceCollection services,
        HazinaCoderConfiguration config)
    {
        // TODO: Provider registration pattern needs update for new config structure
        // Old pattern: config.Provider.OpenAI (doesn't exist)
        // New pattern: config.Provider.Providers["openai"]
        // Provider types (OpenAIProvider, AnthropicProvider) may need refactoring

        // Register ProviderFactory for creating LLM clients
        services.AddSingleton<ProviderFactory>();

        return services;
    }

    /// <summary>
    /// Add feature-flagged services
    /// </summary>
    public static IServiceCollection AddFeaturesIf(
        this IServiceCollection services,
        FeatureFlags flags)
    {
        // TODO: These feature flags and services are not yet implemented in FeatureFlags class
        // Commenting out until implemented

        // if (flags.MultiAgentCoordination)
        // {
        //     services.AddSingleton<CoordinationDatabase>();
        //     services.AddSingleton<HeartbeatManager>();
        //     services.AddSingleton<ConflictDetector>();
        // }

        // if (flags.SmartContextCaching)
        // {
        //     services.AddSingleton<SmartContextCache>();
        // }

        // if (flags.IncrementalEmbedding)
        // {
        //     services.AddSingleton<IncrementalEmbeddingCache>();
        // }

        // if (flags.ParallelToolExecution)
        // {
        //     services.AddSingleton<ParallelToolExecutor>();
        // }

        if (flags.EnableCaching)
        {
            services.AddSingleton<CrashRecoverySystem>();
        }

        // if (flags.NaturalLanguageGit)
        // {
        //     services.AddSingleton<NaturalGitCommands>();
        // }

        return services;
    }

    /// <summary>
    /// Configure logging services
    /// </summary>
    public static IServiceCollection AddHazinaLogging(
        this IServiceCollection services,
        LoggingConfiguration loggingConfig)
    {
        // TODO: StructuredLogger not yet implemented
        // services.AddSingleton<StructuredLogger>();
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
        services.AddSingleton<EventBus>(); // Use EventBus instead of AgentEventBus
        services.AddSingleton<ProviderRouter>();
        services.AddSingleton<StreamingOrchestrator>();

        return services.BuildServiceProvider();
    }
}
