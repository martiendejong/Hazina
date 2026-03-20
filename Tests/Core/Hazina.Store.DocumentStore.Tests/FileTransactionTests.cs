using System;
using System.IO;
using System.Threading.Tasks;
using Hazina.Store.Transactions;
using Xunit;

namespace Hazina.Store.DocumentStore.Tests;

/// <summary>
/// Unit tests for <see cref="FileTransaction"/>.
/// </summary>
public class FileTransactionTests : IDisposable
{
    private readonly string _testDir;

    public FileTransactionTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"FileTransactionTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Happy path
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Commit_WritesAllFiles_WhenAllSucceed()
    {
        // Arrange
        var pathA = Path.Combine(_testDir, "a.txt");
        var pathB = Path.Combine(_testDir, "b.txt");

        using var tx = new FileTransaction();
        tx.BeginTransaction();

        // Act
        await tx.WriteFileAsync(pathA, "content-A");
        await tx.WriteFileAsync(pathB, "content-B");
        await tx.CommitAsync();

        // Assert
        Assert.Equal("content-A", await File.ReadAllTextAsync(pathA));
        Assert.Equal("content-B", await File.ReadAllTextAsync(pathB));
    }

    [Fact]
    public async Task Commit_NoTempFilesRemain_AfterSuccess()
    {
        var path = Path.Combine(_testDir, "file.txt");

        using var tx = new FileTransaction();
        tx.BeginTransaction();
        await tx.WriteFileAsync(path, "hello");
        await tx.CommitAsync();

        // Only the target file should exist; no .tmp or .bak files.
        var extras = Directory.GetFiles(_testDir, "*.tmp")
            .Concat(Directory.GetFiles(_testDir, "*.bak"))
            .ToArray();

        Assert.Empty(extras);
    }

    [Fact]
    public async Task Commit_OverwritesExistingFiles()
    {
        var path = Path.Combine(_testDir, "existing.txt");
        await File.WriteAllTextAsync(path, "original");

        using var tx = new FileTransaction();
        tx.BeginTransaction();
        await tx.WriteFileAsync(path, "updated");
        await tx.CommitAsync();

        Assert.Equal("updated", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task Commit_CreatesParentDirectory_IfMissing()
    {
        var nested = Path.Combine(_testDir, "sub", "dir", "file.txt");

        using var tx = new FileTransaction();
        tx.BeginTransaction();
        await tx.WriteFileAsync(nested, "nested-content");
        await tx.CommitAsync();

        Assert.Equal("nested-content", await File.ReadAllTextAsync(nested));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Rollback
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Rollback_LeavesNoStagedFiles_AndRestoresOriginals()
    {
        var path = Path.Combine(_testDir, "restore.txt");
        await File.WriteAllTextAsync(path, "original");

        using var tx = new FileTransaction();
        tx.BeginTransaction();
        await tx.WriteFileAsync(path, "staged-content");
        tx.Rollback();

        // Original content should be intact.
        Assert.Equal("original", await File.ReadAllTextAsync(path));

        // No .tmp or .bak leftovers.
        var extras = Directory.GetFiles(_testDir, "*.tmp")
            .Concat(Directory.GetFiles(_testDir, "*.bak"))
            .ToArray();
        Assert.Empty(extras);
    }

    [Fact]
    public async Task Rollback_DeletesStagedFile_WhenNoOriginalExisted()
    {
        var path = Path.Combine(_testDir, "new-file.txt");

        using var tx = new FileTransaction();
        tx.BeginTransaction();
        await tx.WriteFileAsync(path, "will-be-rolled-back");
        tx.Rollback();

        // Target must not exist (was never committed).
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task Rollback_IsNoOp_WhenNoTransactionActive()
    {
        // Should not throw.
        using var tx = new FileTransaction();
        tx.Rollback();
    }

    [Fact]
    public async Task Dispose_AutoRollsBack_WhenNotCommitted()
    {
        var path = Path.Combine(_testDir, "auto-rollback.txt");
        await File.WriteAllTextAsync(path, "original");

        // Deliberately do NOT commit.
        using (var tx = new FileTransaction())
        {
            tx.BeginTransaction();
            await tx.WriteFileAsync(path, "modified");
        } // Dispose triggers auto-rollback.

        Assert.Equal("original", await File.ReadAllTextAsync(path));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Guard conditions
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void BeginTransaction_Throws_WhenAlreadyActive()
    {
        using var tx = new FileTransaction();
        tx.BeginTransaction();

        Assert.Throws<InvalidOperationException>(() => tx.BeginTransaction());
    }

    [Fact]
    public async Task WriteFile_Throws_WhenNoTransactionActive()
    {
        using var tx = new FileTransaction();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => tx.WriteFileAsync(Path.Combine(_testDir, "x.txt"), "x"));
    }

    [Fact]
    public async Task Commit_Throws_WhenNoTransactionActive()
    {
        using var tx = new FileTransaction();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => tx.CommitAsync());
    }

    [Fact]
    public async Task AllOperations_Throw_AfterDispose()
    {
        var tx = new FileTransaction();
        tx.Dispose();

        Assert.Throws<ObjectDisposedException>(() => tx.BeginTransaction());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => tx.WriteFileAsync("x", "y"));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => tx.CommitAsync());
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Multi-file atomicity
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CommitAsync_IsActive_ReturnsFalseAfterCommit()
    {
        using var tx = new FileTransaction();
        tx.BeginTransaction();

        Assert.True(tx.IsActive);

        var path = Path.Combine(_testDir, "active.txt");
        await tx.WriteFileAsync(path, "data");
        await tx.CommitAsync();

        Assert.False(tx.IsActive);
    }

    [Fact]
    public async Task Rollback_EndsTransaction()
    {
        using var tx = new FileTransaction();
        tx.BeginTransaction();

        var path = Path.Combine(_testDir, "rollback-ends.txt");
        await tx.WriteFileAsync(path, "data");
        tx.Rollback();

        Assert.False(tx.IsActive);
    }
}
