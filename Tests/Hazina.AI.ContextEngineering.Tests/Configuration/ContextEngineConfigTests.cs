using FluentAssertions;
using Hazina.AI.ContextEngineering.Configuration;
using Hazina.AI.ContextEngineering.Fusion;

namespace Hazina.AI.ContextEngineering.Tests.Configuration;

public class ContextEngineConfigTests
{
    [Fact]
    public void Default_ShouldBeValid()
    {
        // Arrange & Act
        var config = ContextEngineConfig.Default;

        // Assert
        config.Validate().Should().BeEmpty();
        config.Name.Should().Be("Default");
    }

    [Fact]
    public void AllPresets_ShouldBeValid()
    {
        // Arrange
        var presets = new[]
        {
            ContextEngineConfig.Default,
            ContextEngineConfig.SemanticFocused,
            ContextEngineConfig.FactsFocused,
            ContextEngineConfig.TagFocused,
            ContextEngineConfig.RecencyFocused,
            ContextEngineConfig.Compact,
            ContextEngineConfig.Comprehensive
        };

        // Act & Assert
        foreach (var preset in presets)
        {
            preset.Validate().Should().BeEmpty($"preset {preset.Name} should be valid");
        }
    }

    [Fact]
    public void ToJson_ShouldSerialize()
    {
        // Arrange
        var config = ContextEngineConfig.Default;

        // Act
        var json = config.ToJson();

        // Assert
        json.Should().Contain("\"Name\"");
        json.Should().Contain("\"RetrievalPolicy\"");
        json.Should().Contain("\"ScoringPolicy\"");
        json.Should().Contain("\"PackingPolicy\"");
    }

    [Fact]
    public void FromJson_ShouldDeserialize()
    {
        // Arrange
        var original = new ContextEngineConfig
        {
            Name = "Test Config",
            FinalTopK = 15,
            RetrievalPolicy = new RetrievalPolicy { SemanticTopK = 12 },
            ScoringPolicy = new ScoringPolicy { Strategy = FusionStrategy.ReciprocalRankFusion },
            PackingPolicy = new PackingPolicy { MaxTokens = 8000 }
        };
        var json = original.ToJson();

        // Act
        var deserialized = ContextEngineConfig.FromJson(json);

        // Assert
        deserialized.Name.Should().Be("Test Config");
        deserialized.FinalTopK.Should().Be(15);
        deserialized.RetrievalPolicy.SemanticTopK.Should().Be(12);
        deserialized.ScoringPolicy.Strategy.Should().Be(FusionStrategy.ReciprocalRankFusion);
        deserialized.PackingPolicy.MaxTokens.Should().Be(8000);
    }

    [Fact]
    public void Validate_WhenFinalTopKInvalid_ShouldReturnError()
    {
        // Arrange
        var config = ContextEngineConfig.Default;
        config.FinalTopK = 0;

        // Act
        var errors = config.Validate();

        // Assert
        errors.Should().Contain("FinalTopK must be between 1 and 100");
    }

    [Fact]
    public void Validate_ShouldIncludeNestedPolicyErrors()
    {
        // Arrange
        var config = new ContextEngineConfig
        {
            RetrievalPolicy = new RetrievalPolicy
            {
                SemanticEnabled = false,
                FactsEnabled = false,
                MetadataEnabled = false
            }
        };

        // Act
        var errors = config.Validate();

        // Assert
        errors.Should().Contain("At least one retriever must be enabled");
    }

    [Fact]
    public void Compact_ShouldHaveLowTokenLimit()
    {
        // Arrange & Act
        var config = ContextEngineConfig.Compact;

        // Assert
        config.PackingPolicy.MaxTokens.Should().Be(4000);
        config.FinalTopK.Should().Be(5);
    }

    [Fact]
    public void Comprehensive_ShouldHaveHighTokenLimit()
    {
        // Arrange & Act
        var config = ContextEngineConfig.Comprehensive;

        // Assert
        config.PackingPolicy.MaxTokens.Should().Be(32000);
        config.FinalTopK.Should().Be(20);
    }
}
