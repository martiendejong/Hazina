using System.Text.Json;

namespace Hazina.Store.EmbeddingStore.Transactions;

/// <summary>
/// Provides transactional file operations with two-phase commit and rollback support.
/// Wraps an ITextStore to add ACID-like guarantees for multi-file updates.
/// </summary>
/// <remarks>
/// This implementation uses a backup-and-restore strategy:
/// 1. BeginTransaction: Creates backup directory
/// 2. Operations: Backup existing files before modification
/// 3. Commit: Deletes backup directory
/// 4. Rollback: Restores files from backup and cleans up
/// </remarks>
public class TransactionalFileStore : ITextStore, ITransactionalStore
{
    private readonly ITextStore _innerStore;
    private readonly string _backupRoot;
    private readonly object _lock = new object();
    private string? _currentBackupDir;
    private HashSet<string> _modifiedKeys = new();
    private bool _disposed;

    public string RootFolder
    {
        get => _innerStore.RootFolder;
        set => _innerStore.RootFolder = value;
    }

    public bool IsTransactionActive => _currentBackupDir != null;

    /// <summary>
    /// Creates a new TransactionalFileStore.
    /// </summary>
    /// <param name="innerStore">The underlying text store to wrap</param>
    /// <param name="backupRoot">Root directory for transaction backups (optional, defaults to system temp)</param>
    public TransactionalFileStore(ITextStore innerStore, string? backupRoot = null)
    {
        _innerStore = innerStore ?? throw new ArgumentNullException(nameof(innerStore));
        _backupRoot = backupRoot ?? Path.Combine(Path.GetTempPath(), "HazinaTransactions");
        Directory.CreateDirectory(_backupRoot);
    }

    public void BeginTransaction()
    {
        lock (_lock)
        {
            if (IsTransactionActive)
                throw new InvalidOperationException("A transaction is already active. Commit or rollback the current transaction first.");

            _currentBackupDir = Path.Combine(_backupRoot, $"tx_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_currentBackupDir);
            _modifiedKeys.Clear();
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
            // Transaction succeeded - delete backup
            if (Directory.Exists(_currentBackupDir))
            {
                Directory.Delete(_currentBackupDir, recursive: true);
            }

            lock (_lock)
            {
                _currentBackupDir = null;
                _modifiedKeys.Clear();
            }

            return true;
        }
        catch (Exception)
        {
            // Even if cleanup fails, transaction was successful
            return true;
        }
    }

    public async Task<bool> RollbackAsync()
    {
        string? backupDir;
        HashSet<string> keysToRestore;

        lock (_lock)
        {
            if (!IsTransactionActive)
                throw new InvalidOperationException("No active transaction to rollback.");

            backupDir = _currentBackupDir;
            keysToRestore = new HashSet<string>(_modifiedKeys);
        }

        if (backupDir == null)
            return false;

        try
        {
            // Restore all backed up files
            foreach (var key in keysToRestore)
            {
                var backupPath = GetBackupPath(backupDir, key);
                var originalPath = _innerStore.GetPath(key);

                if (File.Exists(backupPath))
                {
                    // Restore from backup
                    var directory = Path.GetDirectoryName(originalPath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    File.Copy(backupPath, originalPath, overwrite: true);
                }
                else
                {
                    // File didn't exist before transaction - delete it
                    if (File.Exists(originalPath))
                    {
                        File.Delete(originalPath);
                    }
                }
            }

            // Clean up backup directory
            if (Directory.Exists(backupDir))
            {
                Directory.Delete(backupDir, recursive: true);
            }

            lock (_lock)
            {
                _currentBackupDir = null;
                _modifiedKeys.Clear();
            }

            return true;
        }
        catch (Exception)
        {
            // Rollback failed - backup directory is preserved for manual recovery
            return false;
        }
    }

    public async Task<bool> Store(string key, string value)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        lock (_lock)
        {
            if (IsTransactionActive && !_modifiedKeys.Contains(key))
            {
                // Backup existing file before modification
                var originalPath = _innerStore.GetPath(key);
                if (File.Exists(originalPath))
                {
                    var backupPath = GetBackupPath(_currentBackupDir!, key);
                    var backupDir = Path.GetDirectoryName(backupPath);
                    if (!string.IsNullOrEmpty(backupDir))
                    {
                        Directory.CreateDirectory(backupDir);
                    }
                    File.Copy(originalPath, backupPath, overwrite: true);
                }
                _modifiedKeys.Add(key);
            }
        }

        return await _innerStore.Store(key, value);
    }

    public async Task<string?> Get(string key)
    {
        return await _innerStore.Get(key);
    }

    public string GetPath(string key)
    {
        return _innerStore.GetPath(key);
    }

    public async Task<bool> Remove(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        lock (_lock)
        {
            if (IsTransactionActive && !_modifiedKeys.Contains(key))
            {
                // Backup existing file before removal
                var originalPath = _innerStore.GetPath(key);
                if (File.Exists(originalPath))
                {
                    var backupPath = GetBackupPath(_currentBackupDir!, key);
                    var backupDir = Path.GetDirectoryName(backupPath);
                    if (!string.IsNullOrEmpty(backupDir))
                    {
                        Directory.CreateDirectory(backupDir);
                    }
                    File.Copy(originalPath, backupPath, overwrite: true);
                }
                _modifiedKeys.Add(key);
            }
        }

        return await _innerStore.Remove(key);
    }

    private string GetBackupPath(string backupDir, string key)
    {
        // Use the same relative path structure as the original store
        var relativePath = Path.GetRelativePath(_innerStore.RootFolder, _innerStore.GetPath(key));
        return Path.Combine(backupDir, relativePath);
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
