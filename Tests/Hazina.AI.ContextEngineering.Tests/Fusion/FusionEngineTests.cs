using FluentAssertions;
using Hazina.AI.ContextEngineering.Fusion;
using Hazina.AI.ContextEngineering.Models;

namespace Hazina.AI.ContextEngineering.Tests.Fusion;

public class FusionEngineTests
{
    private readonly FusionEngine _fusionEngine = new();

    [Fact]
    public void Fuse_WithWeightedSum_ShouldCombineScores()
    {
        // Arrange
        var retrieverResults = new Dictionary<string, List<RetrievalResult>>
        {
            ["semantic"] = new List<RetrievalResult>
            {
                new() { Id = "1", Content = "Result 1", Score = 0.9 },
                new() { Id = "2", Content = "Result 2", Score = 0.7 }
            },
            ["facts"] = new List<RetrievalResult>
            {
                new() { Id = "1", Content = "Result 1", Score = 0.8 },
                new() { Id = "3", Content = "Result 3", Score = 0.6 }
            }
        };
        var weights = new Dictionary<string, double>
        {
            ["semantic"] = 0.7,
            ["facts"] = 0.3
        };

        // Act
        var results = _fusionEngine.Fuse(retrieverResults, weights, FusionStrategy.WeightedSum, topK: 10);

        // Assert
        results.Should().HaveCount(3);

        // ID "1" appears in both retrievers, so should have a combined score
        var result1 = results.First(r => r.Id == "1");
        result1.Score.Should().BeGreaterThan(0);
        result1.Should().NotBeNull(); // Verify the result exists

        // Results should be ordered by score (descending)
        results[0].Score.Should().BeGreaterThanOrEqualTo(results[1].Score);
        results[1].Score.Should().BeGreaterThanOrEqualTo(results[2].Score);
    }

    [Fact]
    public void Fuse_WithReciprocalRankFusion_ShouldUseRanks()
    {
        // Arrange
        var retrieverResults = new Dictionary<string, List<RetrievalResult>>
        {
            ["semantic"] = new List<RetrievalResult>
            {
                new() { Id = "1", Content = "Result 1", Score = 0.9 },
                new() { Id = "2", Content = "Result 2", Score = 0.7 }
            },
            ["facts"] = new List<RetrievalResult>
            {
                new() { Id = "2", Content = "Result 2", Score = 0.8 },
                new() { Id = "1", Content = "Result 1", Score = 0.6 }
            }
        };
        var weights = new Dictionary<string, double>
        {
            ["semantic"] = 0.6,
            ["facts"] = 0.4
        };

        // Act
        var results = _fusionEngine.Fuse(retrieverResults, weights, FusionStrategy.ReciprocalRankFusion, topK: 10);

        // Assert
        results.Should().HaveCount(2);

        // Both appear in both lists, so both should have good scores
        // ID "1": semantic rank 1, facts rank 2
        // ID "2": semantic rank 2, facts rank 1
        results[0].Score.Should().BeGreaterThan(0);
        results[1].Score.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Fuse_WithMaxScore_ShouldTakeHighestScore()
    {
        // Arrange
        var retrieverResults = new Dictionary<string, List<RetrievalResult>>
        {
            ["semantic"] = new List<RetrievalResult>
            {
                new() { Id = "1", Content = "Result 1", Score = 0.9 }
            },
            ["facts"] = new List<RetrievalResult>
            {
                new() { Id = "1", Content = "Result 1", Score = 0.6 }
            }
        };
        var weights = new Dictionary<string, double>
        {
            ["semantic"] = 0.6,
            ["facts"] = 0.4
        };

        // Act
        var results = _fusionEngine.Fuse(retrieverResults, weights, FusionStrategy.MaxScore, topK: 10);

        // Assert
        results.Should().HaveCount(1);
        results[0].Id.Should().Be("1");
        results[0].Score.Should().Be(0.9); // Should take the max (0.9 vs 0.6)
    }

    [Fact]
    public void Fuse_ShouldDeduplicateById()
    {
        // Arrange
        var retrieverResults = new Dictionary<string, List<RetrievalResult>>
        {
            ["semantic"] = new List<RetrievalResult>
            {
                new() { Id = "1", Content = "Result 1", Score = 0.9 },
                new() { Id = "2", Content = "Result 2", Score = 0.7 }
            },
            ["facts"] = new List<RetrievalResult>
            {
                new() { Id = "1", Content = "Result 1 from facts", Score = 0.8 },
                new() { Id = "2", Content = "Result 2 from facts", Score = 0.6 }
            }
        };
        var weights = new Dictionary<string, double>
        {
            ["semantic"] = 0.6,
            ["facts"] = 0.4
        };

        // Act
        var results = _fusionEngine.Fuse(retrieverResults, weights, FusionStrategy.WeightedSum, topK: 10);

        // Assert
        results.Should().HaveCount(2); // Only 2 unique IDs
        results.Select(r => r.Id).Should().BeEquivalentTo(new[] { "1", "2" });
    }

    [Fact]
    public void Fuse_ShouldRespectTopK()
    {
        // Arrange
        var retrieverResults = new Dictionary<string, List<RetrievalResult>>
        {
            ["semantic"] = new List<RetrievalResult>
            {
                new() { Id = "1", Content = "Result 1", Score = 0.9 },
                new() { Id = "2", Content = "Result 2", Score = 0.8 },
                new() { Id = "3", Content = "Result 3", Score = 0.7 },
                new() { Id = "4", Content = "Result 4", Score = 0.6 },
                new() { Id = "5", Content = "Result 5", Score = 0.5 }
            }
        };
        var weights = new Dictionary<string, double>
        {
            ["semantic"] = 1.0
        };

        // Act
        var results = _fusionEngine.Fuse(retrieverResults, weights, FusionStrategy.WeightedSum, topK: 3);

        // Assert
        results.Should().HaveCount(3);
        results[0].Id.Should().Be("1");
        results[1].Id.Should().Be("2");
        results[2].Id.Should().Be("3");
    }

    [Fact]
    public void Fuse_WithEmptyResults_ShouldReturnEmpty()
    {
        // Arrange
        var retrieverResults = new Dictionary<string, List<RetrievalResult>>();
        var weights = new Dictionary<string, double>();

        // Act
        var results = _fusionEngine.Fuse(retrieverResults, weights, FusionStrategy.WeightedSum, topK: 10);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void Fuse_ShouldPreserveMetadata()
    {
        // Arrange
        var retrieverResults = new Dictionary<string, List<RetrievalResult>>
        {
            ["semantic"] = new List<RetrievalResult>
            {
                new()
                {
                    Id = "1",
                    Content = "Result 1",
                    Score = 0.9,
                    Tags = new List<string> { "tag1", "tag2" },
                    Metadata = new Dictionary<string, object> { ["key"] = "value" }
                }
            }
        };
        var weights = new Dictionary<string, double> { ["semantic"] = 1.0 };

        // Act
        var results = _fusionEngine.Fuse(retrieverResults, weights, FusionStrategy.WeightedSum, topK: 10);

        // Assert
        results.Should().HaveCount(1);
        results[0].Tags.Should().BeEquivalentTo(new[] { "tag1", "tag2" });
        results[0].Metadata.Should().ContainKey("key");
    }
}
