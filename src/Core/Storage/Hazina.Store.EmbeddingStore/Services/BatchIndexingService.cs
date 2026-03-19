using System.Security.Cryptography;
using System.Text;

namespace Hazina.Store.EmbeddingStore;

/// <summary>
/// Service for efficient batch indexing operations with progress reporting.
/// </summary>
public class BatchIndexingService
{
    private readonly IBatchEmbeddingStore _store;
    private readonly IEmbeddingGenerator _generator;
    private readonly int _maxConcurrency;

    /// <summary>
    /// Creates a new BatchIndexingService.
    /// </summary>
    /// <param name="store">The batch-capable embedding store</param>
    /// <param name="generator">The embedding generator</param>
    /// <param name="maxConcurrency">Maximum concurrent embedding generation operations (default: 4)</param>
    public BatchIndexingService(
        IBatchEmbeddingStore store,
        IEmbeddingGenerator generator,
        int maxConcurrency = 4)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _maxConcurrency = maxConcurrency > 0 ? maxConcurrency : 4;
    }

    /// <summary>
    /// Indexes multiple text items in parallel with progress reporting.
    /// </summary>
    /// <param name="items">Key-value pairs to index</param>
    /// <param name="progress">Progress callback (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of items successfully indexed</returns>
    public async Task<int> BatchIndexAsync(
        IEnumerable<(string key, string text)> items,
        IProgress<BatchIndexProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var itemList = items.ToList();
        var totalCount = itemList.Count;

        if (totalCount == 0)
            return 0;

        var progressState = new BatchIndexProgress
        {
            TotalCount = totalCount,
            CurrentOperation = "Starting batch indexing"
        };

        progress?.Report(progressState);

        using var semaphore = new SemaphoreSlim(_maxConcurrency);
        var embeddingTasks = new List<Task<(string key, Embedding? embedding, string checksum, bool success)>>();

        // Generate embeddings in parallel with concurrency control
        foreach (var (key, text) in itemList)
        {
            await semaphore.WaitAsync(cancellationToken);

            var task = Task.Run(async () =>
            {
                try
                {
                    progressState.CurrentOperation = $"Generating embedding for: {key}";
                    progress?.Report(progressState);

                    var embedding = await _generator.GenerateAsync(text, cancellationToken);
                    var checksum = ComputeChecksum(text);

                    return (key, embedding, checksum, success: true);
                }
                catch (Exception ex)
                {
                    progressState.LastError = $"Failed to generate embedding for {key}: {ex.Message}";
                    return (key, (Embedding?)null, "", success: false);
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken);

            embeddingTasks.Add(task);
        }

        // Wait for all embeddings to be generated
        var results = await Task.WhenAll(embeddingTasks);

        // Prepare batch for storage
        var batchToStore = results
            .Where(r => r.success && r.embedding != null)
            .Select(r => (r.key, r.embedding!, r.checksum))
            .ToList();

        progressState.CurrentOperation = "Storing embeddings";
        progressState.ProcessedCount = results.Length;
        progressState.SuccessCount = batchToStore.Count;
        progressState.FailureCount = results.Length - batchToStore.Count;
        progress?.Report(progressState);

        // Store all embeddings in a single batch operation
        var storedCount = await _store.StoreBatchAsync(batchToStore, cancellationToken);

        progressState.CurrentOperation = "Complete";
        progress?.Report(progressState);

        return storedCount;
    }

    /// <summary>
    /// Removes multiple embeddings in a batch operation.
    /// </summary>
    /// <param name="keys">Keys to remove</param>
    /// <param name="progress">Progress callback (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of items successfully removed</returns>
    public async Task<int> BatchRemoveAsync(
        IEnumerable<string> keys,
        IProgress<BatchIndexProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var keyList = keys.ToList();
        var totalCount = keyList.Count;

        if (totalCount == 0)
            return 0;

        var progressState = new BatchIndexProgress
        {
            TotalCount = totalCount,
            CurrentOperation = "Starting batch removal"
        };

        progress?.Report(progressState);

        var removed = 0;

        for (int i = 0; i < keyList.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var key = keyList[i];
            var success = await _store.RemoveAsync(key);

            if (success)
                removed++;

            progressState.ProcessedCount = i + 1;
            progressState.SuccessCount = removed;
            progressState.FailureCount = (i + 1) - removed;
            progressState.CurrentOperation = $"Removing: {key}";
            progress?.Report(progressState);
        }

        progressState.CurrentOperation = "Complete";
        progress?.Report(progressState);

        return removed;
    }

    /// <summary>
    /// Computes a checksum for text content.
    /// </summary>
    private static string ComputeChecksum(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
