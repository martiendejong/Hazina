using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Hazina.AgenticOrchestration.Services.Chat;

/// <summary>
/// Repository for persisting chat conversations to disk
/// </summary>
public class ConversationRepository
{
    private readonly string _conversationsPath;
    private readonly ILogger<ConversationRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ConversationRepository(string conversationsPath, ILogger<ConversationRepository> logger)
    {
        _conversationsPath = conversationsPath;
        _logger = logger;

        // Ensure directory exists
        Directory.CreateDirectory(_conversationsPath);

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// Save a conversation to disk
    /// </summary>
    public async Task SaveAsync(string sessionId, ConversationHistory conversation, CancellationToken ct = default)
    {
        try
        {
            var filePath = GetConversationPath(sessionId);
            var json = JsonSerializer.Serialize(conversation, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json, ct);

            _logger.LogInformation("Saved conversation for session {SessionId} ({MessageCount} messages)",
                sessionId, conversation.Messages.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save conversation for session {SessionId}", sessionId);
            throw;
        }
    }

    /// <summary>
    /// Load a conversation from disk
    /// </summary>
    public async Task<ConversationHistory?> LoadAsync(string sessionId, CancellationToken ct = default)
    {
        try
        {
            var filePath = GetConversationPath(sessionId);

            if (!File.Exists(filePath))
            {
                _logger.LogDebug("No saved conversation found for session {SessionId}", sessionId);
                return null;
            }

            var json = await File.ReadAllTextAsync(filePath, ct);
            var conversation = JsonSerializer.Deserialize<ConversationHistory>(json, _jsonOptions);

            _logger.LogInformation("Loaded conversation for session {SessionId} ({MessageCount} messages)",
                sessionId, conversation?.Messages.Count ?? 0);

            return conversation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load conversation for session {SessionId}", sessionId);
            return null;
        }
    }

    /// <summary>
    /// Delete a conversation
    /// </summary>
    public async Task DeleteAsync(string sessionId, CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            try
            {
                var filePath = GetConversationPath(sessionId);

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation("Deleted conversation for session {SessionId}", sessionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete conversation for session {SessionId}", sessionId);
                throw;
            }
        }, ct);
    }

    /// <summary>
    /// List all saved conversations
    /// </summary>
    public async Task<List<ConversationMetadata>> ListAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                var conversations = new List<ConversationMetadata>();
                var files = Directory.GetFiles(_conversationsPath, "chat-*.json");

                foreach (var file in files)
                {
                    var sessionId = Path.GetFileNameWithoutExtension(file);
                    var fileInfo = new FileInfo(file);

                    conversations.Add(new ConversationMetadata
                    {
                        SessionId = sessionId,
                        LastModified = fileInfo.LastWriteTimeUtc,
                        SizeBytes = fileInfo.Length
                    });
                }

                return conversations.OrderByDescending(c => c.LastModified).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list conversations");
                return new List<ConversationMetadata>();
            }
        }, ct);
    }

    /// <summary>
    /// Export conversation to JSON file
    /// </summary>
    public async Task<string> ExportAsync(string sessionId, string exportPath, CancellationToken ct = default)
    {
        try
        {
            var conversation = await LoadAsync(sessionId, ct);

            if (conversation == null)
            {
                throw new InvalidOperationException($"Conversation {sessionId} not found");
            }

            var fileName = $"conversation-{sessionId}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
            var fullPath = Path.Combine(exportPath, fileName);

            Directory.CreateDirectory(exportPath);

            var json = JsonSerializer.Serialize(conversation, _jsonOptions);
            await File.WriteAllTextAsync(fullPath, json, ct);

            _logger.LogInformation("Exported conversation {SessionId} to {Path}", sessionId, fullPath);

            return fullPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export conversation {SessionId}", sessionId);
            throw;
        }
    }

    /// <summary>
    /// Get total storage size
    /// </summary>
    public long GetTotalSizeBytes()
    {
        try
        {
            var files = Directory.GetFiles(_conversationsPath, "chat-*.json");
            return files.Sum(f => new FileInfo(f).Length);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Clean up old conversations (older than specified days)
    /// </summary>
    public async Task CleanupOldAsync(int olderThanDays, CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);
                var files = Directory.GetFiles(_conversationsPath, "chat-*.json");
                var deletedCount = 0;

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);

                    if (fileInfo.LastWriteTimeUtc < cutoffDate)
                    {
                        File.Delete(file);
                        deletedCount++;
                    }
                }

                _logger.LogInformation("Cleaned up {Count} old conversations (older than {Days} days)",
                    deletedCount, olderThanDays);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clean up old conversations");
            }
        }, ct);
    }

    private string GetConversationPath(string sessionId)
    {
        // Sanitize session ID to prevent path traversal
        var sanitized = string.Join("", sessionId.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'));
        return Path.Combine(_conversationsPath, $"{sanitized}.json");
    }
}

/// <summary>
/// Conversation history model
/// </summary>
public class ConversationHistory
{
    public string SessionId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastMessageAt { get; set; }
    public List<ChatMessageRecord> Messages { get; set; } = new();
    public ConversationMetrics Metrics { get; set; } = new();
}

/// <summary>
/// Chat message record for persistence
/// </summary>
public class ChatMessageRecord
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public List<ToolCallRecord>? ToolCalls { get; set; }
}

/// <summary>
/// Tool call record
/// </summary>
public class ToolCallRecord
{
    public string ToolName { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public int DurationMs { get; set; }
}

/// <summary>
/// Conversation metrics
/// </summary>
public class ConversationMetrics
{
    public int TotalMessages { get; set; }
    public int TotalTokens { get; set; }
    public int TotalToolCalls { get; set; }
    public Dictionary<string, int> ToolUsage { get; set; } = new();
}

/// <summary>
/// Conversation metadata for listing
/// </summary>
public class ConversationMetadata
{
    public string SessionId { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
    public long SizeBytes { get; set; }
}
