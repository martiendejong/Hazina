using Hazina.Store.EmbeddingStore.Transactions;
using Xunit;

namespace Hazina.Store.EmbeddingStore.Tests.Transactions;

public class TransactionalFileStoreTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly TransactionalFileStore _store;

    public TransactionalFileStoreTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"hazina_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _store = new TransactionalFileStore(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task CommitAsync_WritesMultipleFiles_Successfully()
    {
        _store.BeginTransaction();
        _store.QueueWrite("file1.txt", "Content 1");
        _store.QueueWrite("file2.txt", "Content 2");
        _store.QueueWrite("file3.txt", "Content 3");

        var result = await _store.CommitAsync();

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(_testDirectory, "file1.txt")));
        Assert.True(File.Exists(Path.Combine(_testDirectory, "file2.txt")));
        Assert.True(File.Exists(Path.Combine(_testDirectory, "file3.txt")));
        Assert.Equal("Content 1", await File.ReadAllTextAsync(Path.Combine(_testDirectory, "file1.txt")));
    }

    [Fact]
    public async Task CommitAsync_WithBinaryContent_WritesSuccessfully()
    {
        var binaryData = new byte[] { 1, 2, 3, 4, 5 };

        _store.BeginTransaction();
        _store.QueueWrite("binary.dat", binaryData);

        var result = await _store.CommitAsync();

        Assert.True(result.Success);
        var writtenData = await File.ReadAllBytesAsync(Path.Combine(_testDirectory, "binary.dat"));
        Assert.Equal(binaryData, writtenData);
    }

    [Fact]
    public async Task CommitAsync_DeletesFiles_Successfully()
    {
        // Create initial file
        var filePath = Path.Combine(_testDirectory, "to-delete.txt");
        await File.WriteAllTextAsync(filePath, "Delete me");

        _store.BeginTransaction();
        _store.QueueDelete("to-delete.txt");

        var result = await _store.CommitAsync();

        Assert.True(result.Success);
        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public async Task CommitAsync_MovesFiles_Successfully()
    {
        // Create initial file
        var sourcePath = Path.Combine(_testDirectory, "source.txt");
        await File.WriteAllTextAsync(sourcePath, "Move me");

        _store.BeginTransaction();
        _store.QueueMove("source.txt", "destination.txt");

        var result = await _store.CommitAsync();

        Assert.True(result.Success);
        Assert.False(File.Exists(sourcePath));
        Assert.True(File.Exists(Path.Combine(_testDirectory, "destination.txt")));
        Assert.Equal("Move me", await File.ReadAllTextAsync(Path.Combine(_testDirectory, "destination.txt")));
    }

    [Fact]
    public async Task RollbackAsync_RestoresOriginalFiles()
    {
        // Create initial files
        var file1Path = Path.Combine(_testDirectory, "file1.txt");
        await File.WriteAllTextAsync(file1Path, "Original content");

        _store.BeginTransaction();
        _store.QueueWrite("file1.txt", "Modified content");
        _store.QueueWrite("file2.txt", "New file");

        // Simulate rollback
        await _store.RollbackAsync();

        // Original file should be restored
        Assert.Equal("Original content", await File.ReadAllTextAsync(file1Path));
        // New file should not exist
        Assert.False(File.Exists(Path.Combine(_testDirectory, "file2.txt")));
    }

    [Fact]
    public async Task CommitAsync_WithFailure_RollsBackAutomatically()
    {
        // Create initial file
        var file1Path = Path.Combine(_testDirectory, "file1.txt");
        await File.WriteAllTextAsync(file1Path, "Original content");

        _store.BeginTransaction();
        _store.QueueWrite("file1.txt", "Modified content");

        // Queue an operation that will fail (invalid path characters)
        _store.QueueWrite("invalid<>path.txt", "This will fail");

        var result = await _store.CommitAsync();

        // Transaction should fail
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);

        // Original file should be restored
        Assert.Equal("Original content", await File.ReadAllTextAsync(file1Path));
    }

    [Fact]
    public async Task CommitAsync_WithNestedDirectories_CreatesDirectoriesAutomatically()
    {
        _store.BeginTransaction();
        _store.QueueWrite("sub/folder/file.txt", "Nested content");

        var result = await _store.CommitAsync();

        Assert.True(result.Success);
        var filePath = Path.Combine(_testDirectory, "sub", "folder", "file.txt");
        Assert.True(File.Exists(filePath));
        Assert.Equal("Nested content", await File.ReadAllTextAsync(filePath));
    }

    [Fact]
    public void BeginTransaction_WhenAlreadyActive_ThrowsException()
    {
        _store.BeginTransaction();

        Assert.Throws<InvalidOperationException>(() => _store.BeginTransaction());
    }

    [Fact]
    public void QueueWrite_WithoutActiveTransaction_ThrowsException()
    {
        Assert.Throws<InvalidOperationException>(() => _store.QueueWrite("file.txt", "content"));
    }

    [Fact]
    public async Task CommitAsync_AllowsNewTransactionAfterCommit()
    {
        _store.BeginTransaction();
        _store.QueueWrite("file1.txt", "Content 1");
        await _store.CommitAsync();

        // Should allow new transaction
        _store.BeginTransaction();
        _store.QueueWrite("file2.txt", "Content 2");
        var result = await _store.CommitAsync();

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(_testDirectory, "file2.txt")));
    }
}
