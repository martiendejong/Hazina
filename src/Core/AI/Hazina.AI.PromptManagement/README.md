# Hazina.AI.PromptManagement

**Self-Learning AI Prompt Versioning and Management System**

Part of Option B: Controlled Self-Learning Implementation

## Overview

Hazina.AI.PromptManagement provides a complete prompt template management system with versioning, performance tracking, and the foundation for self-improving AI systems.

## Features

### ✅ Implemented (Sprint 1)

- **Prompt Versioning**: Git-like version control for all prompts
  - SHA-256 hash-based versioning
  - Parent-child version lineage tracking
  - Complete version history
  - Rollback capability

- **Template Engine Support**:
  - Handlebars (default)
  - Extensible architecture for Liquid, Scriban, etc.
  - Variable extraction and validation

- **Performance Metrics**:
  - Usage tracking (success/failure counts)
  - User ratings and confidence scores
  - Latency monitoring
  - Token usage and cost tracking
  - Evaluation metrics (MRR, NDCG, Precision@K)

- **Storage Backends**:
  - PostgreSQL (production-ready)
  - Extensible for file-based, SQLite, etc.

### 🚧 In Progress (Sprint 2-9)

- Evaluation Pipeline (Week 2)
- Reflection Engine (Week 4)
- Prompt Rewriter (Week 5)
- Safety Coordinator (Week 6)
- Approval Workflow (Week 7)
- Admin UI (Week 8-9)

## Architecture

```
┌─────────────────────────────────────┐
│     IPromptStore Interface          │
│  (Core abstraction)                 │
└─────────────────────────────────────┘
              ↓
┌─────────────────────────────────────┐
│  PostgresPromptStore                │
│  (Storage implementation)           │
└─────────────────────────────────────┘
              ↓
┌─────────────────────────────────────┐
│  ITemplateEngine Interface          │
│  (Rendering abstraction)            │
└─────────────────────────────────────┘
              ↓
┌─────────────────────────────────────┐
│  HandlebarsTemplateEngine           │
│  (Handlebars.Net)                   │
└─────────────────────────────────────┘
```

## Database Schema

The system uses PostgreSQL with the following core tables:

- `prompt_templates`: Template registry with current version
- `prompt_versions`: Version history (append-only log)
- `prompt_metrics`: Aggregated performance metrics
- `eval_test_sets`: Ground truth test sets
- `eval_runs`: Evaluation run history
- `reflection_reports`: Automated reflection analysis
- `prompt_proposals`: Rewriter proposals (upcoming)
- `approval_actions`: Human approval workflow (upcoming)
- `safety_checks`: Safety validation (upcoming)
- `sandbox_tests`: Pre-production testing (upcoming)
- `rollback_history`: Rollback audit trail

See `Migrations/001_PromptManagement.sql` for complete schema.

## Usage

### 1. Setup Database

```sql
-- Run the migration
\i Migrations/001_PromptManagement.sql
```

### 2. Initialize the Store

```csharp
using Hazina.AI.PromptManagement.Core;
using Hazina.AI.PromptManagement.Storage.PostgreSQL;
using Hazina.AI.PromptManagement.Templates;

var connectionString = "Host=localhost;Database=hazina;Username=postgres;Password=***";
var templateEngineFactory = new TemplateEngineFactory();
var promptStore = new PostgresPromptStore(connectionString, templateEngineFactory);
```

### 3. Create a Prompt Template

```csharp
var request = new PromptTemplateRequest
{
    Id = "agent-system-prompt",
    Name = "Agent System Prompt",
    Description = "Main system prompt for AI agents",
    Template = @"You are {{agentName}}, an AI agent. {{description}}

Your capabilities:
{{#each capabilities}}
- {{this}}
{{/each}}

Always be helpful and accurate.",
    TemplateEngine = "Handlebars",
    Category = "agent",
    Variables = new Dictionary<string, object>
    {
        { "agentName", "string" },
        { "description", "string" },
        { "capabilities", "array" }
    },
    Reason = "Initial version",
    CreatedBy = "human:dev@example.com"
};

string versionId = await promptStore.CreateAsync(request);
Console.WriteLine($"Created prompt with version: {versionId}");
```

### 4. Render a Prompt

```csharp
var variables = new Dictionary<string, object>
{
    { "agentName", "HelperBot" },
    { "description", "A helpful AI assistant" },
    { "capabilities", new[] { "Answer questions", "Provide advice", "Help with tasks" } }
};

string rendered = await promptStore.RenderAsync("agent-system-prompt", variables);
Console.WriteLine(rendered);
```

Output:
```
You are HelperBot, an AI assistant. A helpful AI assistant

Your capabilities:
- Answer questions
- Provide advice
- Help with tasks

Always be helpful and accurate.
```

### 5. Track Usage Metrics

```csharp
await promptStore.RecordUsageAsync(
    promptId: "agent-system-prompt",
    versionId: versionId,
    success: true,
    userRating: 0.9,
    confidence: 0.85,
    latencyMs: 150,
    tokensUsed: 250,
    costUsd: 0.001m
);
```

### 6. Get Performance Metrics

```csharp
var metrics = await promptStore.GetMetricsAsync("agent-system-prompt");

Console.WriteLine($"Total uses: {metrics.TotalUses}");
Console.WriteLine($"Success rate: {metrics.SuccessRate:P}");
Console.WriteLine($"Avg rating: {metrics.AvgUserRating}");
Console.WriteLine($"Avg confidence: {metrics.AvgConfidence}");
Console.WriteLine($"Total cost: ${metrics.TotalCostUsd}");
```

### 7. Update a Prompt (Creates New Version)

```csharp
var updateRequest = new PromptTemplateRequest
{
    Id = "agent-system-prompt",
    Template = @"You are {{agentName}}, an AI agent. {{description}}

Your capabilities:
{{#each capabilities}}
- {{this}}
{{/each}}

Guidelines:
- Always be helpful and accurate
- Cite sources when possible
- Admit when uncertain",  // Added guidelines
    Reason = "Added citation and uncertainty guidelines",
    CreatedBy = "human:dev@example.com"
};

string newVersionId = await promptStore.UpdateAsync(updateRequest);
Console.WriteLine($"Updated to version: {newVersionId}");
```

### 8. View Version History

```csharp
var versions = await promptStore.GetVersionHistoryAsync("agent-system-prompt");

foreach (var version in versions)
{
    Console.WriteLine($"v{version.VersionNumber} - {version.VersionId}");
    Console.WriteLine($"  Created: {version.CreatedAt} by {version.CreatedBy}");
    Console.WriteLine($"  Reason: {version.Reason}");
    Console.WriteLine();
}
```

### 9. Rollback to Previous Version

```csharp
var rollbackRequest = new RollbackRequest
{
    PromptId = "agent-system-prompt",
    TargetVersion = "abc123...",  // Version ID from history
    Reason = "Regression in performance",
    InitiatedBy = "human:admin@example.com"
};

await promptStore.RollbackAsync(rollbackRequest);
```

## Integration with Hazina Agents

```csharp
using Hazina.AI.Agents.Core;
using Hazina.AI.PromptManagement.Core;

public class PromptManagedAgent : Agent
{
    private readonly IPromptStore _promptStore;
    private readonly string _promptId;

    public PromptManagedAgent(
        IPromptStore promptStore,
        string promptId,
        string name,
        ILLMClient llmClient)
        : base(name, llmClient)
    {
        _promptStore = promptStore;
        _promptId = promptId;
    }

    protected override async Task<string> BuildSystemPromptAsync(Dictionary<string, object>? context)
    {
        // Render prompt from versioned template
        var variables = new Dictionary<string, object>
        {
            { "agentName", Name },
            { "description", Description },
            { "capabilities", context?["capabilities"] ?? new string[0] }
        };

        return await _promptStore.RenderAsync(_promptId, variables);
    }

    protected override async Task OnResponseReceivedAsync(
        string query,
        string response,
        bool success,
        double? confidence = null)
    {
        // Track usage for this prompt version
        var template = await _promptStore.GetAsync(_promptId);

        await _promptStore.RecordUsageAsync(
            promptId: _promptId,
            versionId: template.CurrentVersion,
            success: success,
            confidence: confidence,
            tokensUsed: /* from LLM response */,
            costUsd: /* calculated cost */
        );
    }
}
```

## Configuration

```json
{
  "PromptManagement": {
    "ConnectionString": "Host=localhost;Database=hazina;Username=postgres",
    "DefaultTemplateEngine": "Handlebars",
    "EnableMetricsTracking": true,
    "MetricsAggregationInterval": "1.00:00:00"  // Daily
  }
}
```

## Roadmap

### Phase 1: Foundation (Weeks 1-3) - ✅ Week 1 Complete

- ✅ Sprint 1: Prompt Store (Week 1)
  - Database schema
  - Core interfaces and models
  - PostgreSQL implementation
  - Handlebars template engine
  - Version management
  - Metrics tracking

- 🚧 Sprint 2: Enhanced Evaluation (Week 2)
  - Scheduled evaluation runs
  - Ground truth test sets
  - Custom quality rubrics
  - Regression detection

- 🚧 Sprint 3: Reflection Dashboard (Week 3)
  - Metrics visualization
  - Pattern analysis UI
  - Temporal drift charts

### Phase 2: Self-Learning (Weeks 4-9)

- Sprint 4: Reflection Engine (Week 4)
- Sprint 5: Prompt Rewriter (Week 5)
- Sprint 6: Safety Coordinator (Week 6)
- Sprint 7: Approval Workflow (Week 7)
- Sprint 8: Admin UI (Week 8-9)
- Sprint 9: Integration & Testing (Week 9)

## Contributing

This is part of the Hazina AI Framework. For contribution guidelines, see the main Hazina repository.

## License

See main Hazina repository for license information.

---

**Status**: Sprint 1 Complete ✅ (2026-01-06)
**Next**: Sprint 2 - Enhanced Evaluation Pipeline
