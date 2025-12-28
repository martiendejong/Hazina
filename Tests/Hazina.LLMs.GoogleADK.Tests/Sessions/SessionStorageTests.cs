using Hazina.LLMs.GoogleADK.Sessions.Models;
using Hazina.LLMs.GoogleADK.Sessions.Storage;
using Xunit;

namespace Hazina.LLMs.GoogleADK.Tests.Sessions;

public class SessionStorageTests
{
    [Fact]
    public async Task InMemoryStorage_SaveAndLoad_ShouldWork()
    {
        // Arrange
        var storage = new InMemorySessionStorage();
        var session = new Session
        {
            SessionId = "test-123",
            AgentName = "TestAgent",
            Status = SessionStatus.Active
        };

        // Act
        await storage.SaveSessionAsync(session);
        var loaded = await storage.LoadSessionAsync("test-123");

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(session.SessionId, loaded.SessionId);
        Assert.Equal(session.AgentName, loaded.AgentName);
        Assert.Equal(session.Status, loaded.Status);
    }

    [Fact]
    public async Task InMemoryStorage_Delete_ShouldRemoveSession()
    {
        // Arrange
        var storage = new InMemorySessionStorage();
        var session = new Session { SessionId = "test-123" };
        await storage.SaveSessionAsync(session);

        // Act
        await storage.DeleteSessionAsync("test-123");
        var loaded = await storage.LoadSessionAsync("test-123");

        // Assert
        Assert.Null(loaded);
    }

    [Fact]
    public async Task InMemoryStorage_ListSessions_WithFilters_ShouldWork()
    {
        // Arrange
        var storage = new InMemorySessionStorage();
        await storage.SaveSessionAsync(new Session { AgentName = "Agent1", UserId = "user1", Status = SessionStatus.Active });
        await storage.SaveSessionAsync(new Session { AgentName = "Agent1", UserId = "user2", Status = SessionStatus.Active });
        await storage.SaveSessionAsync(new Session { AgentName = "Agent2", UserId = "user1", Status = SessionStatus.Completed });

        // Act
        var agent1Sessions = await storage.ListSessionsAsync(agentName: "Agent1");
        var user1Sessions = await storage.ListSessionsAsync(userId: "user1");
        var activeSessions = await storage.ListSessionsAsync(status: SessionStatus.Active);

        // Assert
        Assert.Equal(2, agent1Sessions.Count);
        Assert.Equal(2, user1Sessions.Count);
        Assert.Equal(2, activeSessions.Count);
    }

    [Fact]
    public async Task InMemoryStorage_GetSessionsByTag_ShouldWork()
    {
        // Arrange
        var storage = new InMemorySessionStorage();
        var session1 = new Session { Tags = new List<string> { "production", "important" } };
        var session2 = new Session { Tags = new List<string> { "production" } };
        var session3 = new Session { Tags = new List<string> { "test" } };

        await storage.SaveSessionAsync(session1);
        await storage.SaveSessionAsync(session2);
        await storage.SaveSessionAsync(session3);

        // Act
        var productionSessions = await storage.GetSessionsByTagAsync("production");

        // Assert
        Assert.Equal(2, productionSessions.Count);
    }

    [Fact]
    public async Task InMemoryStorage_CleanupExpired_ShouldWork()
    {
        // Arrange
        var storage = new InMemorySessionStorage();
        var expiredSession = new Session
        {
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
        };
        var validSession = new Session
        {
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };

        await storage.SaveSessionAsync(expiredSession);
        await storage.SaveSessionAsync(validSession);

        // Act
        var cleanedUp = await storage.CleanupExpiredSessionsAsync();

        // Assert
        Assert.Equal(1, cleanedUp);
        var count = await storage.GetSessionCountAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task FileStorage_SaveAndLoad_ShouldWork()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var storage = new FileSessionStorage(tempDir);
        var session = new Session
        {
            SessionId = "test-file-123",
            AgentName = "TestAgent",
            Status = SessionStatus.Active
        };

        try
        {
            // Act
            await storage.SaveSessionAsync(session);
            var loaded = await storage.LoadSessionAsync("test-file-123");

            // Assert
            Assert.NotNull(loaded);
            Assert.Equal(session.SessionId, loaded.SessionId);
            Assert.Equal(session.AgentName, loaded.AgentName);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
