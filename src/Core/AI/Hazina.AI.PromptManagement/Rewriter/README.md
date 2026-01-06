# Hazina Prompt Management - Prompt Rewriter

## Overview

The Prompt Rewriter is an LLM-powered system that transforms improvement hypotheses (from the Reflection Engine) into concrete prompt proposals. It generates improved prompt templates while maintaining semantic similarity to prevent drift.

## Core Concept

```
Improvement Hypothesis → LLM Rewriting → Proposed Template → Semantic Validation → Proposal
```

The rewriter acts as a translator between abstract improvement ideas and concrete prompt modifications, ensuring changes are both effective and safe.

## Architecture

### Components

1. **IPromptRewriter** - Main rewriting interface
   - `ApplyHypothesisAsync()` - Apply single hypothesis
   - `ApplyMultipleHypothesesAsync()` - Combine multiple hypotheses
   - `GenerateVariantsAsync()` - Create A/B test variants
   - `CalculateSimilarityAsync()` - Measure semantic similarity
   - `ValidateSemanticSimilarityAsync()` - Ensure drift prevention

2. **IProposalStore** - Proposal persistence
   - PostgreSQL implementation with JSONB storage
   - Status management (pending/approved/rejected/deployed)
   - Proposal history and queries

3. **PromptProposal** - Output structure
   - Proposed template with changes tracked
   - Rationale and expected improvement
   - Semantic similarity score
   - Metadata for review and deployment

## How It Works

### Step 1: Hypothesis Input

Start with an improvement hypothesis from the Reflection Engine:

```csharp
var hypothesis = new ImprovementHypothesis
{
    Hypothesis = "Add explicit fact-checking step",
    Rationale = "15% of cases have accuracy issues due to lack of verification",
    ExpectedImpact = 0.12,
    Confidence = 0.85,
    Priority = "high",
    SuggestedChanges = new List<SuggestedChange>
    {
        new SuggestedChange
        {
            ChangeType = "Add",
            Section = "instructions",
            ProposedValue = "Before responding, verify facts against the retrieved documents",
            Reason = "Explicit verification reduces factual errors"
        }
    }
};
```

### Step 2: LLM-Based Rewriting

The rewriter constructs a detailed prompt for the LLM:

```
You are an expert prompt engineer. Your task is to improve the following prompt template.

## Current Prompt Template
```
You are a helpful assistant that answers questions using provided documents.
Always be concise and accurate.
```

## Improvement Hypothesis
**What to change**: Add explicit fact-checking step
**Why**: 15% of cases have accuracy issues due to lack of verification
**Expected Impact**: 12.0%
**Target Metrics**: Accuracy, Overall

## Suggested Changes
- Add in instructions: Explicit verification reduces factual errors
  Proposed: "Before responding, verify facts against the retrieved documents"

## Task
Rewrite the prompt template to incorporate these improvements...
```

### Step 3: Parse LLM Output

The LLM returns structured JSON:

```json
{
  "improved_template": "You are a helpful assistant that answers questions using provided documents.\n\nBefore responding:\n1. Verify all facts against the retrieved documents\n2. Cite specific documents for factual claims\n3. Clearly state if information is not available\n\nAlways be concise and accurate.",
  "changes": [
    {
      "type": "Add",
      "section": "instructions",
      "oldValue": "",
      "newValue": "Before responding:\n1. Verify all facts against the retrieved documents\n2. Cite specific documents for factual claims\n3. Clearly state if information is not available",
      "reason": "Added explicit fact-checking process to reduce accuracy errors"
    }
  ]
}
```

### Step 4: Semantic Similarity Validation

Calculate similarity using embeddings (or Jaccard similarity as fallback):

```csharp
var similarity = await rewriter.CalculateSimilarityAsync(
    originalTemplate,
    proposedTemplate
);

// similarity = 0.92 (high similarity, low drift risk)
```

**Thresholds**:
- ≥ 0.85: Safe (default threshold)
- 0.70 - 0.85: Review recommended
- < 0.70: High drift risk - reject or request revision

### Step 5: Proposal Generation

Create a proposal record:

```csharp
var proposal = new PromptProposal
{
    ProposalId = "prop-abc123",
    PromptId = "my-rag-prompt",
    CurrentVersion = "v1-hash",
    ProposedTemplate = "...",  // Improved template
    ProposedVersionId = "v2-hash",
    Changes = [...],  // Tracked changes
    Rationale = "15% of cases have accuracy issues...",
    ExpectedImprovement = 0.12,
    SemanticSimilarity = 0.92,
    Status = "pending",  // Awaiting approval
    HypothesisIds = ["hyp-123"]
};
```

### Step 6: Storage

Save to database for review workflow:

```sql
INSERT INTO prompt_proposals (
    proposal_id, prompt_id, current_version, proposed_template,
    changes, rationale, expected_improvement, semantic_similarity, status
) VALUES (...);
```

## Usage Examples

### Basic Hypothesis Application

```csharp
var rewriter = new PromptRewriter(
    promptStore,
    proposalStore,
    llmClient
);

// Apply single hypothesis
var proposal = await rewriter.ApplyHypothesisAsync(
    "my-prompt-id",
    hypothesis
);

Console.WriteLine($"Proposal ID: {proposal.ProposalId}");
Console.WriteLine($"Expected Improvement: {proposal.ExpectedImprovement:P}");
Console.WriteLine($"Semantic Similarity: {proposal.SemanticSimilarity:P}");
Console.WriteLine($"Status: {proposal.Status}");

// Review changes
foreach (var change in proposal.Changes)
{
    Console.WriteLine($"\n{change.Type} in {change.Section}:");
    Console.WriteLine($"  Reason: {change.Reason}");
    Console.WriteLine($"  Old: {change.OldValue.Substring(0, Math.Min(50, change.OldValue.Length))}...");
    Console.WriteLine($"  New: {change.NewValue.Substring(0, Math.Min(50, change.NewValue.Length))}...");
}
```

### Applying Multiple Hypotheses

Combine several improvements into one proposal:

```csharp
var hypotheses = new List<ImprovementHypothesis>
{
    hypothesis1,  // Add fact-checking
    hypothesis2,  // Improve examples
    hypothesis3   // Add constraints
};

var combinedProposal = await rewriter.ApplyMultipleHypothesesAsync(
    "my-prompt-id",
    hypotheses
);

Console.WriteLine($"Combined {hypotheses.Count} improvements");
Console.WriteLine($"Expected Impact: {combinedProposal.ExpectedImprovement:P}");
Console.WriteLine($"Total Changes: {combinedProposal.Changes.Count}");
```

### A/B Test Variant Generation

Generate multiple approaches for testing:

```csharp
var variants = await rewriter.GenerateVariantsAsync(
    "my-prompt-id",
    hypothesis,
    variantCount: 3
);

Console.WriteLine($"Generated {variants.Count} variants:");
for (int i = 0; i < variants.Count; i++)
{
    var v = variants[i];
    Console.WriteLine($"\nVariant {i + 1}:");
    Console.WriteLine($"  Similarity: {v.SemanticSimilarity:P}");
    Console.WriteLine($"  Temperature: {v.Metadata["temperature"]}");
    Console.WriteLine($"  Preview: {v.ProposedTemplate.Substring(0, 100)}...");
}

// Deploy all variants for A/B testing
foreach (var variant in variants)
{
    // Send to evaluation pipeline for comparison
}
```

### Semantic Similarity Validation

Ensure proposed changes don't drift too far:

```csharp
var proposal = await rewriter.ApplyHypothesisAsync(promptId, hypothesis);

// Validate with default threshold (0.85)
var isValid = await rewriter.ValidateSemanticSimilarityAsync(proposal);

if (!isValid)
{
    Console.WriteLine("⚠️ Proposal exceeds drift threshold");
    Console.WriteLine($"Similarity: {proposal.SemanticSimilarity:P} (minimum: 85.0%)");

    // Option 1: Reject proposal
    await proposalStore.UpdateProposalStatusAsync(proposal.ProposalId, "rejected");

    // Option 2: Request revision with stricter constraints
    // Option 3: Escalate for manual review
}
else
{
    Console.WriteLine("✓ Proposal within acceptable similarity bounds");
    await proposalStore.UpdateProposalStatusAsync(proposal.ProposalId, "pending");
}

// Custom threshold for stricter validation
var isStrictlyValid = await rewriter.ValidateSemanticSimilarityAsync(
    proposal,
    minSimilarity: 0.90  // 90% similarity required
);
```

### Retrieving Proposals

```csharp
// Get all pending proposals for review
var pendingProposals = await proposalStore.GetPendingProposalsAsync(limit: 10);

foreach (var p in pendingProposals)
{
    Console.WriteLine($"\n{p.ProposalId} - {p.PromptId}");
    Console.WriteLine($"  Created: {p.CreatedAt:yyyy-MM-dd HH:mm}");
    Console.WriteLine($"  Rationale: {p.Rationale}");
    Console.WriteLine($"  Expected Improvement: {p.ExpectedImprovement:P}");
    Console.WriteLine($"  Changes: {p.Changes.Count}");
}

// Get proposal history for a specific prompt
var history = await proposalStore.GetProposalsForPromptAsync(
    "my-prompt-id",
    status: null,  // All statuses
    limit: 20
);

// Filter by status
var approvedProposals = await proposalStore.GetProposalsForPromptAsync(
    "my-prompt-id",
    status: "approved",
    limit: 10
);
```

## Data Model

### PromptProposal

```csharp
public class PromptProposal
{
    // Identity
    public string ProposalId { get; set; }
    public string PromptId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; }  // "system:rewriter"

    // Versions
    public string CurrentVersion { get; set; }  // Current version hash
    public string ProposedVersionId { get; set; }  // Proposed version hash

    // Content
    public string ProposedTemplate { get; set; }
    public List<PromptChange> Changes { get; set; }

    // Justification
    public string Rationale { get; set; }
    public double ExpectedImprovement { get; set; }  // 0.0 to 1.0

    // Source
    public string? ReflectionReportId { get; set; }
    public List<string> HypothesisIds { get; set; }

    // Validation
    public double SemanticSimilarity { get; set; }  // 0.0 to 1.0

    // Workflow
    public string Status { get; set; }  // pending|approved|rejected|deployed|rolled_back

    // Metadata
    public Dictionary<string, object> Metadata { get; set; }
}
```

### PromptChange

```csharp
public class PromptChange
{
    public string ChangeId { get; set; }
    public string Type { get; set; }  // Add|Remove|Modify|Restructure
    public string Section { get; set; }  // instructions|examples|constraints|format
    public string OldValue { get; set; }
    public string NewValue { get; set; }
    public string Reason { get; set; }
    public int LineStart { get; set; }  // For diff display
    public int LineEnd { get; set; }
}
```

## Integration with Other Components

### From Reflection Engine

```csharp
// Get latest reflection report
var report = await reflectionEngine.GenerateReportAsync(
    promptId,
    DateTime.UtcNow.AddDays(-30),
    DateTime.UtcNow
);

// Apply top hypothesis
var topHypothesis = report.ImprovementHypotheses
    .OrderByDescending(h => h.ExpectedImpact * h.Confidence)
    .First();

var proposal = await rewriter.ApplyHypothesisAsync(promptId, topHypothesis);
```

### To Safety Coordinator (Sprint 6)

```csharp
// After proposal generation, validate safety
var safetyResult = await safetyCoordinator.ValidateProposalAsync(proposal);

if (!safetyResult.Passed)
{
    Console.WriteLine("⚠️ Safety checks failed:");
    foreach (var violation in safetyResult.Violations)
    {
        Console.WriteLine($"  - {violation.Message}");
    }

    await proposalStore.UpdateProposalStatusAsync(proposal.ProposalId, "rejected");
}
```

### To Approval Workflow (Sprint 7)

```csharp
// Submit for human review
await approvalWorkflow.SubmitForReviewAsync(proposal);

// Notify reviewers
await notificationService.NotifyAsync(
    "team@example.com",
    $"New prompt proposal: {proposal.ProposalId}",
    $"Expected improvement: {proposal.ExpectedImprovement:P}"
);
```

## Best Practices

### 1. Hypothesis Quality

**Good hypothesis** (specific, actionable):
```csharp
new ImprovementHypothesis
{
    Hypothesis = "Add explicit fact-checking step",
    Rationale = "15% of cases have accuracy < 0.5 due to unchecked facts",
    SuggestedChanges = new List<SuggestedChange>
    {
        new() {
            ChangeType = "Add",
            Section = "instructions",
            ProposedValue = "Before responding, verify all facts..."
        }
    }
}
```

**Poor hypothesis** (vague, generic):
```csharp
new ImprovementHypothesis
{
    Hypothesis = "Make it better",
    Rationale = "It's not good enough",
    SuggestedChanges = new List<SuggestedChange>()  // Empty!
}
```

### 2. Temperature Selection

```csharp
// Single hypothesis: Low temperature for focused rewriting
var proposal = await rewriter.ApplyHypothesisAsync(promptId, hypothesis);
// Temperature: 0.4 (default)

// Variants: Higher temperature for diversity
var variants = await rewriter.GenerateVariantsAsync(promptId, hypothesis, 3);
// Temperatures: 0.6, 0.7, 0.8
```

### 3. Combining Hypotheses

Combine when:
- Hypotheses target different sections
- Changes are complementary
- Total expected impact > 15%

Don't combine when:
- Hypotheses conflict (both modify same section differently)
- Individual impacts are unclear
- Combined similarity would drop below 0.85

```csharp
// Check for conflicts before combining
var sections = hypotheses.SelectMany(h => h.SuggestedChanges)
    .Select(c => c.Section)
    .ToList();

if (sections.Distinct().Count() == sections.Count)
{
    // No conflicts - safe to combine
    var combined = await rewriter.ApplyMultipleHypothesesAsync(promptId, hypotheses);
}
```

### 4. Semantic Drift Prevention

Set appropriate thresholds based on prompt type:

```csharp
// Strict: Critical production prompts
var isValid = await rewriter.ValidateSemanticSimilarityAsync(proposal, 0.90);

// Standard: General purpose prompts
var isValid = await rewriter.ValidateSemanticSimilarityAsync(proposal, 0.85);

// Lenient: Experimental prompts
var isValid = await rewriter.ValidateSemanticSimilarityAsync(proposal, 0.75);
```

### 5. Version Control

Proposals generate new version IDs:

```csharp
Console.WriteLine($"Current: {proposal.CurrentVersion}");
Console.WriteLine($"Proposed: {proposal.ProposedVersionId}");

// After approval and deployment, proposed version becomes current
await promptStore.UpdateAsync(new PromptTemplateRequest
{
    PromptId = proposal.PromptId,
    Template = proposal.ProposedTemplate,
    Reason = $"Applied proposal {proposal.ProposalId}",
    CreatedBy = "system:approval-workflow"
});
```

### 6. A/B Testing Strategy

```csharp
// Generate 2-3 variants
var variants = await rewriter.GenerateVariantsAsync(promptId, hypothesis, 3);

// Deploy all to evaluation pipeline
foreach (var variant in variants)
{
    // Create sandbox version
    var sandboxVersionId = await promptStore.CreateAsync(new PromptTemplateRequest
    {
        PromptId = $"{promptId}-variant-{variant.Metadata["variant_number"]}",
        Template = variant.ProposedTemplate,
        Status = "sandbox",
        Reason = $"A/B test variant {variant.Metadata["variant_number"]}"
    });

    // Run evaluation
    await evaluationPipeline.RunAsync(
        $"{promptId}-variant-{variant.Metadata["variant_number"]}",
        testSetId
    );
}

// Compare results and select winner
```

## Advanced Features

### Custom Rewriting Logic

Extend the rewriter for domain-specific improvements:

```csharp
public class MedicalPromptRewriter : PromptRewriter
{
    public override async Task<PromptProposal> ApplyHypothesisAsync(
        string promptId,
        ImprovementHypothesis hypothesis,
        CancellationToken cancellationToken = default)
    {
        // Add medical-specific validation
        if (!hypothesis.Hypothesis.Contains("HIPAA") && IsMedicalPrompt(promptId))
        {
            hypothesis.SuggestedChanges.Add(new SuggestedChange
            {
                ChangeType = "Add",
                Section = "constraints",
                ProposedValue = "Ensure HIPAA compliance in all responses",
                Reason = "Medical prompts require HIPAA compliance"
            });
        }

        return await base.ApplyHypothesisAsync(promptId, hypothesis, cancellationToken);
    }
}
```

### Batch Proposal Generation

Process multiple prompts:

```csharp
var promptIds = new[] { "prompt-1", "prompt-2", "prompt-3" };
var proposals = new List<PromptProposal>();

foreach (var promptId in promptIds)
{
    var report = await reflectionStore.GetLatestReportAsync(promptId);

    if (report != null && report.ImprovementHypotheses.Any())
    {
        var topHypothesis = report.ImprovementHypotheses
            .OrderByDescending(h => h.ExpectedImpact * h.Confidence)
            .First();

        var proposal = await rewriter.ApplyHypothesisAsync(promptId, topHypothesis);
        proposals.Add(proposal);
    }
}

Console.WriteLine($"Generated {proposals.Count} proposals");
```

### Similarity Monitoring

Track similarity trends:

```csharp
var proposals = await proposalStore.GetProposalsForPromptAsync(promptId, limit: 50);

var avgSimilarity = proposals.Average(p => p.SemanticSimilarity);
var minSimilarity = proposals.Min(p => p.SemanticSimilarity);

Console.WriteLine($"Average Similarity: {avgSimilarity:P}");
Console.WriteLine($"Minimum Similarity: {minSimilarity:P}");

if (avgSimilarity < 0.88)
{
    Console.WriteLine("⚠️ Warning: Average similarity trending low");
    Console.WriteLine("Consider reviewing proposal generation strategy");
}
```

## Troubleshooting

### Issue: Proposals have low semantic similarity

**Possible causes**:
- Hypotheses are too aggressive
- LLM making unintended changes
- Embedding model not capturing semantics well

**Solutions**:
```csharp
// 1. Use more conservative hypotheses
var conservativeHypothesis = new ImprovementHypothesis
{
    ExpectedImpact = 0.05,  // Smaller impact
    SuggestedChanges = new List<SuggestedChange>
    {
        new() {
            ChangeType = "Modify",  // Not "Restructure"
            Section = "instructions",
            ProposedValue = "Small, targeted change"
        }
    }
};

// 2. Add explicit similarity constraint to LLM prompt
// (Modify BuildRewritePrompt to emphasize similarity)

// 3. Reject low-similarity proposals
if (proposal.SemanticSimilarity < 0.85)
{
    await proposalStore.UpdateProposalStatusAsync(proposal.ProposalId, "rejected");
}
```

### Issue: LLM not following JSON format

**Solution**: Parse with fallback:

```csharp
// Already handled in ParseRewriteOutput()
// Falls back to treating entire output as template
// Consider adding retry with stronger instructions
```

### Issue: Proposals don't improve performance

**Root cause**: Hypotheses may be incorrect

**Solutions**:
```csharp
// 1. Improve reflection analysis quality
var report = await reflectionEngine.GenerateReportAsync(
    promptId,
    DateTime.UtcNow.AddDays(-60),  // Longer period for more data
    DateTime.UtcNow,
    minRunsRequired: 50  // More runs for confidence
);

// 2. A/B test before full deployment
var variants = await rewriter.GenerateVariantsAsync(promptId, hypothesis, 3);
// Deploy to sandbox and measure actual improvement

// 3. Start with highest-confidence hypotheses
var bestHypothesis = report.ImprovementHypotheses
    .Where(h => h.Confidence >= 0.8)
    .OrderByDescending(h => h.ExpectedImpact)
    .FirstOrDefault();
```

## Performance Considerations

### LLM Costs

Rewriting involves LLM calls (~$0.01-0.10 per proposal):

```csharp
// Optimize by batching
var batch = hypotheses.Take(3).ToList();
var combinedProposal = await rewriter.ApplyMultipleHypothesesAsync(promptId, batch);
// Single LLM call instead of 3

// Use cheaper models for initial generation
var cheapLLM = new LLMClient("gpt-3.5-turbo");  // vs gpt-4
var rewriter = new PromptRewriter(promptStore, proposalStore, cheapLLM);
```

### Database Queries

Proposals are stored with JSONB:

```sql
-- Already indexed in migration
CREATE INDEX idx_prompt_proposals_status ON prompt_proposals(status);
CREATE INDEX idx_prompt_proposals_created_at ON prompt_proposals(created_at DESC);

-- Additional index for common query
CREATE INDEX idx_prompt_proposals_prompt_status
ON prompt_proposals(prompt_id, status);
```

## API Reference

See interface documentation:
- `IPromptRewriter.cs` - Rewriting interface
- `IProposalStore.cs` - Storage interface
- `PromptRewriter.cs` - Main implementation
- `PostgresProposalStore.cs` - PostgreSQL storage

## Next Steps

After proposal generation:
- **Sprint 6: Safety Coordinator** - Validate proposals before deployment
- **Sprint 7: Approval Workflow** - Human review and approval
- **Sandbox Testing** - Evaluate proposals before production

---

**Last Updated**: 2026-01-06
**Related Documentation**:
- Reflection Engine README
- Safety Coordinator README (coming next)
- Approval Workflow README (Sprint 7)
