namespace Hazina.App.HazinaCoder.Core.Events;

// ===== Agent Orchestration Events =====
// Published across the agent pipeline: a user message comes in, intent is
// extracted from it, a plan is generated to satisfy that intent, and the
// user's profile is updated as a result of the interaction.

/// <summary>
/// Raised as soon as a user message reaches the orchestrator, before any
/// processing has happened.
/// </summary>
public record UserMessageReceivedEvent(string SessionId, string UserId, string Message) : BaseEvent;

/// <summary>
/// Raised once the orchestrator has classified the intent behind a received
/// user message.
/// </summary>
public record IntentExtractedEvent(string SessionId, string Intent, double Confidence) : BaseEvent;

/// <summary>
/// Raised once the orchestrator has produced an execution plan for a
/// previously extracted intent.
/// </summary>
public record PlanGeneratedEvent(string SessionId, string PlanId, IReadOnlyList<string> Steps) : BaseEvent;

/// <summary>
/// Raised whenever the orchestrator persists a change to a user's profile
/// (preferences, learned facts, running state, etc.) as a result of a session.
/// </summary>
public record UserProfileUpdatedEvent(string UserId, IReadOnlyDictionary<string, object?> UpdatedFields) : BaseEvent;
