using Hazina.Brain.Domain;
using Hazina.Brain.Repositories;
using Hazina.Store.EmbeddingStore;
using System.Text.Json;

namespace Hazina.Brain.Services;

/// <summary>
/// LLM-powered fact distillation from episodes.
/// Extracts factual statements like preferences, domain knowledge, entity info.
/// </summary>
public class MemoryDistiller : IMemoryDistiller
{
    private readonly IEmbeddingGenerator _embeddingGenerator;
    private readonly IMemoryFactRepository _facts;

    // Simplified implementation without LLM dependency for now
    // In production, inject ILLMProvider and call LLM to extract facts
    public MemoryDistiller(
        IEmbeddingGenerator embeddingGenerator,
        IMemoryFactRepository facts)
    {
        _embeddingGenerator = embeddingGenerator ?? throw new ArgumentNullException(nameof(embeddingGenerator));
        _facts = facts ?? throw new ArgumentNullException(nameof(facts));
    }

    public async Task<int> TryDistillFactsAsync(MemoryEpisode episode, CancellationToken ct = default)
    {
        // Simplified implementation - extracts simple heuristic-based facts
        // In production, this would:
        // 1. Build prompt asking LLM to extract facts
        // 2. Call LLM (gpt-4o-mini / claude-haiku for cost efficiency)
        // 3. Parse JSON response
        // 4. Embed and store facts

        var extractedFacts = new List<string>();

        // Simple heuristic: look for preference statements
        if (episode.RawInput.Contains("prefer", StringComparison.OrdinalIgnoreCase) ||
            episode.RawInput.Contains("like", StringComparison.OrdinalIgnoreCase))
        {
            extractedFacts.Add($"User preference: {episode.RawInput.Trim()}");
        }

        // Look for entity introductions (e.g., "My name is...", "I am...")
        if (episode.RawInput.Contains("my name is", StringComparison.OrdinalIgnoreCase) ||
            episode.RawInput.Contains("i am", StringComparison.OrdinalIgnoreCase))
        {
            extractedFacts.Add($"User identity: {episode.RawInput.Trim()}");
        }

        if (extractedFacts.Count == 0)
            return 0;

        // Store facts
        var factsToStore = new List<MemoryFact>();

        foreach (var factText in extractedFacts)
        {
            // Check for duplicates
            if (await _facts.ExistsSimilarAsync(episode.StoreId, factText, 0.95, ct))
                continue;

            // Generate embedding for fact
            var factEmbedding = await _embeddingGenerator.GenerateAsync(factText, ct);

            factsToStore.Add(new MemoryFact
            {
                Id = Guid.NewGuid(),
                StoreId = episode.StoreId,
                AgentId = episode.AgentId,
                UserId = episode.UserId,
                Text = factText,
                Embedding = factEmbedding.Select(x => (float)x).ToArray(),
                Confidence = 0.7, // Heuristic extraction = lower confidence
                Weight = 1.0,
                CreatedAt = DateTimeOffset.UtcNow,
                LastAccessedAt = DateTimeOffset.UtcNow
            });
        }

        if (factsToStore.Count > 0)
        {
            await _facts.AddFactsAsync(factsToStore, ct);
        }

        return factsToStore.Count;
    }
}

/// <summary>
/// Simple DTO for LLM-extracted facts (for future LLM integration)
/// </summary>
internal record ExtractedFact(string Text, double Confidence);
