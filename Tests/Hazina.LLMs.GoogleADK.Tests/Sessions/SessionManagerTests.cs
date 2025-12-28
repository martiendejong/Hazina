using Hazina.LLMs.GoogleADK.Sessions;
using Hazina.LLMs.GoogleADK.Sessions.Models;
using Hazina.LLMs.GoogleADK.Sessions.Storage;
using Xunit;

namespace Hazina.LLMs.GoogleADK.Tests.Sessions;

public class SessionManagerTests
{
    [Fact]
    public async Task CreateSessionAsync_ShouldCreateNewSession()
    {
        // Arrange
        var storage = new InMemorySessionStorage();
        var manager = new SessionManager(storage);

        // Act
        var session = await manager.CreateSessionAsync("TestAgent", "user123");

        // Assert
        Assert.NotNull(session);
        Assert.NotEmpty(session.SessionId);
        Assert.Equal("TestAgent", session.AgentName);
        Assert.Equal("user123", session.UserId);
        Assert.Equal(SessionStatus.Created, session.Status);

        await manager.DisposeAsync();
    }

    [Fact]
    public async Task ResumeSessionAsync_ShouldLoadExistingSession()
    {
        // Arrange
        var storage = new InMemorySessionStorage();
        var manager = new SessionManager(storage);

        var originalSession = await manager.CreateSessionAsync("TestAgent");
        originalSession.Status = SessionStatus.Active;
        await manager.UpdateSessionAsync(originalSession);

        // Create new manager to simulate restart
        var newManager = new SessionManager(storage);

        // Act
        var resumedSession = await newManager.ResumeSessionAsync(originalSession.SessionId);

        // Assert
        Assert.NotNull(resumedSession);
        Assert.Equal(originalSession.SessionId, resumedSession.SessionId);
        Assert.Equal(SessionStatus.Active, resumedSession.Status);

        await manager.DisposeAsync();
        await newManager.DisposeAsync();
    }

    [Fact]
    public async Task AddMessageAsync_ShouldAddMessageToSession()
    {
        // Arrange
        var storage = new InMemorySessionStorage();
        var manager = new SessionManager(storage);
        var session = await manager.CreateSessionAsync("TestAgent");

        // Act
        await manager.AddMessageAsync(session.SessionId, "user", "Hello");
        await manager.AddMessageAsync(session.SessionId, "assistant", "Hi there!");

        // Assert
        var updatedSession = manager.GetSession(session.SessionId);
        Assert.NotNull(updatedSession);
        Assert.Equal(2, updatedSession.Messages.Count);
        Assert.Equal("Hello", updatedSession.Messages[0].Content);
        Assert.Equal("Hi there!", updatedSession.Messages[1].Content);

        await manager.DisposeAsync();
    }

    [Fact]
    public async Task AddMessageAsync_ShouldTrimMessagesWhenMaxExceeded()
    {
        // Arrange
        var storage = new InMemorySessionStorage();
        var manager = new SessionManager(storage);
        var config = new SessionConfiguration { MaxMessages = 3 };
        var session = await manager.CreateSessionAsync("TestAgent", configuration: config);

        // Act
        await manager.AddMessageAsync(session.SessionId, "user", "Message 1");
        await manager.AddMessageAsync(session.SessionId, "user", "Message 2");
        await manager.AddMessageAsync(session.SessionId, "user", "Message 3");
        await manager.AddMessageAsync(session.SessionId, "user", "Message 4");

        // Assert
        var updatedSession = manager.GetSession(session.SessionId);
        Assert.NotNull(updatedSession);
        Assert.Equal(3, updatedSession.Messages.Count);
        Assert.Equal("Message 2", updatedSession.Messages[0].Content); // First message trimmed
        Assert.Equal("Message 4", updatedSession.Messages[2].Content);

        await manager.DisposeAsync();
    }

    [Fact]
    public async Task CompleteSessionAsync_ShouldMarkSessionAsCompleted()
    {
        // Arrange
        var storage = new InMemorySessionStorage();
        var manager = new SessionManager(storage);
        var session = await manager.CreateSessionAsync("TestAgent");

        // Act
        await manager.CompleteSessionAsync(session.SessionId);

        // Assert
        var activeSession = manager.GetSession(session.SessionId);
        Assert.Null(activeSession); // Should be removed from active sessions

        var storedSession = await storage.LoadSessionAsync(session.SessionId);
        Assert.NotNull(storedSession);
        Assert.Equal(SessionStatus.Completed, storedSession.Status);

        await manager.DisposeAsync();
    }

    [Fact]
    public async Task CleanupExpiredSessionsAsync_ShouldRemoveExpiredSessions()
    {
        // Arrange
        var storage = new InMemorySessionStorage();
        var manager = new SessionManager(storage);

        var config = new SessionConfiguration { TimeoutMinutes = 0 };
        var session = await manager.CreateSessionAsync("TestAgent", configuration: config);
        session.ExpiresAt = DateTime.UtcNow.AddMinutes(-1); // Already expired

        // Act
        await Task.Delay(100); // Small delay
        var cleanedUp = await manager.CleanupExpiredSessionsAsync();

        // Assert
        Assert.True(cleanedUp > 0);

        await manager.DisposeAsync();
    }

    [Fact]
    public async Task GetStatisticsAsync_ShouldReturnCorrectCounts()
    {
        // Arrange
        var storage = new InMemorySessionStorage();
        var manager = new SessionManager(storage);

        await manager.CreateSessionAsync("Agent1");
        await manager.CreateSessionAsync("Agent2");

        // Act
        var stats = await manager.GetStatisticsAsync();

        // Assert
        Assert.Equal(2, stats.ActiveSessions);
        Assert.Equal(2, stats.TotalSessions);

        await manager.DisposeAsync();
    }
}
