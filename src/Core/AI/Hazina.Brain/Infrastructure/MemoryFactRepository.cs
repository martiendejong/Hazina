using Hazina.Brain.Domain;
using Hazina.Brain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Hazina.Brain.Infrastructure;

/// <summary>
/// EF Core implementation of fact repository with cosine similarity search.
/// </summary>
public class MemoryFactRepository : IMemoryFactRepository
{
    private readonly BrainDbContext _context;

    public MemoryFactRepository(BrainDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task AddFactsAsync(IEnumerable<MemoryFact> facts, CancellationToken ct = default)
    {
        _context.Facts.AddRange(facts);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<MemoryFact>> QueryAsync(
        string storeId,
        string? agentId,
        string? userId,
        float[] queryEmbedding,
        int topK,
        CancellationToken ct = default)
    {
        var query = _context.Facts.AsQueryable();

        query = query.Where(f => f.StoreId == storeId);

        if (agentId != null)
            query = query.Where(f => f.AgentId == agentId);

        if (userId != null)
            query = query.Where(f => f.UserId == userId);

        var facts = await query.ToListAsync(ct);

        // Calculate cosine similarity * weight and sort
        var scored = facts
            .Select(f => new
            {
                Fact = f,
                Score = CosineSimilarity(queryEmbedding, f.Embedding) * (float)f.Weight
            })
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => x.Fact)
            .ToList();

        return scored;
    }

    public async Task UpdateWeightsAsync(IReadOnlyDictionary<Guid, double> weightDeltas, CancellationToken ct = default)
    {
        var ids = weightDeltas.Keys.ToHashSet();

        var facts = await _context.Facts
            .Where(f => ids.Contains(f.Id))
            .ToListAsync(ct);

        foreach (var fact in facts)
        {
            if (weightDeltas.TryGetValue(fact.Id, out var delta))
            {
                fact.Weight += delta;
                fact.Weight = Math.Max(0.1, Math.Min(10.0, fact.Weight)); // Clamp to [0.1, 10.0]
                fact.LastAccessedAt = DateTimeOffset.UtcNow;
            }
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task ApplyDecayAsync(TimeSpan halfLife, CancellationToken ct = default)
    {
        var facts = await _context.Facts.ToListAsync(ct);
        var now = DateTimeOffset.UtcNow;

        foreach (var fact in facts)
        {
            var elapsed = now - fact.LastAccessedAt;
            var decayFactor = Math.Exp(-Math.Log(2) * elapsed.TotalDays / halfLife.TotalDays);
            fact.Weight *= decayFactor;
            fact.Weight = Math.Max(0.1, fact.Weight); // Floor at 0.1
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task<int> GetCountAsync(string storeId, CancellationToken ct = default)
    {
        return await _context.Facts
            .Where(f => f.StoreId == storeId)
            .CountAsync(ct);
    }

    public async Task<int> PruneAsync(string storeId, int maxFacts, CancellationToken ct = default)
    {
        var count = await GetCountAsync(storeId, ct);
        if (count <= maxFacts)
            return 0;

        var toDelete = count - maxFacts;

        var factsToDelete = await _context.Facts
            .Where(f => f.StoreId == storeId)
            .OrderBy(f => f.Weight)
            .ThenBy(f => f.LastAccessedAt)
            .Take(toDelete)
            .ToListAsync(ct);

        _context.Facts.RemoveRange(factsToDelete);
        await _context.SaveChangesAsync(ct);

        return factsToDelete.Count;
    }

    public async Task<bool> ExistsSimilarAsync(string storeId, string text, double similarityThreshold = 0.95, CancellationToken ct = default)
    {
        // Simple text comparison for now (can be enhanced with embedding similarity)
        var normalized = text.Trim().ToLowerInvariant();
        return await _context.Facts
            .AnyAsync(f => f.StoreId == storeId && f.Text.ToLower().Trim() == normalized, ct);
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
