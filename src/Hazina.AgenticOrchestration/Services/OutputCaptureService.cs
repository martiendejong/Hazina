using Hazina.AgenticOrchestration.Models;
using Hazina.AgenticOrchestration.Hubs;
using Microsoft.AspNetCore.SignalR;
using System.Text.RegularExpressions;

namespace Hazina.AgenticOrchestration.Services;

public class OutputCaptureService : IOutputCaptureService
{
    private readonly IHubContext<ClaudeOrchestrationHub> _hubContext;
    private readonly string _logsPath;

    public OutputCaptureService(
        IHubContext<ClaudeOrchestrationHub> hubContext,
        string logsPath)
    {
        _hubContext = hubContext;
        _logsPath = logsPath;
    }

    public async Task StreamOutputAsync(string sessionId, CancellationToken cancellationToken)
    {
        var logFile = Path.Combine(_logsPath, $"agent-{sessionId}.log");

        // Wait for file to exist
        int attempts = 0;
        while (!File.Exists(logFile) && attempts < 30)
        {
            await Task.Delay(1000, cancellationToken);
            attempts++;
        }

        if (!File.Exists(logFile))
        {
            return; // File never created
        }

        using var fs = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fs);

        // Start from end of file
        reader.BaseStream.Seek(0, SeekOrigin.End);

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);

            if (line != null)
            {
                // Send to SignalR clients
                await _hubContext.Clients
                    .Group($"instance-{sessionId}")
                    .SendAsync("ReceiveOutput", new
                    {
                        SessionId = sessionId,
                        Timestamp = DateTime.UtcNow,
                        Output = line
                    }, cancellationToken);
            }
            else
            {
                // No new data, wait a bit
                await Task.Delay(100, cancellationToken);
            }
        }
    }

    public async Task<List<OutputLine>> GetOutputHistoryAsync(string sessionId, int lastN = 100)
    {
        var logFile = Path.Combine(_logsPath, $"agent-{sessionId}.log");

        if (!File.Exists(logFile))
        {
            return new List<OutputLine>();
        }

        var lines = await File.ReadAllLinesAsync(logFile);

        return lines
            .TakeLast(lastN)
            .Select((line, index) => new OutputLine
            {
                LineNumber = lines.Length - lastN + index,
                Content = line,
                Timestamp = ExtractTimestamp(line),
                SessionId = sessionId
            })
            .ToList();
    }

    private DateTime ExtractTimestamp(string line)
    {
        // Try to extract timestamp from log line
        // Format: [2026-01-27 02:35:00] message
        var match = Regex.Match(line, @"\[(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})\]");
        if (match.Success && DateTime.TryParse(match.Groups[1].Value, out var timestamp))
        {
            return timestamp;
        }

        return DateTime.UtcNow;
    }
}
