using Hazina.Store.EmbeddingStore;
using Hazina.Store.EmbeddingStore.Transactions;
using Xunit;

namespace Hazina.Store.EmbeddingStore.Tests;

/// <summary>
/// Unit tests for TransactionalFileStore with two-phase commit and rollback.
/// </summary>
public class TransactionalFileStoreTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _backupDirectory;
    private readonly TextFileStore _innerStore;
    private readonly TransactionalFileStore _transactionalStore;

    public TransactionalFileStoreTests()
    {
        // Create temporary test directories
        _testDirectory = Path.Combine(Path.GetTempPath(), $"TransactionalTests_{Guid.NewGuid()}");
        _backupDirectory = Path.Combine(Path.GetTempPath(), $"TransactionalBackup_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        Directory.CreateDirectory(_backupDirectory);

        _innerStore = new TextFileStore(_testDirectory);
        _transactionalStore = new TransactionalFileStore(_innerStore, _backupDirectory);
    }

    public void Dispose()
    {
        _transactionalStore?.Dispose();

        // Clean up test directories
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
        if (Directory.Exists(_backupDirectory))
        {
            Directory.Delete(_backupDirectory, recursive: true);
        }
    }

    [Fact]
    public void BeginTransaction_SetsTransactionActive()
    {
        // Arrange & Act
        _transactionalStore.BeginTransaction();

        // Assert
        Assert.True(_transactionalStore.IsTransactionActive);
    }

    [Fact]
    public void BeginTransaction_WhenAlreadyActive_ThrowsInvalidOperationException()
    {
        // Arrange
        _transactionalStore.BeginTransaction();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _transactionalStore.BeginTransaction());
        Assert.Contains("already active", exception.Message);
    }

    [Fact]
    public async Task CommitAsync_WhenNoTransaction_ThrowsInvalidOperationException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _transactionalStore.CommitAsync());
        Assert.Contains("No active transaction", exception.Message);
    }

    [Fact]
    public async Task RollbackAsync_WhenNoTransaction_ThrowsInvalidOperationException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _transactionalStore.RollbackAsync());
        Assert.Contains("No active transaction", exception.Message);
    }

    [Fact]
    public async Task Store_WithinTransaction_ThenCommit_PersistsChanges()
    {
        // Arrange
        var key = "test.txt";
        var value = "test content";

        // Act
        _transactionalStore.BeginTransaction();
        await _transactionalStore.Store(key, value);
        var committed = await _transactionalStore.CommitAsync();

        // Assert
        Assert.True(committed);
        Assert.False(_transactionalStore.IsTransactionActive);
        var retrieved = await _transactionalStore.Get(key);
        Assert.Equal(value, retrieved);
    }

    [Fact]
    public async Task Store_WithinTransaction_ThenRollback_DiscardsChanges()
    {
        // Arrange
        var key = "test.txt";
        var value = "test content";

        // Act
        _transactionalStore.BeginTransaction();
        await _transactionalStore.Store(key, value);
        var rolledBack = await _transactionalStore.RollbackAsync();

        // Assert
        Assert.True(rolledBack);
        Assert.False(_transactionalStore.IsTransactionActive);
        var retrieved = await _transactionalStore.Get(key);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task Store_UpdateExisting_WithinTransaction_ThenRollback_RestoresOriginal()
    {
        // Arrange
        var key = "test.txt";
        var originalValue = "original content";
        var updatedValue = "updated content";

        // Store original value first
        await _transactionalStore.Store(key, originalValue);

        // Act
        _transactionalStore.BeginTransaction();
        await _transactionalStore.Store(key, updatedValue);

        // Verify update happened
        var duringTransaction = await _transactionalStore.Get(key);
        Assert.Equal(updatedValue, duringTransaction);

        // Rollback
        var rolledBack = await _transactionalStore.RollbackAsync();

        // Assert
        Assert.True(rolledBack);
        var afterRollback = await _transactionalStore.Get(key);
        Assert.Equal(originalValue, afterRollback);
    }

    [Fact]
    public async Task Remove_WithinTransaction_ThenCommit_DeletesFile()
    {
        // Arrange
        var key = "test.txt";
        var value = "test content";
        await _transactionalStore.Store(key, value);

        // Act
        _transactionalStore.BeginTransaction();
        await _transactionalStore.Remove(key);
        var committed = await _transactionalStore.CommitAsync();

        // Assert
        Assert.True(committed);
        var retrieved = await _transactionalStore.Get(key);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task Remove_WithinTransaction_ThenRollback_RestoresFile()
    {
        // Arrange
        var key = "test.txt";
        var value = "test content";
        await _transactionalStore.Store(key, value);

        // Act
        _transactionalStore.BeginTransaction();
        await _transactionalStore.Remove(key);

        // Verify removal happened
        var duringTransaction = await _transactionalStore.Get(key);
        Assert.Null(duringTransaction);

        // Rollback
        var rolledBack = await _transactionalStore.RollbackAsync();

        // Assert
        Assert.True(rolledBack);
        var afterRollback = await _transactionalStore.Get(key);
        Assert.Equal(value, afterRollback);
    }

    [Fact]
    public async Task MultipleStores_WithinTransaction_ThenRollback_RestoresAllFiles()
    {
        // Arrange
        var keys = new[] { "file1.txt", "file2.txt", "file3.txt" };
        var originalValues = new[] { "content1", "content2", "content3" };
        var updatedValues = new[] { "updated1", "updated2", "updated3" };

        // Store original values
        for (int i = 0; i < keys.Length; i++)
        {
            await _transactionalStore.Store(keys[i], originalValues[i]);
        }

        // Act
        _transactionalStore.BeginTransaction();
        for (int i = 0; i < keys.Length; i++)
        {
            await _transactionalStore.Store(keys[i], updatedValues[i]);
        }
        var rolledBack = await _transactionalStore.RollbackAsync();

        // Assert
        Assert.True(rolledBack);
        for (int i = 0; i < keys.Length; i++)
        {
            var retrieved = await _transactionalStore.Get(keys[i]);
            Assert.Equal(originalValues[i], retrieved);
        }
    }

    [Fact]
    public async Task MixedOperations_WithinTransaction_ThenRollback_RestoresCorrectState()
    {
        // Arrange
        var existingKey = "existing.txt";
        var existingValue = "existing content";
        var newKey = "new.txt";
        var newValue = "new content";
        var deleteKey = "delete.txt";
        var deleteValue = "delete content";

        // Setup initial state
        await _transactionalStore.Store(existingKey, existingValue);
        await _transactionalStore.Store(deleteKey, deleteValue);

        // Act
        _transactionalStore.BeginTransaction();
        await _transactionalStore.Store(existingKey, "modified");
        await _transactionalStore.Store(newKey, newValue);
        await _transactionalStore.Remove(deleteKey);
        var rolledBack = await _transactionalStore.RollbackAsync();

        // Assert
        Assert.True(rolledBack);

        // Existing file should be restored
        var existingRetrieved = await _transactionalStore.Get(existingKey);
        Assert.Equal(existingValue, existingRetrieved);

        // New file should not exist
        var newRetrieved = await _transactionalStore.Get(newKey);
        Assert.Null(newRetrieved);

        // Deleted file should be restored
        var deleteRetrieved = await _transactionalStore.Get(deleteKey);
        Assert.Equal(deleteValue, deleteRetrieved);
    }

    [Fact]
    public async Task Store_WithoutTransaction_WorksNormally()
    {
        // Arrange
        var key = "test.txt";
        var value = "test content";

        // Act
        var result = await _transactionalStore.Store(key, value);

        // Assert
        Assert.True(result);
        var retrieved = await _transactionalStore.Get(key);
        Assert.Equal(value, retrieved);
    }

    [Fact]
    public async Task Get_ReturnsNullForNonExistentFile()
    {
        // Act
        var result = await _transactionalStore.Get("nonexistent.txt");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetPath_ReturnsCorrectPath()
    {
        // Arrange
        var key = "test.txt";

        // Act
        var path = _transactionalStore.GetPath(key);

        // Assert
        Assert.Contains(_testDirectory, path);
        Assert.EndsWith(key, path);
    }

    [Fact]
    public async Task RootFolder_CanBeSetAndRetrieved()
    {
        // Arrange
        var newRoot = Path.Combine(Path.GetTempPath(), "NewRoot");

        // Act
        _transactionalStore.RootFolder = newRoot;

        // Assert
        Assert.Equal(newRoot, _transactionalStore.RootFolder);
    }

    [Fact]
    public async Task Dispose_WithActiveTransaction_PerformsRollback()
    {
        // Arrange
        var key = "test.txt";
        var value = "test content";
        var store = new TransactionalFileStore(new TextFileStore(_testDirectory), _backupDirectory);

        // Act
        store.BeginTransaction();
        await store.Store(key, value);
        store.Dispose(); // Should rollback

        // Assert
        var retrieved = await _innerStore.Get(key);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task CommitAsync_CleansUpBackupDirectory()
    {
        // Arrange
        var key = "test.txt";
        var value = "test content";

        // Act
        _transactionalStore.BeginTransaction();
        await _transactionalStore.Store(key, value);

        // Verify backup directory was created
        var backupDirs = Directory.GetDirectories(_backupDirectory, "tx_*");
        Assert.NotEmpty(backupDirs);

        await _transactionalStore.CommitAsync();

        // Assert - backup should be cleaned up
        backupDirs = Directory.GetDirectories(_backupDirectory, "tx_*");
        Assert.Empty(backupDirs);
    }
}
