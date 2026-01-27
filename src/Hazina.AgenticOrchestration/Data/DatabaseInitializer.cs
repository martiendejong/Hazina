using System.Data.SQLite;

namespace Hazina.AgenticOrchestration.Data;

/// <summary>
/// Initializes the agent-activity.db database with required tables
/// </summary>
public class DatabaseInitializer
{
    private readonly string _dbPath;

    public DatabaseInitializer(string dbPath)
    {
        _dbPath = dbPath;
    }

    public void Initialize()
    {
        using var conn = new SQLiteConnection($"Data Source={_dbPath}");
        conn.Open();

        using var cmd = conn.CreateCommand();

        // Create interaction_requests table
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS interaction_requests (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id TEXT NOT NULL,
                request_type TEXT NOT NULL,
                prompt TEXT NOT NULL,
                options TEXT,
                created_at DATETIME NOT NULL,
                status TEXT NOT NULL DEFAULT 'pending',
                response TEXT,
                responded_at DATETIME,
                responded_by TEXT,
                FOREIGN KEY (session_id) REFERENCES agent_sessions(session_id)
            )";
        cmd.ExecuteNonQuery();

        // Create indexes
        cmd.CommandText = @"
            CREATE INDEX IF NOT EXISTS idx_interaction_requests_status
                ON interaction_requests(status, created_at)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
            CREATE INDEX IF NOT EXISTS idx_interaction_requests_session
                ON interaction_requests(session_id)";
        cmd.ExecuteNonQuery();
    }
}
