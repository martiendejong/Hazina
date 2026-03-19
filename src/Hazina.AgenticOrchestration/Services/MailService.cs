using Hazina.AgenticOrchestration.Data;
using Hazina.AgenticOrchestration.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Hazina.AgenticOrchestration.Services;

/// <summary>
/// Inter-agent mail service for message passing
/// Inspired by Overstory's SQLite mail system
/// </summary>
public interface IMailService
{
    string Send(MailMessage message);
    List<MailMessage> Check(string agentName);
    string CheckInject(string agentName);
    (bool AlreadyRead, MailMessage? Message) MarkAsRead(string messageId);
    string Reply(string messageId, string body, string from);
    List<MailMessage> List(string? from = null, string? to = null, bool? unread = null);
    Task<PendingNudge?> GetPendingNudgeAsync(string agentName);
    Task ClearPendingNudgeAsync(string agentName);
}

public class MailService : IMailService
{
    private readonly MailStore _store;
    private readonly string _pendingNudgesPath;
    private readonly ILogger<MailService> _logger;

    // Protocol types that trigger auto-nudge regardless of priority
    private static readonly HashSet<MailMessageType> AutoNudgeTypes = new()
    {
        MailMessageType.WorkerDone,
        MailMessageType.MergeReady,
        MailMessageType.Error,
        MailMessageType.Escalation,
        MailMessageType.MergeFailed
    };

    public MailService(string databasePath, string pendingNudgesPath, ILogger<MailService> logger)
    {
        _store = new MailStore(databasePath);
        _pendingNudgesPath = pendingNudgesPath;
        _logger = logger;

        // Ensure pending nudges directory exists
        Directory.CreateDirectory(_pendingNudgesPath);
    }

    public string Send(MailMessage message)
    {
        _store.Insert(message);
        _logger.LogInformation("Mail sent from {From} to {To}: {Subject}", message.From, message.To, message.Subject);

        // Auto-nudge for urgent/high priority or protocol types
        var shouldNudge = message.Priority is MailPriority.Urgent or MailPriority.High ||
                         AutoNudgeTypes.Contains(message.Type);

        if (shouldNudge)
        {
            var reason = AutoNudgeTypes.Contains(message.Type) ? message.Type.ToString() : $"{message.Priority} priority";
            WritePendingNudge(message.To, new PendingNudge
            {
                From = message.From,
                Reason = reason,
                Subject = message.Subject,
                MessageId = message.Id
            });
            _logger.LogInformation("Pending nudge queued for {To} ({Reason})", message.To, reason);
        }

        return message.Id;
    }

    public List<MailMessage> Check(string agentName)
    {
        return _store.GetUnreadMessages(agentName);
    }

    public string CheckInject(string agentName)
    {
        var messages = _store.GetUnreadMessages(agentName);

        if (messages.Count == 0)
        {
            return string.Empty;
        }

        var output = new System.Text.StringBuilder();

        // Check for pending nudge (priority banner)
        var pendingNudge = GetPendingNudgeSync(agentName);
        if (pendingNudge != null)
        {
            output.AppendLine($"🚨 PRIORITY: {pendingNudge.Reason} message from {pendingNudge.From} — \"{pendingNudge.Subject}\"");
            output.AppendLine();
        }

        output.AppendLine($"📬 {messages.Count} new message{(messages.Count == 1 ? "" : "s")}:");
        output.AppendLine();

        foreach (var msg in messages)
        {
            var readMarker = msg.Read ? " " : "*";
            var priorityTag = msg.Priority != MailPriority.Normal ? $" [{msg.Priority.ToString().ToUpper()}]" : "";

            output.AppendLine($"{readMarker} {msg.Id}  From: {msg.From} → To: {msg.To}{priorityTag}");
            output.AppendLine($"  Subject: {msg.Subject}  ({msg.Type})");
            output.AppendLine($"  {msg.Body}");
            if (msg.Payload != null)
            {
                output.AppendLine($"  Payload: {msg.Payload}");
            }
            output.AppendLine($"  {msg.CreatedAt:O}");
            output.AppendLine();
        }

        return output.ToString();
    }

    public (bool AlreadyRead, MailMessage? Message) MarkAsRead(string messageId)
    {
        var message = _store.GetById(messageId);
        if (message == null)
        {
            return (false, null);
        }

        var wasUnread = _store.MarkAsRead(messageId);
        return (!wasUnread, message);
    }

    public string Reply(string messageId, string body, string from)
    {
        var original = _store.GetById(messageId);
        if (original == null)
        {
            throw new ArgumentException($"Message {messageId} not found");
        }

        var reply = new MailMessage
        {
            Id = Guid.NewGuid().ToString(),
            From = from,
            To = original.From,
            Subject = $"Re: {original.Subject}",
            Body = body,
            Type = MailMessageType.Status,
            Priority = MailPriority.Normal,
            ThreadId = original.ThreadId ?? original.Id
        };

        return Send(reply);
    }

    public List<MailMessage> List(string? from = null, string? to = null, bool? unread = null)
    {
        return _store.GetMessages(from, to, unread);
    }

    public async Task<PendingNudge?> GetPendingNudgeAsync(string agentName)
    {
        var filePath = Path.Combine(_pendingNudgesPath, $"{agentName}.json");
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var nudge = JsonSerializer.Deserialize<PendingNudge>(json);
            File.Delete(filePath); // Clear after reading
            return nudge;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read pending nudge for {AgentName}", agentName);
            // Try to delete corrupt file
            try { File.Delete(filePath); } catch { }
            return null;
        }
    }

    public async Task ClearPendingNudgeAsync(string agentName)
    {
        var filePath = Path.Combine(_pendingNudgesPath, $"{agentName}.json");
        if (File.Exists(filePath))
        {
            try
            {
                await Task.Run(() => File.Delete(filePath));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clear pending nudge for {AgentName}", agentName);
            }
        }
    }

    private PendingNudge? GetPendingNudgeSync(string agentName)
    {
        var filePath = Path.Combine(_pendingNudgesPath, $"{agentName}.json");
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var nudge = JsonSerializer.Deserialize<PendingNudge>(json);
            File.Delete(filePath); // Clear after reading
            return nudge;
        }
        catch
        {
            try { File.Delete(filePath); } catch { }
            return null;
        }
    }

    private void WritePendingNudge(string agentName, PendingNudge nudge)
    {
        var filePath = Path.Combine(_pendingNudgesPath, $"{agentName}.json");
        try
        {
            var json = JsonSerializer.Serialize(nudge, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write pending nudge for {AgentName}", agentName);
        }
    }
}
