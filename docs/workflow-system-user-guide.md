# Hazina Visual Workflow System - User Guide

**Version:** 2.0
**Status:** Phase 1 Complete
**Last Updated:** 2026-01-18

---

## Table of Contents

1. [Introduction](#introduction)
2. [What's New in v2.0](#whats-new-in-v20)
3. [Getting Started](#getting-started)
4. [Workflow Format Reference](#workflow-format-reference)
5. [Per-Step Configuration](#per-step-configuration)
6. [Guardrails System](#guardrails-system)
7. [Migration Guide (v1 to v2)](#migration-guide-v1-to-v2)
8. [Examples](#examples)
9. [Troubleshooting](#troubleshooting)
10. [Best Practices](#best-practices)

---

## Introduction

The Hazina Visual Workflow System allows you to define multi-step AI workflows using declarative `.hazina` configuration files. Version 2.0 introduces intelligent per-step configuration, enabling:

- **Cost Optimization** - Use different models for different steps (GPT-4 for complex analysis, GPT-3.5-turbo for simple tasks)
- **Quality Control** - Apply guardrails to prevent PII leakage, enforce token limits, and validate JSON outputs
- **Fine-Grained Control** - Configure temperature, max tokens, RAG settings independently per step
- **Real-Time Monitoring** - Event-driven architecture provides visibility into workflow execution

**Target Savings:** 20%+ reduction in AI costs while maintaining quality

---

## What's New in v2.0

### Phase 1 Deliverables (Complete)

✅ **Enhanced .hazina Format** - Per-step LLM and RAG configuration
✅ **Intelligent Workflow Engine** - Event-driven execution with comprehensive metrics
✅ **Guardrails System** - 3 built-in guardrails (PII detection, token limits, JSON validation)
✅ **Backward Compatibility** - v1 format workflows still work
✅ **Cost Tracking** - Automatic token usage and cost estimation per step

### Key Features

| Feature | v1.0 | v2.0 |
|---------|------|------|
| Per-step model selection | ❌ | ✅ |
| Per-step temperature/params | ❌ | ✅ |
| Per-step RAG configuration | ❌ | ✅ |
| Guardrails system | ❌ | ✅ |
| Variable substitution | ❌ | ✅ |
| Cost tracking | ❌ | ✅ |
| Event-driven execution | ❌ | ✅ |

---

## Getting Started

### Prerequisites

- Hazina framework installed
- .NET 9.0 or later
- Access to LLM provider (OpenAI, Azure OpenAI, etc.)

### Creating Your First v2.0 Workflow

**1. Create a `.hazina` file** in your workflows directory:

```
C:\stores\brand2boost\.hazina\workflows\my-first-workflow.hazina
```

**2. Define the workflow structure:**

```
# Workflow Definition
Name: MyFirstWorkflow
Description: Simple example workflow
Version: 2.0
Steps: 2

[Step1]
Name: AnalyzeInput
Type: AgentTask
AgentName: Analyzer
Input: {userInput}
Temperature: 0.3
MaxTokens: 500
Model: gpt-3.5-turbo
OutputKey: analysis

[Step2]
Name: GenerateResponse
Type: AgentTask
AgentName: Generator
Input: Based on {analysis}, generate response
Temperature: 0.7
MaxTokens: 1000
Model: gpt-4
Guardrails: no-pii
OutputKey: response
```

**3. Execute the workflow in code:**

```csharp
using Hazina.AI.Workflows.Configuration;
using Hazina.AI.Workflows.Engine;

// Load workflow
var workflow = HazinaWorkflowConfigParser.LoadFromFile(
    @"C:\stores\brand2boost\.hazina\workflows\my-first-workflow.hazina");

// Create engine (injected via DI in production)
var engine = new EnhancedWorkflowEngine(
    llmOrchestrator,
    ragEngines,
    guardrailPipeline,
    logger);

// Execute
var result = await engine.ExecuteWorkflowAsync(
    workflow,
    new Dictionary<string, object> { ["userInput"] = "Hello!" });

// Check results
if (result.Success)
{
    Console.WriteLine($"Workflow completed in {result.Duration.TotalSeconds}s");
    Console.WriteLine($"Total cost: ${result.TotalEstimatedCost}");
    Console.WriteLine($"Final response: {result.FinalContext["response"]}");
}
```

---

## Workflow Format Reference

### File Structure

```
# Workflow Definition (header)
Name: WorkflowName
Description: What this workflow does
Version: 2.0
Steps: N

[Step1]
(step configuration)

[Step2]
(step configuration)

...

[StepN]
(step configuration)
```

### Header Fields

| Field | Required | Description | Example |
|-------|----------|-------------|---------|
| `Name` | ✅ | Workflow identifier | `OnboardingWorkflow` |
| `Description` | ✅ | Human-readable description | `Onboards new users` |
| `Version` | ✅ | Format version (use `2.0`) | `2.0` |
| `Steps` | ✅ | Number of steps | `3` |

### Step Fields

#### Core Fields

| Field | Required | Description | Example |
|-------|----------|-------------|---------|
| `Name` | ✅ | Step identifier | `AnalyzeInput` |
| `Type` | ✅ | Step type | `AgentTask` |
| `AgentName` | ✅ | Agent to execute | `InputAnalyzer` |
| `Input` | ✅ | Input prompt/data | `{userInput}` |
| `OutputKey` | ✅ | Variable name for output | `analysis` |
| `ContinueOnFailure` | ❌ | Continue if step fails | `false` (default) |
| `StepTimeout` | ❌ | Timeout in milliseconds | `30000` (default: 60000) |

#### LLM Configuration Fields

| Field | Required | Description | Default | Example |
|-------|----------|-------------|---------|---------|
| `Model` | ❌ | LLM model to use | `gpt-3.5-turbo` | `gpt-4` |
| `Temperature` | ❌ | Creativity (0.0-1.0) | `0.7` | `0.3` |
| `MaxTokens` | ❌ | Max output tokens | `1000` | `2000` |
| `TopP` | ❌ | Nucleus sampling | `1.0` | `0.9` |
| `FrequencyPenalty` | ❌ | Repetition penalty | `0.0` | `0.3` |
| `PresencePenalty` | ❌ | Topic diversity | `0.0` | `0.3` |
| `FallbackModel` | ❌ | Backup model if primary fails | - | `gpt-3.5-turbo` |

#### RAG Configuration Fields

| Field | Required | Description | Default | Example |
|-------|----------|-------------|---------|---------|
| `RAGStore` | ❌ | RAG store name | - | `brand-knowledge` |
| `RAGTopK` | ❌ | Number of results | `5` | `10` |
| `RAGMinSimilarity` | ❌ | Similarity threshold (0.0-1.0) | `0.7` | `0.8` |
| `RAGUseEmbeddings` | ❌ | Use vector embeddings | `true` | `true` |
| `RAGMetadataFilter` | ❌ | Filter by metadata | - | `tags:onboarding` |

#### Guardrail Configuration

| Field | Required | Description | Example |
|-------|----------|-------------|---------|
| `Guardrails` | ❌ | Comma-separated guardrail names | `no-pii,token-limit,json-schema` |

---

## Per-Step Configuration

### Cost Optimization Strategy

**Principle:** Use expensive models only when necessary

```
# Example: Cost-optimized workflow
[Step1]
Name: ExtractKeywords
Model: gpt-3.5-turbo    # Cheap model for simple extraction
Temperature: 0.0
MaxTokens: 200
OutputKey: keywords

[Step2]
Name: AnalyzeStrategy
Model: gpt-4            # Expensive model for complex analysis
Temperature: 0.5
MaxTokens: 1500
RAGStore: competitor-data
RAGTopK: 10
OutputKey: strategy

[Step3]
Name: FormatJSON
Model: gpt-3.5-turbo    # Cheap model for formatting
Temperature: 0.0
MaxTokens: 300
Guardrails: json-schema
OutputKey: output
```

**Cost Breakdown:**
- Step 1: ~200 tokens × $0.0005/1K = $0.0001
- Step 2: ~1500 tokens × $0.03/1K = $0.045
- Step 3: ~300 tokens × $0.0005/1K = $0.00015
- **Total: ~$0.045 vs $0.057 if all GPT-4 = 21% savings**

### Temperature Guidelines

| Temperature | Use Case | Example Steps |
|-------------|----------|---------------|
| `0.0 - 0.2` | Deterministic, factual | Data extraction, JSON formatting, keyword extraction |
| `0.3 - 0.5` | Balanced analysis | Classification, summarization, technical analysis |
| `0.6 - 0.8` | Creative content | Blog writing, marketing copy, brainstorming |
| `0.9 - 1.0` | Maximum creativity | Poetry, experimental content, idea generation |

### RAG Configuration Strategies

**Use RAG when:**
- Step needs domain-specific knowledge
- Output should reference existing content
- Compliance/consistency with brand guidelines required

**Example:**
```
[Step1]
Name: AnalyzeWithContext
RAGStore: brand-database
RAGTopK: 10              # More results = better context
RAGMinSimilarity: 0.7    # Lower = more permissive
RAGMetadataFilter: tags:approved,status:active
```

**RAG Performance Tips:**
- Higher `RAGTopK` = Better context, slower execution
- Higher `RAGMinSimilarity` = More precise, fewer results
- Use metadata filters to narrow search scope

---

## Guardrails System

### Overview

Guardrails are safety checks applied before (pre-execution) or after (post-execution) LLM calls to ensure quality and compliance.

### Built-in Guardrails

#### 1. PII Detection (`no-pii`)

**Stage:** Post-execution
**Purpose:** Prevent personally identifiable information in outputs

**Detects:**
- Social Security Numbers (SSN): `123-45-6789`
- Email addresses: `user@example.com`
- Phone numbers: `555-123-4567`
- Credit card numbers: `4532 1234 5678 9010`

**Usage:**
```
[Step1]
Name: GenerateContent
Guardrails: no-pii
```

**When to use:**
- Customer-facing content generation
- Public content creation
- Compliance-sensitive workflows

#### 2. Token Limit (`token-limit`)

**Stage:** Post-execution
**Purpose:** Enforce maximum output length

**Default:** 2000 tokens
**Customizable:** Via guardrail context parameters

**Usage:**
```
[Step1]
Name: GenerateSummary
Guardrails: token-limit
MaxTokens: 500          # LLM hint, but guardrail enforces
```

**When to use:**
- API rate limit management
- Cost control for expensive models
- Enforcing brief responses

#### 3. JSON Schema (`json-schema`)

**Stage:** Post-execution
**Purpose:** Validate JSON output format

**Usage:**
```
[Step1]
Name: ExtractMetadata
Input: Extract metadata as JSON
Guardrails: json-schema
```

**When to use:**
- Structured data extraction
- API response generation
- Database record creation

### Combining Guardrails

Apply multiple guardrails by listing them comma-separated:

```
[Step1]
Name: GenerateReport
Guardrails: no-pii,token-limit,json-schema
```

**Execution order:** Left to right, fail-fast (stops at first failure)

### Creating Custom Guardrails

Implement `IGuardrail` interface:

```csharp
public class CustomGuardrail : IGuardrail
{
    public string Name => "my-custom-guardrail";
    public GuardrailStage Stage => GuardrailStage.PostExecution;

    public Task<GuardrailResult> ValidateAsync(
        string content,
        GuardrailContext context,
        CancellationToken cancellationToken = default)
    {
        // Your validation logic
        if (IsValid(content))
        {
            return Task.FromResult(new GuardrailResult { Passed = true });
        }

        return Task.FromResult(new GuardrailResult
        {
            Passed = false,
            FailureReason = "Validation failed: ..."
        });
    }
}
```

Register in DI:
```csharp
services.AddSingleton<IGuardrail, CustomGuardrail>();
```

---

## Migration Guide (v1 to v2)

### Automatic Compatibility

**Good news:** v1 workflows automatically work in v2 engine!

The parser detects v1 format and converts to v2 internally:

```
# v1 Format (still works)
Name: OldWorkflow
Description: Legacy workflow
CallsAgents: Agent1,Agent2,Agent3
```

Converts to:
```
# v2 Equivalent (internal)
Name: OldWorkflow
Version: 1.0
Steps: 3
[Step1] AgentName: Agent1
[Step2] AgentName: Agent2
[Step3] AgentName: Agent3
```

### Manual Migration (Recommended)

**Benefits of migrating:**
- 20%+ cost savings via per-step model selection
- Quality improvements via guardrails
- Better observability (per-step metrics)

**Migration steps:**

**1. Add Version 2.0 header:**
```diff
  # Workflow Definition
  Name: MyWorkflow
  Description: My workflow
+ Version: 2.0
+ Steps: 3
```

**2. Convert agents to steps:**
```diff
- CallsAgents: Analyzer,Generator,Formatter
+ [Step1]
+ Name: Analyzer
+ Type: AgentTask
+ AgentName: Analyzer
+ Input: {userInput}
+ OutputKey: analysis
+
+ [Step2]
+ Name: Generator
+ Type: AgentTask
+ AgentName: Generator
+ Input: {analysis}
+ OutputKey: generated
+
+ [Step3]
+ Name: Formatter
+ Type: AgentTask
+ AgentName: Formatter
+ Input: {generated}
+ OutputKey: final
```

**3. Add per-step configuration:**
```diff
  [Step1]
  Name: Analyzer
+ Model: gpt-3.5-turbo    # Cheap model for analysis
+ Temperature: 0.3
+ MaxTokens: 500

  [Step2]
  Name: Generator
+ Model: gpt-4            # Expensive model for generation
+ Temperature: 0.7
+ MaxTokens: 1500
+ Guardrails: no-pii

  [Step3]
  Name: Formatter
+ Model: gpt-3.5-turbo    # Cheap model for formatting
+ Temperature: 0.0
+ MaxTokens: 300
+ Guardrails: json-schema
```

**4. Test both versions:**
```csharp
// Test v1 (baseline)
var v1Result = await engine.ExecuteWorkflowAsync(v1Workflow, context);
var v1Cost = v1Result.TotalEstimatedCost;

// Test v2 (optimized)
var v2Result = await engine.ExecuteWorkflowAsync(v2Workflow, context);
var v2Cost = v2Result.TotalEstimatedCost;

// Compare
var savings = ((v1Cost - v2Cost) / v1Cost) * 100;
Console.WriteLine($"Cost savings: {savings:F1}%");
```

---

## Examples

### Example 1: Blog Post Generation

```
# Workflow Definition
Name: BlogPostGeneration
Description: Generate SEO-optimized blog post with metadata
Version: 2.0
Steps: 3

[Step1]
Name: BrainstormIdeas
Type: AgentTask
AgentName: IdeaGenerator
Input: Generate 5 blog post ideas about: {topic}
Temperature: 0.8
MaxTokens: 800
Model: gpt-4
RAGStore: content-library
RAGTopK: 5
RAGMinSimilarity: 0.7
Guardrails: token-limit
OutputKey: ideas

[Step2]
Name: WriteBlogPost
Type: AgentTask
AgentName: ContentWriter
Input: Write 1000-word blog post based on first idea from {ideas}
Temperature: 0.7
MaxTokens: 2000
Model: gpt-4
RAGStore: writing-guidelines
RAGTopK: 3
RAGMinSimilarity: 0.8
Guardrails: no-pii,token-limit
OutputKey: blogPost

[Step3]
Name: ExtractSEOMetadata
Type: AgentTask
AgentName: SEOExtractor
Input: Extract SEO metadata from {blogPost}: title, description (160 chars), keywords. Return JSON.
Temperature: 0.0
MaxTokens: 200
Model: gpt-3.5-turbo
Guardrails: json-schema,token-limit
OutputKey: seoMetadata
```

### Example 2: Customer Support Automation

```
# Workflow Definition
Name: CustomerSupportAutomation
Description: Automated customer inquiry response
Version: 2.0
Steps: 4

[Step1]
Name: ClassifyInquiry
Type: AgentTask
AgentName: InquiryClassifier
Input: Classify customer inquiry: {customerMessage}. Categories: billing, technical, general.
Temperature: 0.2
MaxTokens: 100
Model: gpt-3.5-turbo
OutputKey: category

[Step2]
Name: RetrieveKnowledge
Type: AgentTask
AgentName: KnowledgeRetriever
Input: Find relevant help articles for {category} inquiry: {customerMessage}
Temperature: 0.0
MaxTokens: 500
Model: gpt-3.5-turbo
RAGStore: help-articles
RAGTopK: 5
RAGMinSimilarity: 0.75
RAGMetadataFilter: category:{category}
OutputKey: knowledgeArticles

[Step3]
Name: GenerateResponse
Type: AgentTask
AgentName: ResponseGenerator
Input: Generate helpful response based on articles: {knowledgeArticles}. Original inquiry: {customerMessage}
Temperature: 0.5
MaxTokens: 800
Model: gpt-4
Guardrails: no-pii
OutputKey: response

[Step4]
Name: FormatEmail
Type: AgentTask
AgentName: EmailFormatter
Input: Format as professional email JSON: subject, body, attachments. Content: {response}
Temperature: 0.0
MaxTokens: 300
Model: gpt-3.5-turbo
Guardrails: json-schema
OutputKey: email
```

### Example 3: Multi-Language Translation Pipeline

```
# Workflow Definition
Name: TranslationPipeline
Description: Translate content while preserving brand voice
Version: 2.0
Steps: 3

[Step1]
Name: ExtractTone
Type: AgentTask
AgentName: ToneAnalyzer
Input: Analyze tone and style of: {originalContent}
Temperature: 0.3
MaxTokens: 400
Model: gpt-4
OutputKey: toneAnalysis

[Step2]
Name: Translate
Type: AgentTask
AgentName: Translator
Input: Translate to {targetLanguage}: {originalContent}. Maintain tone: {toneAnalysis}
Temperature: 0.4
MaxTokens: 1500
Model: gpt-4
RAGStore: translation-memory
RAGTopK: 10
RAGMinSimilarity: 0.8
RAGMetadataFilter: language:{targetLanguage}
OutputKey: translation

[Step3]
Name: ValidateTranslation
Type: AgentTask
AgentName: QualityValidator
Input: Validate {targetLanguage} translation quality. Return JSON: quality_score (0-100), issues (array), approved (bool).
Temperature: 0.1
MaxTokens: 300
Model: gpt-3.5-turbo
Guardrails: json-schema
OutputKey: validation
```

---

## Troubleshooting

### Common Issues

#### 1. Workflow Fails with "Step 'X' failed: Pre-execution guardrail failed"

**Cause:** Input violates guardrail rules (e.g., contains PII)

**Solution:**
- Check input data for PII
- Remove or mask sensitive information
- If PII is expected, remove `no-pii` guardrail from pre-execution steps

#### 2. "Post-execution guardrail failed: Output exceeds token limit"

**Cause:** LLM generated more tokens than allowed

**Solution:**
- Increase `MaxTokens` in LLM config
- Make prompt more specific (request brevity)
- Increase guardrail token limit parameter

#### 3. "Guardrail 'X' not found"

**Cause:** Referenced guardrail not registered

**Solution:**
- Check spelling: `no-pii` (not `no_pii`)
- Ensure guardrail is registered in DI container
- Built-in guardrails: `no-pii`, `token-limit`, `json-schema`

#### 4. "Invalid JSON" error with `json-schema` guardrail

**Cause:** LLM output is not valid JSON

**Solution:**
- Make prompt more explicit: "Return ONLY valid JSON, no additional text"
- Use `Temperature: 0.0` for JSON generation
- Add example JSON structure in prompt
- Use GPT-4 for complex JSON (more reliable than GPT-3.5-turbo)

#### 5. Variable substitution not working (`{variable}` appears literally)

**Cause:** Variable not in workflow context

**Solution:**
- Check `OutputKey` from previous step matches variable name
- Ensure previous step succeeded
- Verify variable spelling (case-sensitive)

**Example:**
```
[Step1]
OutputKey: userAnalysis   # Output key

[Step2]
Input: {userAnalysis}     # Must match exactly
```

#### 6. High costs despite per-step optimization

**Cause:** Using expensive models for simple tasks

**Solution:**
- Audit model selection per step
- Use GPT-3.5-turbo for: extraction, classification, formatting
- Reserve GPT-4 for: complex analysis, creative generation, reasoning

**Cost audit:**
```csharp
foreach (var step in result.StepResults)
{
    Console.WriteLine($"{step.StepName}: {step.TokensUsed} tokens, ${step.EstimatedCost}");
}
```

#### 7. RAG results irrelevant or low quality

**Cause:** RAG configuration not tuned

**Solution:**
- Increase `RAGTopK` for more context
- Adjust `RAGMinSimilarity` (lower = more permissive)
- Refine `RAGMetadataFilter` to narrow search
- Check RAG store has relevant data

---

## Best Practices

### 1. Cost Optimization

✅ **Do:**
- Use GPT-3.5-turbo for simple tasks (extraction, formatting)
- Use GPT-4 only for complex reasoning/creative tasks
- Set appropriate `MaxTokens` limits
- Monitor `TotalEstimatedCost` per workflow

❌ **Don't:**
- Use GPT-4 for all steps "just to be safe"
- Set excessively high `MaxTokens` (wastes cost)
- Ignore cost metrics

### 2. Quality Assurance

✅ **Do:**
- Apply `no-pii` guardrail to customer-facing content
- Use `json-schema` for structured outputs
- Set `Temperature: 0.0` for deterministic tasks
- Test workflows with edge cases

❌ **Don't:**
- Skip guardrails in production
- Use high temperature for factual tasks
- Assume LLM output is always valid

### 3. Workflow Design

✅ **Do:**
- Keep steps focused (single responsibility)
- Use descriptive step names (`ExtractKeywords` not `Step1`)
- Chain steps with meaningful variable names
- Document workflow purpose in `Description`

❌ **Don't:**
- Create monolithic steps doing multiple things
- Use generic names (`Process`, `Handle`)
- Hard-code values (use variables: `{userInput}`)

### 4. RAG Configuration

✅ **Do:**
- Use RAG for domain-specific knowledge
- Filter by metadata when possible
- Balance `RAGTopK` (too low = missing context, too high = noise)
- Keep RAG stores updated

❌ **Don't:**
- Use RAG for general knowledge (wasteful)
- Set `RAGTopK` extremely high (performance impact)
- Ignore similarity scores (check relevance)

### 5. Error Handling

✅ **Do:**
- Set `ContinueOnFailure: true` for non-critical steps
- Use fallback models (`FallbackModel: gpt-3.5-turbo`)
- Monitor `StepFailed` events
- Log errors for debugging

❌ **Don't:**
- Assume all steps always succeed
- Skip error handling in production
- Ignore failed steps

### 6. Testing

✅ **Do:**
- Write integration tests for workflows
- Test with realistic data
- Validate cost targets (>20% savings)
- Test backward compatibility (v1 format)

❌ **Don't:**
- Test only happy path
- Skip performance testing
- Ignore edge cases

---

## Additional Resources

### Documentation

- [Hazina API Reference](./api-reference.md)
- [.hazina v2.0 Format Specification](./hazina-workflow-format-v2.md)
- [Guardrails Development Guide](./guardrails-development.md)

### Sample Workflows

- [Brand2Boost Sample Workflows](C:\stores\brand2boost\.hazina\workflows\)
  - `onboarding-test.hazina`
  - `brand-analysis-test.hazina`
  - `content-generation-test.hazina`

### Source Code

- [Hazina.AI.Workflows](../../src/Core/AI/Hazina.AI.Workflows/)
- [Hazina.AI.Guardrails](../../src/Core/AI/Hazina.AI.Guardrails/)
- [Integration Tests](../../tests/Hazina.AI.Workflows.Tests/)

---

## Support

**Issues:** [GitHub Issues](https://github.com/martiendejong/Hazina/issues)
**Discussions:** [GitHub Discussions](https://github.com/martiendejong/Hazina/discussions)

---

**Version History:**

- **v2.0 (2026-01-18)** - Phase 1 complete (per-step config, guardrails, cost optimization)
- **v1.0 (Previous)** - Initial workflow system

**Next Phase:** Visual workflow designer UI, advanced control flow (conditionals, loops)
