using Hazina.AgenticOrchestration.Services.Chat;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace Hazina.Tests.Orchestration.Chat;

public class ConversationRepositoryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ConversationRepository _repo;
    private readonly ITestOutputHelper _output;

    public ConversationRepositoryTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = Path.Combine(Path.GetTempPath(), $"hazina-chat-tests-{Guid.NewGuid():N}");
        _repo = new ConversationRepository(_tempDir, NullLogger<ConversationRepository>.Instance);
    }

    [Fact]
    public async Task SaveAsync_CreatesFile()
    {
        var sessionId = "chat-test-session-1";
        var history = BuildHistory(sessionId, 2);

        await _repo.SaveAsync(sessionId, history);

        var files = Directory.GetFiles(_tempDir, "*.json");
        Assert.Single(files);
    }

    [Fact]
    public async Task LoadAsync_ReturnsNull_WhenMissing()
    {
        var result = await _repo.LoadAsync("nonexistent-session");
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveThenLoad_RoundTrips()
    {
        var sessionId = "chat-roundtrip-session";
        var history = BuildHistory(sessionId, 3);

        await _repo.SaveAsync(sessionId, history);
        var loaded = await _repo.LoadAsync(sessionId);

        Assert.NotNull(loaded);
        Assert.Equal(sessionId, loaded.SessionId);
        Assert.Equal(3, loaded.Messages.Count);
        Assert.Equal("user", loaded.Messages[0].Role);
        Assert.Equal("assistant", loaded.Messages[1].Role);
        Assert.Equal("Hello", loaded.Messages[0].Content);
    }

    [Fact]
    public async Task DeleteAsync_RemovesFile()
    {
        var sessionId = "chat-delete-me";
        await _repo.SaveAsync(sessionId, BuildHistory(sessionId, 1));

        await _repo.DeleteAsync(sessionId);

        var loaded = await _repo.LoadAsync(sessionId);
        Assert.Null(loaded);
    }

    [Fact]
    public async Task DeleteAsync_IsIdempotent_WhenFileMissing()
    {
        // Should not throw
        await _repo.DeleteAsync("does-not-exist");
    }

    [Fact]
    public async Task ListAsync_ReturnsAllSaved()
    {
        await _repo.SaveAsync("chat-list-a", BuildHistory("chat-list-a", 1));
        await _repo.SaveAsync("chat-list-b", BuildHistory("chat-list-b", 2));
        await _repo.SaveAsync("chat-list-c", BuildHistory("chat-list-c", 3));

        var list = await _repo.ListAsync();

        Assert.Equal(3, list.Count);
        var ids = list.Select(c => c.SessionId).ToHashSet();
        Assert.Contains("chat-list-a", ids);
        Assert.Contains("chat-list-b", ids);
        Assert.Contains("chat-list-c", ids);
    }

    [Fact]
    public async Task ListAsync_ReturnsEmpty_WhenNothingSaved()
    {
        var list = await _repo.ListAsync();
        Assert.Empty(list);
    }

    [Fact]
    public void GetTotalSizeBytes_ReturnsZero_WhenEmpty()
    {
        var size = _repo.GetTotalSizeBytes();
        Assert.Equal(0L, size);
    }

    [Fact]
    public async Task GetTotalSizeBytes_IsPositive_AfterSave()
    {
        await _repo.SaveAsync("chat-size-test", BuildHistory("chat-size-test", 5));

        var size = _repo.GetTotalSizeBytes();

        Assert.True(size > 0, "Total size should be positive after saving a conversation");
        _output.WriteLine($"Saved conversation size: {size} bytes");
    }

    [Fact]
    public async Task SaveAsync_PathTraversal_IsSanitized()
    {
        // A session ID with path separators should be sanitized (chars stripped), not throw
        var sessionId = "../../../etc/passwd";
        // Should not throw; sanitized name will be "etcpasswd" (no path separators)
        await _repo.SaveAsync(sessionId, BuildHistory("safe", 1));

        // All files must stay within the temp dir
        var allFiles = Directory.GetFiles(_tempDir, "*.json");
        Assert.NotEmpty(allFiles);
        Assert.All(allFiles, f => Assert.StartsWith(_tempDir, f));
    }

    [Fact]
    public async Task ExportAsync_CreatesExportFile()
    {
        var sessionId = "chat-export-session";
        await _repo.SaveAsync(sessionId, BuildHistory(sessionId, 2));
        var exportDir = Path.Combine(_tempDir, "exports");

        var exportedPath = await _repo.ExportAsync(sessionId, exportDir);

        Assert.True(File.Exists(exportedPath));
        _output.WriteLine($"Exported to: {exportedPath}");
    }

    [Fact]
    public async Task ExportAsync_Throws_WhenConversationMissing()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _repo.ExportAsync("not-found", _tempDir));
    }

    [Fact]
    public async Task CleanupOldAsync_DeletesOldFiles()
    {
        var sessionId = "chat-old-session";
        await _repo.SaveAsync(sessionId, BuildHistory(sessionId, 1));

        // Backdate the file
        var files = Directory.GetFiles(_tempDir, "*.json");
        File.SetLastWriteTimeUtc(files[0], DateTime.UtcNow.AddDays(-10));

        await _repo.CleanupOldAsync(olderThanDays: 7);

        var remaining = Directory.GetFiles(_tempDir, "*.json");
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task CleanupOldAsync_KeepsRecentFiles()
    {
        await _repo.SaveAsync("chat-recent-session", BuildHistory("chat-recent-session", 1));

        await _repo.CleanupOldAsync(olderThanDays: 7);

        var remaining = Directory.GetFiles(_tempDir, "*.json");
        Assert.Single(remaining);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup
        }
    }

    private static ConversationHistory BuildHistory(string sessionId, int messageCount)
    {
        var messages = new List<ChatMessageRecord>();
        for (var i = 0; i < messageCount; i++)
        {
            messages.Add(new ChatMessageRecord
            {
                Role = i % 2 == 0 ? "user" : "assistant",
                Content = i % 2 == 0 ? "Hello" : "Hi there!",
                Timestamp = DateTime.UtcNow.AddMinutes(-messageCount + i)
            });
        }

        return new ConversationHistory
        {
            SessionId = sessionId,
            CreatedAt = DateTime.UtcNow.AddMinutes(-messageCount),
            LastMessageAt = DateTime.UtcNow,
            Messages = messages,
            Metrics = new ConversationMetrics { TotalMessages = messageCount }
        };
    }
}
