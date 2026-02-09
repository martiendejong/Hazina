namespace Hazina.AI.LocalLLM;

/// <summary>
/// Intent parser interface for converting user input to structured intents.
/// Abstracts LLM implementation (local vs cloud, different models).
/// </summary>
public interface IIntentParser
{
    /// <summary>
    /// Parses user input into a structured intent.
    /// </summary>
    /// <param name="userInput">Natural language input from user</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Parsed intent or null if parsing failed</returns>
    Task<ParsedIntent?> ParseAsync(string userInput, CancellationToken ct = default);
}

/// <summary>
/// Parsed intent result with confidence score.
/// </summary>
public record ParsedIntent
{
    /// <summary>
    /// Intent type (e.g., "query", "command", "explain", "help").
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Target entity (e.g., "files", "processes", "sessions").
    /// </summary>
    public string? Target { get; init; }

    /// <summary>
    /// Filters to apply (e.g., { "extension": ".py", "modified": "today" }).
    /// </summary>
    public Dictionary<string, object>? Filters { get; init; }

    /// <summary>
    /// Confidence score (0.0 - 1.0).
    /// Values below 0.8 should trigger clarification dialog.
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Original user input that was parsed.
    /// </summary>
    public string? OriginalInput { get; init; }
}
