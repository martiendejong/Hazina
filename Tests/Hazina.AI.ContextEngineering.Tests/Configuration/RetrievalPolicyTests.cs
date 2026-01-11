using FluentAssertions;
using Hazina.AI.ContextEngineering.Configuration;

namespace Hazina.AI.ContextEngineering.Tests.Configuration;

public class RetrievalPolicyTests
{
    [Fact]
    public void Default_ShouldHaveAllRetrieversEnabled()
    {
        // Arrange & Act
        var policy = RetrievalPolicy.Default;

        // Assert
        policy.SemanticEnabled.Should().BeTrue();
        policy.FactsEnabled.Should().BeTrue();
        policy.MetadataEnabled.Should().BeTrue();
    }

    [Fact]
    public void SemanticFocused_ShouldHaveHighSemanticWeight()
    {
        // Arrange & Act
        var policy = RetrievalPolicy.SemanticFocused;

        // Assert
        policy.SemanticEnabled.Should().BeTrue();
        policy.SemanticWeight.Should().Be(0.8);
        policy.FactsWeight.Should().Be(0.2);
        policy.MetadataEnabled.Should().BeFalse();
    }

    [Fact]
    public void FactsFocused_ShouldOnlyHaveFactsEnabled()
    {
        // Arrange & Act
        var policy = RetrievalPolicy.FactsFocused;

        // Assert
        policy.SemanticEnabled.Should().BeFalse();
        policy.FactsEnabled.Should().BeTrue();
        policy.FactsWeight.Should().Be(1.0);
        policy.MetadataEnabled.Should().BeFalse();
    }

    [Fact]
    public void Validate_WhenNoRetrieversEnabled_ShouldReturnError()
    {
        // Arrange
        var policy = new RetrievalPolicy
        {
            SemanticEnabled = false,
            FactsEnabled = false,
            MetadataEnabled = false,
            LookupEnabled = false
        };

        // Act
        var errors = policy.Validate();

        // Assert
        errors.Should().Contain("At least one retriever must be enabled");
    }

    [Fact]
    public void Validate_WhenTotalWeightIsZero_ShouldReturnError()
    {
        // Arrange
        var policy = new RetrievalPolicy
        {
            SemanticEnabled = true,
            SemanticWeight = 0,
            FactsEnabled = true,
            FactsWeight = 0,
            MetadataEnabled = false // Explicitly disable metadata to ensure totalWeight = 0
        };

        // Act
        var errors = policy.Validate();

        // Assert
        errors.Should().Contain("Total weight of enabled retrievers must be > 0");
    }

    [Fact]
    public void Validate_WhenLookupEnabledButNoIds_ShouldReturnError()
    {
        // Arrange
        var policy = new RetrievalPolicy
        {
            LookupEnabled = true,
            LookupIds = null
        };

        // Act
        var errors = policy.Validate();

        // Assert
        errors.Should().Contain("LookupIds must be provided when LookupEnabled is true");
    }

    [Fact]
    public void Validate_WhenValid_ShouldReturnNoErrors()
    {
        // Arrange
        var policy = RetrievalPolicy.Default;

        // Act
        var errors = policy.Validate();

        // Assert
        errors.Should().BeEmpty();
    }
}
