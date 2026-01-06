using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Hazina.AI.PromptManagement.Core;
using Npgsql;

namespace Hazina.AI.PromptManagement.Dashboard;

/// <summary>
/// PostgreSQL implementation of metrics aggregator
/// </summary>
public class MetricsAggregator : IMetricsAggregator
{
    private readonly string _connectionString;
    private readonly IPromptStore _promptStore;

    public MetricsAggregator(string connectionString, IPromptStore promptStore)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _promptStore = promptStore ?? throw new ArgumentNullException(nameof(promptStore));
    }

    private async Task<IDbConnection> GetConnectionAsync()
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task<TimeSeriesData> GetTimeSeriesAsync(
        string promptId,
        string metricName,
        DateTime startDate,
        DateTime endDate,
        string granularity = "day",
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        // Determine time bucket based on granularity
        var timeBucket = granularity.ToLower() switch
        {
            "hour" => "date_trunc('hour', er.started_at)",
            "day" => "date_trunc('day', er.started_at)",
            "week" => "date_trunc('week', er.started_at)",
            _ => "date_trunc('day', er.started_at)"
        };

        var sql = $@"
            SELECT
                {timeBucket} as bucket,
                AVG((er.metrics->>@MetricName)::float) as avg_value,
                COUNT(*) as sample_size,
                STDDEV((er.metrics->>@MetricName)::float) as std_dev
            FROM eval_runs er
            WHERE er.prompt_id = @PromptId
              AND er.status = 'completed'
              AND er.started_at >= @StartDate
              AND er.started_at <= @EndDate
              AND er.metrics ? @MetricName
            GROUP BY bucket
            ORDER BY bucket";

        var results = await connection.QueryAsync<dynamic>(sql, new
        {
            PromptId = promptId,
            MetricName = metricName,
            StartDate = startDate,
            EndDate = endDate
        });

        var points = results.Select(r => new DataPoint
        {
            Timestamp = (DateTime)r.bucket,
            Value = r.avg_value ?? 0.0,
            SampleSize = (int)r.sample_size,
            Metadata = new Dictionary<string, object>
            {
                { "std_dev", r.std_dev ?? 0.0 }
            }
        }).ToList();

        // Calculate statistics
        var stats = CalculateStats(points);

        return new TimeSeriesData
        {
            PromptId = promptId,
            MetricName = metricName,
            Granularity = granularity,
            Points = points,
            Stats = stats
        };
    }

    public async Task<VersionComparisonData> CompareVersionsAsync(
        string promptId,
        List<string> versionIds,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        var versions = new List<VersionMetrics>();

        foreach (var versionId in versionIds)
        {
            var version = await _promptStore.GetVersionAsync(versionId, cancellationToken);
            if (version == null) continue;

            var metrics = await _promptStore.GetMetricsAsync(promptId, versionId, cancellationToken: cancellationToken);

            versions.Add(new VersionMetrics
            {
                VersionId = versionId,
                VersionNumber = version.VersionNumber,
                CreatedAt = version.CreatedAt,
                Metrics = metrics?.EvalMetrics ?? new Dictionary<string, double>(),
                TotalUses = metrics?.TotalUses ?? 0
            });
        }

        // Calculate deltas between first (baseline) and others
        var deltas = new Dictionary<string, VersionDelta>();

        if (versions.Count >= 2)
        {
            var baseline = versions[0];
            var latest = versions[^1];

            foreach (var metricName in baseline.Metrics.Keys)
            {
                if (latest.Metrics.ContainsKey(metricName))
                {
                    var baselineValue = baseline.Metrics[metricName];
                    var newValue = latest.Metrics[metricName];
                    var absoluteChange = newValue - baselineValue;
                    var percentChange = baselineValue > 0 ? (absoluteChange / baselineValue) * 100 : 0;

                    deltas[metricName] = new VersionDelta
                    {
                        MetricName = metricName,
                        BaselineValue = baselineValue,
                        NewValue = newValue,
                        AbsoluteChange = absoluteChange,
                        PercentChange = percentChange,
                        IsImprovement = absoluteChange > 0,
                        IsRegression = percentChange < -5.0,  // >5% drop = regression
                        Severity = CalculateSeverity(percentChange)
                    };
                }
            }
        }

        return new VersionComparisonData
        {
            PromptId = promptId,
            Versions = versions,
            Deltas = deltas
        };
    }

    public async Task<AggregateStats> GetAggregateStatsAsync(
        string promptId,
        string? versionId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        // Get current version if not specified
        if (versionId == null)
        {
            var template = await _promptStore.GetAsync(promptId, cancellationToken: cancellationToken);
            versionId = template?.CurrentVersion;
        }

        // Get base metrics
        var metrics = await _promptStore.GetMetricsAsync(promptId, versionId, startDate, endDate, cancellationToken);

        if (metrics == null)
        {
            return new AggregateStats { PromptId = promptId, VersionId = versionId };
        }

        // Get detailed eval metrics
        var sql = @"
            SELECT
                er.metrics
            FROM eval_runs er
            WHERE er.prompt_id = @PromptId
              AND er.version_id = @VersionId
              AND er.status = 'completed'";

        var parameters = new DynamicParameters();
        parameters.Add("PromptId", promptId);
        parameters.Add("VersionId", versionId);

        if (startDate.HasValue)
        {
            sql += " AND er.started_at >= @StartDate";
            parameters.Add("StartDate", startDate.Value);
        }

        if (endDate.HasValue)
        {
            sql += " AND er.started_at <= @EndDate";
            parameters.Add("EndDate", endDate.Value);
        }

        var evalMetrics = await connection.QueryAsync<string>(sql, parameters);

        // Aggregate eval metrics
        var avgMetrics = new Dictionary<string, double>();
        var minMetrics = new Dictionary<string, double>();
        var maxMetrics = new Dictionary<string, double>();

        // Parse and aggregate (simplified - in production, parse JSON properly)

        return new AggregateStats
        {
            PromptId = promptId,
            VersionId = versionId,
            StartDate = startDate ?? DateTime.MinValue,
            EndDate = endDate ?? DateTime.MaxValue,
            TotalUses = metrics.TotalUses,
            SuccessCount = metrics.SuccessCount,
            FailureCount = metrics.FailureCount,
            SuccessRate = metrics.SuccessRate,
            AverageMetrics = metrics.EvalMetrics ?? new Dictionary<string, double>(),
            MinMetrics = minMetrics,
            MaxMetrics = maxMetrics,
            AvgLatencyMs = metrics.AvgLatencyMs ?? 0,
            P95LatencyMs = 0,  // TODO: Calculate from case results
            P99LatencyMs = 0,
            TotalTokens = metrics.TotalTokensUsed,
            TotalCostUsd = metrics.TotalCostUsd,
            AvgCostPerUse = metrics.TotalUses > 0 ? metrics.TotalCostUsd / metrics.TotalUses : 0,
            Trends = new Dictionary<string, double>()
        };
    }

    public async Task<List<PromptRanking>> GetPromptRankingsAsync(
        string metricName,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        var sql = @"
            SELECT
                pt.prompt_id,
                pt.name,
                pt.category,
                AVG((er.metrics->>@MetricName)::float) as metric_value,
                COUNT(*) as total_uses,
                STDDEV((er.metrics->>@MetricName)::float) / SQRT(COUNT(*)) as std_error
            FROM prompt_templates pt
            JOIN eval_runs er ON pt.prompt_id = er.prompt_id
            WHERE er.status = 'completed'
              AND er.metrics ? @MetricName
            GROUP BY pt.prompt_id, pt.name, pt.category
            HAVING COUNT(*) >= 5  -- At least 5 evaluations for meaningful ranking
            ORDER BY metric_value DESC
            LIMIT @Limit";

        var results = await connection.QueryAsync<dynamic>(sql, new
        {
            MetricName = metricName,
            Limit = limit
        });

        var rankings = results.Select((r, index) => new PromptRanking
        {
            PromptId = r.prompt_id,
            Name = r.name,
            Category = r.category,
            MetricValue = r.metric_value ?? 0.0,
            Rank = index + 1,
            TotalUses = (int)r.total_uses,
            Confidence = CalculateConfidence(r.total_uses, r.std_error)
        }).ToList();

        return rankings;
    }

    // Helper methods

    private static TimeSeriesStats CalculateStats(List<DataPoint> points)
    {
        if (!points.Any())
        {
            return new TimeSeriesStats();
        }

        var values = points.Select(p => p.Value).ToList();

        var mean = values.Average();
        var sortedValues = values.OrderBy(v => v).ToList();
        var median = sortedValues[sortedValues.Count / 2];
        var variance = values.Select(v => Math.Pow(v - mean, 2)).Average();
        var stdDev = Math.Sqrt(variance);
        var min = values.Min();
        var max = values.Max();

        // Calculate trend (simple linear regression)
        var n = points.Count;
        var sumX = 0.0;
        var sumY = 0.0;
        var sumXY = 0.0;
        var sumX2 = 0.0;

        for (int i = 0; i < n; i++)
        {
            var x = i;  // Time index
            var y = points[i].Value;
            sumX += x;
            sumY += y;
            sumXY += x * y;
            sumX2 += x * x;
        }

        var trend = n > 1 ? (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX) : 0;

        // Detect drift (significant trend)
        var isDrifting = Math.Abs(trend) > stdDev * 0.1;  // Trend > 10% of std dev
        var driftRate = trend / mean * 100;  // % change per time period

        return new TimeSeriesStats
        {
            Mean = mean,
            Median = median,
            StdDev = stdDev,
            Min = min,
            Max = max,
            Trend = trend,
            IsDrifting = isDrifting,
            DriftRate = isDrifting ? driftRate : null
        };
    }

    private static string CalculateSeverity(double percentChange)
    {
        var absChange = Math.Abs(percentChange);

        if (absChange >= 20) return "critical";
        if (absChange >= 10) return "high";
        if (absChange >= 5) return "medium";
        if (absChange >= 2) return "low";
        return "none";
    }

    private static double CalculateConfidence(int sampleSize, double? stdError)
    {
        if (stdError == null || stdError == 0) return 1.0;

        // Simple confidence based on sample size and standard error
        var confidence = 1.0 - (stdError.Value / Math.Sqrt(sampleSize));
        return Math.Clamp(confidence, 0.0, 1.0);
    }
}
