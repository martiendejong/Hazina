using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Hazina.AI.PromptManagement.Core;
using Hazina.AI.PromptManagement.Evaluation;
using Npgsql;

namespace Hazina.AI.PromptManagement.Dashboard;

/// <summary>
/// PostgreSQL implementation of alerting service
/// </summary>
public class AlertingService : IAlertingService
{
    private readonly string _connectionString;
    private readonly IEvaluationPipeline _evaluationPipeline;
    private readonly IPatternAnalyzer _patternAnalyzer;
    private readonly IPromptStore _promptStore;

    public AlertingService(
        string connectionString,
        IEvaluationPipeline evaluationPipeline,
        IPatternAnalyzer patternAnalyzer,
        IPromptStore promptStore)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _evaluationPipeline = evaluationPipeline ?? throw new ArgumentNullException(nameof(evaluationPipeline));
        _patternAnalyzer = patternAnalyzer ?? throw new ArgumentNullException(nameof(patternAnalyzer));
        _promptStore = promptStore ?? throw new ArgumentNullException(nameof(promptStore));
    }

    private async Task<IDbConnection> GetConnectionAsync()
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task<AlertResult> CheckRegressionsAsync(
        string promptId,
        CancellationToken cancellationToken = default)
    {
        var alerts = new List<Alert>();

        // Get latest version and previous version
        var versions = await _promptStore.GetVersionHistoryAsync(promptId, limit: 2, cancellationToken);

        if (versions.Count < 2)
        {
            return new AlertResult
            {
                ShouldAlert = false,
                Reason = "Not enough versions to compare"
            };
        }

        var currentVersion = versions[0];
        var previousVersion = versions[1];

        // Get regression report if it exists
        using var connection = await GetConnectionAsync();

        var sql = @"
            SELECT *
            FROM regression_reports
            WHERE prompt_id = @PromptId
              AND new_version_id = @NewVersionId
            ORDER BY created_at DESC
            LIMIT 1";

        var report = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new
        {
            PromptId = promptId,
            NewVersionId = currentVersion.VersionId
        });

        if (report == null)
        {
            return new AlertResult
            {
                ShouldAlert = false,
                Reason = "No regression report available"
            };
        }

        var hasRegression = (bool)report.has_regression;

        if (hasRegression)
        {
            var issues = JsonSerializer.Deserialize<List<RegressionIssue>>(report.issues.ToString()) ?? new List<RegressionIssue>();
            var criticalIssues = issues.Where(i => i.Severity == "critical" || i.Severity == "high").ToList();

            if (criticalIssues.Any())
            {
                alerts.Add(new Alert
                {
                    Severity = "critical",
                    Type = "regression",
                    PromptId = promptId,
                    Title = $"Critical Regression Detected in {promptId}",
                    Message = $"Version {currentVersion.VersionNumber} shows {criticalIssues.Count} critical/high severity regression(s) compared to version {previousVersion.VersionNumber}",
                    Details = new Dictionary<string, object>
                    {
                        { "current_version", currentVersion.VersionId },
                        { "previous_version", previousVersion.VersionId },
                        { "issues", criticalIssues }
                    },
                    RecommendedActions = new List<string>
                    {
                        "Review the regression report for details",
                        "Consider rolling back to previous version",
                        "Investigate root cause of performance degradation"
                    }
                });
            }
        }

        return new AlertResult
        {
            ShouldAlert = alerts.Any(),
            Alerts = alerts,
            Reason = alerts.Any() ? "Regressions detected" : "No regressions"
        };
    }

    public async Task<AlertResult> CheckDriftAsync(
        string promptId,
        string metricName,
        CancellationToken cancellationToken = default)
    {
        var alerts = new List<Alert>();

        // Compare last 7 days vs previous 7 days
        var now = DateTime.UtcNow;
        var currentEnd = now;
        var currentStart = now.AddDays(-7);
        var baselineEnd = currentStart;
        var baselineStart = baselineEnd.AddDays(-7);

        var drift = await _patternAnalyzer.DetectDriftAsync(
            promptId,
            metricName,
            baselineStart,
            baselineEnd,
            currentStart,
            currentEnd,
            cancellationToken
        );

        if (drift.HasDrift && drift.DriftDirection == "degrading")
        {
            var severity = drift.PercentChange < -20 ? "critical" :
                          drift.PercentChange < -10 ? "high" :
                          drift.PercentChange < -5 ? "medium" : "low";

            alerts.Add(new Alert
            {
                Severity = severity,
                Type = "drift",
                PromptId = promptId,
                Title = $"Performance Drift Detected: {metricName}",
                Message = $"{metricName} has degraded by {Math.Abs(drift.PercentChange):F2}% over the past week",
                Details = new Dictionary<string, object>
                {
                    { "metric_name", metricName },
                    { "baseline_mean", drift.BaselineMean },
                    { "current_mean", drift.CurrentMean },
                    { "percent_change", drift.PercentChange },
                    { "p_value", drift.PValue },
                    { "effect_size", drift.EffectSize }
                },
                RecommendedActions = drift.Recommendations
            });
        }

        return new AlertResult
        {
            ShouldAlert = alerts.Any(),
            Alerts = alerts,
            Reason = alerts.Any() ? "Drift detected" : "No significant drift"
        };
    }

    public async Task SendAlertAsync(Alert alert, CancellationToken cancellationToken = default)
    {
        // Save alert to database
        using var connection = await GetConnectionAsync();

        const string sql = @"
            INSERT INTO alerts (
                alert_id, timestamp, severity, type, prompt_id,
                title, message, details, recommended_actions
            )
            VALUES (
                @AlertId, @Timestamp, @Severity, @Type, @PromptId,
                @Title, @Message, @Details::jsonb, @RecommendedActions::jsonb
            )";

        await connection.ExecuteAsync(sql, new
        {
            alert.AlertId,
            alert.Timestamp,
            alert.Severity,
            alert.Type,
            alert.PromptId,
            alert.Title,
            alert.Message,
            Details = JsonSerializer.Serialize(alert.Details),
            RecommendedActions = JsonSerializer.Serialize(alert.RecommendedActions)
        });

        // TODO: Send to notification channels (Email, Slack, etc.)
        // This would integrate with external services

        Console.WriteLine($"[ALERT] {alert.Severity.ToUpper()}: {alert.Title}");
        Console.WriteLine($"  {alert.Message}");
        if (alert.RecommendedActions.Any())
        {
            Console.WriteLine("  Recommended actions:");
            foreach (var action in alert.RecommendedActions)
            {
                Console.WriteLine($"    - {action}");
            }
        }
    }

    public async Task<List<Alert>> GetAlertHistoryAsync(
        string? promptId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        var sql = "SELECT * FROM alerts WHERE 1=1";
        var parameters = new DynamicParameters();

        if (promptId != null)
        {
            sql += " AND prompt_id = @PromptId";
            parameters.Add("PromptId", promptId);
        }

        if (startDate.HasValue)
        {
            sql += " AND timestamp >= @StartDate";
            parameters.Add("StartDate", startDate.Value);
        }

        if (endDate.HasValue)
        {
            sql += " AND timestamp <= @EndDate";
            parameters.Add("EndDate", endDate.Value);
        }

        sql += " ORDER BY timestamp DESC LIMIT 100";

        var results = await connection.QueryAsync<dynamic>(sql, parameters);

        return results.Select(r => new Alert
        {
            AlertId = r.alert_id,
            Timestamp = r.timestamp,
            Severity = r.severity,
            Type = r.type,
            PromptId = r.prompt_id,
            Title = r.title,
            Message = r.message,
            Details = r.details != null
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(r.details.ToString()) ?? new()
                : new(),
            RecommendedActions = r.recommended_actions != null
                ? JsonSerializer.Deserialize<List<string>>(r.recommended_actions.ToString()) ?? new()
                : new(),
            Acknowledged = r.acknowledged,
            AcknowledgedBy = r.acknowledged_by,
            AcknowledgedAt = r.acknowledged_at
        }).ToList();
    }

    public async Task SaveAlertRuleAsync(AlertRule rule, CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = @"
            INSERT INTO alert_rules (
                rule_id, name, prompt_id, rule_type, enabled,
                thresholds, channels, channel_config, cooldown_period
            )
            VALUES (
                @RuleId, @Name, @PromptId, @RuleType, @Enabled,
                @Thresholds::jsonb, @Channels::jsonb, @ChannelConfig::jsonb, @CooldownPeriod
            )
            ON CONFLICT (rule_id)
            DO UPDATE SET
                name = EXCLUDED.name,
                enabled = EXCLUDED.enabled,
                thresholds = EXCLUDED.thresholds,
                channels = EXCLUDED.channels,
                channel_config = EXCLUDED.channel_config,
                cooldown_period = EXCLUDED.cooldown_period";

        await connection.ExecuteAsync(sql, new
        {
            rule.RuleId,
            rule.Name,
            rule.PromptId,
            rule.RuleType,
            rule.Enabled,
            Thresholds = JsonSerializer.Serialize(rule.Thresholds),
            Channels = JsonSerializer.Serialize(rule.Channels),
            ChannelConfig = JsonSerializer.Serialize(rule.ChannelConfig),
            CooldownPeriod = rule.CooldownPeriod
        });
    }

    public async Task<List<AlertRule>> GetAlertRulesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = "SELECT * FROM alert_rules WHERE enabled = true";
        var results = await connection.QueryAsync<dynamic>(sql);

        return results.Select(r => new AlertRule
        {
            RuleId = r.rule_id,
            Name = r.name,
            PromptId = r.prompt_id,
            RuleType = r.rule_type,
            Enabled = r.enabled,
            Thresholds = JsonSerializer.Deserialize<Dictionary<string, double>>(r.thresholds.ToString()) ?? new(),
            Channels = JsonSerializer.Deserialize<List<string>>(r.channels.ToString()) ?? new(),
            ChannelConfig = JsonSerializer.Deserialize<Dictionary<string, object>>(r.channel_config.ToString()) ?? new(),
            CooldownPeriod = (TimeSpan)r.cooldown_period,
            LastAlertSent = r.last_alert_sent
        }).ToList();
    }
}
