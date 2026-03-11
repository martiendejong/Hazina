using System.Text.Json;
using Hazina.Agent.API.Models;

namespace Hazina.Agent.API.Services;

public class SessionLogger : ISessionLogger
{
    private readonly string _baseDirectory = @"E:\data\hazina\sessions";
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public async Task<string> CreateSessionAsync(AgentRequest request, CancellationToken ct = default)
    {
        var sessionId = GenerateSessionId();
        var sessionPath = GetSessionDirectory(sessionId);

        Directory.CreateDirectory(sessionPath);

        // Write request.json
        var requestPath = Path.Combine(sessionPath, "request.json");
        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, _jsonOptions), ct);

        // Create empty stream.log
        var streamPath = Path.Combine(sessionPath, "stream.log");
        await File.WriteAllTextAsync(streamPath, string.Empty, ct);

        return sessionId;
    }

    public async Task LogEventAsync(string sessionId, AgentEvent agentEvent, CancellationToken ct = default)
    {
        var streamPath = Path.Combine(GetSessionDirectory(sessionId), "stream.log");
        var eventLine = JsonSerializer.Serialize(agentEvent) + Environment.NewLine;
        await File.AppendAllTextAsync(streamPath, eventLine, ct);
    }

    public async Task LogCompleteAsync(string sessionId, CompleteData complete, CancellationToken ct = default)
    {
        var sessionPath = GetSessionDirectory(sessionId);

        // Write result.md
        var resultPath = Path.Combine(sessionPath, "result.md");
        await File.WriteAllTextAsync(resultPath, complete.FinalResult, ct);

        // Write complete.json
        var completePath = Path.Combine(sessionPath, "complete.json");
        await File.WriteAllTextAsync(completePath, JsonSerializer.Serialize(complete, _jsonOptions), ct);
    }

    public Task<string> GetSessionPathAsync(string sessionId)
    {
        return Task.FromResult(GetSessionDirectory(sessionId));
    }

    private string GenerateSessionId()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var random = Guid.NewGuid().ToString()[..8];
        return $"{timestamp}-{random}";
    }

    private string GetSessionDirectory(string sessionId)
    {
        var date = sessionId[..8]; // yyyyMMdd
        return Path.Combine(_baseDirectory, date, sessionId);
    }
}
