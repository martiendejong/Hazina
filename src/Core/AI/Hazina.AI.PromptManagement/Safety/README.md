# Hazina Prompt Management - Safety Coordinator

## Overview

The Safety Coordinator is a critical gating system that validates prompt proposals before human approval. It prevents harmful deployments through multiple safety checks including cooldown enforcement, performance thresholds, semantic drift prevention, and mandatory sandbox testing.

## Core Principle

**"Trust, but verify - and never rush"**

The Safety Coordinator ensures that:
1. Changes don't happen too frequently (cooldown)
2. New versions perform at least as well as current versions (performance threshold)
3. Prompt intent doesn't drift (semantic similarity)
4. Changes are tested before production (sandbox)
5. Emergency stops can halt all changes instantly

## Architecture

### Safety Checks

```
Proposal → Emergency Stop Check → Cooldown Check → Semantic Similarity Check →
Sandbox Test → Safety Validation Result → Approval Workflow
```

Each check can:
- **Pass**: Allow proposal to continue
- **Fail** (Violation): Block proposal from approval
- **Warn**: Non-blocking concern for human review

### Components

1. **ISafetyCoordinator** - Main coordination interface
   - `ValidateProposalAsync()` - Run all safety checks
   - `RunSandboxTestAsync()` - Test proposal in sandbox
   - `CheckCooldownAsync()` - Verify cooldown status
   - `EmergencyStopAsync()` - Block all changes to a prompt
   - `ReleaseEmergencyStopAsync()` - Resume changes

2. **ISafetyConfigStore** - Configuration persistence
   - Per-prompt safety settings
   - Threshold configuration
   - Emergency stop state

3. **ISandboxTestStore** - Test result storage
   - Sandbox evaluation results
   - Performance comparisons
   - Test history

## Safety Checks in Detail

### 1. Emergency Stop Check

**Purpose**: Immediately block all changes to a problematic prompt

**When to use**:
- Production incident caused by recent prompt change
- Critical bug discovered in prompt
- Temporary freeze during investigation

**Configuration**:
```csharp
await safetyCoordinator.EmergencyStopAsync(
    "my-prompt-id",
    reason: "Production accuracy dropped to 45% after last deployment",
    initiatedBy: "user@example.com"
);
```

**Violation**:
- Severity: 1.0 (maximum)
- **Blocks**: ALL proposals
- **Resolution**: Manual release required

**Release**:
```csharp
await safetyCoordinator.ReleaseEmergencyStopAsync(
    "my-prompt-id",
    releasedBy: "user@example.com"
);
```

### 2. Cooldown Check

**Purpose**: Prevent too-frequent changes that make debugging difficult

**Default**: 24 hours between changes

**Rationale**:
- Allows metrics to stabilize
- Enables proper evaluation of each change
- Prevents feedback loops

**Configuration**:
```csharp
var config = await safetyCoordinator.GetSafetyConfigAsync("my-prompt-id");
config.CooldownPeriod = TimeSpan.FromHours(48);  // 48-hour cooldown
config.EnforceCooldown = true;
await safetyCoordinator.SaveSafetyConfigAsync(config);
```

**Violation**:
- Severity: 0.7
- **Blocks**: Proposals within cooldown period
- **Resolution**: Wait until cooldown expires

**Check Status**:
```csharp
var status = await safetyCoordinator.CheckCooldownAsync("my-prompt-id");

if (status.InCooldown)
{
    Console.WriteLine($"Cooldown active until {status.CooldownEndsAt}");
    Console.WriteLine($"Time remaining: {status.TimeRemaining?.TotalHours:F1} hours");
}
```

### 3. Semantic Similarity Check

**Purpose**: Prevent prompt drift (gradual change of intent)

**Default**: 0.85 minimum similarity (85%)

**How it works**:
- Calculates cosine similarity using embeddings
- Falls back to Jaccard similarity if embeddings unavailable
- Compares proposed template to current template

**Configuration**:
```csharp
var config = await safetyCoordinator.GetSafetyConfigAsync("my-prompt-id");
config.MinSemanticSimilarity = 0.90;  // Stricter: 90% similarity
config.EnforceSemanticSimilarity = true;
await safetyCoordinator.SaveSafetyConfigAsync(config);
```

**Violation**:
- Severity: 0.8
- **Blocks**: Proposals below similarity threshold
- **Resolution**: Revise proposal to maintain similarity or adjust threshold

**Thresholds**:
- **≥ 0.90**: Very strict (critical prompts)
- **≥ 0.85**: Standard (default)
- **≥ 0.75**: Lenient (experimental prompts)
- **< 0.75**: Risky - high drift

### 4. Performance Threshold Check

**Purpose**: Ensure new version performs at least as well as current version

**Default**: 0.95 ratio (new must be ≥ 95% of baseline)

**How it works**:
- Runs sandbox evaluation on both current and proposed versions
- Compares all metrics (Accuracy, Relevance, Clarity, etc.)
- Requires ALL metrics to meet threshold

**Configuration**:
```csharp
var config = await safetyCoordinator.GetSafetyConfigAsync("my-prompt-id");
config.MinPerformanceRatio = 0.98;  // Stricter: 98% of baseline
config.EnforcePerformanceThreshold = true;
config.DefaultTestSetId = "production-test-set";
await safetyCoordinator.SaveSafetyConfigAsync(config);
```

**Violation**:
- Severity: 0.9
- **Blocks**: Proposals that underperform baseline
- **Resolution**: Revise proposal to improve performance

**Example**:
```
Baseline Accuracy: 0.85
Proposed Accuracy: 0.82
Ratio: 0.82 / 0.85 = 0.965 (96.5%)
Threshold: 0.95 (95%)
Result: ✓ PASS (96.5% >= 95%)
```

### 5. Sandbox Test (Combined with Performance Check)

**Purpose**: Test proposals in safe environment before production

**Default**: Required

**How it works**:
1. Creates temporary sandbox version with proposed template
2. Runs evaluation on configured test set
3. Compares results to baseline (current version)
4. Stores results for review
5. Cleans up sandbox version

**Configuration**:
```csharp
var config = await safetyCoordinator.GetSafetyConfigAsync("my-prompt-id");
config.RequireSandboxTest = true;
config.DefaultTestSetId = "comprehensive-test-set";
await safetyCoordinator.SaveSafetyConfigAsync(config);
```

**Test Result**:
```csharp
var sandboxResult = await safetyCoordinator.RunSandboxTestAsync(
    proposal,
    "test-set-id"
);

Console.WriteLine($"Overall Improvement: {sandboxResult.OverallImprovement:P}");
Console.WriteLine($"Meets Threshold: {sandboxResult.MeetsThreshold}");

foreach (var (metric, baselineValue) in sandboxResult.BaselineMetrics)
{
    var proposedValue = sandboxResult.ProposedMetrics[metric];
    var improvement = sandboxResult.ImprovementPercent[metric];

    Console.WriteLine($"{metric}: {baselineValue:F3} → {proposedValue:F3} ({improvement:+0.0;-0.0}%)");
}
```

## Usage Examples

### Basic Validation

```csharp
var safetyCoordinator = new SafetyCoordinator(
    promptStore,
    proposalStore,
    evaluationPipeline,
    safetyConfigStore,
    sandboxTestStore
);

// Validate a proposal
var result = await safetyCoordinator.ValidateProposalAsync(proposal);

if (result.Passed)
{
    Console.WriteLine("✓ All safety checks passed");
    // Proceed to approval workflow
}
else
{
    Console.WriteLine("✗ Safety violations detected:");
    foreach (var violation in result.Violations)
    {
        Console.WriteLine($"  [{violation.ViolationType}] {violation.Message}");
        Console.WriteLine($"  Severity: {violation.Severity:F2}");
    }
}

// Review warnings (non-blocking)
if (result.Warnings.Any())
{
    Console.WriteLine("\n⚠️ Warnings:");
    foreach (var warning in result.Warnings)
    {
        Console.WriteLine($"  [{warning.WarningType}] {warning.Message}");
        Console.WriteLine($"  Recommendation: {warning.Recommendation}");
    }
}
```

### Detailed Check Results

```csharp
var result = await safetyCoordinator.ValidateProposalAsync(proposal);

Console.WriteLine("Safety Check Results:");
Console.WriteLine($"  Emergency Stop: {(result.EmergencyStopActive ? "✗ ACTIVE" : "✓ Inactive")}");
Console.WriteLine($"  Cooldown: {(result.CooldownPassed ? "✓ Passed" : "✗ Failed")}");
Console.WriteLine($"  Semantic Similarity: {(result.SemanticSimilarityPassed ? "✓ Passed" : "✗ Failed")}");
Console.WriteLine($"  Performance Threshold: {(result.PerformanceThresholdPassed ? "✓ Passed" : "✗ Failed")}");
Console.WriteLine($"  Sandbox Test: {(result.SandboxTestPassed ? "✓ Passed" : "✗ Failed")}");

// Access sandbox test details if available
if (result.Details.ContainsKey("sandbox_test_id"))
{
    var testId = result.Details["sandbox_test_id"].ToString();
    var testResult = await sandboxTestStore.GetTestResultAsync(testId);

    Console.WriteLine($"\nSandbox Test Details:");
    Console.WriteLine($"  Test ID: {testResult.TestId}");
    Console.WriteLine($"  Overall Improvement: {testResult.OverallImprovement:P}");
    Console.WriteLine($"  Duration: {(testResult.CompletedAt - testResult.StartedAt)?.TotalSeconds:F1}s");
}
```

### Custom Safety Configuration

```csharp
// Configure strict safety for critical production prompt
var strictConfig = new SafetyConfig
{
    PromptId = "critical-prod-prompt",

    // Longer cooldown
    CooldownPeriod = TimeSpan.FromHours(72),  // 3 days
    EnforceCooldown = true,

    // Stricter performance threshold
    MinPerformanceRatio = 0.98,  // Must be 98% of baseline
    EnforcePerformanceThreshold = true,

    // Stricter similarity
    MinSemanticSimilarity = 0.90,  // 90% similarity
    EnforceSemanticSimilarity = true,

    // Mandatory sandbox test
    RequireSandboxTest = true,
    DefaultTestSetId = "comprehensive-prod-test-set"
};

await safetyCoordinator.SaveSafetyConfigAsync(strictConfig);

// Configure lenient safety for experimental prompt
var lenientConfig = new SafetyConfig
{
    PromptId = "experimental-prompt",

    CooldownPeriod = TimeSpan.FromHours(6),  // 6 hours
    EnforceCooldown = true,

    MinPerformanceRatio = 0.90,  // Can be 90% of baseline
    EnforcePerformanceThreshold = true,

    MinSemanticSimilarity = 0.75,  // 75% similarity OK
    EnforceSemanticSimilarity = true,

    RequireSandboxTest = true,
    DefaultTestSetId = "quick-test-set"
};

await safetyCoordinator.SaveSafetyConfigAsync(lenientConfig);
```

### Emergency Stop Usage

```csharp
// Activate emergency stop
await safetyCoordinator.EmergencyStopAsync(
    "problematic-prompt",
    reason: "Accuracy dropped from 0.85 to 0.45 after v12 deployment. " +
           "Investigating root cause. All changes blocked until resolved.",
    initiatedBy: "incident-commander@example.com"
);

// Check if emergency stop is active
var config = await safetyCoordinator.GetSafetyConfigAsync("problematic-prompt");

if (config.EmergencyStopActive)
{
    Console.WriteLine($"⛔ EMERGENCY STOP ACTIVE");
    Console.WriteLine($"Reason: {config.EmergencyStopReason}");
    Console.WriteLine($"Activated: {config.EmergencyStopActivatedAt:yyyy-MM-dd HH:mm} UTC");
    Console.WriteLine($"By: {config.EmergencyStopActivatedBy}");
}

// ... investigation and fix ...

// Release emergency stop
await safetyCoordinator.ReleaseEmergencyStopAsync(
    "problematic-prompt",
    releasedBy: "incident-commander@example.com"
);

Console.WriteLine("✓ Emergency stop released. Normal operations resumed.");
```

## Integration with Other Components

### From Prompt Rewriter

```csharp
// Generate proposal
var proposal = await promptRewriter.ApplyHypothesisAsync(promptId, hypothesis);

// Validate before sending to approval
var safetyResult = await safetyCoordinator.ValidateProposalAsync(proposal);

if (!safetyResult.Passed)
{
    // Reject proposal automatically
    await proposalStore.UpdateProposalStatusAsync(proposal.ProposalId, "rejected");

    Console.WriteLine($"Proposal {proposal.ProposalId} automatically rejected:");
    foreach (var violation in safetyResult.Violations)
    {
        Console.WriteLine($"  - {violation.Message}");
    }
}
else
{
    // Send to approval workflow
    await approvalWorkflow.SubmitForReviewAsync(proposal);
}
```

### To Approval Workflow (Sprint 7)

```csharp
// Only proposals that pass safety go to human review
if (safetyResult.Passed)
{
    await approvalWorkflow.SubmitForReviewAsync(proposal, safetyResult);
}
```

## Best Practices

### 1. Cooldown Configuration

**Production prompts**: 24-72 hours
```csharp
config.CooldownPeriod = TimeSpan.FromHours(48);
```

**Development prompts**: 6-12 hours
```csharp
config.CooldownPeriod = TimeSpan.FromHours(6);
```

**Experimental prompts**: 1-6 hours
```csharp
config.CooldownPeriod = TimeSpan.FromHours(1);
```

### 2. Performance Threshold Tuning

**Critical business impact**: 98-100% of baseline
```csharp
config.MinPerformanceRatio = 0.98;
```

**Standard prompts**: 95-98% of baseline (default: 95%)
```csharp
config.MinPerformanceRatio = 0.95;
```

**Experimental/iterative**: 90-95% of baseline
```csharp
config.MinPerformanceRatio = 0.92;
```

### 3. Test Set Selection

Use comprehensive test sets that cover:
- Edge cases
- Common queries
- Historical failures
- Performance-critical scenarios

```csharp
// Production-grade test set
config.DefaultTestSetId = "comprehensive-prod-test";  // 100+ cases

// Quick validation test set
config.DefaultTestSetId = "quick-smoke-test";  // 20-30 critical cases
```

### 4. Emergency Stop Protocol

**When to activate**:
1. Production incident linked to recent prompt change
2. Accuracy/quality drops > 20%
3. User complaints spike
4. Security vulnerability discovered

**Activation checklist**:
```csharp
// 1. Immediate activation
await safetyCoordinator.EmergencyStopAsync(promptId, reason, initiatedBy);

// 2. Rollback to last known good version
await promptStore.RollbackAsync(new RollbackRequest
{
    PromptId = promptId,
    ToVersion = lastGoodVersionId,
    Reason = "Emergency rollback due to: " + reason,
    InitiatedBy = initiatedBy
});

// 3. Investigate root cause
// 4. Fix and test thoroughly
// 5. Release emergency stop only after verification

await safetyCoordinator.ReleaseEmergencyStopAsync(promptId, releasedBy);
```

### 5. Warning Handling

Warnings are non-blocking but should be reviewed:

```csharp
if (result.Warnings.Any())
{
    // Log warnings
    foreach (var warning in result.Warnings)
    {
        logger.LogWarning($"Safety warning for {proposal.ProposalId}: {warning.Message}");
    }

    // Add to proposal metadata for human reviewer
    proposal.Metadata["safety_warnings"] = result.Warnings;
    await proposalStore.SaveProposalAsync(proposal);
}
```

## Advanced Features

### Conditional Safety Rules

```csharp
// Different rules based on prompt category
var config = await safetyCoordinator.GetSafetyConfigAsync(promptId);
var promptTemplate = await promptStore.GetAsync(promptId);

switch (promptTemplate.Category)
{
    case "rag":
        config.MinPerformanceRatio = 0.95;
        config.MinSemanticSimilarity = 0.85;
        break;

    case "agent":
        config.MinPerformanceRatio = 0.98;  // Stricter for agents
        config.MinSemanticSimilarity = 0.90;
        break;

    case "policy":
        config.MinPerformanceRatio = 1.00;  // No regression allowed
        config.MinSemanticSimilarity = 0.95;  // Very high similarity
        config.CooldownPeriod = TimeSpan.FromDays(7);  // 1 week cooldown
        break;
}

await safetyCoordinator.SaveSafetyConfigAsync(config);
```

### Batch Validation

```csharp
var proposals = await proposalStore.GetPendingProposalsAsync(limit: 50);
var validationResults = new Dictionary<string, SafetyValidationResult>();

foreach (var proposal in proposals)
{
    var result = await safetyCoordinator.ValidateProposalAsync(proposal);
    validationResults[proposal.ProposalId] = result;

    if (!result.Passed)
    {
        await proposalStore.UpdateProposalStatusAsync(proposal.ProposalId, "rejected");
    }
}

// Report
var passedCount = validationResults.Count(r => r.Value.Passed);
var failedCount = validationResults.Count - passedCount;

Console.WriteLine($"Validated {proposals.Count} proposals:");
Console.WriteLine($"  Passed: {passedCount}");
Console.WriteLine($"  Failed: {failedCount}");
```

## Troubleshooting

### Issue: All proposals failing cooldown check

**Diagnosis**:
```csharp
var cooldownStatus = await safetyCoordinator.CheckCooldownAsync(promptId);
Console.WriteLine($"Last change: {cooldownStatus.LastChangeAt}");
Console.WriteLine($"Cooldown ends: {cooldownStatus.CooldownEndsAt}");
Console.WriteLine($"Time remaining: {cooldownStatus.TimeRemaining?.TotalHours:F1} hours");
```

**Solutions**:
1. Wait for cooldown to expire (recommended)
2. Temporarily reduce cooldown period (use with caution)
3. Override for emergency fixes only

### Issue: Sandbox tests timing out

**Causes**:
- Test set too large
- Evaluation pipeline slow
- Database connection issues

**Solutions**:
```csharp
// Use smaller test set for faster validation
config.DefaultTestSetId = "quick-validation-set";  // 10-20 cases instead of 100

// Or increase timeout in evaluation pipeline configuration
```

### Issue: Semantic similarity always failing

**Diagnosis**:
```csharp
Console.WriteLine($"Proposal similarity: {proposal.SemanticSimilarity:P}");
Console.WriteLine($"Required minimum: {config.MinSemanticSimilarity:P}");
Console.WriteLine($"Drift: {(config.MinSemanticSimilarity - proposal.SemanticSimilarity):P}");
```

**Solutions**:
1. Review proposed changes - may be too aggressive
2. Adjust similarity threshold if appropriate
3. Generate new proposal with smaller changes

## Performance Considerations

### Sandbox Test Optimization

Sandbox tests are the slowest safety check (~10-60 seconds):

```csharp
// Optimize by:
// 1. Using smaller representative test sets
config.DefaultTestSetId = "quick-representative-set";  // 20 cases

// 2. Caching sandbox test results
var cachedTests = await sandboxTestStore.GetTestResultsForProposalAsync(proposal.ProposalId);
if (cachedTests.Any(t => t.CompletedAt.HasValue && t.CompletedAt.Value > DateTime.UtcNow.AddHours(-1)))
{
    // Reuse recent test result
    return cachedTests.OrderByDescending(t => t.CompletedAt).First();
}

// 3. Running sandbox tests asynchronously
var sandboxTask = safetyCoordinator.RunSandboxTestAsync(proposal, testSetId);
// Continue with other checks while sandbox runs
```

### Database Query Optimization

```sql
-- Indexes already created in migration
CREATE INDEX idx_safety_configs_emergency_stop ON safety_configs(emergency_stop_active);
CREATE INDEX idx_sandbox_tests_proposal_id ON sandbox_tests(proposal_id);
CREATE INDEX idx_sandbox_tests_completed_at ON sandbox_tests(completed_at DESC);
```

## API Reference

See interface documentation:
- `ISafetyCoordinator.cs` - Main safety interface
- `ISafetyConfigStore.cs` - Configuration storage
- `ISandboxTestStore.cs` - Test result storage
- `SafetyCoordinator.cs` - Full implementation

## Next Steps

After safety validation:
- **Sprint 7: Approval Workflow** - Human review of safe proposals
- **Sprint 8-9: Admin UI** - Visual safety status and configuration
- **Sprint 10: Integration** - End-to-end deployment pipeline

---

**Last Updated**: 2026-01-06
**Related Documentation**:
- Prompt Rewriter README
- Approval Workflow README (Sprint 7)
- Safety best practices guide
