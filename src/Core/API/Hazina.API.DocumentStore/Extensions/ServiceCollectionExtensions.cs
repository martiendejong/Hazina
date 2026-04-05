using Hazina.API.DocumentStore.Configuration;
using Hazina.API.DocumentStore.Controllers;
using Hazina.API.DocumentStore.Data;
using Hazina.API.DocumentStore.Integration;
using Hazina.API.DocumentStore.Services;
using Hazina.API.DocumentStore.Services.FormatHandlers;
using Hazina.AI.Providers.Core;
using Hazina.LLMs;
using Hazina.LLMs.OpenAI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hazina.API.DocumentStore.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Hazina Document Store API services to the DI container.
    /// Call AddControllers() separately — this adds the application part for controller discovery.
    /// </summary>
    public static IServiceCollection AddHazinaDocumentStoreApi(
        this IServiceCollection services,
        Action<DocumentStoreApiOptions>? configure = null)
    {
        var options = new DocumentStoreApiOptions();
        configure?.Invoke(options);
        services.AddSingleton(Options.Create(options));

        // Database
        services.AddDbContext<DocumentStoreDbContext>(dbOptions =>
            dbOptions.UseSqlite(options.MetadataConnectionString));

        // LLM Client
        if (options.LLMClientFactory != null)
        {
            services.AddSingleton<ILLMClient>(options.LLMClientFactory);
        }
        else
        {
            services.AddSingleton<OpenAIConfig>(sp =>
            {
                var config = new OpenAIConfig
                {
                    ApiKey = Environment.GetEnvironmentVariable("HAZINA_OPENAI_APIKEY") ?? string.Empty,
                    Model = options.DefaultLLMModel,
                    EmbeddingModel = options.DefaultEmbeddingModel
                };
                return config;
            });
            services.AddSingleton<ILLMClient>(sp =>
            {
                var config = sp.GetRequiredService<OpenAIConfig>();
                return new OpenAIClientWrapper(config);
            });
        }

        // Provider Orchestrator
        if (options.ProviderOrchestratorFactory != null)
        {
            services.AddSingleton<IProviderOrchestrator>(options.ProviderOrchestratorFactory);
        }
        else
        {
            services.AddSingleton<IProviderOrchestrator>(sp =>
            {
                var logger = sp.GetService<Microsoft.Extensions.Logging.ILogger<ProviderOrchestrator>>();
                var orchestrator = new ProviderOrchestrator(logger);

                var openAIClient = sp.GetRequiredService<ILLMClient>();
                var metadata = new ProviderMetadata
                {
                    Name = "openai",
                    DisplayName = "OpenAI",
                    Type = ProviderType.OpenAI,
                    Priority = 1,
                    IsEnabled = true,
                    Capabilities = new ProviderCapabilities
                    {
                        SupportsEmbeddings = true,
                        SupportsChat = true,
                        SupportsImages = true,
                        SupportsTTS = true,
                        SupportsStreaming = true
                    }
                };
                orchestrator.RegisterProvider("openai", openAIClient, metadata);
                return orchestrator;
            });
        }

        // Store Factory
        if (options.StoreFactoryOverride != null)
        {
            services.AddSingleton<IHazinaStoreFactory>(options.StoreFactoryOverride);
        }
        else
        {
            services.AddSingleton<IHazinaStoreFactory, HazinaStoreFactory>();
        }

        // Format Handlers
        if (options.RegisterBuiltInFormatHandlers)
        {
            services.AddTransient<IFormatHandler, TextFormatHandler>();
            services.AddTransient<IFormatHandler, DocxFormatHandler>();
            services.AddTransient<IFormatHandler, PdfFormatHandler>();
            services.AddTransient<IFormatHandler, ImageFormatHandler>();
        }

        foreach (var handlerType in options.AdditionalFormatHandlers)
        {
            services.AddTransient(typeof(IFormatHandler), handlerType);
        }

        // Application Services
        services.AddScoped<RAGStoreRepository>();
        services.AddScoped<ChunkingService>();
        services.AddScoped<DocumentProcessor>();
        services.AddScoped<RAGStoreManager>();
        services.AddScoped<SearchService>();

        // Controller discovery — add this library's assembly as an application part
        var libraryAssembly = typeof(ServiceCollectionExtensions).Assembly;
        services.AddMvcCore()
            .AddApplicationPart(libraryAssembly);

        // Conditionally exclude AuthController
        if (!options.IncludeAuthController)
        {
            services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(_ => { });
            services.ConfigureOptions<ExcludeAuthControllerSetup>();
        }

        return services;
    }
}

/// <summary>
/// Post-configure to remove AuthController when IncludeAuthController = false
/// </summary>
internal class ExcludeAuthControllerSetup : IConfigureOptions<Microsoft.AspNetCore.Mvc.MvcOptions>
{
    private readonly IOptions<DocumentStoreApiOptions> _options;

    public ExcludeAuthControllerSetup(IOptions<DocumentStoreApiOptions> options)
    {
        _options = options;
    }

    public void Configure(Microsoft.AspNetCore.Mvc.MvcOptions mvcOptions)
    {
        // The actual exclusion happens via ApplicationPartManager in the extension method
    }
}
