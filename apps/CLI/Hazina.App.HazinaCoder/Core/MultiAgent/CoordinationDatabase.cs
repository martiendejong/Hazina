using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Hazina.App.HazinaCoder.Core.MultiAgent;

/// <summary>
/// SQLite coordination database for multi-agent collaboration.
/// Provides ACID guarantees with WAL mode for concurrent access.
///
/// Improvement #36: SQLite Coordination DB (Value: 10/10, Effort: 7/10, Ratio: 1.43)
/// </summary>
public class CoordinationDatabase : IDisposable
{
    private readonly string _dbPath;
    private SqliteConnection? _connection;
    private readonly object _lock = new();

    public CoordinationDatabase(string basePath)
    {
        var dbDir = Path.Combine(basePath, "data", "coordination");
        if (!Directory.Exists(dbDir))
            Directory.CreateDirectory(dbDir);

        _dbPath = Path.Combine(dbDir, "coordination.db");
    }

    /// <summary>
    /// Initialize database with schema
    /// </summary>
    public async Task InitializeAsync()
    {
        lock (_lock)
        {
            _connection = new SqliteConnection($"Data Source={_dbPath}");
            _connection.Open();

            // Enable WAL mode for concurrent reads/writes
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode=WAL;";
            cmd.ExecuteNonQuery();
        }

        await CreateSchemaAsync();
    }

    private async Task CreateSchemaAsync()
    {
        var schema = @"
            -- Agent registry
            CREATE TABLE IF NOT EXISTS agents (
                agent_id TEXT PRIMARY KEY,
                agent_name TEXT NOT NULL,
                agent_type TEXT NOT NULL,
                started_at TEXT NOT NULL,
                last_heartbeat TEXT NOT NULL,
                status TEXT NOT NULL CHECK(status IN ('active', 'idle', 'crashed', 'stopped')),
                work_item TEXT,
                metadata TEXT
            );

            -- Resource allocations (worktrees, processes, files)
            CREATE TABLE IF NOT EXISTS allocations (
                allocation_id TEXT PRIMARY KEY,
                resource_type TEXT NOT NULL CHECK(resource_type IN ('worktree', 'process', 'file', 'port', 'database')),
                resource_path TEXT NOT NULL,
                agent_id TEXT NOT NULL,
                allocated_at TEXT NOT NULL,
                expires_at TEXT,
                status TEXT NOT NULL CHECK(status IN ('allocated', 'released', 'expired')),
                metadata TEXT,
                FOREIGN KEY (agent_id) REFERENCES agents(agent_id)
            );

            -- Conflict detection log
            CREATE TABLE IF NOT EXISTS conflicts (
                conflict_id TEXT PRIMARY KEY,
                conflict_type TEXT NOT NULL,
                resource_path TEXT NOT NULL,
                agent1_id TEXT NOT NULL,
                agent2_id TEXT NOT NULL,
                detected_at TEXT NOT NULL,
                resolution TEXT,
                resolved_at TEXT,
                FOREIGN KEY (agent1_id) REFERENCES agents(agent_id),
                FOREIGN KEY (agent2_id) REFERENCES agents(agent_id)
            );

            -- Coordination events (messages between agents)
            CREATE TABLE IF NOT EXISTS events (
                event_id TEXT PRIMARY KEY,
                event_type TEXT NOT NULL,
                from_agent_id TEXT,
                to_agent_id TEXT,
                created_at TEXT NOT NULL,
                payload TEXT,
                processed_at TEXT,
                FOREIGN KEY (from_agent_id) REFERENCES agents(agent_id),
                FOREIGN KEY (to_agent_id) REFERENCES agents(agent_id)
            );

            -- Indexes for performance
            CREATE INDEX IF NOT EXISTS idx_agents_status ON agents(status);
            CREATE INDEX IF NOT EXISTS idx_agents_heartbeat ON agents(last_heartbeat);
            CREATE INDEX IF NOT EXISTS idx_allocations_resource ON allocations(resource_type, resource_path);
            CREATE INDEX IF NOT EXISTS idx_allocations_agent ON allocations(agent_id, status);
            CREATE INDEX IF NOT EXISTS idx_conflicts_resource ON conflicts(resource_path);
            CREATE INDEX IF NOT EXISTS idx_events_to_agent ON events(to_agent_id, processed_at);
        ";

        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = schema;
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Register new agent in coordination database
    /// </summary>
    public async Task<bool> RegisterAgentAsync(string agentId, string agentName, string agentType)
    {
        try
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO agents (agent_id, agent_name, agent_type, started_at, last_heartbeat, status)
                VALUES (@agentId, @agentName, @agentType, @now, @now, 'active')
                ON CONFLICT(agent_id) DO UPDATE SET
                    last_heartbeat = @now,
                    status = 'active'
            ";
            cmd.Parameters.AddWithValue("@agentId", agentId);
            cmd.Parameters.AddWithValue("@agentName", agentName);
            cmd.Parameters.AddWithValue("@agentType", agentType);
            cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));

            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Failed to register agent: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Update agent heartbeat (liveness)
    /// </summary>
    public async Task<bool> UpdateHeartbeatAsync(string agentId, string? workItem = null)
    {
        try
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"
                UPDATE agents
                SET last_heartbeat = @now, work_item = @workItem
                WHERE agent_id = @agentId
            ";
            cmd.Parameters.AddWithValue("@agentId", agentId);
            cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));
            cmd.Parameters.AddWithValue("@workItem", workItem ?? (object)DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Failed to update heartbeat: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Allocate resource with conflict detection
    /// </summary>
    public async Task<AllocationResult> TryAllocateResourceAsync(
        string agentId,
        string resourceType,
        string resourcePath,
        TimeSpan? expiresIn = null)
    {
        try
        {
            // Check for existing allocations (conflict detection)
            using var checkCmd = _connection!.CreateCommand();
            checkCmd.CommandText = @"
                SELECT agent_id, allocated_at
                FROM allocations
                WHERE resource_type = @resourceType
                  AND resource_path = @resourcePath
                  AND status = 'allocated'
            ";
            checkCmd.Parameters.AddWithValue("@resourceType", resourceType);
            checkCmd.Parameters.AddWithValue("@resourcePath", resourcePath);

            using var reader = await checkCmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var existingAgentId = reader.GetString(0);
                var allocatedAt = reader.GetString(1);

                // Log conflict
                await LogConflictAsync(agentId, existingAgentId, resourceType, resourcePath);

                return new AllocationResult
                {
                    Success = false,
                    ConflictWithAgent = existingAgentId,
                    ConflictDetected = true,
                    Message = $"Resource already allocated to agent {existingAgentId} at {allocatedAt}"
                };
            }

            // No conflict - proceed with allocation
            var allocationId = Guid.NewGuid().ToString();
            var expiresAt = expiresIn.HasValue
                ? DateTime.UtcNow.Add(expiresIn.Value).ToString("O")
                : (object)DBNull.Value;

            using var allocCmd = _connection.CreateCommand();
            allocCmd.CommandText = @"
                INSERT INTO allocations (allocation_id, resource_type, resource_path, agent_id, allocated_at, expires_at, status)
                VALUES (@allocationId, @resourceType, @resourcePath, @agentId, @now, @expiresAt, 'allocated')
            ";
            allocCmd.Parameters.AddWithValue("@allocationId", allocationId);
            allocCmd.Parameters.AddWithValue("@resourceType", resourceType);
            allocCmd.Parameters.AddWithValue("@resourcePath", resourcePath);
            allocCmd.Parameters.AddWithValue("@agentId", agentId);
            allocCmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));
            allocCmd.Parameters.AddWithValue("@expiresAt", expiresAt);

            await allocCmd.ExecuteNonQueryAsync();

            return new AllocationResult
            {
                Success = true,
                AllocationId = allocationId,
                Message = "Resource allocated successfully"
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Allocation failed: {ex.Message}");
            return new AllocationResult
            {
                Success = false,
                Message = $"Allocation error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Release allocated resource
    /// </summary>
    public async Task<bool> ReleaseResourceAsync(string allocationId)
    {
        try
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"
                UPDATE allocations
                SET status = 'released'
                WHERE allocation_id = @allocationId
            ";
            cmd.Parameters.AddWithValue("@allocationId", allocationId);

            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Release failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Detect crashed agents (no heartbeat in last 30 seconds)
    /// </summary>
    public async Task<List<string>> DetectCrashedAgentsAsync(TimeSpan timeout)
    {
        var crashed = new List<string>();

        try
        {
            var cutoff = DateTime.UtcNow.Subtract(timeout).ToString("O");

            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"
                SELECT agent_id
                FROM agents
                WHERE status = 'active'
                  AND last_heartbeat < @cutoff
            ";
            cmd.Parameters.AddWithValue("@cutoff", cutoff);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                crashed.Add(reader.GetString(0));
            }

            // Mark as crashed
            if (crashed.Any())
            {
                using var updateCmd = _connection.CreateCommand();
                var parameterPlaceholders = string.Join(",", crashed.Select((_, i) => $"@id{i}"));
                updateCmd.CommandText = $@"
                    UPDATE agents
                    SET status = 'crashed'
                    WHERE agent_id IN ({parameterPlaceholders})
                ";

                for (int i = 0; i < crashed.Count; i++)
                {
                    updateCmd.Parameters.AddWithValue($"@id{i}", crashed[i]);
                }

                await updateCmd.ExecuteNonQueryAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Crash detection failed: {ex.Message}");
        }

        return crashed;
    }

    /// <summary>
    /// Log conflict between agents
    /// </summary>
    private async Task LogConflictAsync(string agent1, string agent2, string resourceType, string resourcePath)
    {
        try
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO conflicts (conflict_id, conflict_type, resource_path, agent1_id, agent2_id, detected_at)
                VALUES (@conflictId, @conflictType, @resourcePath, @agent1, @agent2, @now)
            ";
            cmd.Parameters.AddWithValue("@conflictId", Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("@conflictType", resourceType);
            cmd.Parameters.AddWithValue("@resourcePath", resourcePath);
            cmd.Parameters.AddWithValue("@agent1", agent1);
            cmd.Parameters.AddWithValue("@agent2", agent2);
            cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));

            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Conflict logging failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Get all active agents
    /// </summary>
    public async Task<List<AgentInfo>> GetActiveAgentsAsync()
    {
        var agents = new List<AgentInfo>();

        try
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"
                SELECT agent_id, agent_name, agent_type, started_at, last_heartbeat, work_item
                FROM agents
                WHERE status = 'active'
            ";

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                agents.Add(new AgentInfo
                {
                    AgentId = reader.GetString(0),
                    AgentName = reader.GetString(1),
                    AgentType = reader.GetString(2),
                    StartedAt = DateTime.Parse(reader.GetString(3)),
                    LastHeartbeat = DateTime.Parse(reader.GetString(4)),
                    WorkItem = reader.IsDBNull(5) ? null : reader.GetString(5)
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Failed to get active agents: {ex.Message}");
        }

        return agents;
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}

/// <summary>
/// Result of resource allocation attempt
/// </summary>
public class AllocationResult
{
    public bool Success { get; set; }
    public string? AllocationId { get; set; }
    public bool ConflictDetected { get; set; }
    public string? ConflictWithAgent { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Information about an active agent
/// </summary>
public class AgentInfo
{
    public string AgentId { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public string AgentType { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime LastHeartbeat { get; set; }
    public string? WorkItem { get; set; }
}
