using Hazina.AgenticOrchestration.Services;
using Hazina.AgenticOrchestration.Services.Chat;
using Hazina.AgenticOrchestration.Terminal;
using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Hazina.Tests.Orchestration.Chat;

public class SessionManagementToolsTests
{
    private readonly ITestOutputHelper _output;

    public SessionManagementToolsTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void CreateTools_ReturnsExpectedCount()
    {
        var manager = new FakeSessionManager();
        var tools = SessionManagementTools.CreateTools(manager);
        Assert.Equal(5, tools.Count);
    }

    [Fact]
    public void CreateTools_HasExpectedFunctionNames()
    {
        var manager = new FakeSessionManager();
        var tools = SessionManagementTools.CreateTools(manager);
        var names = tools.Select(t => t.FunctionName).ToHashSet();

        Assert.Contains("list_sessions", names);
        Assert.Contains("get_session_details", names);
        Assert.Contains("list_archived_sessions", names);
        Assert.Contains("get_system_status", names);
        Assert.Contains("search_sessions", names);
    }

    [Fact]
    public async Task ListSessions_ReturnsAllSessions()
    {
        var manager = new FakeSessionManager();
        manager.AddSession(new FakeSession("s1", "echo hello", isRunning: true));
        manager.AddSession(new FakeSession("s2", "dotnet run", isRunning: false));

        var tool = GetTool("list_sessions", manager);
        var result = await InvokeTool(tool);

        _output.WriteLine(result);
        var sessions = JsonSerializer.Deserialize<List<JsonElement>>(result);
        Assert.NotNull(sessions);
        Assert.Equal(2, sessions.Count);
    }

    [Fact]
    public async Task ListSessions_ReturnsEmpty_WhenNoSessions()
    {
        var manager = new FakeSessionManager();
        var tool = GetTool("list_sessions", manager);
        var result = await InvokeTool(tool);

        var sessions = JsonSerializer.Deserialize<List<JsonElement>>(result);
        Assert.NotNull(sessions);
        Assert.Empty(sessions);
    }

    [Fact]
    public async Task GetSessionDetails_ReturnsSessionInfo()
    {
        var manager = new FakeSessionManager();
        manager.AddSession(new FakeSession("abc-123", "git status", isRunning: true));

        var tool = GetTool("get_session_details", manager);
        var toolCall = MakeToolCall("get_session_details", """{"sessionId": "abc-123"}""");

        var result = await tool.Execute!([], toolCall, CancellationToken.None);
        _output.WriteLine(result);

        Assert.Contains("abc-123", result);
        Assert.Contains("git status", result);
    }

    [Fact]
    public async Task GetSessionDetails_ReturnsError_WhenSessionMissing()
    {
        var manager = new FakeSessionManager();
        var tool = GetTool("get_session_details", manager);
        var toolCall = MakeToolCall("get_session_details", """{"sessionId": "not-there"}""");

        var result = await tool.Execute!([], toolCall, CancellationToken.None);
        _output.WriteLine(result);

        var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("error", out _), "Expected an error field");
    }

    [Fact]
    public async Task GetSystemStatus_IncludesSessionCounts()
    {
        var manager = new FakeSessionManager();
        manager.AddSession(new FakeSession("x1", "cmd1", isRunning: true));
        manager.AddSession(new FakeSession("x2", "cmd2", isRunning: true));
        manager.AddSession(new FakeSession("x3", "cmd3", isRunning: false));

        var tool = GetTool("get_system_status", manager);
        var result = await InvokeTool(tool);

        _output.WriteLine(result);
        var doc = JsonDocument.Parse(result);
        Assert.Equal(3, doc.RootElement.GetProperty("TotalSessions").GetInt32());
        Assert.Equal(2, doc.RootElement.GetProperty("ActiveSessions").GetInt32());
    }

    [Fact]
    public async Task SearchSessions_ReturnsMatches()
    {
        var manager = new FakeSessionManager();
        manager.AddSession(new FakeSession("s1", "dotnet build", isRunning: true));
        manager.AddSession(new FakeSession("s2", "dotnet test", isRunning: true));
        manager.AddSession(new FakeSession("s3", "npm install", isRunning: true));

        var tool = GetTool("search_sessions", manager);
        var toolCall = MakeToolCall("search_sessions", """{"query": "dotnet"}""");

        var result = await tool.Execute!([], toolCall, CancellationToken.None);
        _output.WriteLine(result);

        var doc = JsonDocument.Parse(result);
        Assert.Equal(2, doc.RootElement.GetProperty("MatchCount").GetInt32());
    }

    [Fact]
    public async Task SearchSessions_ReturnsEmptyMatches_WhenNoMatch()
    {
        var manager = new FakeSessionManager();
        manager.AddSession(new FakeSession("s1", "npm run dev", isRunning: true));

        var tool = GetTool("search_sessions", manager);
        var toolCall = MakeToolCall("search_sessions", """{"query": "dotnet"}""");

        var result = await tool.Execute!([], toolCall, CancellationToken.None);
        var doc = JsonDocument.Parse(result);
        Assert.Equal(0, doc.RootElement.GetProperty("MatchCount").GetInt32());
    }

    [Fact]
    public async Task ListArchivedSessions_ReturnsPlaceholder()
    {
        var manager = new FakeSessionManager();
        var tool = GetTool("list_archived_sessions", manager);
        var result = await InvokeTool(tool);

        _output.WriteLine(result);
        Assert.NotNull(result);
        var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("ArchivedSessions", out _));
    }

    [Fact]
    public async Task SearchSessions_IsCaseInsensitive()
    {
        var manager = new FakeSessionManager();
        manager.AddSession(new FakeSession("s1", "DOTNET BUILD", isRunning: true));

        var tool = GetTool("search_sessions", manager);
        var toolCall = MakeToolCall("search_sessions", """{"query": "dotnet"}""");
        var result = await tool.Execute!([], toolCall, CancellationToken.None);

        var doc = JsonDocument.Parse(result);
        Assert.Equal(1, doc.RootElement.GetProperty("MatchCount").GetInt32());
    }

    private static HazinaChatTool GetTool(string name, ITerminalSessionManager manager)
    {
        var tools = SessionManagementTools.CreateTools(manager);
        return tools.First(t => t.FunctionName == name);
    }

    private static Task<string> InvokeTool(HazinaChatTool tool, string argsJson = "{}")
    {
        return tool.Execute!([], MakeToolCall(tool.FunctionName, argsJson), CancellationToken.None);
    }

    private static HazinaChatToolCall MakeToolCall(string functionName, string argsJson)
    {
        return new HazinaChatToolCall(
            id: "test-call",
            functionName: functionName,
            functionArguments: BinaryData.FromBytes(Encoding.UTF8.GetBytes(argsJson)));
    }
}

// ---- Fakes ----

internal class FakeSessionManager : ITerminalSessionManager
{
    private readonly Dictionary<string, ITerminalSession> _sessions = new();

    public void AddSession(ITerminalSession session) =>
        _sessions[session.SessionId] = session;

    public ITerminalSession? GetSession(string sessionId) =>
        _sessions.TryGetValue(sessionId, out var s) ? s : null;

    public IEnumerable<ITerminalSession> GetAllSessions() => _sessions.Values;

    public int SessionCount => _sessions.Count;

    public Task<ITerminalSession> CreateSessionAsync(TerminalSessionConfig config, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task RemoveSessionAsync(string sessionId) =>
        throw new NotImplementedException();

    public Task<ITerminalSession?> RestoreSessionAsync(string sessionId, CancellationToken ct = default) =>
        Task.FromResult<ITerminalSession?>(null);

    public Task<IEnumerable<SessionMetadata>> GetRecoverableSessionsAsync() =>
        Task.FromResult<IEnumerable<SessionMetadata>>(Array.Empty<SessionMetadata>());
}

internal class FakeSession : ITerminalSession
{
    public FakeSession(string sessionId, string command, bool isRunning = true, string? title = null)
    {
        SessionId = sessionId;
        Command = command;
        IsRunning = isRunning;
        Title = title;
    }

    public string SessionId { get; }
    public bool IsRunning { get; }
    public int? ExitCode => IsRunning ? null : 0;
    public DateTime StartedAt { get; } = DateTime.UtcNow.AddMinutes(-5);
    public string Command { get; }
    public bool WaitingForInput => false;
    public string? Title { get; }

    public byte[] GetOutputHistory() => Array.Empty<byte>();

    public Task StartAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task WriteInputAsync(byte[] data, CancellationToken ct = default) => Task.CompletedTask;
    public Task WriteInputAsync(string text, CancellationToken ct = default) => Task.CompletedTask;
    public Task SendSignalAsync(TerminalSignal signal) => Task.CompletedTask;
    public Task ResizeAsync(int columns, int rows) => Task.CompletedTask;
    public Task TerminateAsync() => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

#pragma warning disable CS0067
    public event Action<byte[]>? OnOutput;
    public event Action<int>? OnExit;
    public event Action<string>? OnTitleChanged;
#pragma warning restore CS0067
}
