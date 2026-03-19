namespace Hazina.Store.EmbeddingStore;

/// <summary>
/// Service for maintaining embedding store integrity through compaction and repair operations.
/// </summary>
public class EmbeddingCompactionService
{
    private readonly IEmbeddingStore _embeddingStore;
    private readonly IEnumerableEmbeddingStore? _enumerableStore;
    private readonly Func<string, Task<bool>>? _documentExistsCheck;

    /// <summary>
    /// Creates a new EmbeddingCompactionService.
    /// </summary>
    /// <param name="embeddingStore">The embedding store to maintain</param>
    /// <param name="documentExistsCheck">Optional function to check if a document exists (for orphan detection)</param>
    public EmbeddingCompactionService(
        IEmbeddingStore embeddingStore,
        Func<string, Task<bool>>? documentExistsCheck = null)
    {
        _embeddingStore = embeddingStore ?? throw new ArgumentNullException(nameof(embeddingStore));
        _enumerableStore = embeddingStore as IEnumerableEmbeddingStore;
        _documentExistsCheck = documentExistsCheck;
    }

    /// <summary>
    /// Removes orphaned embeddings (embeddings without matching documents).
    /// Requires the embedding store to implement IEnumerableEmbeddingStore.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of orphaned embeddings removed</returns>
    public async Task<int> CompactAsync(CancellationToken cancellationToken = default)
    {
        if (_enumerableStore == null)
        {
            Console.WriteLine("WARNING: EmbeddingStore does not implement IEnumerableEmbeddingStore. Compaction skipped.");
            return 0;
        }

        if (_documentExistsCheck == null)
        {
            Console.WriteLine("WARNING: No document existence check provided. Compaction skipped.");
            return 0;
        }

        var orphanedKeys = new List<string>();
        var totalChecked = 0;

        Console.WriteLine("Starting embedding compaction...");

        await foreach (var embedding in _enumerableStore.GetAllAsync(cancellationToken))
        {
            totalChecked++;

            var documentExists = await _documentExistsCheck(embedding.Key);
            if (!documentExists)
            {
                orphanedKeys.Add(embedding.Key);
                Console.WriteLine($"  Found orphaned embedding: {embedding.Key}");
            }
        }

        // Remove orphaned embeddings
        foreach (var key in orphanedKeys)
        {
            await _embeddingStore.RemoveAsync(key);
        }

        Console.WriteLine($"Compaction complete: {orphanedKeys.Count} orphaned embeddings removed from {totalChecked} total.");

        return orphanedKeys.Count;
    }

    /// <summary>
    /// Verifies integrity of all embedding-document links.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Verification result with statistics</returns>
    public async Task<IntegrityVerificationResult> VerifyIntegrityAsync(CancellationToken cancellationToken = default)
    {
        if (_enumerableStore == null)
        {
            return new IntegrityVerificationResult
            {
                Success = false,
                Message = "EmbeddingStore does not implement IEnumerableEmbeddingStore"
            };
        }

        var result = new IntegrityVerificationResult { Success = true };
        var brokenLinks = new List<string>();

        Console.WriteLine("Starting integrity verification...");

        await foreach (var embedding in _enumerableStore.GetAllAsync(cancellationToken))
        {
            result.TotalEmbeddings++;

            // Check if key is valid
            if (string.IsNullOrWhiteSpace(embedding.Key))
            {
                result.InvalidKeys++;
                brokenLinks.Add($"Empty or null key");
                continue;
            }

            // Check if embedding data is valid
            if (embedding.Data == null || embedding.Data.Count == 0)
            {
                result.InvalidEmbeddings++;
                brokenLinks.Add($"Invalid embedding data for key: {embedding.Key}");
                continue;
            }

            // Check if checksum exists
            if (string.IsNullOrWhiteSpace(embedding.Checksum))
            {
                result.MissingChecksums++;
            }

            // Optional: Check if document exists
            if (_documentExistsCheck != null)
            {
                var documentExists = await _documentExistsCheck(embedding.Key);
                if (!documentExists)
                {
                    result.OrphanedEmbeddings++;
                    brokenLinks.Add($"Orphaned embedding (no document): {embedding.Key}");
                }
            }
        }

        result.BrokenLinks = brokenLinks;
        result.Message = $"Verified {result.TotalEmbeddings} embeddings. " +
                        $"Issues: {result.InvalidKeys} invalid keys, " +
                        $"{result.InvalidEmbeddings} invalid embeddings, " +
                        $"{result.MissingChecksums} missing checksums, " +
                        $"{result.OrphanedEmbeddings} orphaned.";

        Console.WriteLine(result.Message);

        return result;
    }

    /// <summary>
    /// Repairs broken links by removing invalid embeddings.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of embeddings repaired (removed)</returns>
    public async Task<int> RepairAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("Starting repair operations...");

        var verificationResult = await VerifyIntegrityAsync(cancellationToken);

        if (!verificationResult.Success)
        {
            Console.WriteLine($"Repair aborted: {verificationResult.Message}");
            return 0;
        }

        var repaired = 0;

        // Remove orphaned embeddings
        if (_documentExistsCheck != null && verificationResult.OrphanedEmbeddings > 0)
        {
            repaired += await CompactAsync(cancellationToken);
        }

        // Note: We don't auto-remove embeddings with invalid data or missing checksums
        // as this might indicate a data corruption issue that needs manual investigation

        Console.WriteLine($"Repair complete: {repaired} issues fixed.");

        return repaired;
    }
}

/// <summary>
/// Result of an integrity verification operation.
/// </summary>
public class IntegrityVerificationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public int TotalEmbeddings { get; set; }
    public int InvalidKeys { get; set; }
    public int InvalidEmbeddings { get; set; }
    public int MissingChecksums { get; set; }
    public int OrphanedEmbeddings { get; set; }
    public List<string> BrokenLinks { get; set; } = new();
}
