using Hazina.Tools.Models;
using FluentAssertions;

namespace Hazina.Tools.Models.Tests;

public class ToneOfVoiceTests
{
    [Fact]
    public void ToneOfVoice_ShouldInitializeWithEmptyList()
    {
        // Act
        var toneOfVoice = new ToneOfVoice();

        // Assert
        toneOfVoice.ToneOfVoiceDescriptors.Should().NotBeNull();
        toneOfVoice.ToneOfVoiceDescriptors.Should().BeEmpty();
    }

    [Fact]
    public void ToneOfVoice_ShouldAddDescriptors()
    {
        // Arrange
        var toneOfVoice = new ToneOfVoice();

        // Act
        toneOfVoice.ToneOfVoiceDescriptors.Add("Friendly");
        toneOfVoice.ToneOfVoiceDescriptors.Add("Professional");
        toneOfVoice.ToneOfVoiceDescriptors.Add("Confident");

        // Assert
        toneOfVoice.ToneOfVoiceDescriptors.Should().HaveCount(3);
        toneOfVoice.ToneOfVoiceDescriptors.Should().Contain("Friendly");
        toneOfVoice.ToneOfVoiceDescriptors.Should().Contain("Professional");
        toneOfVoice.ToneOfVoiceDescriptors.Should().Contain("Confident");
    }

    [Fact]
    public void ToneOfVoice_Example_ShouldHaveCorrectStructure()
    {
        // Arrange
        var toneOfVoice = new ToneOfVoice();

        // Act
        var example = toneOfVoice._example;

        // Assert
        example.Should().NotBeNull();
        example.ToneOfVoiceDescriptors.Should().NotBeEmpty();
        example.ToneOfVoiceDescriptors.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public void ToneOfVoice_Signature_ShouldBeValid()
    {
        // Arrange
        var toneOfVoice = new ToneOfVoice();

        // Act
        var signature = toneOfVoice._signature;

        // Assert
        signature.Should().NotBeNullOrEmpty();
        signature.Should().Contain("ToneOfVoiceDescriptors");
    }

    [Fact]
    public void ToneOfVoice_WithMultipleDescriptors_ShouldMaintainOrder()
    {
        // Arrange
        var toneOfVoice = new ToneOfVoice();
        var descriptors = new[] { "First", "Second", "Third", "Fourth" };

        // Act
        foreach (var descriptor in descriptors)
        {
            toneOfVoice.ToneOfVoiceDescriptors.Add(descriptor);
        }

        // Assert
        toneOfVoice.ToneOfVoiceDescriptors.Should().ContainInOrder(descriptors);
    }
}
