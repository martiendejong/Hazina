using DevGPT.GenerationTools.Services.FileOps.Helpers;
using FluentAssertions;

namespace DevGPT.GenerationTools.Services.FileOps.Tests.Helpers;

public class LogEntryTests
{
    [Fact]
    public void LogEntry_ShouldSetPropertiesCorrectly()
    {
        // Act
        var logEntry = new LogEntry
        {
            Project = "TestProject",
            Date = "2025-11-08",
            Source = "TestSource",
            Messages = new List<LogMessage>
            {
                new LogMessage { Role = "user", Message = "Test message" }
            }
        };

        // Assert
        logEntry.Project.Should().Be("TestProject");
        logEntry.Date.Should().Be("2025-11-08");
        logEntry.Source.Should().Be("TestSource");
        logEntry.Messages.Should().HaveCount(1);
    }

    [Fact]
    public void LogMessage_ShouldSetPropertiesCorrectly()
    {
        // Act
        var logMessage = new LogMessage
        {
            Role = "assistant",
            Message = "Response message"
        };

        // Assert
        logMessage.Role.Should().Be("assistant");
        logMessage.Message.Should().Be("Response message");
    }

    [Fact]
    public void LogEntry_WithMultipleMessages_ShouldStoreAll()
    {
        // Arrange
        var messages = new List<LogMessage>
        {
            new LogMessage { Role = "user", Message = "First" },
            new LogMessage { Role = "assistant", Message = "Second" },
            new LogMessage { Role = "user", Message = "Third" }
        };

        // Act
        var logEntry = new LogEntry
        {
            Project = "Test",
            Date = "2025-11-08",
            Source = "UnitTest",
            Messages = messages
        };

        // Assert
        logEntry.Messages.Should().HaveCount(3);
        logEntry.Messages[0].Message.Should().Be("First");
        logEntry.Messages[1].Message.Should().Be("Second");
        logEntry.Messages[2].Message.Should().Be("Third");
    }
}
