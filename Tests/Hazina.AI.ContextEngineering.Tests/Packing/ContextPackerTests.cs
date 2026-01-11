using FluentAssertions;
using Hazina.AI.ContextEngineering.Configuration;
using Hazina.AI.ContextEngineering.Models;
using Hazina.AI.ContextEngineering.Packing;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hazina.AI.ContextEngineering.Tests.Packing;

public class ContextPackerTests
{
    private readonly ContextPacker _packer = new(NullLogger<ContextPacker>.Instance);

    [Fact]
    public async Task PackAsync_ShouldIncludeAllConfiguredSections()
    {
        // Arrange
        var results = new List<RetrievalResult>
        {
            new() { Id = "1", Content = "Fact 1", Score = 0.9, Source = "facts" },
            new() { Id = "2", Content = "Chunk 1", Score = 0.8, Source = "semantic" }
        };
        var query = "Test query";
        var policy = new PackingPolicy
        {
            Sections = new List<string> { "facts", "chunks", "query" },
            MaxTokens = 10000
        };

        // Act
        var context = await _packer.PackAsync(results, query, policy);

        // Assert
        context.Should().Contain("[FACTS]");
        context.Should().Contain("[CHUNKS]");
        context.Should().Contain("[QUERY]");
        context.Should().Contain("Fact 1");
        context.Should().Contain("Chunk 1");
        context.Should().Contain("Test query");
    }

    [Fact]
    public async Task PackAsync_ShouldIncludeScoresWhenConfigured()
    {
        // Arrange
        var results = new List<RetrievalResult>
        {
            new() { Id = "1", Content = "Fact 1", Score = 0.92, Source = "facts" }
        };
        var policy = new PackingPolicy
        {
            Sections = new List<string> { "facts" },
            IncludeScores = true,
            MaxTokens = 10000
        };

        // Act
        var context = await _packer.PackAsync(results, "query", policy);

        // Assert
        // Check for score (format may vary by culture: "0.92" or "0,92")
        (context.Contains("0.92") || context.Contains("0,92")).Should().BeTrue();
    }

    [Fact]
    public async Task PackAsync_ShouldExcludeScoresWhenNotConfigured()
    {
        // Arrange
        var results = new List<RetrievalResult>
        {
            new() { Id = "1", Content = "Fact 1", Score = 0.92, Source = "facts" }
        };
        var policy = new PackingPolicy
        {
            Sections = new List<string> { "facts" },
            IncludeScores = false,
            MaxTokens = 10000
        };

        // Act
        var context = await _packer.PackAsync(results, "query", policy);

        // Assert
        context.Should().Contain("Fact 1");
        context.Should().NotContain("score:");
    }

    [Fact]
    public async Task PackAsync_ShouldIncludeTagsWhenConfigured()
    {
        // Arrange
        var results = new List<RetrievalResult>
        {
            new()
            {
                Id = "1",
                Content = "Fact 1",
                Score = 0.9,
                Source = "facts",
                Tags = new List<string> { "tag1", "tag2" }
            }
        };
        var policy = new PackingPolicy
        {
            Sections = new List<string> { "facts" },
            IncludeTags = true,
            MaxTokens = 10000
        };

        // Act
        var context = await _packer.PackAsync(results, "query", policy);

        // Assert
        context.Should().Contain("tag1");
        context.Should().Contain("tag2");
    }

    [Fact]
    public async Task PackAsync_ShouldRespectSectionOrder()
    {
        // Arrange
        var results = new List<RetrievalResult>
        {
            new() { Id = "1", Content = "Fact 1", Score = 0.9, Source = "facts" },
            new() { Id = "2", Content = "Chunk 1", Score = 0.8, Source = "semantic" }
        };
        var policy = new PackingPolicy
        {
            Sections = new List<string> { "query", "facts", "chunks" },
            MaxTokens = 10000
        };

        // Act
        var context = await _packer.PackAsync(results, "Test query", policy);

        // Assert
        var queryIndex = context.IndexOf("[QUERY]");
        var factsIndex = context.IndexOf("[FACTS]");
        var chunksIndex = context.IndexOf("[CHUNKS]");

        queryIndex.Should().BeLessThan(factsIndex);
        factsIndex.Should().BeLessThan(chunksIndex);
    }

    [Fact]
    public async Task PackAsync_ShouldTrimWhenExceedsMaxTokens()
    {
        // Arrange
        var longContent = new string('x', 10000); // Very long content
        var results = new List<RetrievalResult>
        {
            new() { Id = "1", Content = longContent, Score = 0.9, Source = "semantic" }
        };
        var policy = new PackingPolicy
        {
            Sections = new List<string> { "chunks", "query" },
            MaxTokens = 100,
            TrimToFit = true,
            TrimPriority = new List<string> { "query", "chunks" } // Trim chunks first
        };

        // Act
        var context = await _packer.PackAsync(results, "query", policy);

        // Assert
        var tokens = _packer.EstimateTokens(context);
        tokens.Should().BeLessThanOrEqualTo(110); // Small tolerance for headers/formatting
        context.Should().Contain("query"); // Query should be preserved (higher priority)
    }

    [Fact]
    public async Task PackAsync_ShouldThrowWhenExceedsMaxTokensAndTrimDisabled()
    {
        // Arrange
        var longContent = new string('x', 100000);
        var results = new List<RetrievalResult>
        {
            new() { Id = "1", Content = longContent, Score = 0.9, Source = "semantic" }
        };
        var policy = new PackingPolicy
        {
            Sections = new List<string> { "chunks" },
            MaxTokens = 100,
            TrimToFit = false
        };

        // Act & Assert
        await _packer.Invoking(p => p.PackAsync(results, "query", policy))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*exceeds MaxTokens*");
    }

    [Fact]
    public void EstimateTokens_ShouldReturnZeroForEmpty()
    {
        // Act
        var tokens = _packer.EstimateTokens(string.Empty);

        // Assert
        tokens.Should().Be(0);
    }

    [Fact]
    public void EstimateTokens_ShouldEstimateCorrectly()
    {
        // Arrange
        var text = new string('x', 400); // 400 chars

        // Act
        var tokens = _packer.EstimateTokens(text);

        // Assert
        // Should be ~100 tokens (400 / 4)
        tokens.Should().BeInRange(95, 105);
    }

    [Fact]
    public async Task PackAsync_WithMetadataSection_ShouldIncludeMetadataResults()
    {
        // Arrange
        var results = new List<RetrievalResult>
        {
            new()
            {
                Id = "1",
                Content = "Document Title",
                Score = 0.9,
                Source = "metadata",
                Tags = new List<string> { "tag1", "tag2" }
            }
        };
        var policy = new PackingPolicy
        {
            Sections = new List<string> { "metadata" },
            MaxTokens = 10000
        };

        // Act
        var context = await _packer.PackAsync(results, "query", policy);

        // Assert
        context.Should().Contain("[METADATA]");
        context.Should().Contain("tag1");
        context.Should().Contain("tag2");
        context.Should().Contain("Documents: 1 relevant documents found");
    }

    [Fact]
    public async Task PackAsync_WithTagsSection_ShouldShowTagFrequency()
    {
        // Arrange
        var results = new List<RetrievalResult>
        {
            new() { Id = "1", Content = "R1", Score = 0.9, Source = "semantic", Tags = new List<string> { "tag1", "tag2" } },
            new() { Id = "2", Content = "R2", Score = 0.8, Source = "semantic", Tags = new List<string> { "tag1", "tag3" } },
            new() { Id = "3", Content = "R3", Score = 0.7, Source = "semantic", Tags = new List<string> { "tag1" } }
        };
        var policy = new PackingPolicy
        {
            Sections = new List<string> { "tags" },
            IncludeSourceInfo = true,
            MaxTokens = 10000
        };

        // Act
        var context = await _packer.PackAsync(results, "query", policy);

        // Assert
        context.Should().Contain("[TAGS]");
        context.Should().Contain("tag1");
        context.Should().Contain("appears in 3 results");
    }
}
