using Hazina.AI.Providers.Core;

namespace Hazina.AI.FluentAPI.Core;

/// <summary>
/// Main entry point for Hazina Fluent API
/// Example: var result = await Hazina.AI().WithProvider("openai").Ask("Hello").ExecuteAsync();
/// </summary>
public static class Hazina
{
    private static IProviderOrchestrator? _defaultOrchestrator;

    /// <summary>
    /// Configure default provider orchestrator for all subsequent calls
    /// </summary>
    public static void ConfigureDefaultOrchestrator(IProviderOrchestrator orchestrator)
    {
        _defaultOrchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
    }

    /// <summary>
    /// Start building an AI request
    /// </summary>
    public static HazinaBuilder AI()
    {
        var builder = new HazinaBuilder();

        // Use default orchestrator if configured
        if (_defaultOrchestrator != null)
        {
            builder.WithOrchestrator(_defaultOrchestrator);
        }

        return builder;
    }

    /// <summary>
    /// Quick ask - simplified interface for single questions
    /// Requires ConfigureDefaultOrchestrator() to be called first
    /// </summary>
    public static async Task<string> AskAsync(string question, CancellationToken cancellationToken = default)
    {
        if (_defaultOrchestrator == null)
        {
            throw new InvalidOperationException("Call Hazina.ConfigureDefaultOrchestrator() first");
        }

        return await AI()
            .Ask(question)
            .ExecuteAsync(cancellationToken);
    }

    /// <summary>
    /// Quick ask with fault detection
    /// </summary>
    public static async Task<string> AskSafeAsync(
        string question,
        double minConfidence = 0.7,
        CancellationToken cancellationToken = default)
    {
        if (_defaultOrchestrator == null)
        {
            throw new InvalidOperationException("Call Hazina.ConfigureDefaultOrchestrator() first");
        }

        return await AI()
            .WithFaultDetection(minConfidence)
            .Ask(question)
            .ExecuteAsync(cancellationToken);
    }

    /// <summary>
    /// Quick ask expecting JSON response
    /// </summary>
    public static async Task<string> AskForJsonAsync(
        string question,
        CancellationToken cancellationToken = default)
    {
        if (_defaultOrchestrator == null)
        {
            throw new InvalidOperationException("Call Hazina.ConfigureDefaultOrchestrator() first");
        }

        return await AI()
            .WithFaultDetection()
            .Ask(question)
            .ExpectJson()
            .ExecuteAsync(cancellationToken);
    }

    /// <summary>
    /// Quick ask expecting code response
    /// </summary>
    public static async Task<string> AskForCodeAsync(
        string question,
        CancellationToken cancellationToken = default)
    {
        if (_defaultOrchestrator == null)
        {
            throw new InvalidOperationException("Call Hazina.ConfigureDefaultOrchestrator() first");
        }

        return await AI()
            .WithFaultDetection()
            .Ask(question)
            .ExpectCode()
            .ExecuteAsync(cancellationToken);
    }
}
