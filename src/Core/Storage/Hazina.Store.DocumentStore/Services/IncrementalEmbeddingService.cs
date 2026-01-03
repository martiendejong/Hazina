using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hazina.AI.Providers.Core;

/// <summary>
/// Default implementation of incremental embedding service.
/// Stores chunk hashes and embeddings in a file-based cache.
/// </summary>
public class IncrementalEmbeddingService : IIncrementalEmbeddingService
{
    private readonly string _cachePath;
    private readonly IProviderOrchestrator _orchestrator;
    private readonly string _defaultModel;

    // Cost per 1K tokens (approximate for OpenAI ada-002)
    private const double CostPer1KTokens = 0.0001;
    private const int AverageTokensPerChunk = 200;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public IncrementalEmbeddingService(
        string cachePath,
        IProviderOrchestrator orchestrator,
        string defaultModel = "openai")
    {
        _cachePath = cachePath ?? throw new ArgumentNullException(nameof(cachePath));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _defaultModel = defaultModel;
        Directory.CreateDirectory(_cachePath);
    }

    public async Task<ChunkDiff> ComputeDiffAsync(
        string documentId,
        List<ContentChunk> newChunks,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var diff = new ChunkDiff();
        var existingHashes = await GetChunkHashesAsync(documentId, ct);

        var newChunkIds = new HashSet<string>();

        foreach (var chunk in newChunks)
        {
            newChunkIds.Add(chunk.ChunkId);

            if (existingHashes.TryGetValue(chunk.ChunkId, out var existingHash))
            {
                if (existingHash == chunk.ContentHash)
                {
                    // Content unchanged, use cache
                    diff.UnchangedChunkIds.Add(chunk.ChunkId);
                }
                else
                {
                    // Content changed, need re-embedding
                    diff.ModifiedChunks.Add(chunk);
                }
            }
            else
            {
                // New chunk, need embedding
                diff.NewChunks.Add(chunk);
            }
        }

        // Find deleted chunks
        foreach (var existingChunkId in existingHashes.Keys)
        {
            if (!newChunkIds.Contains(existingChunkId))
            {
                diff.DeletedChunkIds.Add(existingChunkId);
            }
        }

        return diff;
    }

    public async Task<IncrementalEmbeddingResult> EmbedIncrementallyAsync(
        string documentId,
        List<ContentChunk> chunks,
        bool forceReembed = false,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new IncrementalEmbeddingResult
        {
            DocumentId = documentId,
            TotalChunks = chunks.Count
        };

        try
        {
            // Compute what needs embedding
            ChunkDiff diff;
            if (forceReembed)
            {
                diff = new ChunkDiff { NewChunks = chunks };
            }
            else
            {
                // Ensure all chunks have hashes
                foreach (var chunk in chunks)
                {
                    if (string.IsNullOrEmpty(chunk.ContentHash))
                    {
                        chunk.ComputeHash();
                    }
                }
                diff = await ComputeDiffAsync(documentId, chunks, ct);
            }

            result.CachedUsed = diff.UnchangedChunkIds.Count;
            result.Deleted = diff.DeletedChunkIds.Count;

            // Delete removed chunk embeddings
            if (diff.DeletedChunkIds.Count > 0)
            {
                await DeleteChunkEmbeddingsAsync(documentId, diff.DeletedChunkIds, ct);
            }

            // Embed new and modified chunks
            var chunksToEmbed = diff.NewChunks.Concat(diff.ModifiedChunks).ToList();
            if (chunksToEmbed.Count > 0)
            {
                var embeddings = new List<ChunkEmbeddingRecord>();

                foreach (var chunk in chunksToEmbed)
                {
                    ct.ThrowIfCancellationRequested();

                    try
                    {
                        var embedding = await _orchestrator.GenerateEmbedding(chunk.Content);
                        var record = new ChunkEmbeddingRecord
                        {
                            ChunkId = chunk.ChunkId,
                            ContentHash = chunk.ContentHash,
                            Embedding = embedding.Select(d => (float)d).ToArray(),
                            Model = _defaultModel,
                            ComputedAt = DateTime.UtcNow,
                            TokenCount = EstimateTokenCount(chunk.Content)
                        };
                        embeddings.Add(record);

                        if (diff.NewChunks.Contains(chunk))
                            result.NewlyEmbedded++;
                        else
                            result.ReEmbedded++;
                    }
                    catch (Exception ex)
                    {
                        result.Failed++;
                        result.Errors.Add($"Chunk {chunk.ChunkId}: {ex.Message}");
                    }
                }

                // Store embeddings
                if (embeddings.Count > 0)
                {
                    await StoreChunkEmbeddingsAsync(documentId, embeddings, ct);
                }
            }

            // Calculate cost savings
            result.EstimatedCostSaved = result.CachedUsed * AverageTokensPerChunk * CostPer1KTokens / 1000;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Fatal error: {ex.Message}");
        }

        sw.Stop();
        result.Duration = sw.Elapsed;
        return result;
    }

    public async Task<Dictionary<string, string>> GetChunkHashesAsync(
        string documentId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var indexPath = GetIndexPath(documentId);
        if (!File.Exists(indexPath))
        {
            return new Dictionary<string, string>();
        }

        var json = await File.ReadAllTextAsync(indexPath, ct);
        var records = JsonSerializer.Deserialize<List<ChunkEmbeddingRecord>>(json, JsonOptions);
        return records?.ToDictionary(r => r.ChunkId, r => r.ContentHash)
            ?? new Dictionary<string, string>();
    }

    public async Task StoreChunkEmbeddingsAsync(
        string documentId,
        List<ChunkEmbeddingRecord> embeddings,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var indexPath = GetIndexPath(documentId);
        var embeddingsPath = GetEmbeddingsPath(documentId);

        // Load existing records
        var existing = new List<ChunkEmbeddingRecord>();
        if (File.Exists(indexPath))
        {
            var json = await File.ReadAllTextAsync(indexPath, ct);
            existing = JsonSerializer.Deserialize<List<ChunkEmbeddingRecord>>(json, JsonOptions)
                ?? new List<ChunkEmbeddingRecord>();
        }

        // Merge: update existing, add new
        var merged = new Dictionary<string, ChunkEmbeddingRecord>();
        foreach (var record in existing)
        {
            merged[record.ChunkId] = record;
        }
        foreach (var record in embeddings)
        {
            merged[record.ChunkId] = record;
        }

        // Save updated records
        var indexJson = JsonSerializer.Serialize(merged.Values.ToList(), JsonOptions);
        await File.WriteAllTextAsync(indexPath, indexJson, ct);

        // Save embeddings separately (binary format for efficiency)
        using var embeddingsFile = File.Create(embeddingsPath);
        using var writer = new BinaryWriter(embeddingsFile);
        writer.Write(merged.Count);
        foreach (var record in merged.Values)
        {
            writer.Write(record.ChunkId);
            writer.Write(record.Embedding.Length);
            foreach (var val in record.Embedding)
            {
                writer.Write(val);
            }
        }
    }

    public async Task DeleteChunkEmbeddingsAsync(
        string documentId,
        List<string> chunkIds,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var indexPath = GetIndexPath(documentId);
        if (!File.Exists(indexPath))
        {
            return;
        }

        var json = await File.ReadAllTextAsync(indexPath, ct);
        var records = JsonSerializer.Deserialize<List<ChunkEmbeddingRecord>>(json, JsonOptions)
            ?? new List<ChunkEmbeddingRecord>();

        var toDelete = new HashSet<string>(chunkIds);
        records = records.Where(r => !toDelete.Contains(r.ChunkId)).ToList();

        var newJson = JsonSerializer.Serialize(records, JsonOptions);
        await File.WriteAllTextAsync(indexPath, newJson, ct);
    }

    public async Task<EmbeddingStatistics> GetStatisticsAsync(
        string documentId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var stats = new EmbeddingStatistics();
        var indexPath = GetIndexPath(documentId);

        if (!File.Exists(indexPath))
        {
            return stats;
        }

        var json = await File.ReadAllTextAsync(indexPath, ct);
        var records = JsonSerializer.Deserialize<List<ChunkEmbeddingRecord>>(json, JsonOptions)
            ?? new List<ChunkEmbeddingRecord>();

        stats.TotalChunks = records.Count;
        stats.EmbeddedChunks = records.Count(r => r.Embedding.Length > 0);
        stats.PendingChunks = 0;
        stats.TotalTokens = records.Sum(r => r.TokenCount);
        stats.EstimatedCost = stats.TotalTokens * CostPer1KTokens / 1000;

        if (records.Count > 0)
        {
            stats.LastUpdated = records.Max(r => r.ComputedAt);
            stats.Model = records.FirstOrDefault()?.Model;
        }

        return stats;
    }

    private string GetIndexPath(string documentId)
    {
        var safeId = SanitizeFileName(documentId);
        return Path.Combine(_cachePath, $"{safeId}_index.json");
    }

    private string GetEmbeddingsPath(string documentId)
    {
        var safeId = SanitizeFileName(documentId);
        return Path.Combine(_cachePath, $"{safeId}_embeddings.bin");
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }

    private static int EstimateTokenCount(string text)
    {
        // Rough estimate: ~4 characters per token for English
        return string.IsNullOrEmpty(text) ? 0 : text.Length / 4;
    }
}
