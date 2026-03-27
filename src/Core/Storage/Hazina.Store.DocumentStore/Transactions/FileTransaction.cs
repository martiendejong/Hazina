using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Hazina.Store.Transactions;

/// <summary>
/// Implements atomic, transactional multi-file write operations.
///
/// Write flow (for each file):
///   1. If the target already exists → copy it to a .bak temp file (backup).
///   2. Write staged content to a .tmp temp file.
///
/// Commit flow:
///   1. For each staged operation: <c>File.Move(tmpPath, targetPath, overwrite: true)</c>
///      (atomic rename on same-volume, best-effort on cross-volume).
///   2. Delete all .bak backup files.
///
/// Rollback flow:
///   1. Delete all .tmp staged files.
///   2. For each backup: <c>File.Move(bakPath, targetPath, overwrite: true)</c>.
/// </summary>
public sealed class FileTransaction : ITransactionalFileOperations
{
    private readonly ILogger<FileTransaction>? _logger;

    // Per-operation record: what we staged and what we backed up.
    private readonly record struct FileOperation(string TargetPath, string TmpPath, string? BackupPath);

    private List<FileOperation>? _operations;
    private bool _disposed;

    public FileTransaction(ILogger<FileTransaction>? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool IsActive => _operations is not null;

    /// <inheritdoc/>
    public void BeginTransaction()
    {
        EnsureNotDisposed();
        if (_operations is not null)
            throw new InvalidOperationException("A transaction is already active. Call Commit or Rollback first.");

        _operations = new List<FileOperation>();
        _logger?.LogDebug("[FileTransaction] Transaction begun.");
    }

    /// <inheritdoc/>
    public async Task WriteFileAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        EnsureActive();

        ArgumentException.ThrowIfNullOrEmpty(path);

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        // --- Backup existing file ---
        string? backupPath = null;
        if (File.Exists(path))
        {
            backupPath = path + $".{Guid.NewGuid():N}.bak";
            File.Copy(path, backupPath, overwrite: false);
            _logger?.LogDebug("[FileTransaction] Backed up '{Target}' → '{Backup}'", path, backupPath);
        }

        // --- Stage to temp ---
        var tmpPath = path + $".{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(tmpPath, content, cancellationToken);
        _logger?.LogDebug("[FileTransaction] Staged write '{Tmp}' for target '{Target}'", tmpPath, path);

        _operations!.Add(new FileOperation(path, tmpPath, backupPath));
    }

    /// <inheritdoc/>
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        EnsureActive();

        _logger?.LogInformation("[FileTransaction] Committing {Count} file operation(s).", _operations!.Count);

        var committed = new List<FileOperation>();

        try
        {
            foreach (var op in _operations!)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Atomically promote the staged temp file to the target path.
                File.Move(op.TmpPath, op.TargetPath, overwrite: true);
                committed.Add(op);
                _logger?.LogDebug("[FileTransaction] Committed '{Target}'", op.TargetPath);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[FileTransaction] Commit failed after {N}/{Total} file(s). Rolling back.",
                committed.Count, _operations!.Count);

            // Partially committed: undo what we already moved.
            RollbackCommitted(committed);

            // Clean up remaining staged temps.
            foreach (var op in _operations!)
            {
                if (committed.Contains(op)) continue; // already handled above
                SafeDelete(op.TmpPath);
            }

            EndTransaction();
            throw;
        }

        // Success: clean up all backups.
        foreach (var op in _operations!)
        {
            if (op.BackupPath is not null)
                SafeDelete(op.BackupPath);
        }

        _logger?.LogInformation("[FileTransaction] Commit successful.");
        EndTransaction();

        await Task.CompletedTask; // kept async for interface consistency
    }

    /// <inheritdoc/>
    public void Rollback()
    {
        if (!IsActive)
        {
            _logger?.LogDebug("[FileTransaction] Rollback called but no active transaction — no-op.");
            return;
        }

        _logger?.LogInformation("[FileTransaction] Rolling back {Count} file operation(s).", _operations!.Count);

        foreach (var op in _operations!)
        {
            // Delete the staged temp.
            SafeDelete(op.TmpPath);

            // Restore backup if one was taken.
            if (op.BackupPath is not null && File.Exists(op.BackupPath))
            {
                try
                {
                    File.Move(op.BackupPath, op.TargetPath, overwrite: true);
                    _logger?.LogDebug("[FileTransaction] Restored '{Target}' from backup.", op.TargetPath);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[FileTransaction] Failed to restore backup for '{Target}'. Backup at '{Backup}'.",
                        op.TargetPath, op.BackupPath);
                }
            }
        }

        EndTransaction();
        _logger?.LogInformation("[FileTransaction] Rollback complete.");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Auto-rollback if the transaction was not committed.
        if (IsActive)
        {
            _logger?.LogWarning("[FileTransaction] Dispose called on an active transaction — rolling back.");
            Rollback();
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────────────────────

    private void EnsureNotDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FileTransaction));
    }

    private void EnsureActive()
    {
        if (_operations is null)
            throw new InvalidOperationException("No active transaction. Call BeginTransaction() first.");
    }

    private void EndTransaction() => _operations = null;

    private void RollbackCommitted(IReadOnlyList<FileOperation> committed)
    {
        // Restore previously committed files from their backups (reverse order).
        for (int i = committed.Count - 1; i >= 0; i--)
        {
            var op = committed[i];
            // The committed target now holds the new content — remove it.
            SafeDelete(op.TargetPath);

            if (op.BackupPath is not null && File.Exists(op.BackupPath))
            {
                try
                {
                    File.Move(op.BackupPath, op.TargetPath, overwrite: true);
                    _logger?.LogDebug("[FileTransaction] Partial-commit restore: '{Target}' from backup.", op.TargetPath);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex,
                        "[FileTransaction] CRITICAL: Could not restore '{Target}' from '{Backup}' after partial commit failure.",
                        op.TargetPath, op.BackupPath);
                }
            }
        }
    }

    private void SafeDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[FileTransaction] Could not delete temp/backup file '{Path}'.", path);
        }
    }
}
