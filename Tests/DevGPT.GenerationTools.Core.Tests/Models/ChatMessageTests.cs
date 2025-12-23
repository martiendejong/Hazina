using DevGPTStore.Core.Models;
using FluentAssertions;

namespace DevGPT.GenerationTools.Core.Tests.Models;

public class ChatMessageTests
{
    [Fact]
    public void ChatMessage_ShouldInitializeWithDefaultValues()
    {
        // Act
        var message = new ChatMessage();

        // Assert
        message.Role.Should().BeNull();
        message.Text.Should().BeNull();
        message.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        message.Metadata.Should().BeNull();
    }

    [Fact]
    public void ChatMessage_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        var timestamp = DateTime.UtcNow.AddHours(-1);

        // Act
        var message = new ChatMessage
        {
            Role = "user",
            Text = "Hello, world!",
            Timestamp = timestamp,
            Metadata = "{\"type\":\"test\"}"
        };

        // Assert
        message.Role.Should().Be("user");
        message.Text.Should().Be("Hello, world!");
        message.Timestamp.Should().Be(timestamp);
        message.Metadata.Should().Be("{\"type\":\"test\"}");
    }

    [Fact]
    public void ChatMessage_WithUserRole_ShouldWorkCorrectly()
    {
        // Act
        var message = new ChatMessage
        {
            Role = "user",
            Text = "User message"
        };

        // Assert
        message.Role.Should().Be("user");
        message.Text.Should().Be("User message");
    }

    [Fact]
    public void ChatMessage_WithAssistantRole_ShouldWorkCorrectly()
    {
        // Act
        var message = new ChatMessage
        {
            Role = "assistant",
            Text = "Assistant response"
        };

        // Assert
        message.Role.Should().Be("assistant");
        message.Text.Should().Be("Assistant response");
    }

    [Fact]
    public void ChatMessage_WithSystemRole_ShouldWorkCorrectly()
    {
        // Act
        var message = new ChatMessage
        {
            Role = "system",
            Text = "System instruction"
        };

        // Assert
        message.Role.Should().Be("system");
        message.Text.Should().Be("System instruction");
    }
}
