using Hazina.Brain.Domain;

namespace Hazina.Brain.Api;

/// <summary>
/// Result of a recall query - contains relevant episodes and facts.
/// This data should be injected into the LLM prompt to provide memory context.
/// </summary>
/// <param name="Episodes">Most relevant recent conversation episodes (chronologically ordered)</param>
/// <param name="Facts">Most relevant long-term facts (weight-ordered)</param>
public sealed record RecallResult(
    IReadOnlyList<MemoryEpisode> Episodes,
    IReadOnlyList<MemoryFact> Facts
);
