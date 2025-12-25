using DevGPTStore.Core.Models;
using FluentAssertions;

namespace DevGPT.GenerationTools.Core.Tests;

/// <summary>
/// Tests for Chat-related models and behavior
/// Note: Chat class has internal constructor, so these tests focus on ChatMessage and chat-related models
/// </summary>
public class ChatTests
{
    [Fact]
    public void ChatMessage_ShouldValidateRoleValues()
    {
        // Arrange & Act
        var userMessage = new ChatMessage { Role = "user", Text = "Test" };
        var assistantMessage = new ChatMessage { Role = "assistant", Text = "Response" };
        var systemMessage = new ChatMessage { Role = "system", Text = "Instruction" };

        // Assert
        userMessage.Role.Should().Be("user");
        assistantMessage.Role.Should().Be("assistant");
        systemMessage.Role.Should().Be("system");
    }

    [Fact]
    public void ChatMessage_ShouldHandleEmptyText()
    {
        // Arrange & Act
        var message = new ChatMessage
        {
            Role = "user",
            Text = ""
        };

        // Assert
        message.Text.Should().BeEmpty();
        message.Role.Should().Be("user");
    }

    [Fact]
    public void ChatMessage_ShouldHandleLongText()
    {
        // Arrange
        var longText = new string('a', 10000);

        // Act
        var message = new ChatMessage
        {
            Role = "user",
            Text = longText
        };

        // Assert
        message.Text.Should().HaveLength(10000);
        message.Text.Should().Be(longText);
    }

    [Fact]
    public void ChatMessage_TimestampShouldBeUtc()
    {
        // Arrange & Act
        var message = new ChatMessage
        {
            Role = "user",
            Text = "Test"
        };

        // Assert
        message.Timestamp.Kind.Should().Be(DateTimeKind.Utc);
        message.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }
}
