using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hazina.AI.PromptManagement.Core;
using Hazina.AI.PromptManagement.Evaluation;
using Hazina.AI.PromptManagement.Rewriter;

namespace Hazina.AI.PromptManagement.Safety;

/// <summary>
/// Safety coordinator implementation that validates proposals before deployment
/// </summary>
public class SafetyCoordinator : ISafetyCoordinator
{
    private readonly IPromptStore _promptStore;
    private readonly IProposalStore _proposalStore;
    private readonly IEvaluationPipeline _evaluationPipeline;
    private readonly ISafetyConfigStore _safetyConfigStore;
    private readonly ISandboxTestStore _sandboxTestStore;

    public SafetyCoordinator(
        IPromptStore promptStore,
        IProposalStore proposalStore,
        IEvaluationPipeline evaluationPipeline,
        ISafetyConfigStore safetyConfigStore,
        ISandboxTestStore sandboxTestStore)
    {
        _promptStore = promptStore ?? throw new ArgumentNullException(nameof(promptStore));
        _proposalStore = proposalStore ?? throw new ArgumentNullException(nameof(proposalStore));
        _evaluationPipeline = evaluationPipeline ?? throw new ArgumentNullException(nameof(evaluationPipeline));
        _safetyConfigStore = safetyConfigStore ?? throw new ArgumentNullException(nameof(safetyConfigStore));
        _sandboxTestStore = sandboxTestStore ?? throw new ArgumentNullException(nameof(sandboxTestStore));
    }

    public async Task<SafetyValidationResult> ValidateProposalAsync(
        PromptProposal proposal,
        CancellationToken cancellationToken = default)
    {
        var result = new SafetyValidationResult();
        var config = await GetSafetyConfigAsync(proposal.PromptId, cancellationToken);

        // Check 1: Emergency Stop
        if (config.EmergencyStopActive)
        {
            result.EmergencyStopActive = true;
            result.Violations.Add(new SafetyViolation
            {
                ViolationType = "emergency_stop",
                Message = $"Emergency stop active: {config.EmergencyStopReason}",
                Severity = 1.0,
                Details = new Dictionary<string, object>
                {
                    { "activated_at", config.EmergencyStopActivatedAt ?? DateTime.MinValue },
                    { "activated_by", config.EmergencyStopActivatedBy ?? "unknown" },
                    { "reason", config.EmergencyStopReason ?? "" }
                }
            });
        }

        // Check 2: Cooldown Period
        if (config.EnforceCooldown)
        {
            var cooldownStatus = await CheckCooldownAsync(proposal.PromptId, cancellationToken);
            result.CooldownPassed = !cooldownStatus.InCooldown;

            if (cooldownStatus.InCooldown)
            {
                result.Violations.Add(new SafetyViolation
                {
                    ViolationType = "cooldown",
                    Message = $"Cooldown period active. Last change at {cooldownStatus.LastChangeAt:yyyy-MM-dd HH:mm}. Cooldown ends at {cooldownStatus.CooldownEndsAt:yyyy-MM-dd HH:mm}",
                    Severity = 0.7,
                    Details = new Dictionary<string, object>
                    {
                        { "last_change_at", cooldownStatus.LastChangeAt ?? DateTime.MinValue },
                        { "cooldown_ends_at", cooldownStatus.CooldownEndsAt ?? DateTime.MinValue },
                        { "time_remaining", cooldownStatus.TimeRemaining?.TotalMinutes ?? 0 }
                    }
                });
            }
        }
        else
        {
            result.CooldownPassed = true;  // Not enforced
        }

        // Check 3: Semantic Similarity
        if (config.EnforceSemanticSimilarity)
        {
            result.SemanticSimilarityPassed = proposal.SemanticSimilarity >= config.MinSemanticSimilarity;

            if (!result.SemanticSimilarityPassed)
            {
                result.Violations.Add(new SafetyViolation
                {
                    ViolationType = "semantic_drift",
                    Message = $"Semantic similarity ({proposal.SemanticSimilarity:P}) below threshold ({config.MinSemanticSimilarity:P})",
                    Severity = 0.8,
                    Details = new Dictionary<string, object>
                    {
                        { "similarity", proposal.SemanticSimilarity },
                        { "threshold", config.MinSemanticSimilarity },
                        { "drift", config.MinSemanticSimilarity - proposal.SemanticSimilarity }
                    }
                });
            }
            else if (proposal.SemanticSimilarity < config.MinSemanticSimilarity + 0.05)
            {
                // Within threshold but close - add warning
                result.Warnings.Add(new SafetyWarning
                {
                    WarningType = "low_similarity",
                    Message = $"Semantic similarity ({proposal.SemanticSimilarity:P}) is close to threshold ({config.MinSemanticSimilarity:P})",
                    Recommendation = "Review changes carefully to ensure prompt intent is preserved"
                });
            }
        }
        else
        {
            result.SemanticSimilarityPassed = true;  // Not enforced
        }

        // Check 4: Sandbox Testing
        if (config.RequireSandboxTest)
        {
            var testSetId = config.DefaultTestSetId;

            if (string.IsNullOrEmpty(testSetId))
            {
                // Try to find a test set for this prompt's category
                var promptTemplate = await _promptStore.GetAsync(proposal.PromptId, cancellationToken: cancellationToken);
                if (promptTemplate != null)
                {
                    // In production, this would query for test sets by category
                    // For now, we'll add a warning if no test set is configured
                    result.Warnings.Add(new SafetyWarning
                    {
                        WarningType = "no_test_set",
                        Message = "No default test set configured for sandbox testing",
                        Recommendation = "Configure a default test set in safety config"
                    });
                    result.SandboxTestPassed = true;  // Skip if not configured
                }
            }
            else
            {
                // Run sandbox test
                try
                {
                    var sandboxResult = await RunSandboxTestAsync(proposal, testSetId, cancellationToken);
                    result.SandboxTestPassed = sandboxResult.MeetsThreshold;

                    if (!sandboxResult.MeetsThreshold)
                    {
                        result.Violations.Add(new SafetyViolation
                        {
                            ViolationType = "performance_threshold",
                            Message = $"Sandbox test failed to meet performance threshold. Overall improvement: {sandboxResult.OverallImprovement:P} (required: >= {config.MinPerformanceRatio - 1:P})",
                            Severity = 0.9,
                            Details = new Dictionary<string, object>
                            {
                                { "test_id", sandboxResult.TestId },
                                { "baseline_metrics", sandboxResult.BaselineMetrics },
                                { "proposed_metrics", sandboxResult.ProposedMetrics },
                                { "improvement", sandboxResult.ImprovementPercent }
                            }
                        });
                    }

                    result.Details["sandbox_test_id"] = sandboxResult.TestId;
                    result.Details["sandbox_improvement"] = sandboxResult.OverallImprovement;
                }
                catch (Exception ex)
                {
                    result.SandboxTestPassed = false;
                    result.Violations.Add(new SafetyViolation
                    {
                        ViolationType = "sandbox_error",
                        Message = $"Sandbox test failed with error: {ex.Message}",
                        Severity = 1.0,
                        Details = new Dictionary<string, object>
                        {
                            { "error", ex.Message },
                            { "stack_trace", ex.StackTrace ?? "" }
                        }
                    });
                }
            }
        }
        else
        {
            result.SandboxTestPassed = true;  // Not required
        }

        // Overall result
        result.Passed = !result.Violations.Any();

        return result;
    }

    public async Task<SandboxTestResult> RunSandboxTestAsync(
        PromptProposal proposal,
        string testSetId,
        CancellationToken cancellationToken = default)
    {
        var result = new SandboxTestResult
        {
            ProposalId = proposal.ProposalId,
            TestSetId = testSetId,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            // Get current prompt template
            var promptTemplate = await _promptStore.GetAsync(proposal.PromptId, cancellationToken: cancellationToken);
            if (promptTemplate == null)
            {
                throw new ArgumentException($"Prompt '{proposal.PromptId}' not found");
            }

            // Create temporary sandbox version for testing
            var sandboxVersionId = await _promptStore.CreateAsync(new PromptTemplateRequest
            {
                PromptId = $"{proposal.PromptId}-sandbox-{proposal.ProposalId}",
                Template = proposal.ProposedTemplate,
                Name = $"{promptTemplate.Name} (Sandbox Test)",
                Description = $"Sandbox test for proposal {proposal.ProposalId}",
                Category = promptTemplate.Category,
                TemplateEngine = promptTemplate.TemplateEngine,
                Variables = promptTemplate.Variables,
                Status = "sandbox",
                Reason = $"Safety validation for proposal {proposal.ProposalId}",
                CreatedBy = "system:safety-coordinator"
            }, cancellationToken);

            // Run evaluation on baseline (current version)
            var baselineRun = await _evaluationPipeline.RunAsync(
                proposal.PromptId,
                testSetId,
                proposal.CurrentVersion,
                cancellationToken
            );

            // Run evaluation on proposed version
            var proposedRun = await _evaluationPipeline.RunAsync(
                $"{proposal.PromptId}-sandbox-{proposal.ProposalId}",
                testSetId,
                sandboxVersionId,
                cancellationToken
            );

            // Compare metrics
            result.BaselineMetrics = baselineRun.Metrics;
            result.ProposedMetrics = proposedRun.Metrics;

            foreach (var (metricName, baselineValue) in result.BaselineMetrics)
            {
                if (result.ProposedMetrics.ContainsKey(metricName))
                {
                    var proposedValue = result.ProposedMetrics[metricName];
                    var improvementPct = baselineValue > 0
                        ? ((proposedValue - baselineValue) / baselineValue) * 100
                        : 0;

                    result.ImprovementPercent[metricName] = improvementPct;
                }
            }

            // Calculate overall improvement
            result.OverallImprovement = result.ImprovementPercent.Any()
                ? result.ImprovementPercent.Average(kv => kv.Value) / 100.0
                : 0;

            // Get safety config for threshold
            var config = await GetSafetyConfigAsync(proposal.PromptId, cancellationToken);

            // Check if proposed version meets threshold (must be >= 95% of baseline)
            result.MeetsThreshold = true;  // Assume true, check each metric

            foreach (var (metricName, baselineValue) in result.BaselineMetrics)
            {
                if (result.ProposedMetrics.ContainsKey(metricName))
                {
                    var proposedValue = result.ProposedMetrics[metricName];
                    var ratio = baselineValue > 0 ? proposedValue / baselineValue : 1.0;

                    if (ratio < config.MinPerformanceRatio)
                    {
                        result.MeetsThreshold = false;
                        break;
                    }
                }
            }

            result.CompletedAt = DateTime.UtcNow;

            // Save sandbox test result
            await _sandboxTestStore.SaveTestResultAsync(result, cancellationToken);

            // Clean up sandbox version
            // In production, you might want to keep sandbox versions for a period
            // await _promptStore.DeleteAsync(sandboxPromptId, cancellationToken);
        }
        catch (Exception ex)
        {
            result.Errors = ex.Message;
            result.MeetsThreshold = false;
            result.CompletedAt = DateTime.UtcNow;

            await _sandboxTestStore.SaveTestResultAsync(result, cancellationToken);
            throw;
        }

        return result;
    }

    public async Task<CooldownStatus> CheckCooldownAsync(
        string promptId,
        CancellationToken cancellationToken = default)
    {
        var config = await GetSafetyConfigAsync(promptId, cancellationToken);

        // Get last change timestamp from version history
        var versions = await _promptStore.GetVersionHistoryAsync(promptId, limit: 2, cancellationToken);

        if (!versions.Any())
        {
            return new CooldownStatus
            {
                InCooldown = false,
                CooldownPeriod = config.CooldownPeriod
            };
        }

        var lastVersion = versions.First();
        var lastChangeAt = lastVersion.CreatedAt;
        var cooldownEndsAt = lastChangeAt.Add(config.CooldownPeriod);
        var now = DateTime.UtcNow;

        var inCooldown = now < cooldownEndsAt;

        return new CooldownStatus
        {
            InCooldown = inCooldown,
            LastChangeAt = lastChangeAt,
            CooldownPeriod = config.CooldownPeriod,
            CooldownEndsAt = cooldownEndsAt,
            TimeRemaining = inCooldown ? cooldownEndsAt - now : null
        };
    }

    public async Task<SafetyConfig> GetSafetyConfigAsync(
        string promptId,
        CancellationToken cancellationToken = default)
    {
        var config = await _safetyConfigStore.GetConfigAsync(promptId, cancellationToken);

        if (config == null)
        {
            // Return default config
            return new SafetyConfig
            {
                PromptId = promptId,
                CooldownPeriod = TimeSpan.FromHours(24),
                EnforceCooldown = true,
                MinPerformanceRatio = 0.95,
                EnforcePerformanceThreshold = true,
                MinSemanticSimilarity = 0.85,
                EnforceSemanticSimilarity = true,
                RequireSandboxTest = true
            };
        }

        return config;
    }

    public async Task SaveSafetyConfigAsync(
        SafetyConfig config,
        CancellationToken cancellationToken = default)
    {
        await _safetyConfigStore.SaveConfigAsync(config, cancellationToken);
    }

    public async Task EmergencyStopAsync(
        string promptId,
        string reason,
        string initiatedBy,
        CancellationToken cancellationToken = default)
    {
        var config = await GetSafetyConfigAsync(promptId, cancellationToken);

        config.EmergencyStopActive = true;
        config.EmergencyStopReason = reason;
        config.EmergencyStopActivatedAt = DateTime.UtcNow;
        config.EmergencyStopActivatedBy = initiatedBy;

        await SaveSafetyConfigAsync(config, cancellationToken);
    }

    public async Task ReleaseEmergencyStopAsync(
        string promptId,
        string releasedBy,
        CancellationToken cancellationToken = default)
    {
        var config = await GetSafetyConfigAsync(promptId, cancellationToken);

        config.EmergencyStopActive = false;
        config.Metadata["last_emergency_stop_released_at"] = DateTime.UtcNow;
        config.Metadata["last_emergency_stop_released_by"] = releasedBy;
        config.Metadata["last_emergency_stop_reason"] = config.EmergencyStopReason ?? "";

        config.EmergencyStopReason = null;
        config.EmergencyStopActivatedAt = null;
        config.EmergencyStopActivatedBy = null;

        await SaveSafetyConfigAsync(config, cancellationToken);
    }
}
