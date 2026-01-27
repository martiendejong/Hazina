using System.Data.SQLite;
using System.Text.Json;
using Hazina.AgenticOrchestration.Models;
using Hazina.AgenticOrchestration.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Hazina.AgenticOrchestration.Services;

public class InteractionService : IInteractionService
{
    private readonly IHubContext<ClaudeOrchestrationHub> _hubContext;
    private readonly string _dbPath;

    public InteractionService(
        IHubContext<ClaudeOrchestrationHub> hubContext,
        string dbPath)
    {
        _hubContext = hubContext;
        _dbPath = dbPath;
    }

    public async Task<long> NotifyAwaitingInputAsync(string sessionId, AwaitInputRequest request)
    {
        // Store in database
        using var conn = new SQLiteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var cmd = new SQLiteCommand(@"
            INSERT INTO interaction_requests (
                session_id,
                request_type,
                prompt,
                options,
                created_at,
                status
            ) VALUES (
                @sessionId,
                @requestType,
                @prompt,
                @options,
                datetime('now'),
                'pending'
            )
            RETURNING id
        ", conn);

        cmd.Parameters.AddWithValue("@sessionId", sessionId);
        cmd.Parameters.AddWithValue("@requestType", request.Type);
        cmd.Parameters.AddWithValue("@prompt", request.Prompt);
        cmd.Parameters.AddWithValue("@options", JsonSerializer.Serialize(request.Options));

        var interactionId = (long)(await cmd.ExecuteScalarAsync() ?? 0L);

        // Get agent name
        var agentName = await GetAgentNameAsync(sessionId);

        // Send real-time notification to web clients
        await _hubContext.Clients
            .Group($"instance-{sessionId}")
            .SendAsync("InputRequired", new
            {
                SessionId = sessionId,
                InteractionId = interactionId,
                Type = request.Type,
                Prompt = request.Prompt,
                Options = request.Options,
                Timestamp = DateTime.UtcNow
            });

        // Also send to all users with permission to interact
        await _hubContext.Clients
            .Group("agentic-orchestrators")
            .SendAsync("GlobalInputRequired", new
            {
                SessionId = sessionId,
                InteractionId = interactionId,
                AgentName = agentName,
                Prompt = request.Prompt
            });

        return interactionId;
    }

    public async Task<UserResponse?> PollForResponseAsync(string sessionId, long interactionId)
    {
        using var conn = new SQLiteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var cmd = new SQLiteCommand(@"
            SELECT response, responded_at, responded_by
            FROM interaction_requests
            WHERE id = @interactionId
              AND session_id = @sessionId
              AND status = 'responded'
        ", conn);

        cmd.Parameters.AddWithValue("@interactionId", interactionId);
        cmd.Parameters.AddWithValue("@sessionId", sessionId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new UserResponse
            {
                Response = reader.GetString(0),
                RespondedAt = DateTime.Parse(reader.GetString(1)),
                RespondedBy = reader.GetString(2)
            };
        }

        return null;
    }

    public async Task<bool> RespondToInteractionAsync(string sessionId, long interactionId, string response, string userId)
    {
        using var conn = new SQLiteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var cmd = new SQLiteCommand(@"
            UPDATE interaction_requests
            SET status = 'responded',
                response = @response,
                responded_at = datetime('now'),
                responded_by = @userId
            WHERE id = @interactionId
              AND session_id = @sessionId
              AND status = 'pending'
        ", conn);

        cmd.Parameters.AddWithValue("@interactionId", interactionId);
        cmd.Parameters.AddWithValue("@sessionId", sessionId);
        cmd.Parameters.AddWithValue("@response", response);
        cmd.Parameters.AddWithValue("@userId", userId);

        int rowsAffected = await cmd.ExecuteNonQueryAsync();

        if (rowsAffected > 0)
        {
            // Notify Claude instance via SignalR
            await _hubContext.Clients
                .Group($"instance-{sessionId}")
                .SendAsync("ResponseReceived", new
                {
                    InteractionId = interactionId,
                    Response = response,
                    RespondedBy = userId,
                    Timestamp = DateTime.UtcNow
                });

            return true;
        }

        return false;
    }

    public async Task<List<PendingInteraction>> GetPendingInteractionsAsync()
    {
        using var conn = new SQLiteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var cmd = new SQLiteCommand(@"
            SELECT
                ir.id,
                ir.session_id,
                ags.agent_name,
                ir.request_type,
                ir.prompt,
                ir.options,
                ir.created_at
            FROM interaction_requests ir
            JOIN agent_sessions ags ON ir.session_id = ags.session_id
            WHERE ir.status = 'pending'
            ORDER BY ir.created_at DESC
        ", conn);

        var pending = new List<PendingInteraction>();

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            pending.Add(new PendingInteraction
            {
                Id = reader.GetInt64(0),
                SessionId = reader.GetString(1),
                AgentName = reader.GetString(2),
                Type = reader.GetString(3),
                Prompt = reader.GetString(4),
                Options = JsonSerializer.Deserialize<List<string>>(reader.GetString(5)) ?? new List<string>(),
                CreatedAt = DateTime.Parse(reader.GetString(6))
            });
        }

        return pending;
    }

    private async Task<string> GetAgentNameAsync(string sessionId)
    {
        using var conn = new SQLiteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var cmd = new SQLiteCommand(@"
            SELECT agent_name
            FROM agent_sessions
            WHERE session_id = @sessionId
        ", conn);
        cmd.Parameters.AddWithValue("@sessionId", sessionId);

        var result = await cmd.ExecuteScalarAsync();
        return result?.ToString() ?? "Unknown Agent";
    }
}
