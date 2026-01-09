using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.AI.PromptManagement.Dashboard;

/// <summary>
/// Service for monitoring and alerting on prompt performance
/// </summary>
public interface IAlertingService
{
    /// <summary>
    /// Check for regressions and send alerts if found
    /// </summary>
    /// <param name="promptId">Prompt ID to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Alert result</returns>
    Task<AlertResult> CheckRegressionsAsync(
        string promptId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check for drift in metrics
    /// </summary>
    /// <param name="promptId">Prompt ID to check</param>
    /// <param name="metricName">Metric to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Alert result</returns>
    Task<AlertResult> CheckDriftAsync(
        string promptId,
        string metricName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send alert through configured channels
    /// </summary>
    /// <param name="alert">Alert to send</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SendAlertAsync(
        Alert alert,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get alert history
    /// </summary>
    /// <param name="promptId">Optional prompt ID filter</param>
    /// <param name="startDate">Start date</param>
    /// <param name="endDate">End date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Alert history</returns>
    Task<List<Alert>> GetAlertHistoryAsync(
        string? promptId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Configure alert rules
    /// </summary>
    /// <param name="rule">Alert rule configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveAlertRuleAsync(
        AlertRule rule,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all alert rules
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Alert rules</returns>
    Task<List<AlertRule>> GetAlertRulesAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of an alert check
/// </summary>
public class AlertResult
{
    public bool ShouldAlert { get; set; }
    public List<Alert> Alerts { get; set; } = new();
    public string? Reason { get; set; }
}

/// <summary>
/// Alert message
/// </summary>
public class Alert
{
    public string AlertId { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Severity { get; set; } = "medium";  // "low", "medium", "high", "critical"
    public string Type { get; set; } = string.Empty;  // "regression", "drift", "failure_pattern"
    public string PromptId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object> Details { get; set; } = new();
    public List<string> RecommendedActions { get; set; } = new();
    public bool Acknowledged { get; set; } = false;
    public string? AcknowledgedBy { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
}

/// <summary>
/// Alert rule configuration
/// </summary>
public class AlertRule
{
    public string RuleId { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? PromptId { get; set; }  // null = applies to all prompts
    public string RuleType { get; set; } = string.Empty;  // "regression", "drift", "threshold"
    public bool Enabled { get; set; } = true;

    // Threshold configuration
    public Dictionary<string, double> Thresholds { get; set; } = new();  // metric → threshold

    // Notification channels
    public List<string> Channels { get; set; } = new();  // "email", "slack", "webhook"

    // Channel-specific configuration
    public Dictionary<string, object> ChannelConfig { get; set; } = new();

    // Cooldown to prevent alert spam
    public TimeSpan CooldownPeriod { get; set; } = TimeSpan.FromHours(1);
    public DateTime? LastAlertSent { get; set; }
}
