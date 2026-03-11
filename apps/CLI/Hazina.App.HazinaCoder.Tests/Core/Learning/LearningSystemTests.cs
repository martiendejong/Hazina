using Xunit;
using FluentAssertions;
using Hazina.App.HazinaCoder.Core.Learning;

namespace Hazina.App.HazinaCoder.Tests.Core.Learning;

public class LearningSystemTests
{
    [Fact]
    public void CaptureExperience_ShouldStore_ExperienceData()
    {
        // Arrange
        var storage = new ExperienceStorage("test.jsonl");
        var capture = new ExperienceCapture(storage);

        // Act
        var exp = capture.Capture("TestAction", "TestOutcome", true);

        // Assert
        exp.Should().NotBeNull();
        exp.Action.Should().Be("TestAction");
        exp.Success.Should().BeTrue();
    }

    [Fact]
    public void PatternRecognition_ShouldIdentify_RepeatedMistakes()
    {
        // Arrange
        var storage = new ExperienceStorage("test.jsonl");
        var recognizer = new SuccessPatternRecognizer(storage);

        // Add repeated failure pattern
        for (int i = 0; i < 3; i++)
        {
            storage.Save(new Experience
            {
                Action = "SameAction",
                Success = false,
                Timestamp = DateTime.UtcNow.AddMinutes(-i)
            });
        }

        // Act
        var patterns = recognizer.FindFailurePatterns();

        // Assert
        patterns.Should().Contain(p => p.Contains("SameAction"));
    }
}
