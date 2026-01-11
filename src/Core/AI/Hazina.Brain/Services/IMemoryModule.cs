using Hazina.Brain.Api;

namespace Hazina.Brain.Services;

/// <summary>
/// Core Brain API - provides persistent, cumulative memory for agents and stores.
/// The "brain" maintains episodic memory (conversation history) and long-term facts (distilled knowledge).
/// The LLM stays stateless; Hazina.Brain provides the memory.
/// </summary>
public interface IMemoryModule
{
    /// <summary>
    /// Observe a new conversation turn - stores as episode and optionally distills facts.
    /// Call this AFTER the LLM responds to persist the interaction.
    /// </summary>
    /// <param name="context">The conversation turn to remember</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task</returns>
    /// <example>
    /// <code>
    /// await memoryModule.ObserveAsync(new ObservationContext(
    ///     StoreId: "store-123",
    ///     AgentId: "agent-001",
    ///     UserId: "user-456",
    ///     UserInput: "I prefer responses in Dutch",
    ///     ModelOutput: "Understood! I'll respond in Dutch from now on."));
    /// </code>
    /// </example>
    Task ObserveAsync(ObservationContext context, CancellationToken ct = default);

    /// <summary>
    /// Recall relevant memories (episodes + facts) for a given query.
    /// Call this BEFORE the LLM to provide memory context for the prompt.
    /// </summary>
    /// <param name="query">Query specifying what to recall and how much</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Relevant episodes and facts to inject into LLM context</returns>
    /// <example>
    /// <code>
    /// var recall = await memoryModule.RecallAsync(new RecallQuery(
    ///     StoreId: "store-123",
    ///     AgentId: "agent-001",
    ///     UserId: "user-456",
    ///     Prompt: "What's my preferred language?",
    ///     EpisodesTopK: 5,
    ///     FactsTopK: 10));
    ///
    /// // recall.Facts should contain: "user prefers Dutch responses"
    /// </code>
    /// </example>
    Task<RecallResult> RecallAsync(RecallQuery query, CancellationToken ct = default);
}
