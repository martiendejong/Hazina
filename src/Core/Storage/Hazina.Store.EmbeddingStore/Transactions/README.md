# Transactional Multi-File Updates

This module provides ACID-like transactional capabilities for file-based stores in Hazina. It ensures that batch operations either complete fully or rollback completely, preventing partial updates.

## Features

- **Two-Phase Commit**: Begin, Commit, or Rollback transactions
- **Automatic Backup**: Original files are backed up before modification
- **Rollback Safety**: Failed operations restore original state
- **Multi-File Support**: Handle multiple file operations atomically
- **Thread-Safe**: Proper locking for concurrent access

## Architecture

### ITransactionalStore Interface

Core interface providing transaction lifecycle methods:

```csharp
public interface ITransactionalStore : IDisposable
{
    void BeginTransaction();
    Task<bool> CommitAsync();
    Task<bool> RollbackAsync();
    bool IsTransactionActive { get; }
}
```

### TransactionalFileStore

File-based implementation using backup-and-restore strategy:

1. **BeginTransaction**: Creates backup directory
2. **Operations**: Backs up files before modification
3. **CommitAsync**: Deletes backup (changes are permanent)
4. **RollbackAsync**: Restores files from backup

### TransactionalTextEmbeddingStore

In-memory implementation for embedding stores:

1. **BeginTransaction**: Snapshots current state
2. **Operations**: Tracks changes in memory
3. **CommitAsync**: Keeps changes
4. **RollbackAsync**: Restores from snapshot

## Usage Examples

### Basic File Transaction

```csharp
var textStore = new TextFileStore("/data");
var transactionalStore = new TransactionalFileStore(textStore);

transactionalStore.BeginTransaction();
try
{
    await transactionalStore.Store("file1.txt", "content1");
    await transactionalStore.Store("file2.txt", "content2");
    await transactionalStore.Store("file3.txt", "content3");

    await transactionalStore.CommitAsync();
}
catch (Exception ex)
{
    await transactionalStore.RollbackAsync();
    throw;
}
```

### Embedding Store Transaction

```csharp
var embeddingStore = new TextEmbeddingMemoryStore();
var transactionalStore = new TransactionalTextEmbeddingStore(embeddingStore);

transactionalStore.BeginTransaction();
try
{
    foreach (var doc in documents)
    {
        var embedding = await GenerateEmbedding(doc);
        await transactionalStore.StoreAsync(doc.Id, embedding, doc.Checksum);
    }

    await transactionalStore.CommitAsync();
}
catch
{
    await transactionalStore.RollbackAsync();
    throw;
}
```

### Mixed Operations

```csharp
transactionalStore.BeginTransaction();
try
{
    // Update existing
    await transactionalStore.Store("existing.txt", "updated content");

    // Add new
    await transactionalStore.Store("new.txt", "new content");

    // Remove old
    await transactionalStore.Remove("obsolete.txt");

    await transactionalStore.CommitAsync();
}
catch
{
    await transactionalStore.RollbackAsync(); // All operations reverted
}
```

### Automatic Rollback on Dispose

```csharp
using (var store = new TransactionalFileStore(innerStore))
{
    store.BeginTransaction();
    await store.Store("test.txt", "content");

    // If exception or early return, Dispose() automatically rolls back
}
```

## Implementation Details

### Backup Strategy (TransactionalFileStore)

- **Backup Location**: `{backupRoot}/tx_{guid}/`
- **Backup Timing**: On first modification of each key
- **Backup Content**: Complete file copy
- **Cleanup**: On commit or successful rollback

### Snapshot Strategy (TransactionalTextEmbeddingStore)

- **Snapshot**: Dictionary of original values
- **Timing**: On first modification of each key
- **Memory**: Stores only modified keys
- **Cleanup**: On commit or successful rollback

## Error Handling

### Transaction Already Active

```csharp
store.BeginTransaction();
store.BeginTransaction(); // Throws InvalidOperationException
```

### No Active Transaction

```csharp
await store.CommitAsync(); // Throws InvalidOperationException
await store.RollbackAsync(); // Throws InvalidOperationException
```

### Commit/Rollback Failures

Both methods return `bool` indicating success:

```csharp
var success = await store.CommitAsync();
if (!success)
{
    // Handle commit failure
}
```

## Thread Safety

All implementations use proper locking:

- `lock (_lock)` protects transaction state
- Snapshot operations are atomic
- Backup operations use file system locks

## Best Practices

1. **Always use try-catch-rollback pattern**
2. **Keep transactions short** - minimize lock duration
3. **Use using statements** for automatic cleanup
4. **Check IsTransactionActive** before operations
5. **Handle rollback failures** gracefully

## Integration with Existing Stores

The transactional wrappers are designed as decorators:

```csharp
// Before
ITextStore store = new TextFileStore("/data");

// After - add transactional capability
ITextStore store = new TransactionalFileStore(new TextFileStore("/data"));
```

No changes needed to existing store implementations.

## Testing

Comprehensive test suite covers:

- Transaction lifecycle
- Commit persistence
- Rollback restoration
- Multiple file operations
- Mixed operations (update/add/remove)
- Error conditions
- Thread safety
- Backup cleanup

Run tests:

```bash
dotnet test tests/Core/Hazina.Store.EmbeddingStore.Tests/TransactionalFileStoreTests.cs
```

## Performance Considerations

### TransactionalFileStore

- **Overhead**: File copy on first modification of each key
- **Disk I/O**: 2x for modified files (backup + actual write)
- **Best for**: Small to medium batch operations (< 1000 files)

### TransactionalTextEmbeddingStore

- **Overhead**: Memory copy of original values
- **Memory**: O(n) for n modified embeddings
- **Best for**: In-memory stores with moderate batch sizes

## Future Enhancements

Potential improvements:

- **Nested transactions** support
- **Savepoint** mechanism for partial rollback
- **Write-ahead logging** for better durability
- **Async backup** for improved performance
- **Compression** for backup storage efficiency

## Related Documentation

- [ITextStore Interface](../Interfaces/ITextStore.cs)
- [IEmbeddingStore Interface](../Interfaces/IEmbeddingStore.cs)
- [TextFileStore](../Stores/File/TextFileStore.cs)
- [EmbeddingJsonFileStore](../Stores/File/EmbeddingJsonFileStore.cs)
