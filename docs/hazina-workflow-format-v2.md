# .hazina Workflow Format v2.0 Specification

**Version:** 2.0
**Date:** 2026-01-18
**Status:** Draft

---

## Overview

The .hazina workflow format v2.0 extends the original format to support per-step configuration for LLM parameters, RAG search settings, and guardrails. This enables fine-grained control over each workflow step while maintaining backward compatibility with v1.0.

## Format Structure

### Workflow Header

```
# Workflow Definition
Name: <workflow-name>
Description: <description>
Version: 2.0
Steps: <number>
```

### Step Sections

Each step is defined in a `[StepN]` section where N is the step number (1-based):

```
[Step1]
Name: <step-name>
Type: <AgentTask|Parallel|Conditional|Loop>
AgentName: <agent-name>
Input: <input-template>
Temperature: <0.0-2.0>
MaxTokens: <integer>
Model: <model-name>
TopP: <0.0-1.0>
FrequencyPenalty: <-2.0 to 2.0>
PresencePenalty: <-2.0 to 2.0>
FallbackModel: <model-name>
RAGStore: <store-name>
RAGTopK: <integer>
RAGMinSimilarity: <0.0-1.0>
RAGUseEmbeddings: <true|false>
RAGMetadataFilter: <filter-expression>
RAGMaxContextLength: <integer>
Guardrails: <guardrail1,guardrail2,...>
StepTimeout: <milliseconds>
OutputKey: <variable-name>
ContinueOnFailure: <true|false>
```

## Field Descriptions

### Step Identity Fields

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| Name | string | Yes | - | Human-readable step name |
| Type | enum | Yes | AgentTask | Step type (AgentTask, Parallel, Conditional, Loop) |
| AgentName | string | Yes* | - | Name of agent to execute (*required for AgentTask) |
| Input | string | Yes | - | Input template with {variable} placeholders |
| OutputKey | string | No | - | Variable name to store step output |
| ContinueOnFailure | boolean | No | false | Whether to continue workflow if step fails |
| StepTimeout | integer | No | 60000 | Maximum execution time in milliseconds |

### LLM Configuration Fields

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| Model | string | No | gpt-3.5-turbo | LLM model to use for this step |
| FallbackModel | string | No | - | Model to use if primary model fails |
| Temperature | float | No | 0.7 | Sampling temperature (0.0-2.0) |
| MaxTokens | integer | No | 1000 | Maximum tokens in response |
| TopP | float | No | 1.0 | Nucleus sampling parameter (0.0-1.0) |
| FrequencyPenalty | float | No | 0.0 | Frequency penalty (-2.0 to 2.0) |
| PresencePenalty | float | No | 0.0 | Presence penalty (-2.0 to 2.0) |

### RAG Configuration Fields

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| RAGStore | string | No | - | Vector store name to search |
| RAGTopK | integer | No | 5 | Number of search results to retrieve |
| RAGMinSimilarity | float | No | 0.7 | Minimum similarity threshold (0.0-1.0) |
| RAGUseEmbeddings | boolean | No | true | Use semantic search vs keyword search |
| RAGMetadataFilter | string | No | - | Metadata filter expression |
| RAGMaxContextLength | integer | No | 4000 | Maximum context length from RAG results |

### Guardrails

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| Guardrails | string (comma-separated) | No | - | List of guardrails to apply (e.g., "no-pii,token-limit") |

**Built-in Guardrails:**
- `no-pii` - Detect and block personally identifiable information
- `token-limit` - Enforce token limits
- `json-schema` - Validate JSON output against schema
- `content-filter` - Toxicity and inappropriate content detection
- `tone-check` - Validate output tone
- `language-check` - Ensure output language matches requirement

## Variable Substitution

Input templates support variable substitution using `{variableName}` syntax:

- `{userInput}` - Initial workflow input
- `{stepName}` - Output from step with name "stepName"
- `{lastResult}` - Output from previous step
- `{data.key}` - Value from workflow context data

**Example:**
```
Input: Based on {analysis}, generate content for {userInput}
```

## Complete Example

```
# Workflow Definition
Name: OnboardingWorkflow
Description: Onboards new users with personalized content
Version: 2.0
Steps: 3

[Step1]
Name: AnalyzeInput
Type: AgentTask
AgentName: InputAnalyzer
Input: {userInput}
Temperature: 0.3
MaxTokens: 500
Model: gpt-4
TopP: 0.9
FrequencyPenalty: 0.0
PresencePenalty: 0.0
RAGStore: brand-knowledge
RAGTopK: 5
RAGMinSimilarity: 0.8
RAGUseEmbeddings: true
RAGMetadataFilter: tags:onboarding
Guardrails: no-pii,token-limit
StepTimeout: 30000
OutputKey: analysis
ContinueOnFailure: false

[Step2]
Name: GenerateResponse
Type: AgentTask
AgentName: ResponseGenerator
Input: Based on {analysis}, generate personalized response
Temperature: 0.7
MaxTokens: 1000
Model: gpt-4-turbo
FallbackModel: gpt-3.5-turbo
RAGStore: content-templates
RAGTopK: 3
RAGMinSimilarity: 0.7
Guardrails: tone-check,length-limit
OutputKey: response
ContinueOnFailure: false

[Step3]
Name: SaveResults
Type: AgentTask
AgentName: StorageAgent
Input: Save {response} to user profile
Temperature: 0.0
MaxTokens: 100
Model: gpt-3.5-turbo
Guardrails: json-schema
OutputKey: saved
ContinueOnFailure: true
```

## Backward Compatibility

The parser automatically detects v1.0 format (no `[StepN]` sections) and converts it to v2.0 internally:

**V1.0 Format:**
```
Name: SimpleFlow
Description: A simple flow
CallsAgents: Agent1,Agent2
```

**Converted to V2.0:**
```
Name: SimpleFlow
Description: A simple flow
Version: 1.0

[Step1]
Name: Agent1
Type: AgentTask
AgentName: Agent1
Input: {previousResult}
OutputKey: agent1

[Step2]
Name: Agent2
Type: AgentTask
AgentName: Agent2
Input: {previousResult}
OutputKey: agent2
```

## Version Detection

The parser detects format version using this logic:

1. **Explicit Version:** If `Version: 2.0` is present in header → v2.0
2. **Step Sections:** If `[StepN]` sections present → v2.0
3. **Default:** Otherwise → v1.0 (backward compatibility)

## Best Practices

### Temperature Settings

- **Low (0.0-0.3):** Data extraction, structured output, precise tasks
- **Medium (0.4-0.7):** Balanced creativity and consistency
- **High (0.8-1.0):** Creative writing, brainstorming, varied output

### Model Selection

- **GPT-4:** Complex analysis, reasoning, high-quality output (expensive)
- **GPT-3.5-turbo:** General tasks, cost-effective (cheaper)
- **GPT-4-turbo:** Faster than GPT-4, cheaper, good balance

**Cost Optimization:** Use expensive models only where needed:
```
[Step1 - Analysis]
Model: gpt-4          # Complex reasoning needs GPT-4

[Step2 - Formatting]
Model: gpt-3.5-turbo  # Simple task, save cost
```

### RAG Configuration

- **TopK=3-5:** General content retrieval
- **TopK=10-20:** Comprehensive search, need many examples
- **MinSimilarity=0.8-1.0:** High precision, exact matches
- **MinSimilarity=0.6-0.7:** High recall, broader results

### Guardrails

Always include guardrails for production workflows:

- **User-facing output:** `no-pii`, `tone-check`, `content-filter`
- **Data extraction:** `json-schema`
- **Cost control:** `token-limit`

## File Organization

Store workflows in application-specific folders:

```
C:\stores\{appName}\.hazina\
├── workflows\
│   ├── onboarding.hazina
│   ├── brand-analysis.hazina
│   └── content-generation.hazina
├── agents\
│   ├── input-analyzer.hazina
│   └── response-generator.hazina
└── config\
    ├── llm-defaults.json
    └── rag-defaults.json
```

## Migration from V1.0

To migrate existing v1.0 workflows:

1. Add `Version: 2.0` to header
2. Convert `CallsAgents` list to `[StepN]` sections
3. Add per-step LLM/RAG configuration as needed
4. Add guardrails for safety

**Migration Tool:**
```csharp
var v1Config = HazinaWorkflowConfigParser.LoadFromFile("old-workflow.hazina");
// Automatically converted to v2.0 internally
HazinaWorkflowConfigParser.SaveToFile(v1Config, "new-workflow-v2.hazina");
```

## Future Extensions

Planned for future versions:

- Expression-based conditions: `{score} > 0.8 && {language} == 'en'`
- Multi-way branching: Switch/case style decisions
- External API decision nodes
- Workflow nesting: Call sub-workflows
- Streaming support for long-running steps

---

**Document Version:** 1.0
**Last Updated:** 2026-01-18
**Maintained By:** Hazina Framework Team
