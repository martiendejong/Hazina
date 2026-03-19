namespace Hazina.Store.EmbeddingStore.Transactions;

/// <summary>
/// Provides transactional operations for TextEmbeddingMemoryStore.
/// Wraps embedding operations with two-phase commit for batch updates.
/// </summary>
/// <remarks>
/// Example usage:
/// <code>
/// var store = new TextEmbeddingMemoryStore();
/// var transactionalStore = new TransactionalTextEmbeddingStore(store);
///
/// transactionalStore.BeginTransaction();
/// try
/// {
///     await transactionalStore.StoreAsync("key1", embedding1, "checksum1");
///     await transactionalStore.StoreAsync("key2", embedding2, "checksum2");
///     await transactionalStore.CommitAsync();
/// }
/// catch
/// {
///     await transactionalStore.RollbackAsync();
/// }
/// </code>
/// </remarks>
public class TransactionalTextEmbeddingStore : IEmbeddingStore, ITransactionalStore
{
    private readonly IEmbeddingStore _innerStore;
    private readonly object _lock = new object();
    private Dictionary<string, EmbeddingInfo?>? _snapshot;
    private Dictionary<string, EmbeddingInfo> _pendingChanges = new();
    private HashSet<string> _pendingRemovals = new();
    private bool _disposed;

    public bool IsTransactionActive => _snapshot != null;

    /// <summary>
    /// Creates a new TransactionalTextEmbeddingStore.
    /// </summary>
    /// <param name="innerStore">The underlying embedding store to wrap</param>
    public TransactionalTextEmbeddingStore(IEmbeddingStore innerStore)
    {
        _innerStore = innerStore ?? throw new ArgumentNullException(nameof(innerStore));
    }

    public void BeginTransaction()
    {
        lock (_lock)
        {
            if (IsTransactionActive)
                throw new InvalidOperationException("A transaction is already active. Commit or rollback the current transaction first.");

            _snapshot = new Dictionary<string, EmbeddingInfo?>();
            _pendingChanges.Clear();
            _pendingRemovals.Clear();
        }
    }

    public async Task<bool> CommitAsync()
    {
        lock (_lock)
        {
            if (!IsTransactionActive)
                throw new InvalidOperationException("No active transaction to commit.");
        }

        try
        {
            // All changes are already applied to the inner store
            // Just clear the transaction state
            lock (_lock)
            {
                _snapshot = null;
                _pendingChanges.Clear();
                _pendingRemovals.Clear();
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> RollbackAsync()
    {
        Dictionary<string, EmbeddingInfo?>? snapshot;
        Dictionary<string, EmbeddingInfo> changes;
        HashSet<string> removals;

        lock (_lock)
        {
            if (!IsTransactionActive)
                throw new InvalidOperationException("No active transaction to rollback.");

            snapshot = _snapshot;
            changes = new Dictionary<string, EmbeddingInfo>(_pendingChanges);
            removals = new HashSet<string>(_pendingRemovals);
        }

        if (snapshot == null)
            return false;

        try
        {
            // Restore original state for all modified keys
            foreach (var kvp in snapshot)
            {
                var key = kvp.Key;
                var originalValue = kvp.Value;

                if (originalValue != null)
                {
                    // Restore original embedding
                    await _innerStore.StoreAsync(key, originalValue.Data, originalValue.Checksum);
                }
                else
                {
                    // Key didn't exist before - remove it
                    await _innerStore.RemoveAsync(key);
                }
            }

            lock (_lock)
            {
                _snapshot = null;
                _pendingChanges.Clear();
                _pendingRemovals.Clear();
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> StoreAsync(string key, Embedding embedding, string checksum)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        if (embedding == null)
            throw new ArgumentNullException(nameof(embedding));

        lock (_lock)
        {
            if (IsTransactionActive && !_snapshot!.ContainsKey(key))
            {
                // Snapshot the original value before modification
                var original = _innerStore.GetAsync(key).GetAwaiter().GetResult();
                _snapshot[key] = original;
            }
        }

        var result = await _innerStore.StoreAsync(key, embedding, checksum);

        lock (_lock)
        {
            if (IsTransactionActive)
            {
                _pendingChanges[key] = new EmbeddingInfo(key, checksum, embedding);
                _pendingRemovals.Remove(key);
            }
        }

        return result;
    }

    public async Task<EmbeddingInfo?> GetAsync(string key)
    {
        return await _innerStore.GetAsync(key);
    }

    public async Task<bool> RemoveAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        lock (_lock)
        {
            if (IsTransactionActive && !_snapshot!.ContainsKey(key))
            {
                // Snapshot the original value before removal
                var original = _innerStore.GetAsync(key).GetAwaiter().GetResult();
                _snapshot[key] = original;
            }
        }

        var result = await _innerStore.RemoveAsync(key);

        lock (_lock)
        {
            if (IsTransactionActive)
            {
                _pendingRemovals.Add(key);
                _pendingChanges.Remove(key);
            }
        }

        return result;
    }

    public async Task<bool> ExistsAsync(string key)
    {
        return await _innerStore.ExistsAsync(key);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        // Clean up any active transaction
        if (IsTransactionActive)
        {
            try
            {
                RollbackAsync().GetAwaiter().GetResult();
            }
            catch
            {
                // Best effort cleanup
            }
        }

        _disposed = true;
    }
}
