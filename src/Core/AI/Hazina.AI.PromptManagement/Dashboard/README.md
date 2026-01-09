# Hazina Prompt Management - Reflection Dashboard

## Overview

The Reflection Dashboard provides real-time monitoring and analysis of prompt performance, enabling data-driven optimization of AI prompts through automated pattern detection, regression alerting, and drift analysis.

## Architecture

### Components

1. **Metrics Aggregator** (`IMetricsAggregator`)
   - Time series data collection and aggregation
   - Version comparison analytics
   - Prompt ranking and leaderboards
   - Statistical analysis (mean, median, std dev, trends)

2. **Pattern Analyzer** (`IPatternAnalyzer`)
   - Failure pattern detection (low accuracy, relevance, latency issues)
   - Success pattern identification (high-performing query types)
   - Statistical drift detection (t-tests, effect sizes)
   - Pattern evolution tracking over time

3. **Alerting Service** (`IAlertingService`)
   - Regression detection and alerting
   - Performance drift monitoring
   - Multi-channel notifications (console, email, Slack, webhook)
   - Alert rule configuration and cooldown management

## Key Metrics

### Quality Metrics
- **Accuracy**: Factual correctness (0.0 - 1.0)
- **Relevance**: Query-response alignment (0.0 - 1.0)
- **Clarity**: Response readability (0.0 - 1.0)
- **Overall**: Weighted average of all quality metrics

### Performance Metrics
- **Latency**: Response generation time (ms)
- **Token Usage**: Total tokens consumed
- **Cost**: USD cost per query
- **Success Rate**: Percentage of successful evaluations

### Statistical Metrics
- **Drift Magnitude**: Performance change in standard deviations
- **P-Value**: Statistical significance (< 0.05 = significant)
- **Effect Size**: Cohen's d for practical significance
- **Trend**: Linear regression slope for time series

## Usage

### 1. Time Series Analysis

```csharp
var aggregator = new MetricsAggregator(connectionString, promptStore);

// Get daily accuracy metrics for the past 30 days
var timeSeries = await aggregator.GetTimeSeriesAsync(
    promptId: "my-prompt",
    metricName: "Accuracy",
    startDate: DateTime.UtcNow.AddDays(-30),
    endDate: DateTime.UtcNow,
    granularity: "day"
);

// Check for trends
if (timeSeries.Stats.IsDrifting)
{
    Console.WriteLine($"Drift detected: {timeSeries.Stats.DriftRate:F2}% per day");
}
```

### 2. Version Comparison

```csharp
// Compare last 3 versions
var comparison = await aggregator.CompareVersionsAsync(
    promptId: "my-prompt",
    versionIds: new List<string> { "v1-hash", "v2-hash", "v3-hash" }
);

// Check for regressions
foreach (var (metricName, delta) in comparison.Deltas)
{
    if (delta.IsRegression)
    {
        Console.WriteLine($"⚠️ Regression in {metricName}: {delta.PercentChange:F2}%");
    }
}
```

### 3. Pattern Detection

```csharp
var analyzer = new PatternAnalyzer(connectionString);

// Detect failure patterns in the past 7 days
var failures = await analyzer.DetectFailurePatternsAsync(
    promptId: "my-prompt",
    startDate: DateTime.UtcNow.AddDays(-7),
    endDate: DateTime.UtcNow,
    minFrequency: 0.1  // 10% threshold
);

foreach (var pattern in failures)
{
    Console.WriteLine($"Pattern: {pattern.Description}");
    Console.WriteLine($"Frequency: {pattern.Frequency:P}");
    Console.WriteLine($"Severity: {pattern.Severity:F2}");
    Console.WriteLine($"Root Cause: {pattern.RootCause}");
    Console.WriteLine("Examples:");
    foreach (var example in pattern.Examples.Take(3))
    {
        Console.WriteLine($"  - {example}");
    }
}
```

### 4. Drift Detection

```csharp
// Compare last 7 days vs previous 7 days
var drift = await analyzer.DetectDriftAsync(
    promptId: "my-prompt",
    metricName: "Accuracy",
    baselineStartDate: DateTime.UtcNow.AddDays(-14),
    baselineEndDate: DateTime.UtcNow.AddDays(-7),
    currentStartDate: DateTime.UtcNow.AddDays(-7),
    currentEndDate: DateTime.UtcNow
);

if (drift.HasDrift)
{
    Console.WriteLine($"Drift Direction: {drift.DriftDirection}");
    Console.WriteLine($"Percent Change: {drift.PercentChange:F2}%");
    Console.WriteLine($"P-Value: {drift.PValue:F4}");
    Console.WriteLine($"Effect Size (Cohen's d): {drift.EffectSize:F2}");

    if (drift.IsSignificant)
    {
        Console.WriteLine("✓ Statistically significant (p < 0.05)");
    }
}
```

### 5. Alerting

```csharp
var alerting = new AlertingService(
    connectionString,
    evaluationPipeline,
    patternAnalyzer,
    promptStore
);

// Check for regressions
var regressionAlert = await alerting.CheckRegressionsAsync("my-prompt");
if (regressionAlert.ShouldAlert)
{
    foreach (var alert in regressionAlert.Alerts)
    {
        await alerting.SendAlertAsync(alert);
    }
}

// Check for drift
var driftAlert = await alerting.CheckDriftAsync("my-prompt", "Accuracy");
if (driftAlert.ShouldAlert)
{
    foreach (var alert in driftAlert.Alerts)
    {
        await alerting.SendAlertAsync(alert);
    }
}

// Configure alert rules
var rule = new AlertRule
{
    Name = "Critical Accuracy Regression",
    PromptId = "my-prompt",
    RuleType = "regression",
    Enabled = true,
    Thresholds = new Dictionary<string, double>
    {
        { "Accuracy", -5.0 },  // Alert on >5% drop
        { "Overall", -10.0 }   // Alert on >10% drop
    },
    Channels = new List<string> { "email", "slack" },
    ChannelConfig = new Dictionary<string, object>
    {
        { "email", "team@example.com" },
        { "slack_webhook", "https://hooks.slack.com/..." }
    },
    CooldownPeriod = TimeSpan.FromHours(4)
};

await alerting.SaveAlertRuleAsync(rule);
```

## Dashboard Visualization

### Recommended Panels

#### 1. Quality Metrics Over Time
**Type**: Line chart
**Query**: Time series for Accuracy, Relevance, Clarity
**Granularity**: Hour/Day
**Use Case**: Monitor prompt quality trends

```sql
SELECT
    date_trunc('day', er.started_at) as bucket,
    AVG((er.metrics->>'Accuracy')::float) as accuracy,
    AVG((er.metrics->>'Relevance')::float) as relevance,
    AVG((er.metrics->>'Clarity')::float) as clarity,
    AVG((er.metrics->>'Overall')::float) as overall
FROM eval_runs er
WHERE er.prompt_id = 'my-prompt'
  AND er.status = 'completed'
  AND er.started_at >= NOW() - INTERVAL '30 days'
GROUP BY bucket
ORDER BY bucket;
```

#### 2. Version Performance Comparison
**Type**: Bar chart
**Query**: Aggregate metrics by version
**Use Case**: Compare prompt versions side-by-side

```sql
SELECT
    pv.version_number,
    AVG((er.metrics->>'Overall')::float) as avg_quality,
    AVG((er.metrics->>'Latency')::float) as avg_latency_ms,
    COUNT(*) as total_runs
FROM eval_runs er
JOIN prompt_versions pv ON er.version_id = pv.version_id
WHERE er.prompt_id = 'my-prompt'
  AND er.status = 'completed'
GROUP BY pv.version_number
ORDER BY pv.version_number DESC
LIMIT 10;
```

#### 3. Failure Patterns
**Type**: Table
**Query**: Top failure patterns by frequency
**Use Case**: Identify recurring issues

```sql
-- This query would be complex; use the PatternAnalyzer API instead
-- Example usage shown in "Pattern Detection" section above
```

#### 4. Active Alerts
**Type**: Alert list
**Query**: Recent unacknowledged alerts
**Use Case**: Monitor active issues

```sql
SELECT
    alert_id,
    timestamp,
    severity,
    type,
    title,
    message,
    recommended_actions
FROM alerts
WHERE acknowledged = false
  AND timestamp >= NOW() - INTERVAL '7 days'
ORDER BY
    CASE severity
        WHEN 'critical' THEN 1
        WHEN 'high' THEN 2
        WHEN 'medium' THEN 3
        WHEN 'low' THEN 4
    END,
    timestamp DESC;
```

#### 5. Prompt Leaderboard
**Type**: Table
**Query**: Top prompts by metric
**Use Case**: Identify best-performing prompts

```sql
SELECT
    pt.name,
    pt.category,
    AVG((er.metrics->>'Overall')::float) as avg_quality,
    COUNT(*) as total_runs,
    STDDEV((er.metrics->>'Overall')::float) / SQRT(COUNT(*)) as std_error
FROM prompt_templates pt
JOIN eval_runs er ON pt.prompt_id = er.prompt_id
WHERE er.status = 'completed'
  AND er.started_at >= NOW() - INTERVAL '30 days'
GROUP BY pt.prompt_id, pt.name, pt.category
HAVING COUNT(*) >= 10
ORDER BY avg_quality DESC
LIMIT 20;
```

#### 6. Drift Detection Chart
**Type**: Dual-axis line chart
**Query**: Baseline vs current performance
**Use Case**: Visualize performance drift

```sql
-- Baseline period (14-7 days ago)
SELECT
    'baseline' as period,
    AVG((er.metrics->>'Accuracy')::float) as mean,
    STDDEV((er.metrics->>'Accuracy')::float) as std_dev
FROM eval_runs er
WHERE er.prompt_id = 'my-prompt'
  AND er.status = 'completed'
  AND er.started_at >= NOW() - INTERVAL '14 days'
  AND er.started_at < NOW() - INTERVAL '7 days'

UNION ALL

-- Current period (last 7 days)
SELECT
    'current' as period,
    AVG((er.metrics->>'Accuracy')::float) as mean,
    STDDEV((er.metrics->>'Accuracy')::float) as std_dev
FROM eval_runs er
WHERE er.prompt_id = 'my-prompt'
  AND er.status = 'completed'
  AND er.started_at >= NOW() - INTERVAL '7 days';
```

## Grafana Integration

### Setup

1. **Install Grafana**
   ```bash
   docker run -d -p 3000:3000 --name=grafana grafana/grafana
   ```

2. **Add PostgreSQL Data Source**
   - Navigate to Configuration > Data Sources
   - Add PostgreSQL with your connection details
   - Database: `hazina`
   - SSL Mode: `require` (if applicable)

3. **Import Dashboard**
   - Use the provided `grafana-dashboard.json` (see below)
   - Or create custom panels using the SQL queries above

4. **Configure Variables**
   - `$prompt_id`: Dropdown of all prompt IDs
   - `$timeRange`: Time range selector (default: Last 30 days)
   - `$granularity`: Hour/Day/Week

### Sample Dashboard JSON

```json
{
  "dashboard": {
    "title": "Hazina Prompt Performance",
    "panels": [
      {
        "id": 1,
        "title": "Quality Metrics Trend",
        "type": "graph",
        "datasource": "PostgreSQL",
        "targets": [
          {
            "rawSql": "SELECT date_trunc('$granularity', er.started_at) as time, AVG((er.metrics->>'Overall')::float) as value FROM eval_runs er WHERE er.prompt_id = '$prompt_id' AND er.status = 'completed' AND $__timeFilter(er.started_at) GROUP BY time ORDER BY time"
          }
        ]
      }
    ],
    "templating": {
      "list": [
        {
          "name": "prompt_id",
          "type": "query",
          "datasource": "PostgreSQL",
          "query": "SELECT prompt_id FROM prompt_templates ORDER BY name"
        }
      ]
    }
  }
}
```

## Alert Severity Levels

| Severity | Criteria | Action Required |
|----------|----------|-----------------|
| **Critical** | >20% regression in key metrics | Immediate action - consider rollback |
| **High** | 10-20% regression or significant drift | Investigate within 1 hour |
| **Medium** | 5-10% regression or moderate drift | Review within 4 hours |
| **Low** | 2-5% change or minor patterns | Monitor and document |

## Statistical Significance

### Interpreting Results

- **P-Value < 0.05**: Statistically significant change
- **Effect Size (Cohen's d)**:
  - Small: 0.2 - 0.5
  - Medium: 0.5 - 0.8
  - Large: > 0.8

### Example Interpretation

```
Drift Analysis for "Accuracy":
  Baseline Mean: 0.85 (±0.05)
  Current Mean: 0.78 (±0.06)
  Percent Change: -8.24%
  P-Value: 0.0023 ✓ Significant
  Effect Size: 1.23 (Large)
  Direction: Degrading

Interpretation:
The 8.24% drop in accuracy is both statistically and practically significant.
This represents a large effect size, indicating a meaningful degradation in
prompt performance that requires immediate attention.
```

## Best Practices

### 1. Monitoring Cadence
- **Real-time**: Critical production prompts
- **Hourly**: High-traffic prompts
- **Daily**: Standard prompts
- **Weekly**: Experimental or low-traffic prompts

### 2. Alert Configuration
- Set reasonable thresholds (5-10% for quality metrics)
- Use cooldown periods to prevent alert fatigue
- Configure multiple channels for critical alerts
- Acknowledge alerts after investigation

### 3. Pattern Analysis
- Run weekly pattern detection
- Document identified patterns
- Create test cases for recurring failures
- Share success patterns across prompts

### 4. Drift Detection
- Compare equivalent time periods (week-over-week)
- Require minimum sample sizes (n ≥ 30)
- Consider seasonality and business cycles
- Validate statistical significance (p < 0.05)

### 5. Dashboard Usage
- Review daily for production prompts
- Set up automated reports for stakeholders
- Create custom views per team/domain
- Export metrics for compliance/auditing

## Troubleshooting

### No Data in Dashboard
- Verify evaluation runs exist: `SELECT COUNT(*) FROM eval_runs WHERE status = 'completed'`
- Check time range filter
- Ensure metrics are being collected

### Alerts Not Firing
- Verify alert rules are enabled: `SELECT * FROM alert_rules WHERE enabled = true`
- Check cooldown period hasn't blocked alerts
- Ensure regression reports exist

### High False Positive Rate
- Increase alert thresholds
- Extend cooldown periods
- Add minimum sample size requirements
- Use statistical significance filters (p-value)

## API Reference

See interface documentation:
- `IMetricsAggregator.cs` - Time series and aggregation
- `IPatternAnalyzer.cs` - Pattern detection and drift
- `IAlertingService.cs` - Alerting and notifications

## Examples

Complete working examples are available in:
- `Examples/DashboardUsage.cs` - Full dashboard implementation
- `Examples/AlertingSetup.cs` - Alert configuration examples
- `Examples/GrafanaQueries.sql` - All SQL queries for Grafana

## Support

For questions or issues:
- File an issue in the Hazina repository
- Consult the main README for architecture overview
- Review the evaluation pipeline documentation
