namespace Hazina.Store.EmbeddingStore.Transactions;

/// <summary>
/// Interface for stores that support transactional operations.
/// Provides two-phase commit semantics for batch updates.
/// </summary>
public interface ITransactionalStore : IDisposable
{
    /// <summary>
    /// Begins a new transaction.
    /// </summary>
    void BeginTransaction();

    /// <summary>
    /// Commits the current transaction, making all changes permanent.
    /// </summary>
    /// <returns>True if commit succeeded, false otherwise</returns>
    Task<bool> CommitAsync();

    /// <summary>
    /// Rolls back the current transaction, undoing all changes.
    /// </summary>
    /// <returns>True if rollback succeeded, false otherwise</returns>
    Task<bool> RollbackAsync();

    /// <summary>
    /// Gets whether a transaction is currently active.
    /// </summary>
    bool IsTransactionActive { get; }
}
