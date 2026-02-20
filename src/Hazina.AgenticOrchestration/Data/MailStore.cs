using Microsoft.Data.Sqlite;
using Hazina.AgenticOrchestration.Models;
using System.Globalization;
using System.Text.Json;

namespace Hazina.AgenticOrchestration.Data;

/// <summary>
/// SQLite-based mail storage for inter-agent messaging
/// Inspired by Overstory's mail.db system
/// </summary>
public class MailStore : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly object _lock = new object(); // Thread safety lock
    private bool _disposed;

    public MailStore(string databasePath)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ConnectionString;

        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        InitializeSchema();
    }

    private void InitializeSchema()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS messages (
                id TEXT PRIMARY KEY,
                from_agent TEXT NOT NULL,
                to_agent TEXT NOT NULL,
                subject TEXT NOT NULL,
                body TEXT NOT NULL,
                type TEXT NOT NULL,
                priority TEXT NOT NULL,
                payload TEXT,
                read INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                thread_id TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_messages_to_unread ON messages(to_agent, read);
            CREATE INDEX IF NOT EXISTS idx_messages_created ON messages(created_at DESC);
            CREATE INDEX IF NOT EXISTS idx_messages_thread ON messages(thread_id);
        ";
        command.ExecuteNonQuery();
    }

    public void Insert(MailMessage message)
    {
        lock (_lock) // Thread safety
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO messages
                (id, from_agent, to_agent, subject, body, type, priority, payload, read, created_at, thread_id)
                VALUES
                (@id, @from, @to, @subject, @body, @type, @priority, @payload, @read, @created_at, @thread_id)
            ";

            command.Parameters.AddWithValue("@id", message.Id);
            command.Parameters.AddWithValue("@from", message.From);
            command.Parameters.AddWithValue("@to", message.To);
            command.Parameters.AddWithValue("@subject", message.Subject);
            command.Parameters.AddWithValue("@body", message.Body);
            command.Parameters.AddWithValue("@type", message.Type.ToString());
            command.Parameters.AddWithValue("@priority", message.Priority.ToString());
            command.Parameters.AddWithValue("@payload", message.Payload ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@read", message.Read ? 1 : 0);
            command.Parameters.AddWithValue("@created_at", message.CreatedAt.ToString("O"));
            command.Parameters.AddWithValue("@thread_id", message.ThreadId ?? (object)DBNull.Value);

            command.ExecuteNonQuery();
        }
    }

    public List<MailMessage> GetUnreadMessages(string agentName)
    {
        lock (_lock) // Thread safety
        {
            var messages = new List<MailMessage>();

            using var command = _connection.CreateCommand();
            command.CommandText = @"
                SELECT * FROM messages
                WHERE to_agent = @agent AND read = 0
                ORDER BY created_at ASC
            ";
            command.Parameters.AddWithValue("@agent", agentName);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                messages.Add(ReadMessage(reader));
            }

            return messages;
        }
    }

    public List<MailMessage> GetMessages(string? from = null, string? to = null, bool? unread = null)
    {
        lock (_lock) // Thread safety
        {
            var messages = new List<MailMessage>();
            var conditions = new List<string>();
            var parameters = new Dictionary<string, object>();

            if (!string.IsNullOrEmpty(from))
            {
                conditions.Add("from_agent = @from");
                parameters["@from"] = from;
            }

            if (!string.IsNullOrEmpty(to))
            {
                conditions.Add("to_agent = @to");
                parameters["@to"] = to;
            }

            if (unread.HasValue)
            {
                conditions.Add("read = @read");
                parameters["@read"] = unread.Value ? 0 : 1;
            }

            using var command = _connection.CreateCommand();
            command.CommandText = $@"
                SELECT * FROM messages
                {(conditions.Any() ? "WHERE " + string.Join(" AND ", conditions) : "")}
                ORDER BY created_at DESC
            ";

            foreach (var param in parameters)
            {
                command.Parameters.AddWithValue(param.Key, param.Value);
            }

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                messages.Add(ReadMessage(reader));
            }

            return messages;
        }
    }

    public bool MarkAsRead(string messageId)
    {
        lock (_lock) // Thread safety
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"
                UPDATE messages
                SET read = 1
                WHERE id = @id AND read = 0
            ";
            command.Parameters.AddWithValue("@id", messageId);

            var rowsAffected = command.ExecuteNonQuery();
            return rowsAffected > 0;
        }
    }

    public MailMessage? GetById(string messageId)
    {
        lock (_lock) // Thread safety
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "SELECT * FROM messages WHERE id = @id";
            command.Parameters.AddWithValue("@id", messageId);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return ReadMessage(reader);
            }

            return null;
        }
    }

    public int Purge(bool all = false, TimeSpan? olderThan = null, string? agentName = null)
    {
        lock (_lock) // Thread safety
        {
            var conditions = new List<string>();
            var parameters = new Dictionary<string, object>();

            if (!all)
            {
                if (olderThan.HasValue)
                {
                    var cutoff = DateTime.UtcNow - olderThan.Value;
                    conditions.Add("created_at < @cutoff");
                    parameters["@cutoff"] = cutoff.ToString("O");
                }

                if (!string.IsNullOrEmpty(agentName))
                {
                    conditions.Add("(from_agent = @agent OR to_agent = @agent)");
                    parameters["@agent"] = agentName;
                }
            }

            using var command = _connection.CreateCommand();
            command.CommandText = $@"
                DELETE FROM messages
                {(conditions.Any() ? "WHERE " + string.Join(" AND ", conditions) : "")}
            ";

            foreach (var param in parameters)
            {
                command.Parameters.AddWithValue(param.Key, param.Value);
            }

            return command.ExecuteNonQuery();
        }
    }

    private static MailMessage ReadMessage(SqliteDataReader reader)
    {
        return new MailMessage
        {
            Id = reader.GetString(0),
            From = reader.GetString(1),
            To = reader.GetString(2),
            Subject = reader.GetString(3),
            Body = reader.GetString(4),
            Type = Enum.Parse<MailMessageType>(reader.GetString(5)),
            Priority = Enum.Parse<MailPriority>(reader.GetString(6)),
            Payload = reader.IsDBNull(7) ? null : reader.GetString(7),
            Read = reader.GetInt32(8) == 1,
            CreatedAt = DateTime.ParseExact(reader.GetString(9), "O", CultureInfo.InvariantCulture),
            ThreadId = reader.IsDBNull(10) ? null : reader.GetString(10)
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _connection?.Dispose();
            _disposed = true;
        }
    }
}
