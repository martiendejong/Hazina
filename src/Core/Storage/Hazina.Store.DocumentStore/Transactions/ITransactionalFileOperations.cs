using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.Store.Transactions;

/// <summary>
/// Provides atomic, transactional file write operations with rollback support.
/// All writes are staged to temporary files first. On Commit, temp files are
/// atomically renamed to their target paths. On Rollback (or Dispose without
/// Commit), temp files are deleted and any backed-up originals are restored.
/// </summary>
public interface ITransactionalFileOperations : IDisposable
{
    /// <summary>Gets whether a transaction is currently open.</summary>
    bool IsActive { get; }

    /// <summary>
    /// Opens the transaction. Must be called before any <see cref="WriteFileAsync"/> calls.
    /// Calling on an already-active transaction throws <see cref="InvalidOperationException"/>.
    /// </summary>
    void BeginTransaction();

    /// <summary>
    /// Stages a file write. The content is written to a temp file immediately;
    /// the target path is not modified until <see cref="CommitAsync"/> succeeds.
    /// If the target file already exists its content is backed up so it can be
    /// restored by <see cref="Rollback"/>.
    /// </summary>
    Task WriteFileAsync(string path, string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically promotes all staged temp files to their target paths and
    /// removes any backups. Ends the transaction.
    /// Throws if no transaction is active.
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Discards all staged writes (deletes temp files) and restores any backed-up
    /// originals. Ends the transaction.
    /// Safe to call even when no transaction is active (no-op).
    /// </summary>
    void Rollback();
}
