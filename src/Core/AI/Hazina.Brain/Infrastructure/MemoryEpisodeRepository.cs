using Hazina.Brain.Domain;
using Hazina.Brain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Hazina.Brain.Infrastructure;

/// <summary>
/// EF Core implementation of episode repository with cosine similarity search.
/// For SQLite: uses in-memory similarity calculation.
/// For PostgreSQL: can be extended to use pgvector for native vector search.
/// </summary>
public class MemoryEpisodeRepository : IMemoryEpisodeRepository
{
    private readonly BrainDbContext _context;

    public MemoryEpisodeRepository(BrainDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task StoreAsync(MemoryEpisode episode, CancellationToken ct = default)
    {
        _context.Episodes.Add(episode);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<MemoryEpisode>> QueryAsync(
        string storeId,
        string? agentId,
        string? userId,
        float[] queryEmbedding,
        int topK,
        CancellationToken ct = default)
    {
        // Build base query with filters
        var query = _context.Episodes.AsQueryable();

        query = query.Where(e => e.StoreId == storeId);

        if (agentId != null)
            query = query.Where(e => e.AgentId == agentId);

        if (userId != null)
            query = query.Where(e => e.UserId == userId);

        // Get all episodes (filtered) and calculate similarity in-memory
        var episodes = await query.ToListAsync(ct);

        // Calculate cosine similarity and sort
        var scored = episodes
            .Select(e => new
            {
                Episode = e,
                Similarity = CosineSimilarity(queryEmbedding, e.Embedding)
            })
            .OrderByDescending(x => x.Similarity)
            .Take(topK)
            .Select(x => x.Episode)
            .ToList();

        return scored;
    }

    public async Task UpdateAccessAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var idsSet = ids.ToHashSet();

        var episodes = await _context.Episodes
            .Where(e => idsSet.Contains(e.Id))
            .ToListAsync(ct);

        foreach (var episode in episodes)
        {
            episode.LastAccessedAt = now;
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task<int> GetCountAsync(string storeId, CancellationToken ct = default)
    {
        return await _context.Episodes
            .Where(e => e.StoreId == storeId)
            .CountAsync(ct);
    }

    public async Task<int> PruneAsync(string storeId, int maxEpisodes, CancellationToken ct = default)
    {
        var count = await GetCountAsync(storeId, ct);
        if (count <= maxEpisodes)
            return 0;

        var toDelete = count - maxEpisodes;

        // Get lowest-weighted episodes
        var episodesToDelete = await _context.Episodes
            .Where(e => e.StoreId == storeId)
            .OrderBy(e => e.RelevanceScore)
            .ThenBy(e => e.LastAccessedAt)
            .Take(toDelete)
            .ToListAsync(ct);

        _context.Episodes.RemoveRange(episodesToDelete);
        await _context.SaveChangesAsync(ct);

        return episodesToDelete.Count;
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Vectors must have same length");

        float dotProduct = 0f;
        float magnitudeA = 0f;
        float magnitudeB = 0f;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        if (magnitudeA == 0f || magnitudeB == 0f)
            return 0f;

        return dotProduct / (MathF.Sqrt(magnitudeA) * MathF.Sqrt(magnitudeB));
    }
}
