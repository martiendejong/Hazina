using Xunit;
using Hazina.AI.RAG.Embeddings;

namespace Hazina.Store.DocumentStore.Tests;

/// <summary>
/// Unit tests for HierarchicalChunkerExtensions
/// Note: These tests verify the configuration and data structure behavior.
/// Full integration tests with actual hierarchical chunking would require
/// complex mocking of the embedding provider and are better suited for
/// integration test suites.
/// </summary>
public class HierarchicalChunkerExtensionsTests
{
    [Fact]
    public void SemanticChunkingOptions_DefaultGenerateChunkSetsIsTrue()
    {
        // Arrange & Act
        var options = new SemanticChunkingOptions();

        // Assert
        Assert.True(options.GenerateChunkSets);
    }

    [Fact]
    public void SemanticChunkingOptions_DefaultChunkSetSummaryTierIsBasic()
    {
        // Arrange & Act
        var options = new SemanticChunkingOptions();

        // Assert
        Assert.Equal(MetadataExtractionTier.Basic, options.ChunkSetSummaryTier);
    }

    [Fact]
    public void SemanticChunkingOptions_CanDisableChunkSetGeneration()
    {
        // Arrange & Act
        var options = new SemanticChunkingOptions
        {
            GenerateChunkSets = false
        };

        // Assert
        Assert.False(options.GenerateChunkSets);
    }

    [Fact]
    public void SemanticChunkingOptions_CanSetChunkSetSummaryTierToLLM()
    {
        // Arrange & Act
        var options = new SemanticChunkingOptions
        {
            ChunkSetSummaryTier = MetadataExtractionTier.LLM
        };

        // Assert
        Assert.Equal(MetadataExtractionTier.LLM, options.ChunkSetSummaryTier);
    }

    [Fact]
    public void BasicMetadataExtractor_ExtractSummary_ReturnsNonEmptySummary()
    {
        // Arrange
        var extractor = new BasicMetadataExtractor();
        var text = "This is the first sentence. This is the second sentence. And a third one.";

        // Act
        var summary = extractor.ExtractSummary(text);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(summary));
    }

    [Fact]
    public void BasicMetadataExtractor_ExtractSummary_TruncatesLongText()
    {
        // Arrange
        var extractor = new BasicMetadataExtractor();
        var longText = new string('a', 300) + ". More text here.";

        // Act
        var summary = extractor.ExtractSummary(longText);

        // Assert
        // Summary should be truncated to max 200 characters
        Assert.True(summary.Length <= 200);
    }

    [Fact]
    public void BasicMetadataExtractor_ExtractSummary_CombinesShortSentences()
    {
        // Arrange
        var extractor = new BasicMetadataExtractor();
        var text = "Short. Another short sentence here.";

        // Act
        var summary = extractor.ExtractSummary(text);

        // Assert
        // Should combine both sentences if first is <50 chars
        Assert.Contains("Short", summary);
    }
}

