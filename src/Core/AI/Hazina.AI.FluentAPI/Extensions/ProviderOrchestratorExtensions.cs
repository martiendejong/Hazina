using Hazina.AI.Providers.Core;
using Hazina.AI.FluentAPI.Core;

namespace Hazina.AI.FluentAPI.Extensions;

/// <summary>
/// Extension methods for IProviderOrchestrator
/// </summary>
public static class ProviderOrchestratorExtensions
{
    /// <summary>
    /// Create a fluent builder from orchestrator
    /// </summary>
    public static HazinaBuilder CreateBuilder(this IProviderOrchestrator orchestrator)
    {
        return new HazinaBuilder().WithOrchestrator(orchestrator);
    }

    /// <summary>
    /// Quick ask using orchestrator
    /// </summary>
    public static async Task<string> AskAsync(
        this IProviderOrchestrator orchestrator,
        string question,
        CancellationToken cancellationToken = default)
    {
        return await orchestrator
            .CreateBuilder()
            .Ask(question)
            .ExecuteAsync(cancellationToken);
    }

    /// <summary>
    /// Ask with fault detection
    /// </summary>
    public static async Task<string> AskSafeAsync(
        this IProviderOrchestrator orchestrator,
        string question,
        double minConfidence = 0.7,
        CancellationToken cancellationToken = default)
    {
        return await orchestrator
            .CreateBuilder()
            .WithFaultDetection(minConfidence)
            .Ask(question)
            .ExecuteAsync(cancellationToken);
    }

    /// <summary>
    /// Ask expecting JSON
    /// </summary>
    public static async Task<string> AskForJsonAsync(
        this IProviderOrchestrator orchestrator,
        string question,
        CancellationToken cancellationToken = default)
    {
        return await orchestrator
            .CreateBuilder()
            .WithFaultDetection()
            .Ask(question)
            .ExpectJson()
            .ExecuteAsync(cancellationToken);
    }
}
