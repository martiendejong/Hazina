using System.Data.SQLite;
using Hazina.AgenticOrchestration.Models;

namespace Hazina.AgenticOrchestration.Services;

public class ClaudeInstanceManager : IClaudeInstanceManager
{
    private readonly string _dbPath;

    public ClaudeInstanceManager(string dbPath)
    {
        _dbPath = dbPath;
    }

    public async Task<List<ClaudeInstance>> GetActiveInstancesAsync()
    {
        var instances = new List<ClaudeInstance>();

        using var conn = new SQLiteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var cmd = new SQLiteCommand(@"
            SELECT
                session_id,
                agent_name,
                start_time,
                last_heartbeat,
                status,
                current_task
            FROM agent_sessions
            WHERE status = 'active'
              AND julianday('now') - julianday(last_heartbeat) < 0.0007
            ORDER BY last_heartbeat DESC
        ", conn);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            instances.Add(new ClaudeInstance
            {
                SessionId = reader.GetString(0),
                AgentName = reader.GetString(1),
                StartTime = DateTime.Parse(reader.GetString(2)),
                LastHeartbeat = DateTime.Parse(reader.GetString(3)),
                Status = reader.GetString(4),
                CurrentTask = reader.IsDBNull(5) ? null : reader.GetString(5)
            });
        }

        return instances;
    }

    public async Task<ClaudeInstance?> GetInstanceDetailsAsync(string sessionId)
    {
        using var conn = new SQLiteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var cmd = new SQLiteCommand(@"
            SELECT
                session_id,
                agent_name,
                start_time,
                last_heartbeat,
                status,
                current_task,
                worktree_seat,
                tasks_completed,
                tasks_failed
            FROM agent_sessions
            WHERE session_id = @sessionId
        ", conn);
        cmd.Parameters.AddWithValue("@sessionId", sessionId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new ClaudeInstance
            {
                SessionId = reader.GetString(0),
                AgentName = reader.GetString(1),
                StartTime = DateTime.Parse(reader.GetString(2)),
                LastHeartbeat = DateTime.Parse(reader.GetString(3)),
                Status = reader.GetString(4),
                CurrentTask = reader.IsDBNull(5) ? null : reader.GetString(5),
                WorktreeSeat = reader.IsDBNull(6) ? null : reader.GetString(6),
                TasksCompleted = reader.GetInt32(7),
                TasksFailed = reader.GetInt32(8)
            };
        }

        return null;
    }
}
