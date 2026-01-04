using Hazina.Observability.LLMLogs.Configuration;
using Hazina.Observability.LLMLogs.Decorators;
using Hazina.Observability.LLMLogs.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hazina.Observability.LLMLogs.Extensions
{
    /// <summary>
    /// Extension methods for configuring LLM logging services.
    /// </summary>
    public static class LLMLoggingServiceExtensions
    {
        /// <summary>
        /// Adds LLM logging services to the service collection.
        /// Call this before registering ILLMClient implementations.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">Configuration containing LLMLogging section.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddLLMLogging(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Bind configuration
            services.Configure<LLMLoggingOptions>(configuration.GetSection(LLMLoggingOptions.SectionName));

            // Register repository
            services.TryAddSingleton<ILLMLogRepository, SqliteLLMLogRepository>();

            // Initialize database on startup
            services.AddHostedService<LLMLoggingInitializationService>();

            return services;
        }

        /// <summary>
        /// Decorates an ILLMClient registration with logging.
        /// Use this after registering your ILLMClient implementation.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="providerName">Name of the LLM provider (e.g., "OpenAI", "Anthropic", "Google").</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection DecorateWithLogging(
            this IServiceCollection services,
            string providerName)
        {
            services.Decorate<ILLMClient>((inner, provider) =>
            {
                var repository = provider.GetRequiredService<ILLMLogRepository>();
                var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<LLMLoggingOptions>>();
                return new LLMLoggingClientDecorator(inner, repository, options, providerName);
            });

            return services;
        }
    }
}
