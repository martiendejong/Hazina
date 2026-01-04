using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hazina.Observability.LLMLogs.Configuration;
using Hazina.Observability.LLMLogs.Storage.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Hazina.Observability.LLMLogs.Storage
{
    /// <summary>
    /// SQLite implementation of LLM log repository.
    /// </summary>
    public class SqliteLLMLogRepository : ILLMLogRepository
    {
        private readonly LLMLoggingOptions _options;
        private readonly string _connectionString;
        private bool _initialized;
        private readonly SemaphoreSlim _initLock = new(1, 1);

        public SqliteLLMLogRepository(IOptions<LLMLoggingOptions> options)
        {
            _options = options.Value;

            // Resolve database path
            var dbPath = _options.DatabasePath;
            if (!Path.IsPathRooted(dbPath))
            {
                dbPath = Path.Combine(AppContext.BaseDirectory, dbPath);
            }

            // Ensure directory exists
            var directory = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _connectionString = $"Data Source={dbPath}";
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_initialized) return;

            await _initLock.WaitAsync(cancellationToken);
            try
            {
                if (_initialized) return;

                await using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                var createTableSql = @"
                    CREATE TABLE IF NOT EXISTS llm_call_logs (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        call_id TEXT NOT NULL UNIQUE,
                        parent_call_id TEXT NULL,
                        username TEXT NOT NULL,
                        feature TEXT NOT NULL,
                        step TEXT NULL,
                        datetime_utc TEXT NOT NULL,
                        provider TEXT NOT NULL,
                        model TEXT NOT NULL,
                        is_tool_call INTEGER NOT NULL DEFAULT 0,
                        tool_name TEXT NULL,
                        tool_arguments TEXT NULL,
                        request_messages TEXT NOT NULL,
                        response_data TEXT NOT NULL,
                        message_count INTEGER NOT NULL DEFAULT 0,
                        embedded_documents TEXT NULL,
                        embedded_document_count INTEGER NOT NULL DEFAULT 0,
                        input_tokens INTEGER NOT NULL DEFAULT 0,
                        output_tokens INTEGER NOT NULL DEFAULT 0,
                        total_tokens INTEGER NOT NULL DEFAULT 0,
                        input_cost REAL NOT NULL DEFAULT 0.0,
                        output_cost REAL NOT NULL DEFAULT 0.0,
                        total_cost REAL NOT NULL DEFAULT 0.0,
                        execution_time_ms INTEGER NOT NULL DEFAULT 0,
                        success INTEGER NOT NULL DEFAULT 1,
                        error_message TEXT NULL,
                        created_at TEXT NOT NULL DEFAULT (datetime('now'))
                    );

                    CREATE INDEX IF NOT EXISTS idx_call_id ON llm_call_logs(call_id);
                    CREATE INDEX IF NOT EXISTS idx_parent_call_id ON llm_call_logs(parent_call_id);
                    CREATE INDEX IF NOT EXISTS idx_username ON llm_call_logs(username);
                    CREATE INDEX IF NOT EXISTS idx_feature ON llm_call_logs(feature);
                    CREATE INDEX IF NOT EXISTS idx_provider ON llm_call_logs(provider);
                    CREATE INDEX IF NOT EXISTS idx_datetime_utc ON llm_call_logs(datetime_utc);
                    CREATE INDEX IF NOT EXISTS idx_created_at ON llm_call_logs(created_at);
                ";

                await using var command = new SqliteCommand(createTableSql, connection);
                await command.ExecuteNonQueryAsync(cancellationToken);

                _initialized = true;
            }
            finally
            {
                _initLock.Release();
            }
        }

        public async Task LogCallAsync(LLMCallLog log, CancellationToken cancellationToken = default)
        {
            await InitializeAsync(cancellationToken);

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
                INSERT INTO llm_call_logs (
                    call_id, parent_call_id, username, feature, step, datetime_utc,
                    provider, model, is_tool_call, tool_name, tool_arguments,
                    request_messages, response_data,
                    message_count, embedded_documents, embedded_document_count,
                    input_tokens, output_tokens, total_tokens,
                    input_cost, output_cost, total_cost,
                    execution_time_ms, success, error_message, created_at
                ) VALUES (
                    @CallId, @ParentCallId, @Username, @Feature, @Step, @DateTimeUtc,
                    @Provider, @Model, @IsToolCall, @ToolName, @ToolArguments,
                    @RequestMessages, @ResponseData,
                    @MessageCount, @EmbeddedDocuments, @EmbeddedDocumentCount,
                    @InputTokens, @OutputTokens, @TotalTokens,
                    @InputCost, @OutputCost, @TotalCost,
                    @ExecutionTimeMs, @Success, @ErrorMessage, @CreatedAt
                )
            ";

            await using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@CallId", log.CallId);
            command.Parameters.AddWithValue("@ParentCallId", log.ParentCallId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Username", log.Username);
            command.Parameters.AddWithValue("@Feature", log.Feature);
            command.Parameters.AddWithValue("@Step", log.Step ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@DateTimeUtc", log.DateTimeUtc.ToString("o"));
            command.Parameters.AddWithValue("@Provider", log.Provider);
            command.Parameters.AddWithValue("@Model", log.Model);
            command.Parameters.AddWithValue("@IsToolCall", log.IsToolCall ? 1 : 0);
            command.Parameters.AddWithValue("@ToolName", log.ToolName ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@ToolArguments", log.ToolArguments ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@RequestMessages", _options.LogRequestMessages ? log.RequestMessages : string.Empty);
            command.Parameters.AddWithValue("@ResponseData", _options.LogResponseData ? log.ResponseData : string.Empty);
            command.Parameters.AddWithValue("@MessageCount", log.MessageCount);
            command.Parameters.AddWithValue("@EmbeddedDocuments", log.EmbeddedDocuments ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@EmbeddedDocumentCount", log.EmbeddedDocumentCount);
            command.Parameters.AddWithValue("@InputTokens", log.InputTokens);
            command.Parameters.AddWithValue("@OutputTokens", log.OutputTokens);
            command.Parameters.AddWithValue("@TotalTokens", log.TotalTokens);
            command.Parameters.AddWithValue("@InputCost", (double)log.InputCost);
            command.Parameters.AddWithValue("@OutputCost", (double)log.OutputCost);
            command.Parameters.AddWithValue("@TotalCost", (double)log.TotalCost);
            command.Parameters.AddWithValue("@ExecutionTimeMs", log.ExecutionTimeMs);
            command.Parameters.AddWithValue("@Success", log.Success ? 1 : 0);
            command.Parameters.AddWithValue("@ErrorMessage", log.ErrorMessage ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@CreatedAt", log.CreatedAt.ToString("o"));

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<LLMCallLog>> GetLogsAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            string? username = null,
            string? feature = null,
            string? provider = null,
            int limit = 100,
            CancellationToken cancellationToken = default)
        {
            await InitializeAsync(cancellationToken);

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = "SELECT * FROM llm_call_logs WHERE 1=1";
            if (startDate.HasValue) sql += " AND datetime_utc >= @StartDate";
            if (endDate.HasValue) sql += " AND datetime_utc <= @EndDate";
            if (!string.IsNullOrEmpty(username)) sql += " AND username = @Username";
            if (!string.IsNullOrEmpty(feature)) sql += " AND feature = @Feature";
            if (!string.IsNullOrEmpty(provider)) sql += " AND provider = @Provider";
            sql += " ORDER BY datetime_utc DESC LIMIT @Limit";

            await using var command = new SqliteCommand(sql, connection);
            if (startDate.HasValue) command.Parameters.AddWithValue("@StartDate", startDate.Value.ToString("o"));
            if (endDate.HasValue) command.Parameters.AddWithValue("@EndDate", endDate.Value.ToString("o"));
            if (!string.IsNullOrEmpty(username)) command.Parameters.AddWithValue("@Username", username);
            if (!string.IsNullOrEmpty(feature)) command.Parameters.AddWithValue("@Feature", feature);
            if (!string.IsNullOrEmpty(provider)) command.Parameters.AddWithValue("@Provider", provider);
            command.Parameters.AddWithValue("@Limit", limit);

            var logs = new List<LLMCallLog>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                logs.Add(MapFromReader(reader));
            }

            return logs;
        }

        public async Task<decimal> GetTotalCostAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            string? username = null,
            string? feature = null,
            CancellationToken cancellationToken = default)
        {
            await InitializeAsync(cancellationToken);

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = "SELECT COALESCE(SUM(total_cost), 0) FROM llm_call_logs WHERE 1=1";
            if (startDate.HasValue) sql += " AND datetime_utc >= @StartDate";
            if (endDate.HasValue) sql += " AND datetime_utc <= @EndDate";
            if (!string.IsNullOrEmpty(username)) sql += " AND username = @Username";
            if (!string.IsNullOrEmpty(feature)) sql += " AND feature = @Feature";

            await using var command = new SqliteCommand(sql, connection);
            if (startDate.HasValue) command.Parameters.AddWithValue("@StartDate", startDate.Value.ToString("o"));
            if (endDate.HasValue) command.Parameters.AddWithValue("@EndDate", endDate.Value.ToString("o"));
            if (!string.IsNullOrEmpty(username)) command.Parameters.AddWithValue("@Username", username);
            if (!string.IsNullOrEmpty(feature)) command.Parameters.AddWithValue("@Feature", feature);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToDecimal(result);
        }

        public async Task<(long inputTokens, long outputTokens, long totalTokens)> GetTokenUsageAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            string? username = null,
            string? feature = null,
            CancellationToken cancellationToken = default)
        {
            await InitializeAsync(cancellationToken);

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
                SELECT
                    COALESCE(SUM(input_tokens), 0) as input,
                    COALESCE(SUM(output_tokens), 0) as output,
                    COALESCE(SUM(total_tokens), 0) as total
                FROM llm_call_logs WHERE 1=1";
            if (startDate.HasValue) sql += " AND datetime_utc >= @StartDate";
            if (endDate.HasValue) sql += " AND datetime_utc <= @EndDate";
            if (!string.IsNullOrEmpty(username)) sql += " AND username = @Username";
            if (!string.IsNullOrEmpty(feature)) sql += " AND feature = @Feature";

            await using var command = new SqliteCommand(sql, connection);
            if (startDate.HasValue) command.Parameters.AddWithValue("@StartDate", startDate.Value.ToString("o"));
            if (endDate.HasValue) command.Parameters.AddWithValue("@EndDate", endDate.Value.ToString("o"));
            if (!string.IsNullOrEmpty(username)) command.Parameters.AddWithValue("@Username", username);
            if (!string.IsNullOrEmpty(feature)) command.Parameters.AddWithValue("@Feature", feature);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return (
                    reader.GetInt64(0),
                    reader.GetInt64(1),
                    reader.GetInt64(2)
                );
            }

            return (0, 0, 0);
        }

        public async Task CleanupOldLogsAsync(int retentionDays, CancellationToken cancellationToken = default)
        {
            if (retentionDays <= 0) return;

            await InitializeAsync(cancellationToken);

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
            var sql = "DELETE FROM llm_call_logs WHERE datetime_utc < @CutoffDate";

            await using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@CutoffDate", cutoffDate.ToString("o"));

            var deleted = await command.ExecuteNonQueryAsync(cancellationToken);
            if (deleted > 0)
            {
                Console.WriteLine($"[LLMLogging] Cleaned up {deleted} old log entries older than {retentionDays} days");
            }
        }

        private static LLMCallLog MapFromReader(SqliteDataReader reader)
        {
            return new LLMCallLog
            {
                CallId = reader.GetString(reader.GetOrdinal("call_id")),
                ParentCallId = reader.IsDBNull(reader.GetOrdinal("parent_call_id")) ? null : reader.GetString(reader.GetOrdinal("parent_call_id")),
                Username = reader.GetString(reader.GetOrdinal("username")),
                Feature = reader.GetString(reader.GetOrdinal("feature")),
                Step = reader.IsDBNull(reader.GetOrdinal("step")) ? null : reader.GetString(reader.GetOrdinal("step")),
                DateTimeUtc = DateTime.Parse(reader.GetString(reader.GetOrdinal("datetime_utc"))),
                Provider = reader.GetString(reader.GetOrdinal("provider")),
                Model = reader.GetString(reader.GetOrdinal("model")),
                IsToolCall = reader.GetInt32(reader.GetOrdinal("is_tool_call")) == 1,
                ToolName = reader.IsDBNull(reader.GetOrdinal("tool_name")) ? null : reader.GetString(reader.GetOrdinal("tool_name")),
                ToolArguments = reader.IsDBNull(reader.GetOrdinal("tool_arguments")) ? null : reader.GetString(reader.GetOrdinal("tool_arguments")),
                RequestMessages = reader.GetString(reader.GetOrdinal("request_messages")),
                ResponseData = reader.GetString(reader.GetOrdinal("response_data")),
                MessageCount = reader.GetInt32(reader.GetOrdinal("message_count")),
                EmbeddedDocuments = reader.IsDBNull(reader.GetOrdinal("embedded_documents")) ? null : reader.GetString(reader.GetOrdinal("embedded_documents")),
                EmbeddedDocumentCount = reader.GetInt32(reader.GetOrdinal("embedded_document_count")),
                InputTokens = reader.GetInt32(reader.GetOrdinal("input_tokens")),
                OutputTokens = reader.GetInt32(reader.GetOrdinal("output_tokens")),
                TotalTokens = reader.GetInt32(reader.GetOrdinal("total_tokens")),
                InputCost = (decimal)reader.GetDouble(reader.GetOrdinal("input_cost")),
                OutputCost = (decimal)reader.GetDouble(reader.GetOrdinal("output_cost")),
                TotalCost = (decimal)reader.GetDouble(reader.GetOrdinal("total_cost")),
                ExecutionTimeMs = reader.GetInt64(reader.GetOrdinal("execution_time_ms")),
                Success = reader.GetInt32(reader.GetOrdinal("success")) == 1,
                ErrorMessage = reader.IsDBNull(reader.GetOrdinal("error_message")) ? null : reader.GetString(reader.GetOrdinal("error_message")),
                CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("created_at")))
            };
        }
    }
}
