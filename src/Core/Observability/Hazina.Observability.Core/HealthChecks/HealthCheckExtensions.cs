using Hazina.AI.Providers.Core;
using Hazina.Neurochain.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hazina.Observability.Core.HealthChecks;

/// <summary>
/// Extension methods for registering Hazina health checks
/// </summary>
public static class HealthCheckExtensions
{
    /// <summary>
    /// Add Hazina provider health checks
    /// </summary>
    public static IHealthChecksBuilder AddHazinaProviderHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "hazina_providers",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null)
    {
        return builder.Add(new HealthCheckRegistration(
            name,
            sp => new HazinaProviderHealthCheck(
                sp.GetRequiredService<IProviderOrchestrator>()
            ),
            failureStatus,
            tags,
            timeout
        ));
    }

    /// <summary>
    /// Add Hazina NeuroChain health checks
    /// </summary>
    public static IHealthChecksBuilder AddHazinaNeuroChainHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "hazina_neurochain",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null)
    {
        return builder.Add(new HealthCheckRegistration(
            name,
            sp => new HazinaNeuroChainHealthCheck(
                sp.GetRequiredService<NeuroChainOrchestrator>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<HazinaNeuroChainHealthCheck>>(),
                timeout
            ),
            failureStatus,
            tags,
            timeout
        ));
    }

    /// <summary>
    /// Add all Hazina health checks
    /// </summary>
    public static IHealthChecksBuilder AddHazinaHealthChecks(
        this IHealthChecksBuilder builder,
        bool includeProviders = true,
        bool includeNeuroChain = true,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null)
    {
        var hazinaTag = new[] { "hazina" };
        var allTags = tags != null ? hazinaTag.Concat(tags) : hazinaTag;

        if (includeProviders)
        {
            builder.AddHazinaProviderHealthCheck(
                tags: allTags.Concat(new[] { "providers" }),
                timeout: timeout
            );
        }

        if (includeNeuroChain)
        {
            builder.AddHazinaNeuroChainHealthCheck(
                tags: allTags.Concat(new[] { "neurochain" }),
                timeout: timeout
            );
        }

        return builder;
    }
}
