namespace Hazina.Brain.Api;

/// <summary>
/// Context for a new observation (conversation turn) to be stored in episodic memory.
/// This triggers episode storage and optionally fact distillation.
/// </summary>
/// <param name="StoreId">Store identifier - required for multi-tenant isolation</param>
/// <param name="AgentId">Optional agent identifier - for per-agent memory</param>
/// <param name="UserId">Optional user identifier - for per-user memory</param>
/// <param name="UserInput">User's input text (question, command, etc.)</param>
/// <param name="ModelOutput">Agent/model's response text</param>
public sealed record ObservationContext(
    string StoreId,
    string? AgentId,
    string? UserId,
    string UserInput,
    string ModelOutput
);
