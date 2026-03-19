namespace Hazina.AI.FluentAPI.Bootstrap;

/// <summary>
/// High-level Hazina service interface for use in ASP.NET Core applications.
/// Registered by <see cref="HazinaBootstrap.AddHazina"/> as a singleton.
/// </summary>
/// <remarks>
/// This interface wraps <c>AgentManager</c> in a DI-friendly abstraction that is
/// safe to inject into controllers and minimal API handlers.
///
/// For advanced use cases (custom agents, stores, flows) inject <c>AgentManager</c>
/// directly instead.
/// </remarks>
public interface IHazinaService
{
    /// <summary>
    /// Send a message to the default agent and receive a full response.
    /// </summary>
    /// <param name="message">User message.</param>
    /// <param name="conversationId">
    /// Optional conversation identifier. When provided, history is scoped to this ID.
    /// When null, a shared conversation history is used.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Chat response with the agent's reply and usage metadata.</returns>
    Task<HazinaServiceResponse> ChatAsync(
        string message,
        string? conversationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send a message to the default agent and stream the response token by token.
    /// Suitable for Server-Sent Events (SSE) endpoints.
    /// </summary>
    /// <param name="message">User message.</param>
    /// <param name="conversationId">Optional conversation identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of text chunks as they arrive from the LLM.</returns>
    IAsyncEnumerable<string> StreamChatAsync(
        string message,
        string? conversationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the current status of configured providers and loaded agents.
    /// Useful for health check endpoints.
    /// </summary>
    HazinaServiceStatus GetStatus();
}
