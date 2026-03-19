namespace Hazina.AI.FluentAPI.Bootstrap;

/// <summary>
/// Response from a Hazina agent chat call via <see cref="IHazinaService"/>.
/// </summary>
public sealed class HazinaServiceResponse
{
    /// <summary>
    /// The agent's reply text.
    /// </summary>
    public string Result { get; init; } = string.Empty;

    /// <summary>
    /// The conversation ID this response belongs to.
    /// </summary>
    public string? ConversationId { get; init; }

    /// <summary>
    /// Token usage details. May be null if the provider does not report usage.
    /// </summary>
    public HazinaTokenUsage? TokenUsage { get; init; }
}

/// <summary>
/// Token usage and cost metadata for a single Hazina LLM call.
/// </summary>
public sealed class HazinaTokenUsage
{
    /// <summary>Total tokens consumed (input + output).</summary>
    public long TotalTokens { get; init; }

    /// <summary>Estimated cost in USD.</summary>
    public decimal TotalCost { get; init; }
}

/// <summary>
/// Status snapshot of the Hazina service - provider and agent health.
/// </summary>
public sealed class HazinaServiceStatus
{
    /// <summary>Whether the service is fully initialized and ready.</summary>
    public bool IsReady { get; init; }

    /// <summary>Number of agents currently loaded.</summary>
    public int AgentCount { get; init; }

    /// <summary>Names of all loaded agents.</summary>
    public IReadOnlyList<string> AgentNames { get; init; } = [];

    /// <summary>
    /// Names of configured LLM providers.
    /// </summary>
    public IReadOnlyList<string> Providers { get; init; } = [];
}
