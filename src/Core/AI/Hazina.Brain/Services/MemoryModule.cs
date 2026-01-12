using Hazina.Brain.Api;
using Hazina.Brain.Domain;
using Hazina.Brain.Options;
using Hazina.Brain.Repositories;
using Hazina.Store.EmbeddingStore;
using Microsoft.Extensions.Options;

namespace Hazina.Brain.Services;

/// <summary>
/// Core implementation of the Brain memory module.
/// Orchestrates episodic storage, fact distillation, and retrieval.
/// </summary>
public class MemoryModule : IMemoryModule
{
    private readonly IEmbeddingGenerator _embeddingGenerator;
    private readonly IMemoryEpisodeRepository _episodes;
    private readonly IMemoryFactRepository _facts;
    private readonly IMemoryDistiller _distiller;
    private readonly BrainOptions _options;

    public MemoryModule(
        IEmbeddingGenerator embeddingGenerator,
        IMemoryEpisodeRepository episodes,
        IMemoryFactRepository facts,
        IMemoryDistiller distiller,
        IOptions<BrainOptions> options)
    {
        _embeddingGenerator = embeddingGenerator ?? throw new ArgumentNullException(nameof(embeddingGenerator));
        _episodes = episodes ?? throw new ArgumentNullException(nameof(episodes));
        _facts = facts ?? throw new ArgumentNullException(nameof(facts));
        _distiller = distiller ?? throw new ArgumentNullException(nameof(distiller));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task ObserveAsync(ObservationContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(context.StoreId))
            throw new ArgumentException("StoreId is required", nameof(context));

        // Combine user input and model output for embedding
        var textForEmbedding = $"{context.UserInput}\n---\n{context.ModelOutput}";

        // Generate embedding
        var embedding = await _embeddingGenerator.GenerateAsync(textForEmbedding, ct);

        // Create episode
        var episode = new MemoryEpisode
        {
            Id = Guid.NewGuid(),
            StoreId = context.StoreId,
            AgentId = context.AgentId,
            UserId = context.UserId,
            RawInput = context.UserInput,
            RawOutput = context.ModelOutput,
            Embedding = embedding.Select(x => (float)x).ToArray(),
            RelevanceScore = 1.0f,
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow,
            Tags = Array.Empty<string>()
        };

        // Store episode
        await _episodes.StoreAsync(episode, ct);

        // Optionally distill facts (fire-and-forget if configured)
        if (_options.DistillOnObserve)
        {
            // For now, await (can be made background task with IHostedService queue)
            await _distiller.TryDistillFactsAsync(episode, ct);
        }
    }

    public async Task<RecallResult> RecallAsync(RecallQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(query.StoreId))
            throw new ArgumentException("StoreId is required", nameof(query));

        if (string.IsNullOrWhiteSpace(query.Prompt))
            throw new ArgumentException("Prompt is required", nameof(query));

        // Generate embedding for query
        var queryEmbedding = await _embeddingGenerator.GenerateAsync(query.Prompt, ct);
        var queryVector = queryEmbedding.Select(x => (float)x).ToArray();

        // Query episodes and facts in parallel
        var episodesTask = _episodes.QueryAsync(
            query.StoreId,
            query.AgentId,
            query.UserId,
            queryVector,
            query.EpisodesTopK,
            ct);

        var factsTask = _facts.QueryAsync(
            query.StoreId,
            query.AgentId,
            query.UserId,
            queryVector,
            query.FactsTopK,
            ct);

        await Task.WhenAll(episodesTask, factsTask);

        var episodes = await episodesTask;
        var facts = await factsTask;

        // Update access timestamps (fire-and-forget for performance)
        _ = Task.Run(async () =>
        {
            try
            {
                await _episodes.UpdateAccessAsync(episodes.Select(e => e.Id), ct);

                // Boost fact weights (small increment for being retrieved)
                var factWeightBoosts = facts.ToDictionary(f => f.Id, _ => 0.1);
                if (factWeightBoosts.Count > 0)
                {
                    await _facts.UpdateWeightsAsync(factWeightBoosts, ct);
                }
            }
            catch
            {
                // Ignore access update failures (non-critical)
            }
        }, ct);

        return new RecallResult(episodes, facts);
    }
}
