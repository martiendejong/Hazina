using Hazina.Tools.Services.Store;
using FluentAssertions;

namespace Hazina.Tools.Models.Tests;

public class CoreValuesTests
{
    [Fact]
    public void CoreValue_ShouldSetPropertiesCorrectly()
    {
        // Act
        var coreValue = new CoreValue
        {
            Title = "Integrity",
            Description = "We act with honesty and strong moral principles"
        };

        // Assert
        coreValue.Title.Should().Be("Integrity");
        coreValue.Description.Should().Be("We act with honesty and strong moral principles");
    }

    [Fact]
    public void CoreValues_ShouldInitializeWithEmptyList()
    {
        // Act
        var coreValues = new CoreValues();

        // Assert
        coreValues.Values.Should().NotBeNull();
        coreValues.Values.Should().BeEmpty();
    }

    [Fact]
    public void CoreValues_ShouldAddMultipleValues()
    {
        // Arrange
        var coreValues = new CoreValues();

        // Act
        coreValues.Values.Add(new CoreValue { Title = "Integrity", Description = "Honesty and ethics" });
        coreValues.Values.Add(new CoreValue { Title = "Innovation", Description = "Creative solutions" });
        coreValues.Values.Add(new CoreValue { Title = "Excellence", Description = "High quality standards" });

        // Assert
        coreValues.Values.Should().HaveCount(3);
        coreValues.Values[0].Title.Should().Be("Integrity");
        coreValues.Values[1].Title.Should().Be("Innovation");
        coreValues.Values[2].Title.Should().Be("Excellence");
    }

    [Fact]
    public void CoreValues_Example_ShouldHaveCorrectStructure()
    {
        // Arrange
        var coreValues = new CoreValues();

        // Act
        var example = coreValues._example;

        // Assert
        example.Should().NotBeNull();
        example.Values.Should().NotBeEmpty();
        example.Values.Should().HaveCountGreaterThan(0);
        example.Values[0].Title.Should().NotBeNullOrEmpty();
        example.Values[0].Description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void CoreValues_Signature_ShouldBeValid()
    {
        // Arrange
        var coreValues = new CoreValues();

        // Act
        var signature = coreValues._signature;

        // Assert
        signature.Should().NotBeNullOrEmpty();
        signature.Should().Contain("Values");
        signature.Should().Contain("Title");
        signature.Should().Contain("Description");
    }
}
