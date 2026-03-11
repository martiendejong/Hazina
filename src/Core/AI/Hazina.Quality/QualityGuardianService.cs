using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Hazina.Quality;

/// <summary>
/// Proactive quality monitoring service
/// </summary>
public class QualityGuardianService : IQualityGuardian
{
    private readonly ILogger<QualityGuardianService> _logger;
    private readonly string _violationsPath;
    private readonly List<QualityCheck> _checks = [];

    public QualityGuardianService(ILogger<QualityGuardianService> logger, string? basePath = null)
    {
        _logger = logger;
        var dir = basePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Hazina",
            "Quality"
        );

        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        _violationsPath = Path.Combine(dir, "violations.jsonl");

        // Register default checks
        RegisterDefaultChecks();
    }

    public async Task<List<Violation>> DetectViolations(WorkflowContext context)
    {
        var violations = new List<Violation>();

        foreach (var check in _checks)
        {
            try
            {
                var violation = await check.CheckFunc(context);
                if (violation != null)
                {
                    violation.Type = check.Name;
                    violation.Severity = check.Severity;
                    violations.Add(violation);

                    _logger.LogWarning(
                        "Quality violation detected in {Workflow}: {Type} ({Severity})",
                        context.Id, violation.Type, violation.Severity
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Quality check {Check} failed for {Workflow}", check.Name, context.Id);
            }
        }

        // Save violations
        foreach (var violation in violations)
        {
            await SaveViolation(context.Id, violation);
        }

        return violations;
    }

    public async Task PostViolation(string targetId, Violation violation)
    {
        await Task.CompletedTask;

        // This would integrate with ClickUp/GitHub/etc APIs
        _logger.LogInformation(
            "Would post violation to {Target}: {Description}",
            targetId, violation.Description
        );
    }

    public async Task<QualityReport> GenerateReport(TimeSpan period)
    {
        var cutoff = DateTime.UtcNow - period;
        var violations = await LoadViolationsSince(cutoff);

        var report = new QualityReport
        {
            Period = period,
            TotalWorkflows = violations.Select(v => v.context).Distinct().Count(),
            ViolationsBySeverity = violations
                .GroupBy(v => v.violation.Severity)
                .ToDictionary(g => g.Key, g => g.Count()),
            ViolationsByType = violations
                .GroupBy(v => v.violation.Type)
                .ToDictionary(g => g.Key, g => g.Count())
        };

        // Calculate trend (compare to previous period)
        var previousViolations = await LoadViolationsBetween(cutoff - period, cutoff);
        var currentCount = violations.Count;
        var previousCount = previousViolations.Count;

        if (previousCount > 0)
        {
            var change = (currentCount - previousCount) / (double)previousCount;
            report.Trend = change < -0.1 ? TrendDirection.Improving
                : change > 0.1 ? TrendDirection.Degrading
                : TrendDirection.Stable;
        }

        return report;
    }

    public async Task RegisterCheck(QualityCheck check)
    {
        await Task.CompletedTask;
        _checks.Add(check);
        _logger.LogInformation("Registered quality check: {Name}", check.Name);
    }

    private void RegisterDefaultChecks()
    {
        // Workflow timing check
        RegisterCheck(new QualityCheck
        {
            Id = "workflow-stuck",
            Name = "WorkflowStuck",
            Severity = ViolationSeverity.Warning,
            CheckFunc = async ctx =>
            {
                await Task.CompletedTask;
                var duration = DateTime.UtcNow - ctx.StartedAt;
                if (ctx.State == "busy" && duration > TimeSpan.FromHours(4))
                {
                    return new Violation
                    {
                        Type = "WorkflowStuck",
                        Description = $"Workflow has been in 'busy' state for {duration.TotalHours:F1} hours",
                        SuggestedFix = "Check if workflow is blocked or needs intervention"
                    };
                }
                return null;
            }
        }).Wait();

        // Missing metadata check
        RegisterCheck(new QualityCheck
        {
            Id = "missing-metadata",
            Name = "MissingMetadata",
            Severity = ViolationSeverity.Info,
            CheckFunc = async ctx =>
            {
                await Task.CompletedTask;
                var requiredKeys = new[] { "description", "type", "priority" };
                var missing = requiredKeys.Where(k => !ctx.Metadata.ContainsKey(k)).ToList();

                if (missing.Count > 0)
                {
                    return new Violation
                    {
                        Type = "MissingMetadata",
                        Description = $"Missing metadata: {string.Join(", ", missing)}",
                        SuggestedFix = "Add required metadata to improve tracking"
                    };
                }
                return null;
            }
        }).Wait();
    }

    private async Task SaveViolation(string workflowId, Violation violation)
    {
        var record = new
        {
            workflow_id = workflowId,
            violation,
            timestamp = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(record);
        await File.AppendAllLinesAsync(_violationsPath, new[] { json });
    }

    private async Task<List<(string context, Violation violation)>> LoadViolationsSince(DateTime cutoff)
    {
        if (!File.Exists(_violationsPath))
            return [];

        var lines = await File.ReadAllLinesAsync(_violationsPath);
        var violations = new List<(string context, Violation violation)>();

        foreach (var line in lines)
        {
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                var timestamp = root.GetProperty("timestamp").GetDateTime();

                if (timestamp >= cutoff)
                {
                    var workflowId = root.GetProperty("workflow_id").GetString() ?? "";
                    var violation = root.GetProperty("violation").Deserialize<Violation>();
                    if (violation != null)
                        violations.Add((workflowId, violation));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse violation record");
            }
        }

        return violations;
    }

    private async Task<List<(string context, Violation violation)>> LoadViolationsBetween(DateTime start, DateTime end)
    {
        if (!File.Exists(_violationsPath))
            return [];

        var lines = await File.ReadAllLinesAsync(_violationsPath);
        var violations = new List<(string context, Violation violation)>();

        foreach (var line in lines)
        {
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                var timestamp = root.GetProperty("timestamp").GetDateTime();

                if (timestamp >= start && timestamp < end)
                {
                    var workflowId = root.GetProperty("workflow_id").GetString() ?? "";
                    var violation = root.GetProperty("violation").Deserialize<Violation>();
                    if (violation != null)
                        violations.Add((workflowId, violation));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse violation record");
            }
        }

        return violations;
    }
}
