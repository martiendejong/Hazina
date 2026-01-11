using Xunit;
using System.IO;

namespace Hazina.Store.DocumentStore.Tests;

/// <summary>
/// Unit tests for ChunkSetFileStore
/// </summary>
public class ChunkSetFileStoreTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly ChunkSetFileStore _store;

    public ChunkSetFileStoreTests()
    {
        // Create a temporary test directory
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ChunkSetTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _store = new ChunkSetFileStore(_testDirectory);
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task StoreAsync_WithValidChunkSets_ReturnsTrue()
    {
        // Arrange
        var documentId = "test-doc.txt";
        var chunkSets = new[]
        {
            ChunkSet.Create(documentId, 0, new[] { "chunk:0", "chunk:1" }, "Section 1 summary", 0, 1, "Introduction"),
            ChunkSet.Create(documentId, 1, new[] { "chunk:2", "chunk:3" }, "Section 2 summary", 2, 3, "Body")
        };

        // Act
        var result = await _store.StoreAsync(documentId, chunkSets);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task StoreAsync_CreatesFileWithCorrectNaming()
    {
        // Arrange
        var documentId = "test-doc.txt";
        var chunkSets = new[]
        {
            ChunkSet.Create(documentId, 0, new[] { "chunk:0" }, "Summary", 0, 0)
        };

        // Act
        await _store.StoreAsync(documentId, chunkSets);

        // Assert
        var expectedFile = Path.Combine(_testDirectory, "test-doc.txt.chunksets.json");
        Assert.True(File.Exists(expectedFile));
    }

    [Fact]
    public async Task StoreAsync_SanitizesDocumentIdForFilename()
    {
        // Arrange
        var documentId = "folder/sub:folder\\doc.txt";
        var chunkSets = new[]
        {
            ChunkSet.Create(documentId, 0, new[] { "chunk:0" }, "Summary", 0, 0)
        };

        // Act
        await _store.StoreAsync(documentId, chunkSets);

        // Assert
        // Colons, slashes, and backslashes should be replaced with underscores
        var sanitizedFilename = "folder_sub_folder_doc.txt.chunksets.json";
        var expectedFile = Path.Combine(_testDirectory, sanitizedFilename);
        Assert.True(File.Exists(expectedFile));
    }

    [Fact]
    public async Task GetAsync_WithExistingDocument_ReturnsChunkSets()
    {
        // Arrange
        var documentId = "test-doc.txt";
        var originalChunkSets = new[]
        {
            ChunkSet.Create(documentId, 0, new[] { "chunk:0", "chunk:1" }, "Section 1 summary", 0, 1, "Introduction"),
            ChunkSet.Create(documentId, 1, new[] { "chunk:2", "chunk:3" }, "Section 2 summary", 2, 3, "Body")
        };
        await _store.StoreAsync(documentId, originalChunkSets);

        // Act
        var retrievedChunkSets = await _store.GetAsync(documentId);

        // Assert
        Assert.NotNull(retrievedChunkSets);
        var chunkSetList = retrievedChunkSets.ToList();
        Assert.Equal(2, chunkSetList.Count);
        Assert.Equal("test-doc.txt:section:0", chunkSetList[0].SetId);
        Assert.Equal("test-doc.txt:section:1", chunkSetList[1].SetId);
    }

    [Fact]
    public async Task GetAsync_WithNonExistentDocument_ReturnsEmptyEnumerable()
    {
        // Act
        var result = await _store.GetAsync("non-existent-doc.txt");

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingSetId_ReturnsChunkSet()
    {
        // Arrange
        var documentId = "test-doc.txt";
        var chunkSets = new[]
        {
            ChunkSet.Create(documentId, 0, new[] { "chunk:0" }, "Section 1", 0, 0, "Intro"),
            ChunkSet.Create(documentId, 1, new[] { "chunk:1" }, "Section 2", 1, 1, "Body")
        };
        await _store.StoreAsync(documentId, chunkSets);

        // Act
        var result = await _store.GetByIdAsync("test-doc.txt:section:1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test-doc.txt:section:1", result.SetId);
        Assert.Equal("Section 2", result.Summary);
        Assert.Equal("Body", result.SectionTitle);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentSetId_ReturnsNull()
    {
        // Act
        var result = await _store.GetByIdAsync("non-existent:section:0");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidSetIdFormat_ReturnsNull()
    {
        // Act
        var result = await _store.GetByIdAsync("invalid-format");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RemoveAsync_WithExistingDocument_DeletesFileAndReturnsTrue()
    {
        // Arrange
        var documentId = "test-doc.txt";
        var chunkSets = new[]
        {
            ChunkSet.Create(documentId, 0, new[] { "chunk:0" }, "Summary", 0, 0)
        };
        await _store.StoreAsync(documentId, chunkSets);
        var filePath = Path.Combine(_testDirectory, "test-doc.txt.chunksets.json");
        Assert.True(File.Exists(filePath)); // Verify file was created

        // Act
        var result = await _store.RemoveAsync(documentId);

        // Assert
        Assert.True(result);
        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public async Task RemoveAsync_WithNonExistentDocument_ReturnsTrue()
    {
        // Act
        var result = await _store.RemoveAsync("non-existent-doc.txt");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ListDocumentIdsAsync_WithMultipleDocuments_ReturnsAllDocumentIds()
    {
        // Arrange
        await _store.StoreAsync("doc1.txt", new[] { ChunkSet.Create("doc1.txt", 0, Array.Empty<string>(), "S", 0, 0) });
        await _store.StoreAsync("doc2.md", new[] { ChunkSet.Create("doc2.md", 0, Array.Empty<string>(), "S", 0, 0) });
        await _store.StoreAsync("doc3.pdf", new[] { ChunkSet.Create("doc3.pdf", 0, Array.Empty<string>(), "S", 0, 0) });

        // Act
        var documentIds = await _store.ListDocumentIdsAsync();

        // Assert
        var idList = documentIds.ToList();
        Assert.Equal(3, idList.Count);
        Assert.Contains("doc1.txt", idList);
        Assert.Contains("doc2.md", idList);
        Assert.Contains("doc3.pdf", idList);
    }

    [Fact]
    public async Task ListDocumentIdsAsync_WithNoDocuments_ReturnsEmptyEnumerable()
    {
        // Act
        var documentIds = await _store.ListDocumentIdsAsync();

        // Assert
        Assert.NotNull(documentIds);
        Assert.Empty(documentIds);
    }

    [Fact]
    public async Task StoreAsync_Overwrites_ExistingFile()
    {
        // Arrange
        var documentId = "test-doc.txt";
        var initialChunkSets = new[]
        {
            ChunkSet.Create(documentId, 0, new[] { "chunk:0" }, "Initial summary", 0, 0)
        };
        await _store.StoreAsync(documentId, initialChunkSets);

        var updatedChunkSets = new[]
        {
            ChunkSet.Create(documentId, 0, new[] { "chunk:0", "chunk:1" }, "Updated summary", 0, 1),
            ChunkSet.Create(documentId, 1, new[] { "chunk:2" }, "New section", 2, 2)
        };

        // Act
        await _store.StoreAsync(documentId, updatedChunkSets);

        // Assert
        var retrieved = await _store.GetAsync(documentId);
        var retrievedList = retrieved.ToList();
        Assert.Equal(2, retrievedList.Count);
        Assert.Equal("Updated summary", retrievedList[0].Summary);
        Assert.Equal(2, retrievedList[0].ChunkCount);
    }

    [Fact]
    public async Task StoreAsync_WithEmptyChunkSets_CreatesEmptyFile()
    {
        // Arrange
        var documentId = "empty-doc.txt";
        var emptyChunkSets = Array.Empty<ChunkSet>();

        // Act
        var result = await _store.StoreAsync(documentId, emptyChunkSets);

        // Assert
        Assert.True(result);
        var retrieved = await _store.GetAsync(documentId);
        Assert.Empty(retrieved);
    }

    [Fact]
    public async Task RoundTrip_PreservesAllChunkSetProperties()
    {
        // Arrange
        var documentId = "round-trip-test.txt";
        var originalChunkSet = ChunkSet.Create(
            documentId: documentId,
            sectionIndex: 5,
            chunkIds: new[] { "chunk:10", "chunk:11", "chunk:12" },
            summary: "This is a detailed summary of the section content.",
            startChunkIndex: 10,
            endChunkIndex: 12,
            sectionTitle: "Advanced Topics"
        );

        // Act
        await _store.StoreAsync(documentId, new[] { originalChunkSet });
        var retrieved = (await _store.GetAsync(documentId)).First();

        // Assert
        Assert.Equal(originalChunkSet.SetId, retrieved.SetId);
        Assert.Equal(originalChunkSet.DocumentId, retrieved.DocumentId);
        Assert.Equal(originalChunkSet.ChunkIds, retrieved.ChunkIds);
        Assert.Equal(originalChunkSet.Summary, retrieved.Summary);
        Assert.Equal(originalChunkSet.StartChunkIndex, retrieved.StartChunkIndex);
        Assert.Equal(originalChunkSet.EndChunkIndex, retrieved.EndChunkIndex);
        Assert.Equal(originalChunkSet.SectionTitle, retrieved.SectionTitle);
        Assert.Equal(originalChunkSet.ChunkCount, retrieved.ChunkCount);
    }
}
