using Hazina.Brain.Domain;

namespace Hazina.Brain.Services;

/// <summary>
/// Service for distilling facts from episodic memories using LLM.
/// Extracts stable, persistent knowledge from conversation turns.
/// </summary>
public interface IMemoryDistiller
{
    /// <summary>
    /// Attempt to distill facts from an episode.
    /// Returns the number of facts extracted and stored.
    /// </summary>
    Task<int> TryDistillFactsAsync(MemoryEpisode episode, CancellationToken ct = default);
}
