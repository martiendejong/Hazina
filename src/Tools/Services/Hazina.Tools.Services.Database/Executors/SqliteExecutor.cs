using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Hazina.Tools.Services.Database.Core;

namespace Hazina.Tools.Services.Database.Executors;

/// <summary>
/// SQLite database executor with connection pooling and safe execution.
/// </summary>
public class SqliteExecutor : IDbExecutor
{
    private readonly string _connectionString;
    private SqliteConnection? _connection;
    private bool _disposed;

    /// <summary>
    /// Creates a new SQLite executor.
    /// </summary>
    /// <param name="databasePath">Path to SQLite database file.</param>
    public SqliteExecutor(string databasePath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    /// <summary>
    /// Creates a new SQLite executor with a connection string.
    /// </summary>
    /// <param name="connectionString">SQLite connection string.</param>
    /// <param name="isConnectionString">Marker to differentiate from path constructor.</param>
    public SqliteExecutor(string connectionString, bool isConnectionString)
    {
        _connectionString = connectionString;
    }

    private async Task<SqliteConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection == null)
        {
            _connection = new SqliteConnection(_connectionString);
            await _connection.OpenAsync(cancellationToken);
        }
        else if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync(cancellationToken);
        }

        return _connection;
    }

    /// <inheritdoc />
    public async Task<QueryResult> ExecuteAsync(CompiledQuery query, CancellationToken cancellationToken = default)
    {
        var result = new QueryResult();
        var stopwatch = Stopwatch.StartNew();

        if (!query.IsValid)
        {
            result.Error = string.Join("; ", query.Errors);
            return result;
        }

        try
        {
            var connection = await GetConnectionAsync(cancellationToken);
            using var command = connection.CreateCommand();
            command.CommandText = query.Sql;

            // Add parameters
            foreach (var param in query.Parameters)
            {
                command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
            }

            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            // Get column names
            for (int i = 0; i < reader.FieldCount; i++)
            {
                result.Columns.Add(reader.GetName(i));
            }

            // Read rows
            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var value = reader.GetValue(i);
                    row[result.Columns[i]] = value == DBNull.Value ? null : value;
                }
                result.Rows.Add(row);
            }
        }
        catch (SqliteException ex)
        {
            result.Error = $"Database error: {ex.Message}";
        }
        catch (Exception ex)
        {
            result.Error = $"Query execution failed: {ex.Message}";
        }
        finally
        {
            stopwatch.Stop();
            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<int> ExecuteNonQueryAsync(string sql, Dictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        if (parameters != null)
        {
            foreach (var param in parameters)
            {
                command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
            }
        }

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DatabaseSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        var schema = new DatabaseSchema();
        var connection = await GetConnectionAsync(cancellationToken);

        // Get all tables
        using var tablesCommand = connection.CreateCommand();
        tablesCommand.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";

        var tableNames = new List<string>();
        using (var reader = await tablesCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                tableNames.Add(reader.GetString(0));
            }
        }

        // Get columns for each table
        foreach (var tableName in tableNames)
        {
            var tableSchema = new TableSchema { Name = tableName };

            using var columnsCommand = connection.CreateCommand();
            columnsCommand.CommandText = $"PRAGMA table_info(\"{tableName}\")";

            using var columnsReader = await columnsCommand.ExecuteReaderAsync(cancellationToken);
            while (await columnsReader.ReadAsync(cancellationToken))
            {
                var columnName = columnsReader.GetString(1);
                var columnType = columnsReader.GetString(2);

                tableSchema.Columns.Add(new ColumnSchema
                {
                    Name = columnName,
                    DataType = MapSqliteType(columnType)
                });
            }

            schema.AddTable(tableSchema);
        }

        return schema;
    }

    /// <inheritdoc />
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = await GetConnectionAsync(cancellationToken);
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string MapSqliteType(string sqliteType)
    {
        var upperType = sqliteType.ToUpperInvariant();
        if (upperType.Contains("INT")) return "INTEGER";
        if (upperType.Contains("CHAR") || upperType.Contains("TEXT") || upperType.Contains("CLOB")) return "TEXT";
        if (upperType.Contains("REAL") || upperType.Contains("FLOA") || upperType.Contains("DOUB")) return "REAL";
        if (upperType.Contains("BLOB")) return "BLOB";
        if (upperType.Contains("DATE") || upperType.Contains("TIME")) return "DATETIME";
        return "TEXT";
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            if (_connection != null)
            {
                await _connection.DisposeAsync();
                _connection = null;
            }
            _disposed = true;
        }
    }
}
