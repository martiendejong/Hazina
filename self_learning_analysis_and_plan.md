# Self-Learning AI System: Comprehensive Analysis & Implementation Plan

**Date**: 2026-01-06
**Repositories**: Hazina Framework + SCP (Synthetic Cognitive Platform)
**Focus**: Self-improving AI through evaluation → reflection → versioning → controlled improvement

---

## EXECUTIVE SUMMARY

This document presents a comprehensive analysis of the Hazina and SCP codebases to determine existing self-learning capabilities and design a complete self-improving AI architecture. The analysis reveals that while both systems have strong foundations for observability, feedback collection, and evaluation, **critical components for true self-improvement are missing**: prompt versioning, automated evaluation pipelines, and controlled rewrite mechanisms.

**Key Findings**:
- ✅ **Strong Foundation**: Comprehensive logging, feedback collection, evaluation framework, fault detection
- ⚠️ **Missing Components**: Prompt versioning, automated evaluation loops, policy evolution mechanisms
- 🎯 **Recommendation**: Implement controlled self-learning (Option B) with human approval for production safety

---

## PART 1: CODEBASE ANALYSIS

### 1.1 HAZINA FRAMEWORK - Core Infrastructure

#### Agent Architecture
**File**: `C:\projects\hazina\src\Core\AI\Hazina.AI.Providers\Core\ProviderOrchestrator.cs`

**LLM Call Orchestration**:
- **ILLMClient Interface**: Provider-agnostic abstraction for all LLM interactions
- **ProviderOrchestrator**: Multi-provider management with:
  - Automatic failover (circuit breaker pattern)
  - Selection strategies: Priority, LeastCost, FastestResponse, RoundRobin, Random
  - Cost tracking: Real-time per-provider cost monitoring
  - Budget management: Alerts at 50%, 75%, 90%, 95% of budget
  - Health monitoring: Continuous availability checks

**Agent Class** (`C:\projects\hazina\src\Core\AI\Hazina.AI.Agents\Core\Agent.cs`):
- High-level agent abstraction with tool calling support
- Conversation history management
- Integration with NeuroChain for multi-layer reasoning
- Safety: Max iteration limit (default: 10)

**Evidence**: Lines 308-348 (Agent.cs) show system prompt construction with dynamic context injection.

#### Prompt Structure
**Status**: ❌ **NO EXTERNAL VERSIONING**

**Current State**:
- All prompts **hardcoded in C# code**
- Dynamic construction at runtime using StringBuilder
- Format instructions injected programmatically

**Examples**:
```csharp
// Agent.cs:308-348
private string BuildSystemPrompt(Dictionary<string, object>? context)
{
    var prompt = $"You are {Name}, an AI agent. {Description}";
    // ... context injection
}

// RAGEngine.cs:492-512
private string BuildRAGPrompt(string query, string context, RAGQueryOptions options)
{
    var sb = new StringBuilder();
    sb.AppendLine("Context:");
    sb.AppendLine(context);
    sb.AppendLine($"Question: {query}");
    return sb.ToString();
}
```

**Gap Identified**: No external template system, no versioning, no A/B testing capability. Prompt changes require code deployment.

#### Logging & Observability
**Status**: ✅ **COMPREHENSIVE IMPLEMENTATION**

**File**: `C:\projects\hazina\src\Core\Observability\Hazina.Observability.LLMLogs\Decorators\LLMLoggingClientDecorator.cs`

**Decorator Pattern** capturing every LLM interaction:
- CallId, ParentCallId (for tool chains)
- Username, ProjectId, Feature, Step
- Provider, Model
- Request messages, Response data
- Token usage (input/output/total)
- Cost estimation (USD)
- Execution time (milliseconds)
- Tool call metadata
- Embedded documents (for RAG)
- Success/failure status

**Prometheus Metrics** (`C:\projects\hazina\src\Core\Observability\Hazina.Observability.Core\Metrics\HazinaMetrics.cs`):
- OperationDuration (Histogram)
- OperationsTotal (Counter)
- ProviderHealth (Gauge)
- HallucinationsDetected (Counter)
- FaultsDetected (Counter)
- TotalCost (Counter)
- TokensUsed (Counter)

**Evidence**: Complete observability stack with Grafana dashboards pre-configured.

#### Document Store & Memory
**Status**: ✅ **PRODUCTION-READY**

**Three-Layer Architecture**:

1. **DocumentStore** (`C:\projects\hazina\src\Core\Storage\Hazina.Store.DocumentStore\`):
   - Metadata storage (PostgreSQL/Supabase/File-based)
   - Chunk storage with indexing
   - Full-text search + filtering
   - Document graph relationships
   - Hierarchical scopes

2. **EmbeddingStore** (`C:\projects\hazina\src\Core\Storage\Hazina.Store.EmbeddingStore\`):
   - Multiple backends: Faiss, pgvector, SQLite, File-based
   - Vector indexing: IVFFlat and HNSW
   - Batch operations for efficiency

3. **Memory Systems** (`C:\projects\hazina\src\Core\AI\Hazina.AI.Memory\Core\WorkingMemory.cs`):
   - Token-bounded working memory (default: 8000 tokens)
   - FIFO eviction with priority support
   - Memory types: Instruction, Observation, ToolResult, Decision, Context

**Evidence**: Flexible storage with hybrid mode (local files + cloud database) for optimal performance.

#### Evaluation Framework
**Status**: ✅ **IMPLEMENTED** (but not connected to self-improvement loop)

**File**: `C:\projects\hazina\src\Core\AI\Hazina.Evals\Core\EvaluationRunner.cs`

**Metrics**:
- MRR (Mean Reciprocal Rank): Position of first relevant result
- Hit@K: Did we find any relevant in top K?
- NDCG (Normalized Discounted Cumulative Gain): Ranking quality
- Precision@K: Relevant/Retrieved ratio
- Recall@K: Coverage of relevant documents

**Test Case Structure**:
```csharp
EvalCase {
    Id: string
    Query: string
    RelevantDocuments: List<string>  // Ground truth
}

EvalRun {
    RunId: string
    Retriever: IRetriever
    Reranker: IReranker
    AggregateMetrics: EvalMetrics
}
```

**Gap**: Evaluation exists but is **not automated** and **not connected to prompt improvement**.

#### Fault Detection & Self-Correction
**Status**: ✅ **AUTO-RETRY IMPLEMENTED** (but no learning from failures)

**File**: `C:\projects\hazina\src\Core\AI\Hazina.AI.FaultDetection\AdaptiveFaultHandler.cs`

**Multi-Stage Validation**:
1. **BasicResponseValidator**: Format validation (JSON, XML, Code)
2. **BasicHallucinationDetector**: 7 types of hallucinations
   - FabricatedFact, Contradiction, ContextMismatch
   - UnsupportedClaim, AttributionError
   - TemporalError, QuantitativeError
3. **BasicConfidenceScorer**: Response quality analysis
4. **BasicErrorPatternRecognizer**: Pattern learning (not fully implemented)

**Auto-Retry Flow**:
- Validates response → Checks for hallucinations → Scores confidence
- If failed: Refines prompt and retries (max 3 attempts)
- Logs to TelemetrySystem

**Gap**: Failures are detected and retried, but **patterns are not systematically learned** to improve future prompts.

#### NeuroChain Cross-Validation
**Status**: ✅ **ADVANCED CONSENSUS MECHANISM**

**File**: `C:\projects\hazina\src\Core\AI\Hazina.Neurochain.Core\Core\NeuroChainOrchestrator.cs`

**Multi-Layer Reasoning**:
- FastReasoningLayer: Quick analysis (GPT-3.5/Haiku)
- DeepReasoningLayer: Thorough analysis (GPT-4/Sonnet)
- VerificationLayer: Independent cross-validation

**Cross-Validation** (lines 147-220):
```csharp
CrossValidationResult {
    IsValid: bool
    Confidence: double
    Issues: List<ValidationIssue>
    Agreements: List<string>
    Disagreements: List<string>
    ConsensusAnswer: string
}
```

**Features**:
- Perfect consensus detection
- Majority voting (threshold: 2/3)
- Confidence variance analysis
- Weighted voting by confidence
- Early stopping for cost optimization

**Evidence**: This provides quality assessment but **results are not fed back to improve prompts**.

---

### 1.2 SCP (SYNTHETIC COGNITIVE PLATFORM) - Cognitive Layer

#### Cognitive Architecture
**Status**: ✅ **REVOLUTIONARY METACOGNITIVE SYSTEM**

**File**: `C:\projects\scp\src\Scp.Core\CognitiveCoordinator.cs`

**Metacognitive Features**:

1. **Self-Aware Confidence Calibration** (lines 708-736):
   ```csharp
   private double CalibrateConfidence(
       double baseConfidence,
       Dictionary<string, ChannelResponse> channelResults,
       List<ChannelConflict> conflicts)
   {
       var calibrated = baseConfidence;
       var conflictPenalty = conflicts.Count * 0.1;
       var variance = confidences.Select(c => Math.Pow(c - avgConfidence, 2)).Average();
       var stdDev = Math.Sqrt(variance);
       var variancePenalty = stdDev * 0.15;
       calibrated -= (conflictPenalty + variancePenalty);
       return Math.Clamp(calibrated, 0.0, 1.0);
   }
   ```
   - System knows what it doesn't know
   - Penalizes itself for conflicts and variance
   - Self-regulating confidence

2. **Cross-Channel Attention** (lines 551-589):
   - Dynamic weighting based on query type and confidence
   - Self-normalizing: weights sum to 1.0

3. **Conflict Detection** (lines 591-646):
   - Factual-ethical tensions
   - Emotional-factual conflicts
   - Self-aware of internal contradictions

4. **Explainable Reasoning Chains** (lines 739-799):
   - Every decision documented step-by-step
   - Full transparency into cognitive process

**Evidence**: True metacognitive awareness - system evaluates its own reasoning quality.

#### Four Cognitive Channels

**1. Causal Reasoning Channel** (`C:\Projects\scp\src\Scp.Channels\Causality\CausalReasoningChannel.cs`):
- Builds causal graphs from evidence
- Traces causal chains to root causes
- Performs counterfactual analysis ("What if X didn't happen?")
- Distinguishes correlation from causation

**Causal Models**:
```csharp
public class CausalGraph {
    public List<CausalRelationship> Relationships { get; set; }

    public List<CausalRelationship> GetDirectCauses(string effect)
    public List<List<CausalRelationship>> TraceCausalChains(string effect)
}

public enum CausalRelationType {
    DirectCause, RootCause, Contributing,
    Necessary, Sufficient, PreventsCause, Correlation
}
```

**2. Validity Channel** (`C:\Projects\scp\src\Scp.Channels\Validity\ValidityChannel.cs`):
- RAG-based fact-checking
- Semantic similarity search
- Source citation with confidence scores

**3. Empathy Channel** (`C:\Projects\scp\src\Scp.Channels\Empathy\EmpathyChannel.cs`):
- Emotional state detection
- User need inference
- Adaptive response strategies

**4. Context Channel** (`C:\Projects\scp\src\Scp.Channels\Context\ContextChannel.cs`):
- Temporal reasoning
- Conversation context loading
- Context shift detection

**Evidence**: Complete multi-channel cognitive architecture with independent reasoning systems.

#### Prompt Management
**Status**: ⚠️ **POLICY-DRIVEN BUT NOT VERSIONED**

**File**: `C:\Projects\scp\policy.neurochain.json`

```json
{
  "blockedIntents": ["harmful", "manipulate"],
  "sensitiveTopics": ["medical", "legal", "financial"],
  "darkPatternPhrases": ["act now", "limited time"],
  "maxArousalScore": 0.8,
  "requireHitlOn": ["personal_info"],
  "maxFixationOverlap": 0.7
}
```

**Gap**: Policies are static JSON files. No version history, no effectiveness tracking, no automated evolution.

#### Feedback Learning System
**Status**: ✅ **COMPLETE LEARNING LOOP**

**File**: `C:\Projects\scp\src\Scp.Core\Feedback\FeedbackLearningEngine.cs`

**Learning Pipeline**:

1. **Heuristic Analysis** (lines 107-203):
   - Pattern matching for common preferences
   - Fast, cost-free preference detection
   - Examples: "too technical", "too simple", "needs examples"

2. **LLM-Based Analysis** (lines 208-288):
   - Deep analysis of complex feedback
   - Multi-category preference extraction
   - Confidence-weighted insights

3. **Profile Updates** (lines 302-324):
   ```csharp
   private async Task UpdateUserProfileFromInsightsAsync(
       string userId,
       List<FeedbackInsight> insights)
   {
       var profile = await _longTermMemory.GetUserProfileAsync(userId);
       foreach (var insight in insights.Where(i =>
           i.Confidence >= _config.MinimumConfidenceThreshold))
       {
           var prefKey = $"preference-{insight.PreferenceCategory}";
           profile.Preferences[prefKey] = insight.PreferenceValue;
       }
       await _longTermMemory.SaveUserProfileAsync(profile);
   }
   ```

4. **Pattern Learning** (lines 327-344):
   - Aggregates repeated preferences
   - Confidence-weighted pattern evolution

**Database Schema** (`C:\Projects\scp\src\Scp.Memory\Migrations\002_FeedbackLearning.sql`):
```sql
CREATE TABLE feedback_records (
    feedback_id, user_id, query_text, response_text,
    rating, comment, processed
);

CREATE TABLE feedback_insights (
    insight_id, feedback_id,
    preference_category, preference_value,
    confidence, reasoning
);
```

**Evidence**: Complete feedback loop that learns user preferences and updates profiles, but **doesn't automatically improve prompts/policies**.

#### Memory & Knowledge Management
**Status**: ✅ **PRODUCTION-READY**

**Long-Term Memory** (`C:\Projects\scp\src\Scp.Memory\LongTermMemory\PostgresLongTermMemory.cs`):
- User profiles with communication preferences
- Learned patterns (category, confidence, occurrences)
- Interaction history
- Feedback persistence

**Working Memory**:
- RedisWorkingMemory: High-speed session storage
- InMemoryWorkingMemory: Development/testing

**Incremental Learning** (`C:\Projects\scp\src\Scp.Embeddings\IncrementalEmbeddingService.cs`):
- SHA-256 content hashing
- Only regenerates embeddings when content changes
- 80-90% cost reduction

**Evidence**: Robust memory systems with pattern learning, but **patterns don't feed back to prompt evolution**.

#### Safety Mechanisms
**Status**: ✅ **MULTI-LAYER SAFETY PIPELINE**

**File**: `C:\Projects\scp\src\Scp.Core\Pipeline\NeurochainPipeline.cs`

**Pre-LLM Layers**:
1. PerceptualGate: Intent classification and filtering
2. LimbicShield: Arousal detection and de-escalation

**Post-LLM Layers**:
3. DarkPatternBlocking: Manipulative pattern detection
4. NeuroIntegrity: Dependency and fixation prevention

**Human-in-the-Loop** (`C:\Projects\scp\src\Scp.Core\Hitl\InMemoryHitlQueue.cs`):
- Escalation queue for sensitive requests
- Approve/deny workflow
- Audit trail

**Evidence**: Comprehensive safety system with policy enforcement.

---

## PART 2: FUNCTIONAL GAP ANALYSIS

### 2.1 Self-Improving AI Architecture Components

A complete self-improving system requires:

1. **Execution Logging**: Input/output/context capture
2. **Evaluation Layer**: AI evaluating AI via rubrics
3. **Reflection & Aggregation**: Multi-run pattern analysis
4. **Prompt/Policy Rewrite**: Automated improvement proposals
5. **Versioning + Rollback**: Change tracking and safety
6. **Safety Nets**: Cooldowns, thresholds, drift limits
7. **Admin Interface**: Inspection and control

### 2.2 Gap Analysis Table

| Functionality | Hazina | SCP | What's Missing |
|--------------|--------|-----|----------------|
| **LOGGING & OBSERVABILITY** |
| Execution logging | ✅ Complete | ✅ Via Hazina | Nothing - comprehensive |
| Token/cost tracking | ✅ Per-call | ✅ Via Hazina | Nothing - production-ready |
| Failure tracking | ✅ TelemetrySystem | ✅ Audit logs | Nothing |
| Context capture | ✅ Full context | ✅ Full context | Nothing |
| **EVALUATION** |
| Metrics framework | ✅ MRR/NDCG/P@K/R@K | ⚠️ Manual | Automated pipeline missing |
| Ground truth management | ⚠️ Manual EvalCases | ❌ None | Systematic test set management |
| Continuous evaluation | ❌ None | ❌ None | Scheduled eval runs |
| Regression detection | ❌ None | ❌ None | Automatic performance monitoring |
| Quality rubrics | ⚠️ Basic validators | ⚠️ Confidence only | Multi-dimensional quality assessment |
| **FEEDBACK & LEARNING** |
| User feedback collection | ⚠️ Not implemented | ✅ Complete | Hazina needs feedback system |
| Preference learning | ❌ None | ✅ Complete | Hazina needs this |
| Implicit signal tracking | ❌ None | ❌ None | Click-through, dwell time, reformulation |
| Pattern aggregation | ❌ None | ⚠️ User-level only | Cross-user pattern learning |
| **REFLECTION & ANALYSIS** |
| Failure pattern analysis | ⚠️ BasicErrorPatternRecognizer | ❌ None | Systematic root cause analysis |
| Success pattern analysis | ❌ None | ❌ None | What works? Why? |
| Cross-run aggregation | ❌ None | ❌ None | Batch analysis of multiple runs |
| Drift detection | ❌ None | ❌ None | Performance drift over time |
| A/B test analysis | ❌ None | ❌ None | Statistical comparison framework |
| **PROMPT/POLICY MANAGEMENT** |
| External prompts | ❌ Hardcoded in C# | ❌ Hardcoded | Template system needed |
| Prompt versioning | ❌ None | ❌ None | Git-like version control |
| Policy versioning | ❌ None | ❌ None | Track policy evolution |
| A/B testing | ❌ None | ❌ None | Multi-variant testing |
| Effectiveness tracking | ❌ None | ❌ None | Which prompts perform better? |
| **AUTOMATED IMPROVEMENT** |
| Prompt rewrite proposals | ❌ None | ❌ None | LLM-based prompt optimization |
| Policy evolution | ❌ None | ❌ None | Learned policy adjustments |
| Human approval workflow | ❌ None | ⚠️ HITL for queries only | Approval for prompt changes |
| Rollback capability | ❌ None | ❌ None | Revert to previous version |
| **SAFETY NETS** |
| Change cooldowns | ❌ None | ❌ None | Rate-limit prompt updates |
| Performance thresholds | ❌ None | ❌ None | Reject changes that hurt quality |
| Drift limits | ❌ None | ❌ None | Prevent semantic drift |
| Sandbox testing | ❌ None | ❌ None | Test new prompts before production |
| Emergency stop | ❌ None | ❌ None | Disable self-learning if needed |
| **ADMIN & INSPECTION** |
| Version history UI | ❌ None | ❌ None | Browse prompt versions |
| Effectiveness dashboard | ⚠️ Grafana (ops) | ❌ None | Quality metrics dashboard |
| Approval queue | ❌ None | ⚠️ HITL queue | Prompt approval interface |
| Rollback UI | ❌ None | ❌ None | One-click rollback |

### 2.3 Summary of Gaps

**What EXISTS**:
- ✅ Comprehensive logging (Hazina)
- ✅ Cost tracking (Hazina)
- ✅ Evaluation metrics framework (Hazina)
- ✅ Fault detection (Hazina)
- ✅ User feedback collection (SCP)
- ✅ Preference learning (SCP)
- ✅ Metacognitive awareness (SCP)
- ✅ Safety pipeline (SCP)

**What's MISSING** (critical for self-improvement):
- ❌ **Prompt versioning system**
- ❌ **Automated evaluation pipeline**
- ❌ **Reflection & pattern aggregation engine**
- ❌ **Prompt rewrite mechanism**
- ❌ **Human approval workflow for changes**
- ❌ **Rollback capability**
- ❌ **Safety nets (cooldowns, thresholds)**
- ❌ **Admin interface for inspection/control**

**What EXISTS but is INCOMPLETE**:
- ⚠️ Evaluation framework exists but not automated
- ⚠️ Error pattern recognition exists but not systematic
- ⚠️ Feedback learning works but doesn't improve prompts
- ⚠️ HITL exists for queries but not for prompt changes

---

## PART 3: TARGET ARCHITECTURE

### 3.1 Design Principles

1. **Hazina = Generic Infrastructure**
   - Reusable across all AI applications
   - No domain-specific logic
   - NuGet-packaged components

2. **SCP = Cognitive Experimentation**
   - Can use experimental self-learning features
   - Domain-specific cognitive architecture
   - Innovation sandbox

3. **Configuration-Driven**
   - Self-learning ON/OFF via config
   - All safety parameters configurable
   - Zero code changes for behavior changes

4. **Storage Agnostic**
   - File-based for development
   - PostgreSQL/Supabase for production
   - Redis for hot data

5. **Provider Agnostic**
   - Works with any LLM provider
   - Evaluation doesn't depend on specific models

### 3.2 Architecture Layers

```
┌─────────────────────────────────────────────────────────────┐
│                      ADMIN INTERFACE                         │
│  (Version History, Approval Queue, Effectiveness Dashboard)  │
└─────────────────────────────────────────────────────────────┘
                              ↕
┌─────────────────────────────────────────────────────────────┐
│                    SELF-LEARNING ENGINE                      │
├─────────────────────────────────────────────────────────────┤
│  Reflection & Aggregation  →  Prompt Rewriter  →  Approver  │
│         ↑                           ↓                        │
│    Evaluation Pipeline    ←    Version Control               │
└─────────────────────────────────────────────────────────────┘
                              ↕
┌─────────────────────────────────────────────────────────────┐
│                    EXECUTION LAYER                           │
├─────────────────────────────────────────────────────────────┤
│  Prompt Store  →  Agent/LLM  →  Response  →  Logger         │
│                      ↓                          ↓            │
│                   User Feedback  →  Feedback Collector       │
└─────────────────────────────────────────────────────────────┘
                              ↕
┌─────────────────────────────────────────────────────────────┐
│                    STORAGE LAYER                             │
├─────────────────────────────────────────────────────────────┤
│  Prompt Versions │ Eval Results │ Feedback │ Metrics │ Logs │
└─────────────────────────────────────────────────────────────┘
```

### 3.3 Component Specifications

#### 3.3.1 Prompt Store (NEW)
**Location**: `Hazina.AI.PromptManagement`

**Responsibilities**:
- Load prompts from external templates (Handlebars, Liquid, Scriban)
- Version tracking (Git-like hash-based versioning)
- A/B test variant management
- Rollback capability
- Effectiveness metrics per version

**Interfaces**:
```csharp
public interface IPromptStore
{
    Task<PromptTemplate> GetAsync(string promptId, string? version = null);
    Task<string> SaveAsync(PromptTemplate template, string reason);
    Task<PromptVersion[]> GetVersionHistoryAsync(string promptId);
    Task RollbackAsync(string promptId, string targetVersion);
    Task<PromptMetrics> GetEffectivenessAsync(string promptId, string version);
}

public class PromptTemplate
{
    public string Id { get; set; }
    public string Version { get; set; }  // SHA-256 hash of content
    public string Template { get; set; }
    public Dictionary<string, string> Variables { get; set; }
    public string Engine { get; set; }  // "Handlebars" | "Liquid" | "Scriban"
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; }  // "human" | "system"
    public string Reason { get; set; }
    public PromptMetrics? Metrics { get; set; }
}

public class PromptMetrics
{
    public int TotalUses { get; set; }
    public double AvgUserRating { get; set; }
    public double AvgConfidence { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public Dictionary<string, double> EvalMetrics { get; set; }  // MRR, NDCG, etc.
}
```

**Storage**:
- Templates: File-based (version-controlled directory) or PostgreSQL JSONB
- Metrics: PostgreSQL for aggregation
- Version history: Append-only log

#### 3.3.2 Evaluation Pipeline (ENHANCED)
**Location**: `Hazina.Evals.Pipeline`

**Responsibilities**:
- Scheduled evaluation runs (cron-based)
- Ground truth test set management
- Multi-metric evaluation (MRR, NDCG, Precision, Recall, custom rubrics)
- Regression detection (alert if performance drops)
- A/B test statistical analysis

**Interfaces**:
```csharp
public interface IEvaluationPipeline
{
    Task<EvalRunResult> RunAsync(string promptId, string version, EvalTestSet testSet);
    Task ScheduleAsync(string promptId, string cronExpression);
    Task<RegressionReport> DetectRegressionsAsync(string promptId, string baseVersion, string newVersion);
}

public class EvalTestSet
{
    public string Id { get; set; }
    public List<EvalCase> Cases { get; set; }
    public Dictionary<string, QualityRubric> Rubrics { get; set; }
}

public class QualityRubric
{
    public string Name { get; set; }
    public string Description { get; set; }
    public Func<string, string, Task<double>> Evaluator { get; set; }  // (query, response) -> score
}

public class EvalRunResult
{
    public string RunId { get; set; }
    public string PromptId { get; set; }
    public string Version { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, double> Metrics { get; set; }
    public List<EvalCaseResult> CaseResults { get; set; }
}
```

**Enhancements**:
- Add custom quality rubrics (accuracy, relevance, safety, tone)
- LLM-as-judge evaluations (GPT-4 rates responses)
- Statistical significance testing for A/B comparisons

#### 3.3.3 Reflection Engine (NEW)
**Location**: `Hazina.AI.Reflection`

**Responsibilities**:
- Aggregate patterns across multiple runs
- Identify failure patterns (when does this prompt fail?)
- Identify success patterns (when does this prompt excel?)
- Detect performance drift over time
- Generate improvement hypotheses

**Interfaces**:
```csharp
public interface IReflectionEngine
{
    Task<ReflectionReport> AnalyzeAsync(string promptId, DateRange dateRange);
    Task<List<FailurePattern>> FindFailurePatternsAsync(string promptId);
    Task<List<SuccessPattern>> FindSuccessPatternsAsync(string promptId);
    Task<DriftReport> DetectDriftAsync(string promptId, DateRange baseline, DateRange current);
}

public class ReflectionReport
{
    public string PromptId { get; set; }
    public int TotalRuns { get; set; }
    public double SuccessRate { get; set; }
    public List<FailurePattern> FailurePatterns { get; set; }
    public List<SuccessPattern> SuccessPatterns { get; set; }
    public List<ImprovementHypothesis> Suggestions { get; set; }
}

public class FailurePattern
{
    public string Pattern { get; set; }  // "Fails on multi-step reasoning questions"
    public double Frequency { get; set; }
    public List<string> Examples { get; set; }
    public string RootCause { get; set; }
}

public class ImprovementHypothesis
{
    public string Hypothesis { get; set; }  // "Add 'think step-by-step' instruction"
    public double ExpectedImpact { get; set; }
    public string Rationale { get; set; }
}
```

**Implementation**:
- LLM-based pattern analysis (Claude Sonnet analyzes failure logs)
- Statistical clustering of similar failures
- Temporal drift detection (performance changes over time)

#### 3.3.4 Prompt Rewriter (NEW)
**Location**: `Hazina.AI.PromptOptimization`

**Responsibilities**:
- Generate prompt improvement proposals
- Apply improvement hypotheses to templates
- Create A/B test variants
- Maintain semantic similarity (prevent drift)

**Interfaces**:
```csharp
public interface IPromptRewriter
{
    Task<PromptProposal> GenerateImprovementAsync(
        PromptTemplate current,
        ReflectionReport reflection,
        EvalRunResult latestEval);

    Task<List<PromptVariant>> GenerateABTestVariantsAsync(
        PromptTemplate baseline,
        List<ImprovementHypothesis> hypotheses);
}

public class PromptProposal
{
    public string ProposalId { get; set; }
    public PromptTemplate Current { get; set; }
    public PromptTemplate Proposed { get; set; }
    public List<Change> Changes { get; set; }
    public string Rationale { get; set; }
    public double ExpectedImprovement { get; set; }
    public ApprovalStatus Status { get; set; }  // Pending, Approved, Rejected
}

public class Change
{
    public string Type { get; set; }  // "Add", "Remove", "Modify"
    public string Section { get; set; }
    public string OldValue { get; set; }
    public string NewValue { get; set; }
    public string Reason { get; set; }
}
```

**Implementation**:
- LLM-based rewriting (GPT-4 or Claude Opus)
- Constrained generation (maintain key instructions)
- Semantic similarity check (embedding-based drift detection)
- Safety: Max edit distance threshold

#### 3.3.5 Approval Workflow (NEW)
**Location**: `Hazina.AI.Approval`

**Responsibilities**:
- Queue prompt proposals for human review
- Present changes with diff view
- Collect approval/rejection with comments
- Track approval history

**Interfaces**:
```csharp
public interface IApprovalWorkflow
{
    Task<string> SubmitForApprovalAsync(PromptProposal proposal);
    Task<List<PromptProposal>> GetPendingApprovalsAsync();
    Task ApproveAsync(string proposalId, string approver, string comments);
    Task RejectAsync(string proposalId, string approver, string reason);
}
```

**UI**:
- Web dashboard showing side-by-side diff
- Performance metrics comparison (current vs. proposed)
- Rationale explanation
- Approve/Reject buttons

#### 3.3.6 Safety Coordinator (NEW)
**Location**: `Hazina.AI.Safety`

**Responsibilities**:
- Enforce change cooldowns (max 1 change per 24h per prompt)
- Validate performance thresholds (new version must be ≥ 95% of baseline)
- Prevent semantic drift (embedding similarity ≥ 0.85)
- Emergency stop capability
- Sandbox testing before production

**Interfaces**:
```csharp
public interface ISafetyCoordinator
{
    Task<SafetyCheckResult> ValidateProposalAsync(PromptProposal proposal);
    Task<bool> CanUpdateNowAsync(string promptId);
    Task EmergencyStopAsync(string reason);
    Task<TestResult> SandboxTestAsync(PromptTemplate newVersion, EvalTestSet testSet);
}

public class SafetyCheckResult
{
    public bool Passed { get; set; }
    public List<SafetyViolation> Violations { get; set; }
}

public class SafetyViolation
{
    public string Type { get; set; }  // "Cooldown", "Threshold", "Drift"
    public string Message { get; set; }
    public double Severity { get; set; }  // 0.0 to 1.0
}
```

**Configuration**:
```csharp
public class SafetyConfig
{
    public TimeSpan MinTimeBetweenChanges { get; set; } = TimeSpan.FromHours(24);
    public double MinPerformanceRatio { get; set; } = 0.95;  // New ≥ 95% of old
    public double MinSemanticSimilarity { get; set; } = 0.85;
    public int MaxChangesPerWeek { get; set; } = 3;
    public bool RequireSandboxTesting { get; set; } = true;
}
```

### 3.4 Data Flow

**Self-Learning Loop**:

```
1. EXECUTION
   Agent uses Prompt v1.0 → Response → Logger captures everything
                                      ↓
2. FEEDBACK
   User rates response (👍/👎) → FeedbackCollector → Long-term memory
                                                    ↓
3. EVALUATION (Scheduled, e.g., daily)
   EvaluationPipeline runs test set against Prompt v1.0 → EvalRunResult
                                                          ↓
4. REFLECTION (Triggered after N runs or on schedule)
   ReflectionEngine analyzes logs + feedback + eval results → ReflectionReport
   - "Fails on multi-hop questions" (failure pattern)
   - "Suggest: Add chain-of-thought prompting" (hypothesis)
                                                          ↓
5. REWRITE
   PromptRewriter generates Prompt v1.1 proposal → PromptProposal
   - Adds "Let's think step-by-step" instruction
   - Rationale: "Should improve multi-hop reasoning"
                                                          ↓
6. SAFETY CHECKS
   SafetyCoordinator validates proposal:
   - ✅ Cooldown OK (last change was 2 days ago)
   - ✅ Semantic similarity: 0.92 (above 0.85 threshold)
   - ⚠️ Sandbox test required
                                                          ↓
7. SANDBOX TEST
   Run Prompt v1.1 on test set → EvalRunResult
   - Performance: MRR 0.78 (was 0.72) ✅ +8.3%
   - Passes threshold (≥ 95% of baseline)
                                                          ↓
8. APPROVAL (if not autonomous mode)
   PromptProposal queued for human review
   - Shows diff, rationale, sandbox results
   - Human approves → Promote to production
   - Human rejects → Log reason, back to reflection
                                                          ↓
9. DEPLOYMENT
   PromptStore saves Prompt v1.1 as new version
   - Version history maintained
   - Old version remains available (rollback ready)
                                                          ↓
10. MONITORING
   Track Prompt v1.1 performance in production
   - If regression detected → Alert + auto-rollback option
   - If successful → Baseline for next iteration
```

---

## PART 4: IMPLEMENTATION OPTIONS

### OPTION A: CONSERVATIVE (Observability Only)

#### Description
Add comprehensive observability and evaluation infrastructure **without** automated prompt modification. Focus on giving humans the data to make informed decisions.

#### What Gets Built

**Hazina Components**:
1. **Prompt Store** (`Hazina.AI.PromptManagement`)
   - External template system (Handlebars)
   - Manual versioning (humans create versions)
   - Version history tracking
   - Effectiveness metrics per version

2. **Enhanced Evaluation** (`Hazina.Evals.Pipeline`)
   - Scheduled evaluation runs
   - Ground truth test set management
   - Custom quality rubrics
   - Regression detection with alerts

3. **Reflection Dashboard** (`Hazina.Evals.Analysis`)
   - Aggregated metrics visualization
   - Failure pattern highlighting
   - Success pattern identification
   - Temporal drift charts

**SCP Components**:
- Integrate Hazina Prompt Store for policy management
- Dashboard for feedback + evaluation results
- No automated changes

#### Where It Lives
- **Hazina Core**: `src/Core/AI/Hazina.AI.PromptManagement/`
- **Hazina Core**: `src/Core/AI/Hazina.Evals.Pipeline/`
- **Hazina Tools**: `src/Tools/Services/Hazina.Tools.Services.PromptAnalytics/` (dashboard)
- **SCP**: Consumes Hazina services via DI

#### Risks
- ⚠️ **Low risk**: No automation means no unintended changes
- ⚠️ **Slow iteration**: Humans must manually update prompts
- ⚠️ **Human bottleneck**: Requires constant monitoring

#### Complexity
- **Low-Medium**: Mostly infrastructure work
  - Prompt storage: 2-3 days
  - Enhanced evaluation: 3-4 days
  - Dashboard: 4-5 days
  - **Total: ~10-12 days**

#### Reusability
- ✅ **Highly reusable**: Prompt store useful for any AI app
- ✅ **Generic evaluation**: Works with any LLM/RAG system
- ✅ **NuGet-ready**: Can be packaged independently

#### When to Use
- **Production systems** where safety is paramount
- **Regulated domains** (medical, legal, financial)
- **Early-stage products** still finding product-market fit
- **Teams without ML expertise**

---

### OPTION B: CONTROLLED SELF-LEARNING (RECOMMENDED)

#### Description
Full self-learning pipeline with **human approval required** before production deployment. System proposes improvements, humans decide.

#### What Gets Built

**Everything from Option A, plus**:

**Hazina Components**:
4. **Reflection Engine** (`Hazina.AI.Reflection`)
   - Failure pattern analysis
   - Success pattern analysis
   - Drift detection
   - Improvement hypothesis generation

5. **Prompt Rewriter** (`Hazina.AI.PromptOptimization`)
   - LLM-based prompt improvement
   - A/B test variant generation
   - Semantic similarity checks

6. **Approval Workflow** (`Hazina.AI.Approval`)
   - Proposal queue
   - Diff visualization
   - Approval/rejection tracking
   - Email/Slack notifications

7. **Safety Coordinator** (`Hazina.AI.Safety`)
   - Cooldown enforcement
   - Performance threshold validation
   - Semantic drift prevention
   - Sandbox testing orchestration

**SCP Components**:
- Policy evolution proposals (based on feedback patterns)
- Cognitive channel prompt optimization
- Admin UI for approval workflow

#### Where It Lives
- **Hazina Core**:
  - `src/Core/AI/Hazina.AI.Reflection/`
  - `src/Core/AI/Hazina.AI.PromptOptimization/`
  - `src/Core/AI/Hazina.AI.Approval/`
  - `src/Core/AI/Hazina.AI.Safety/`
- **Hazina Apps**: `apps/Web/Hazina.App.SelfLearningAdmin/` (approval UI)
- **SCP**: Uses full Hazina self-learning stack

#### Risks
- ⚠️ **Medium risk**: Requires careful safety validation
- ⚠️ **LLM cost**: Reflection + rewriting uses API calls (mitigated with haiku/3.5 for non-critical)
- ⚠️ **Human availability**: Requires timely approval
- ✅ **Rollback safety**: Can always revert to previous version

#### Complexity
- **Medium-High**: Full pipeline implementation
  - Reflection engine: 4-5 days
  - Prompt rewriter: 3-4 days
  - Approval workflow: 3-4 days
  - Safety coordinator: 3-4 days
  - Admin UI: 5-6 days
  - Integration + testing: 4-5 days
  - **Total: ~22-28 days (~5-6 weeks)**

#### Reusability
- ✅ **Highly reusable**: Generic self-learning infrastructure
- ✅ **Framework-grade**: Can be core Hazina feature
- ✅ **Configurable**: ON/OFF per application
- ✅ **NuGet packages**: Each component independently usable

#### When to Use
- **Mature products** with established baselines
- **Teams with ML expertise** to review proposals
- **High-traffic systems** where small improvements have big impact
- **SCP-like experimental platforms** pushing AI boundaries

#### Configuration Example
```json
{
  "SelfLearning": {
    "Enabled": true,
    "Mode": "HumanApproval",  // "HumanApproval" | "Autonomous"
    "Reflection": {
      "Schedule": "0 2 * * *",  // Daily at 2 AM
      "MinRunsForAnalysis": 100
    },
    "Rewriting": {
      "Provider": "anthropic",
      "Model": "claude-opus-4",
      "MaxProposalsPerWeek": 3
    },
    "Safety": {
      "MinTimeBetweenChanges": "24:00:00",
      "MinPerformanceRatio": 0.95,
      "MinSemanticSimilarity": 0.85,
      "RequireSandboxTesting": true
    },
    "Approval": {
      "NotifyVia": ["email", "slack"],
      "AutoApproveIfImprovement": false,
      "RequireMinApprovers": 1
    }
  }
}
```

---

### OPTION C: AUTONOMOUS (EXPERIMENTAL)

#### Description
Fully automated self-learning with **no human approval** required. System continuously improves itself within strict safety bounds. **Suitable only for experimental/research contexts like SCP.**

#### What Gets Built

**Everything from Option B**, with modifications:

**Changes**:
- Approval workflow becomes **optional** (can be bypassed)
- Safety coordinator becomes **stricter**:
  - Sandbox testing is **mandatory**
  - Performance threshold raised to **98%** (vs. 95%)
  - Semantic similarity threshold raised to **0.90** (vs. 0.85)
  - Cooldown extended to **48 hours** (vs. 24 hours)
  - Emergency stop on 3 consecutive regressions
- Monitoring becomes **more aggressive**:
  - Real-time regression detection
  - Automatic rollback on performance drop
  - Detailed audit logs for all changes

**New Components**:
8. **Autonomous Orchestrator** (`Hazina.AI.Autonomous`)
   - End-to-end pipeline automation
   - Health monitoring
   - Auto-rollback logic
   - Anomaly detection

#### Where It Lives
- **Hazina Core**: `src/Core/AI/Hazina.AI.Autonomous/` (orchestrator)
- **SCP**: Primary consumer (experimental cognitive platform)
- **Hazina Apps**: Monitoring dashboard (no approval UI)

#### Risks
- ⚠️ **HIGH RISK**: Automated changes can introduce bugs
- ⚠️ **Semantic drift**: Prompts may slowly change meaning
- ⚠️ **Feedback loops**: Bad change → worse performance → more bad changes
- ⚠️ **Cost**: Continuous LLM usage for reflection + rewriting
- ⚠️ **Unpredictability**: System behavior evolves without human oversight

#### Mitigation Strategies
1. **Strict safety bounds**: Higher thresholds, longer cooldowns
2. **Emergency stop**: Auto-disable after repeated failures
3. **Audit everything**: Complete change history
4. **Canary deployment**: Test on 5% of traffic before full rollout
5. **Human monitoring**: Daily review of changes (post-facto)

#### Complexity
- **High**: Requires robust monitoring + safety
  - Autonomous orchestrator: 4-5 days
  - Enhanced safety checks: 3-4 days
  - Canary deployment: 3-4 days
  - Monitoring dashboard: 4-5 days
  - Load testing: 2-3 days
  - **Total: ~16-21 days (on top of Option B)**
  - **Combined: ~38-49 days (~7-10 weeks)**

#### Reusability
- ⚠️ **Limited reusability**: Too risky for most applications
- ✅ **Research value**: Proves autonomous learning is possible
- ✅ **Opt-in feature**: Can be disabled via config
- ⚠️ **SCP-specific**: Best suited for experimental platforms

#### When to Use
- **Research/experimental platforms** (e.g., SCP)
- **Internal tools** with sophisticated users
- **Sandboxed environments** with no production impact
- **Proof-of-concept** to demonstrate autonomous learning

#### Configuration Example
```json
{
  "SelfLearning": {
    "Enabled": true,
    "Mode": "Autonomous",  // ⚠️ No human approval
    "Reflection": {
      "Schedule": "0 */6 * * *",  // Every 6 hours
      "MinRunsForAnalysis": 50
    },
    "Rewriting": {
      "Provider": "anthropic",
      "Model": "claude-opus-4",
      "MaxProposalsPerDay": 2
    },
    "Safety": {
      "MinTimeBetweenChanges": "48:00:00",  // Stricter: 48h
      "MinPerformanceRatio": 0.98,  // Stricter: 98%
      "MinSemanticSimilarity": 0.90,  // Stricter: 0.90
      "RequireSandboxTesting": true,  // Always mandatory
      "CanaryPercentage": 5,  // Test on 5% first
      "EmergencyStopAfterRegressions": 3
    },
    "Monitoring": {
      "RealtimeRegressionDetection": true,
      "AutoRollbackOnRegression": true,
      "AlertOnEveryChange": true
    }
  }
}
```

---

## PART 5: COMPARISON MATRIX

| Aspect | Option A: Conservative | Option B: Controlled | Option C: Autonomous |
|--------|------------------------|----------------------|----------------------|
| **Human Involvement** | High (manual changes) | Medium (approve proposals) | Low (monitor only) |
| **Automation** | None | Proposal generation only | Full pipeline |
| **Safety** | Highest (no automation) | High (human gate) | Medium (strict rules) |
| **Iteration Speed** | Slow | Medium | Fast |
| **LLM Cost** | Low | Medium | High |
| **Engineering Effort** | 10-12 days | 22-28 days | 38-49 days |
| **Reusability** | High | High | Medium |
| **Production Ready** | Yes | Yes (with review) | No (experimental only) |
| **Best For** | Regulated domains | Mature products | Research platforms |
| **Hazina Core?** | ✅ Yes | ✅ Yes | ⚠️ Optional feature |
| **SCP Use Case?** | Too slow | ✅ Recommended | ✅ Experimental mode |

---

## PART 6: IMPLEMENTATION PLAN

### PHASE 1: FOUNDATION (Option A) - 2-3 weeks

**Goal**: Build observability infrastructure without automation

**Sprint 1: Prompt Store (Week 1)**
1. Design schema for prompts + versions + metrics
   - `prompt_templates` table (PostgreSQL JSONB)
   - `prompt_versions` append-only log
   - `prompt_metrics` aggregated stats
2. Implement `IPromptStore` interface
3. Add Handlebars template engine integration
4. Build version history API
5. Write unit tests
6. **Deliverable**: External prompts with versioning

**Sprint 2: Enhanced Evaluation (Week 2)**
7. Extend `Hazina.Evals` with scheduling
8. Implement cron-based evaluation runner
9. Add ground truth test set management
10. Implement custom quality rubrics framework
11. Build regression detection logic
12. Write integration tests
13. **Deliverable**: Automated evaluation pipeline

**Sprint 3: Reflection Dashboard (Week 3)**
14. Design dashboard schema (metrics aggregation)
15. Implement pattern analysis queries
16. Build Grafana dashboard (or custom UI)
17. Add temporal drift visualization
18. Create email/Slack alerts for regressions
19. **Deliverable**: Observability dashboard

**Milestone**: Can observe prompt performance, identify patterns, manually iterate

---

### PHASE 2: SELF-LEARNING (Option B) - 5-6 weeks

**Goal**: Add controlled self-learning with human approval

**Sprint 4: Reflection Engine (Week 4)**
1. Design reflection analysis pipeline
2. Implement failure pattern detection (LLM-based clustering)
3. Implement success pattern detection
4. Build improvement hypothesis generator
5. Add drift detection algorithm
6. Write unit + integration tests
7. **Deliverable**: Automated reflection reports

**Sprint 5: Prompt Rewriter (Week 5)**
8. Design prompt rewriting prompt (meta-prompt!)
9. Implement LLM-based rewriter (Claude Opus)
10. Add semantic similarity checker (embeddings)
11. Implement A/B test variant generator
12. Build change diff generator
13. Write tests with real prompts
14. **Deliverable**: Automated improvement proposals

**Sprint 6: Safety Coordinator (Week 6)**
15. Implement cooldown enforcement
16. Build performance threshold validator
17. Add semantic drift detector
18. Implement sandbox testing orchestrator
19. Build emergency stop mechanism
20. Write comprehensive safety tests
21. **Deliverable**: Safety guardrails

**Sprint 7: Approval Workflow (Week 7)**
22. Design approval queue database schema
23. Implement `IApprovalWorkflow` interface
24. Build REST API for proposals
25. Add email/Slack notification system
26. Write approval history tracking
27. **Deliverable**: Backend approval system

**Sprint 8: Admin UI (Week 8-9)**
28. Design React/Blazor admin interface
29. Implement proposal list view
30. Build diff visualization component
31. Add metrics comparison charts
32. Implement approve/reject actions
33. Add rollback UI
34. Write end-to-end tests
35. **Deliverable**: Full admin interface

**Sprint 9: Integration & Testing (Week 9)**
36. Integrate all components into orchestration flow
37. End-to-end testing with real prompts
38. Load testing (simulate 1000s of runs)
39. Security audit (prompt injection risks)
40. Documentation (setup guide, architecture docs)
41. **Deliverable**: Production-ready Option B

**Milestone**: System proposes improvements, humans approve, safe deployment

---

### PHASE 3: AUTONOMOUS (Option C) - 2-3 weeks (OPTIONAL)

**Goal**: Enable fully autonomous operation for SCP experiments

**Sprint 10: Autonomous Orchestrator (Week 10)**
1. Design end-to-end pipeline orchestrator
2. Implement auto-approval logic (bypass human gate)
3. Build health monitoring system
4. Add auto-rollback on regression
5. Implement anomaly detection
6. **Deliverable**: Autonomous pipeline

**Sprint 11: Enhanced Safety (Week 11)**
7. Increase safety thresholds (98%, 0.90, 48h)
8. Implement canary deployment (5% traffic test)
9. Build real-time regression detector
10. Add emergency stop triggers
11. Comprehensive load testing
12. **Deliverable**: Production-grade autonomous system

**Sprint 12: Monitoring & Docs (Week 12)**
13. Build autonomous monitoring dashboard
14. Add detailed audit logging
15. Write operational runbooks
16. Create incident response procedures
17. Document safety mechanisms
18. **Deliverable**: Fully operational Option C

**Milestone**: SCP can autonomously improve its prompts/policies within safety bounds

---

## PART 7: TECHNICAL SPECIFICATIONS

### 7.1 Database Schema

```sql
-- Prompt Store
CREATE TABLE prompt_templates (
    prompt_id VARCHAR(255) PRIMARY KEY,
    current_version VARCHAR(64) NOT NULL,  -- SHA-256 hash
    name VARCHAR(255) NOT NULL,
    description TEXT,
    template_engine VARCHAR(50) NOT NULL,  -- "Handlebars" | "Liquid"
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NOT NULL
);

CREATE TABLE prompt_versions (
    version_id VARCHAR(64) PRIMARY KEY,  -- SHA-256 of (prompt_id + content)
    prompt_id VARCHAR(255) REFERENCES prompt_templates(prompt_id),
    version_number INT NOT NULL,  -- Sequential version number
    template TEXT NOT NULL,
    variables JSONB,
    created_at TIMESTAMP NOT NULL,
    created_by VARCHAR(255) NOT NULL,  -- "human:user@example.com" | "system:rewriter"
    reason TEXT,  -- Why was this version created?
    parent_version VARCHAR(64),  -- For tracking lineage
    status VARCHAR(50) NOT NULL DEFAULT 'active',  -- "active" | "archived" | "rolled_back"
    UNIQUE(prompt_id, version_number)
);

CREATE INDEX idx_prompt_versions_prompt_id ON prompt_versions(prompt_id);
CREATE INDEX idx_prompt_versions_created_at ON prompt_versions(created_at DESC);

-- Prompt Metrics
CREATE TABLE prompt_metrics (
    metric_id SERIAL PRIMARY KEY,
    prompt_id VARCHAR(255) REFERENCES prompt_templates(prompt_id),
    version_id VARCHAR(64) REFERENCES prompt_versions(version_id),
    timestamp TIMESTAMP NOT NULL,
    total_uses INT NOT NULL DEFAULT 0,
    success_count INT NOT NULL DEFAULT 0,
    failure_count INT NOT NULL DEFAULT 0,
    avg_user_rating FLOAT,
    avg_confidence FLOAT,
    eval_metrics JSONB,  -- { "mrr": 0.75, "ndcg": 0.82, ... }
    UNIQUE(prompt_id, version_id, DATE(timestamp))
);

CREATE INDEX idx_prompt_metrics_version ON prompt_metrics(version_id);
CREATE INDEX idx_prompt_metrics_timestamp ON prompt_metrics(timestamp DESC);

-- Evaluation Pipeline
CREATE TABLE eval_test_sets (
    test_set_id VARCHAR(255) PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    description TEXT,
    cases JSONB NOT NULL,  -- Array of { query, relevantDocuments }
    rubrics JSONB,  -- Custom quality rubrics
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NOT NULL
);

CREATE TABLE eval_runs (
    run_id VARCHAR(255) PRIMARY KEY,
    test_set_id VARCHAR(255) REFERENCES eval_test_sets(test_set_id),
    prompt_id VARCHAR(255) REFERENCES prompt_templates(prompt_id),
    version_id VARCHAR(64) REFERENCES prompt_versions(version_id),
    started_at TIMESTAMP NOT NULL,
    completed_at TIMESTAMP,
    status VARCHAR(50) NOT NULL,  -- "running" | "completed" | "failed"
    metrics JSONB,  -- Aggregate metrics
    case_results JSONB,  -- Per-case results
    errors TEXT
);

CREATE INDEX idx_eval_runs_prompt_version ON eval_runs(prompt_id, version_id);
CREATE INDEX idx_eval_runs_started_at ON eval_runs(started_at DESC);

-- Reflection Engine
CREATE TABLE reflection_reports (
    report_id VARCHAR(255) PRIMARY KEY,
    prompt_id VARCHAR(255) REFERENCES prompt_templates(prompt_id),
    start_date TIMESTAMP NOT NULL,
    end_date TIMESTAMP NOT NULL,
    total_runs INT NOT NULL,
    success_rate FLOAT NOT NULL,
    failure_patterns JSONB,  -- Array of { pattern, frequency, examples }
    success_patterns JSONB,
    improvement_hypotheses JSONB,  -- Array of { hypothesis, expectedImpact, rationale }
    created_at TIMESTAMP NOT NULL
);

CREATE INDEX idx_reflection_reports_prompt_id ON reflection_reports(prompt_id);
CREATE INDEX idx_reflection_reports_created_at ON reflection_reports(created_at DESC);

-- Prompt Rewriter
CREATE TABLE prompt_proposals (
    proposal_id VARCHAR(255) PRIMARY KEY,
    prompt_id VARCHAR(255) REFERENCES prompt_templates(prompt_id),
    current_version VARCHAR(64) REFERENCES prompt_versions(version_id),
    proposed_template TEXT NOT NULL,
    proposed_version VARCHAR(64),  -- Hash of proposed content
    changes JSONB NOT NULL,  -- Array of { type, section, oldValue, newValue, reason }
    rationale TEXT NOT NULL,
    expected_improvement FLOAT,
    status VARCHAR(50) NOT NULL DEFAULT 'pending',  -- "pending" | "approved" | "rejected" | "deployed"
    created_at TIMESTAMP NOT NULL,
    created_by VARCHAR(255) NOT NULL DEFAULT 'system:rewriter'
);

CREATE INDEX idx_prompt_proposals_prompt_id ON prompt_proposals(prompt_id);
CREATE INDEX idx_prompt_proposals_status ON prompt_proposals(status);

-- Approval Workflow
CREATE TABLE approval_actions (
    action_id SERIAL PRIMARY KEY,
    proposal_id VARCHAR(255) REFERENCES prompt_proposals(proposal_id),
    action VARCHAR(50) NOT NULL,  -- "approve" | "reject"
    approver VARCHAR(255) NOT NULL,
    comments TEXT,
    created_at TIMESTAMP NOT NULL
);

CREATE INDEX idx_approval_actions_proposal_id ON approval_actions(proposal_id);

-- Safety Coordinator
CREATE TABLE safety_checks (
    check_id SERIAL PRIMARY KEY,
    proposal_id VARCHAR(255) REFERENCES prompt_proposals(proposal_id),
    check_type VARCHAR(100) NOT NULL,  -- "cooldown" | "threshold" | "drift" | "sandbox"
    passed BOOLEAN NOT NULL,
    violations JSONB,  -- Array of { type, message, severity }
    created_at TIMESTAMP NOT NULL
);

CREATE INDEX idx_safety_checks_proposal_id ON safety_checks(proposal_id);

-- Sandbox Testing
CREATE TABLE sandbox_tests (
    test_id VARCHAR(255) PRIMARY KEY,
    proposal_id VARCHAR(255) REFERENCES prompt_proposals(proposal_id),
    test_set_id VARCHAR(255) REFERENCES eval_test_sets(test_set_id),
    started_at TIMESTAMP NOT NULL,
    completed_at TIMESTAMP,
    baseline_metrics JSONB,  -- Current version performance
    proposed_metrics JSONB,  -- New version performance
    improvement_pct FLOAT,  -- (proposed - baseline) / baseline * 100
    passed BOOLEAN,
    errors TEXT
);

CREATE INDEX idx_sandbox_tests_proposal_id ON sandbox_tests(proposal_id);
```

### 7.2 Configuration Schema

```csharp
public class SelfLearningConfig
{
    public bool Enabled { get; set; } = false;
    public SelfLearningMode Mode { get; set; } = SelfLearningMode.HumanApproval;

    public ReflectionConfig Reflection { get; set; } = new();
    public RewritingConfig Rewriting { get; set; } = new();
    public SafetyConfig Safety { get; set; } = new();
    public ApprovalConfig Approval { get; set; } = new();
    public MonitoringConfig Monitoring { get; set; } = new();
}

public enum SelfLearningMode
{
    Disabled,
    ObservabilityOnly,  // Option A
    HumanApproval,      // Option B
    Autonomous          // Option C
}

public class ReflectionConfig
{
    public string Schedule { get; set; } = "0 2 * * *";  // Cron: daily at 2 AM
    public int MinRunsForAnalysis { get; set; } = 100;
    public int LookbackDays { get; set; } = 7;
    public double MinPatternFrequency { get; set; } = 0.1;  // 10%
}

public class RewritingConfig
{
    public string Provider { get; set; } = "anthropic";
    public string Model { get; set; } = "claude-opus-4";
    public int MaxProposalsPerWeek { get; set; } = 3;
    public bool GenerateABTestVariants { get; set; } = false;
}

public class SafetyConfig
{
    public TimeSpan MinTimeBetweenChanges { get; set; } = TimeSpan.FromHours(24);
    public double MinPerformanceRatio { get; set; } = 0.95;
    public double MinSemanticSimilarity { get; set; } = 0.85;
    public int MaxChangesPerWeek { get; set; } = 3;
    public bool RequireSandboxTesting { get; set; } = true;
    public double CanaryPercentage { get; set; } = 0.0;  // 0 = disabled
    public int EmergencyStopAfterRegressions { get; set; } = 5;
}

public class ApprovalConfig
{
    public List<string> NotifyVia { get; set; } = new() { "email" };
    public bool AutoApproveIfImprovement { get; set; } = false;
    public int RequireMinApprovers { get; set; } = 1;
    public TimeSpan ApprovalTimeout { get; set; } = TimeSpan.FromDays(7);
    public string DefaultAction { get; set; } = "reject";  // "approve" | "reject"
}

public class MonitoringConfig
{
    public bool RealtimeRegressionDetection { get; set; } = true;
    public bool AutoRollbackOnRegression { get; set; } = false;
    public bool AlertOnEveryChange { get; set; } = false;
    public double RegressionThreshold { get; set; } = 0.05;  // 5% drop
}
```

### 7.3 API Design

```csharp
// Prompt Store API
[Route("api/prompts")]
public class PromptsController : ControllerBase
{
    [HttpGet]
    public Task<List<PromptTemplate>> GetAllAsync();

    [HttpGet("{promptId}")]
    public Task<PromptTemplate> GetAsync(string promptId, [FromQuery] string? version = null);

    [HttpPost]
    public Task<string> CreateAsync([FromBody] PromptTemplate template);

    [HttpPut("{promptId}")]
    public Task<string> UpdateAsync(string promptId, [FromBody] PromptTemplate template, [FromQuery] string reason);

    [HttpGet("{promptId}/versions")]
    public Task<List<PromptVersion>> GetVersionHistoryAsync(string promptId);

    [HttpPost("{promptId}/rollback")]
    public Task RollbackAsync(string promptId, [FromQuery] string targetVersion);

    [HttpGet("{promptId}/metrics")]
    public Task<PromptMetrics> GetMetricsAsync(string promptId, [FromQuery] string? version = null);
}

// Evaluation API
[Route("api/evaluations")]
public class EvaluationsController : ControllerBase
{
    [HttpPost("run")]
    public Task<string> RunEvaluationAsync([FromBody] EvalRunRequest request);

    [HttpGet("runs/{runId}")]
    public Task<EvalRunResult> GetRunResultAsync(string runId);

    [HttpPost("schedule")]
    public Task ScheduleEvaluationAsync([FromBody] EvalScheduleRequest request);

    [HttpGet("regressions")]
    public Task<List<RegressionReport>> GetRegressionsAsync([FromQuery] DateTime since);
}

// Reflection API
[Route("api/reflection")]
public class ReflectionController : ControllerBase
{
    [HttpPost("analyze")]
    public Task<string> AnalyzeAsync([FromBody] ReflectionRequest request);

    [HttpGet("reports/{reportId}")]
    public Task<ReflectionReport> GetReportAsync(string reportId);

    [HttpGet("reports")]
    public Task<List<ReflectionReport>> GetReportsAsync([FromQuery] string promptId);
}

// Proposals API
[Route("api/proposals")]
public class ProposalsController : ControllerBase
{
    [HttpGet]
    public Task<List<PromptProposal>> GetPendingAsync();

    [HttpGet("{proposalId}")]
    public Task<PromptProposal> GetAsync(string proposalId);

    [HttpPost("{proposalId}/approve")]
    public Task ApproveAsync(string proposalId, [FromBody] ApprovalRequest request);

    [HttpPost("{proposalId}/reject")]
    public Task RejectAsync(string proposalId, [FromBody] RejectionRequest request);

    [HttpGet("{proposalId}/sandbox-test")]
    public Task<TestResult> GetSandboxTestAsync(string proposalId);
}

// Safety API
[Route("api/safety")]
public class SafetyController : ControllerBase
{
    [HttpPost("check")]
    public Task<SafetyCheckResult> ValidateProposalAsync([FromBody] PromptProposal proposal);

    [HttpPost("emergency-stop")]
    public Task EmergencyStopAsync([FromBody] EmergencyStopRequest request);

    [HttpGet("status")]
    public Task<SafetyStatus> GetStatusAsync();
}
```

---

## PART 8: RISKS & MITIGATION

### 8.1 Technical Risks

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|-----------|
| **Semantic Drift** | Prompts slowly change meaning over iterations | Medium | - Embedding-based similarity checks (threshold: 0.85)<br>- Human review of proposals<br>- Rollback capability |
| **Feedback Loops** | Bad change → worse performance → more bad changes | High (Option C) | - Performance thresholds (must be ≥ 95% of baseline)<br>- Cooldown periods (24-48h)<br>- Emergency stop after N regressions<br>- Sandbox testing before production |
| **Prompt Injection** | Malicious feedback manipulates rewriter | Medium | - Input sanitization<br>- Semantic similarity check (reject if similarity < 0.85)<br>- Human review (Option B)<br>- Audit logs |
| **LLM Provider Failures** | Rewriter/evaluator LLM unavailable | Low | - Hazina's automatic failover<br>- Queue proposals for later processing<br>- Graceful degradation (skip rewriting) |
| **Cost Overruns** | Continuous reflection/rewriting expensive | Medium | - Use cheaper models (Haiku/3.5) for non-critical tasks<br>- Rate limiting (max N proposals/week)<br>- Budget alerts via Hazina |
| **Regression Detection False Positives** | System thinks performance dropped but didn't | Medium | - Statistical significance testing<br>- Multiple runs for confidence<br>- Human review before rollback |

### 8.2 Organizational Risks

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|-----------|
| **Approval Bottleneck** | Proposals sit in queue for weeks | Medium | - Approval timeout (default: 7 days → reject)<br>- Slack notifications<br>- Approval dashboard for easy review |
| **Lack of ML Expertise** | Team can't evaluate proposals effectively | Medium | - Detailed rationale with examples<br>- Side-by-side diff view<br>- Sandbox test results included<br>- Rollback always available |
| **Over-Reliance on Automation** | Team stops monitoring system | Medium | - Mandatory audit logs review<br>- Weekly summary emails<br>- Alerts on regressions |
| **Regulatory Compliance** | Automated changes violate regulations | High (regulated domains) | - Use Option A (observability only) for regulated domains<br>- Option B with mandatory human approval<br>- Complete audit trail<br>- Explainable changes |

### 8.3 Mitigation Summary

**For Option A (Conservative)**:
- ✅ Minimal risk: No automation
- ⚠️ Human bottleneck: Mitigated by good dashboards

**For Option B (Controlled)**:
- ✅ Safety: Human approval gate
- ✅ Rollback: Always available
- ⚠️ Approval delays: Mitigated by notifications + timeout

**For Option C (Autonomous)**:
- ⚠️ **High risk**: Multiple mitigation layers required
- ✅ Sandbox testing: Mandatory
- ✅ Stricter thresholds: 98% performance, 0.90 similarity
- ✅ Emergency stop: Auto-disable after 3 regressions
- ✅ Canary deployment: Test on 5% first
- ⚠️ **Only for SCP/experimental contexts**

---

## PART 9: SUCCESS METRICS

### 9.1 Option A Metrics

**Observability Goals**:
- ✅ 100% of LLM calls logged with context
- ✅ Evaluation runs daily with <5 min latency
- ✅ Failure patterns identified within 24h
- ✅ Dashboard response time <500ms
- ✅ Zero data loss (append-only logs)

**Business Impact**:
- 📊 Engineers can identify issues 10x faster
- 📊 Data-driven prompt improvements (manual)
- 📊 Reduced guesswork in prompt engineering

### 9.2 Option B Metrics

**Self-Learning Goals**:
- ✅ Proposals generated within 1 hour of reflection run
- ✅ Sandbox tests complete in <10 minutes
- ✅ Approval latency <48 hours (avg)
- ✅ Approved changes deployed within 5 minutes
- ✅ Zero unauthorized deployments
- ✅ 100% rollback success rate

**Performance Goals**:
- 🎯 Average improvement: +5% on key metrics per iteration
- 🎯 False positive rate (bad proposals): <10%
- 🎯 Regression rate: <2% (must rollback)

**Business Impact**:
- 📊 Prompt iteration speed: 10x faster (days → hours)
- 📊 Engineer time saved: 60-80% (less manual prompt tuning)
- 📊 User satisfaction: +10-15% (from better responses)
- 💰 Cost optimization: Automatic prompt efficiency improvements

### 9.3 Option C Metrics

**Autonomous Goals**:
- ✅ End-to-end pipeline latency <30 minutes
- ✅ Zero unauthorized changes (within safety bounds)
- ✅ Emergency stop triggers <1% of runs
- ✅ Canary test pass rate >90%
- ✅ Auto-rollback latency <5 minutes

**Performance Goals**:
- 🎯 Average improvement: +3% per iteration (conservative due to no human oversight)
- 🎯 Regression rate: <1% (stricter safety)
- 🎯 Semantic drift: <0.05 per month (embeddings)

**Business Impact**:
- 📊 Continuous improvement without human intervention
- 📊 SCP cognitive architecture evolves autonomously
- 📊 Research insights into self-improving AI systems
- ⚠️ **Risk**: Unpredictable system behavior (mitigated by strict safety)

---

## PART 10: RECOMMENDATIONS

### 10.1 For Hazina Framework

**Implement Option A + Option B as core features**:

1. **Phase 1 (Immediate)**: Option A - Observability
   - Prompt store with versioning
   - Enhanced evaluation pipeline
   - Reflection dashboard
   - **Target**: All AI applications benefit from better observability

2. **Phase 2 (3-6 months)**: Option B - Controlled Self-Learning
   - Reflection engine
   - Prompt rewriter
   - Approval workflow
   - Safety coordinator
   - **Target**: Production-ready self-learning for mature products

3. **Phase 3 (Optional)**: Option C - Autonomous Mode
   - Autonomous orchestrator
   - Enhanced safety
   - **Target**: Research/experimental platforms (SCP)
   - **Configuration**: OFF by default, opt-in via config

**Packaging Strategy**:
```
Hazina.AI.PromptManagement (NuGet)
├── Hazina.AI.PromptManagement.Core
├── Hazina.AI.PromptManagement.Storage.PostgreSQL
└── Hazina.AI.PromptManagement.Storage.File

Hazina.Evals.Pipeline (NuGet)
├── Hazina.Evals.Pipeline.Core
├── Hazina.Evals.Pipeline.Scheduling
└── Hazina.Evals.Pipeline.Rubrics

Hazina.AI.SelfLearning (NuGet) - Requires Option A + Option B
├── Hazina.AI.Reflection
├── Hazina.AI.PromptOptimization
├── Hazina.AI.Approval
├── Hazina.AI.Safety
└── Hazina.AI.Autonomous (optional)
```

### 10.2 For SCP Platform

**Recommended Approach**:

1. **Phase 1 (2-3 weeks)**: Integrate Hazina Option A
   - Replace hardcoded prompts with Hazina PromptStore
   - Version all cognitive channel prompts
   - Version NeurochainPolicy configurations
   - Set up evaluation pipeline for causal reasoning quality
   - Build reflection dashboard for CognitiveCoordinator performance

2. **Phase 2 (5-6 weeks)**: Enable Option B for Cognitive Channels
   - Use reflection engine to identify channel prompt weaknesses
   - Use prompt rewriter to propose improvements to causal reasoning prompts
   - Human approval workflow for critical cognitive changes
   - Sandbox test new prompts on historical queries
   - **Target**: Continuously improving cognitive architecture

3. **Phase 3 (Experimental)**: Test Option C for Non-Critical Channels
   - Enable autonomous mode for Empathy Channel (lower risk)
   - Keep Causal Reasoning + Validity on Option B (higher risk)
   - Monitor for 30 days before expanding
   - **Goal**: Prove autonomous learning in production

**SCP-Specific Enhancements**:
- **Policy Evolution**: Learn from HITL escalations to refine NeurochainPolicy
  - If many medical queries escalate → adjust sensitiveTopics threshold
  - If dark patterns frequently detected → learn new patterns
- **Channel Weighting**: Learn optimal attention weights from feedback
  - If users prefer empathetic responses → increase Empathy channel weight
  - If users demand facts → increase Validity channel weight
- **Causal Graph Improvement**: Learn better causal relationship detection
  - From user corrections: "Actually, X caused Y, not Z"
  - Improve CausalReasoningEngine prompts based on corrections

### 10.3 Timeline Recommendations

**Conservative (Low Risk)**:
- **Now → Month 3**: Option A for Hazina + SCP
- **Month 3 → Month 6**: Stabilize, gather data
- **Month 6 → Month 9**: Option B for Hazina
- **Month 9 → Month 12**: Option B for SCP
- **Month 12+**: Consider Option C for SCP only

**Aggressive (Higher Risk, Faster Innovation)**:
- **Now → Month 2**: Option A for Hazina + SCP
- **Month 2 → Month 4**: Option B for Hazina
- **Month 4 → Month 5**: Option B for SCP
- **Month 5 → Month 6**: Option C experimental for SCP
- **Month 6+**: Iterate and expand

**Recommended**: Conservative timeline for Hazina (reusable framework), Aggressive for SCP (experimental platform)

### 10.4 Resource Requirements

**Option A (Foundation)**:
- 1 Senior Backend Engineer (prompt store, evaluation)
- 1 Frontend Engineer (dashboard)
- 1 DevOps Engineer (deployment, monitoring)
- **Duration**: 2-3 weeks
- **Cost**: ~$15-20k (labor)

**Option B (Controlled Self-Learning)**:
- 1 Senior Backend Engineer (reflection, rewriter, safety)
- 1 Mid-Level Backend Engineer (approval workflow)
- 1 Frontend Engineer (admin UI)
- 1 ML Engineer (evaluation rubrics, LLM-as-judge)
- 1 DevOps Engineer (deployment, integration)
- **Duration**: 5-6 weeks
- **Cost**: ~$50-70k (labor)

**Option C (Autonomous)**:
- Same team as Option B + 1 ML Research Engineer
- **Duration**: 2-3 weeks (on top of Option B)
- **Cost**: ~$20-30k (labor)

**Total for Full Implementation (A + B + C)**:
- Team: 5-6 engineers
- Duration: 9-12 weeks
- Cost: ~$85-120k (labor)

**Ongoing Costs**:
- LLM usage (reflection + rewriting): ~$100-500/month depending on volume
- Storage (PostgreSQL): ~$50-200/month
- Monitoring (Prometheus + Grafana): ~$0-100/month (self-hosted)

---

## PART 11: CONCLUSION

### 11.1 What We Have

**Hazina Framework**:
- ✅ Excellent foundation: Multi-provider orchestration, comprehensive logging, evaluation metrics
- ✅ Production-ready infrastructure: Cost tracking, health monitoring, fault detection
- ⚠️ Missing: Prompt versioning, automated evaluation loops, reflection mechanisms

**SCP Platform**:
- ✅ Revolutionary cognitive architecture: Metacognition, multi-channel reasoning, causal analysis
- ✅ Complete feedback loop: User feedback → preference learning → profile updates
- ✅ Production integration: Hazina LLM provider with failover and cost tracking
- ⚠️ Missing: Prompt versioning, policy evolution, self-improvement loop closure

### 11.2 What We Need

To build a **true self-improving AI system**, we need:

1. **Prompt Versioning** (External templates + Git-like history)
2. **Automated Evaluation Pipeline** (Scheduled runs + regression detection)
3. **Reflection Engine** (Pattern analysis + improvement hypotheses)
4. **Prompt Rewriter** (LLM-based optimization + A/B testing)
5. **Approval Workflow** (Human gate for production safety)
6. **Safety Coordinator** (Cooldowns + thresholds + drift prevention)
7. **Admin Interface** (Inspection + approval + rollback UI)

### 11.3 How to Build It

**Recommended Path**:
1. **Start with Option A** (2-3 weeks): Build observability foundation
   - Immediately useful for both Hazina and SCP
   - Low risk, high value
   - Enables data-driven manual improvements

2. **Upgrade to Option B** (5-6 weeks): Enable controlled self-learning
   - Production-ready with human approval
   - Dramatic improvement in iteration speed
   - Safe for production use

3. **Experiment with Option C** (2-3 weeks, optional): Test autonomous mode
   - SCP-specific experimental feature
   - Proves autonomous learning feasibility
   - Valuable research insights

**Total Timeline**: 9-12 weeks for full implementation

### 11.4 Expected Impact

**For Hazina**:
- 🚀 10x faster prompt iteration (days → hours)
- 📊 Data-driven optimization (vs. guesswork)
- 💰 Cost reduction (more efficient prompts)
- 🎯 Automatic quality improvements (+5-10% per iteration)
- 🏆 Competitive advantage (self-improving framework)

**For SCP**:
- 🧠 Continuously evolving cognitive architecture
- 🎯 Automatic policy refinement from feedback
- 📈 Improving causal reasoning quality
- 🔬 Research platform for autonomous AI
- 🌟 World-class self-improving cognitive system

### 11.5 Final Recommendation

**Implement Option B (Controlled Self-Learning) for production use**, with Option C as an experimental feature for SCP.

**Rationale**:
- ✅ **Safe**: Human approval prevents unintended changes
- ✅ **Effective**: Dramatically speeds up iteration
- ✅ **Reusable**: Benefits all Hazina applications
- ✅ **Proven**: Similar to how Anthropic/OpenAI improve their models
- ✅ **Pragmatic**: Balances automation with safety

**Option C is too risky for most production systems**, but valuable for SCP as a research platform to prove autonomous learning feasibility.

---

## APPENDIX A: REFERENCES

### Concrete Code Locations

**Hazina Framework**:
- Provider Orchestration: `src\Core\AI\Hazina.AI.Providers\Core\ProviderOrchestrator.cs`
- Agent System: `src\Core\AI\Hazina.AI.Agents\Core\Agent.cs`
- LLM Logging: `src\Core\Observability\Hazina.Observability.LLMLogs\Decorators\LLMLoggingClientDecorator.cs`
- Evaluation: `src\Core\AI\Hazina.Evals\Core\EvaluationRunner.cs`
- Fault Detection: `src\Core\AI\Hazina.AI.FaultDetection\AdaptiveFaultHandler.cs`
- NeuroChain: `src\Core\AI\Hazina.Neurochain.Core\Core\NeuroChainOrchestrator.cs`
- Document Store: `src\Core\Storage\Hazina.Store.DocumentStore\Core\DocumentStore.cs`
- Memory: `src\Core\AI\Hazina.AI.Memory\Core\WorkingMemory.cs`

**SCP Platform**:
- Cognitive Coordinator: `src\Scp.Core\CognitiveCoordinator.cs` (875 lines)
- Causal Reasoning: `src\Scp.Channels\Causality\CausalReasoningEngine.cs` (603 lines)
- Feedback Learning: `src\Scp.Core\Feedback\FeedbackLearningEngine.cs` (385 lines)
- Long-Term Memory: `src\Scp.Memory\LongTermMemory\PostgresLongTermMemory.cs` (481 lines)
- Safety Pipeline: `src\Scp.Core\Pipeline\NeurochainPipeline.cs` (210 lines)
- Hazina Integration: `src\Scp.Core\Providers\HazinaLlmProvider.cs` (182 lines)

### External Resources

- **Prompt Optimization Research**:
  - DSPy: Declarative Self-improving Language Programs
  - PromptBreeder: Self-Referential Self-Improvement
- **Evaluation Frameworks**:
  - HELM (Holistic Evaluation of Language Models)
  - LangChain Evaluators
- **LLM-as-Judge**:
  - G-Eval: Evaluation using GPT-4
  - Prometheus: Open-source LLM evaluator

---

## APPENDIX B: DECISION LOG

### Why External Prompt Templates?
- **Pro**: Faster iteration (no code deployment)
- **Pro**: Non-engineers can update prompts
- **Pro**: Version control via Git
- **Con**: Adds complexity (template engine)
- **Decision**: Worth it for iteration speed

### Why Human Approval (Option B)?
- **Pro**: Safety against unintended changes
- **Pro**: Builds trust in self-learning
- **Pro**: Regulatory compliance
- **Con**: Slows down iteration
- **Decision**: Recommended for production

### Why Autonomous Mode (Option C)?
- **Pro**: Fastest iteration
- **Pro**: Research value
- **Pro**: Proves feasibility
- **Con**: High risk
- **Decision**: SCP experiments only, not Hazina default

### Why Hazina (not SCP) for Core?
- **Pro**: Reusable across all applications
- **Pro**: Framework-grade quality
- **Pro**: NuGet packaging
- **Decision**: Build in Hazina, SCP consumes

### Why PostgreSQL (not SQLite)?
- **Pro**: Production-grade
- **Pro**: Concurrent access
- **Pro**: JSONB for flexible schemas
- **Con**: Heavier setup
- **Decision**: Primary storage, with file-based fallback for dev

---

## APPENDIX C: OPEN QUESTIONS

1. **Should prompt templates support conditional logic?**
   - Example: `{{#if user.isPremium}}Show detailed answer{{else}}Show basic answer{{/if}}`
   - Recommendation: Yes, use Handlebars or Liquid for this

2. **Should we support multi-model evaluation?**
   - Example: Evaluate same prompt with GPT-4 vs Claude vs Gemini
   - Recommendation: Yes, add to `EvalRunRequest`

3. **Should we support federated learning across multiple deployments?**
   - Example: SCP instance A learns something, shares with instance B
   - Recommendation: Future work (post Option B)

4. **Should we support prompt A/B testing with automatic winner promotion?**
   - Example: Run variant A on 20% traffic, B on 20%, baseline on 60%
   - Recommendation: Yes, include in Option B

5. **Should we support cost-based rewriting?**
   - Example: "Reduce token count by 20% while maintaining quality"
   - Recommendation: Yes, add as rewriting objective

---

**End of Document**

**Next Steps**:
1. Review this plan with stakeholders
2. Decide on implementation option (A, B, or C)
3. Allocate resources (engineers, timeline, budget)
4. Begin Phase 1: Foundation (Option A)
5. Iterate based on learnings

**Document Version**: 1.0
**Last Updated**: 2026-01-06
**Authors**: Claude Sonnet 4.5 (AI Analysis) + Development Team
