using Xunit;

namespace Hazina.Store.DocumentStore.Tests;

/// <summary>
/// Unit tests for ChunkSet data model
/// </summary>
public class ChunkSetTests
{
    [Fact]
    public void Create_WithValidParameters_ReturnsChunkSet()
    {
        // Arrange
        var documentId = "test-doc.txt";
        var sectionIndex = 0;
        var chunkIds = new[] { "test-doc.txt:chunk:0", "test-doc.txt:chunk:1", "test-doc.txt:chunk:2" };
        var summary = "This section covers the introduction to the topic.";
        var startChunkIndex = 0;
        var endChunkIndex = 2;
        var sectionTitle = "Introduction";

        // Act
        var chunkSet = ChunkSet.Create(
            documentId,
            sectionIndex,
            chunkIds,
            summary,
            startChunkIndex,
            endChunkIndex,
            sectionTitle
        );

        // Assert
        Assert.NotNull(chunkSet);
        Assert.Equal("test-doc.txt:section:0", chunkSet.SetId);
        Assert.Equal(documentId, chunkSet.DocumentId);
        Assert.Equal(3, chunkSet.ChunkIds.Count);
        Assert.Equal(summary, chunkSet.Summary);
        Assert.Equal(startChunkIndex, chunkSet.StartChunkIndex);
        Assert.Equal(endChunkIndex, chunkSet.EndChunkIndex);
        Assert.Equal(sectionTitle, chunkSet.SectionTitle);
        Assert.Equal(3, chunkSet.ChunkCount);
    }

    [Fact]
    public void Create_WithoutSectionTitle_ReturnsChunkSetWithNullTitle()
    {
        // Arrange
        var documentId = "test-doc.txt";
        var sectionIndex = 1;
        var chunkIds = new[] { "test-doc.txt:chunk:3", "test-doc.txt:chunk:4" };
        var summary = "This section covers advanced topics.";
        var startChunkIndex = 3;
        var endChunkIndex = 4;

        // Act
        var chunkSet = ChunkSet.Create(
            documentId,
            sectionIndex,
            chunkIds,
            summary,
            startChunkIndex,
            endChunkIndex
        );

        // Assert
        Assert.NotNull(chunkSet);
        Assert.Equal("test-doc.txt:section:1", chunkSet.SetId);
        Assert.Null(chunkSet.SectionTitle);
    }

    [Fact]
    public void ChunkCount_ReturnsCorrectCount()
    {
        // Arrange
        var chunkSet = ChunkSet.Create(
            "doc.txt",
            0,
            new[] { "chunk1", "chunk2", "chunk3", "chunk4", "chunk5" },
            "Test summary",
            0,
            4
        );

        // Act
        var count = chunkSet.ChunkCount;

        // Assert
        Assert.Equal(5, count);
    }

    [Fact]
    public void ChunkCount_WithEmptyChunkIds_ReturnsZero()
    {
        // Arrange
        var chunkSet = ChunkSet.Create(
            "doc.txt",
            0,
            Array.Empty<string>(),
            "Test summary",
            0,
            0
        );

        // Act
        var count = chunkSet.ChunkCount;

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public void SetId_Format_FollowsDocumentIdSectionIndexPattern()
    {
        // Arrange & Act
        var chunkSet1 = ChunkSet.Create("doc1.txt", 0, Array.Empty<string>(), "Summary", 0, 0);
        var chunkSet2 = ChunkSet.Create("doc2.md", 5, Array.Empty<string>(), "Summary", 10, 15);
        var chunkSet3 = ChunkSet.Create("folder/doc.txt", 2, Array.Empty<string>(), "Summary", 5, 8);

        // Assert
        Assert.Equal("doc1.txt:section:0", chunkSet1.SetId);
        Assert.Equal("doc2.md:section:5", chunkSet2.SetId);
        Assert.Equal("folder/doc.txt:section:2", chunkSet3.SetId);
    }

    [Fact]
    public void Created_IsSetAutomatically()
    {
        // Arrange
        var before = DateTime.UtcNow.AddSeconds(-1);

        // Act
        var chunkSet = ChunkSet.Create(
            "doc.txt",
            0,
            Array.Empty<string>(),
            "Summary",
            0,
            0
        );

        var after = DateTime.UtcNow.AddSeconds(1);

        // Assert
        Assert.True(chunkSet.Created >= before);
        Assert.True(chunkSet.Created <= after);
    }

    [Fact]
    public void LastModified_IsSetAutomatically()
    {
        // Arrange
        var before = DateTime.UtcNow.AddSeconds(-1);

        // Act
        var chunkSet = ChunkSet.Create(
            "doc.txt",
            0,
            Array.Empty<string>(),
            "Summary",
            0,
            0
        );

        var after = DateTime.UtcNow.AddSeconds(1);

        // Assert
        Assert.True(chunkSet.LastModified >= before);
        Assert.True(chunkSet.LastModified <= after);
    }
}
