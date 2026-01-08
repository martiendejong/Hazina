# AI Domain Architecture

## Overview Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              YOUR APPLICATION                                │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                           HAZINA FLUENT API                                  │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │  Hazina.AskAsync()  │  AskSafeAsync()  │  AskForJsonAsync()         │   │
│  │  4 lines to production AI                                            │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                      │                                       │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │  QuickSetup.SetupOpenAI()  │  SetupWithFailover()  │  SetupAndConfigure()│
│  └─────────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                    ┌─────────────────┴─────────────────┐
                    ▼                                   ▼
┌───────────────────────────────┐     ┌───────────────────────────────┐
│      FAULT DETECTION          │     │        ORCHESTRATION          │
│  ┌─────────────────────────┐ │     │  ┌─────────────────────────┐  │
│  │ HallucinationDetector   │ │     │  │   ConversationContext   │  │
│  │ ResponseValidator       │ │     │  │   ContextManager        │  │
│  │ ConfidenceScorer        │ │     │  │   TaskOrchestrator      │  │
│  │ ErrorPatternRecognizer  │ │     │  └─────────────────────────┘  │
│  └─────────────────────────┘ │     │                               │
│  • 7 hallucination types     │     │  • 128K token context         │
│  • Auto-retry on low conf.   │     │  • Auto-summarization         │
│  • Ground truth validation   │     │  • Multi-turn memory          │
└───────────────────────────────┘     └───────────────────────────────┘
                    │                                   │
                    └─────────────────┬─────────────────┘
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         PROVIDER ORCHESTRATOR                                │
│  ┌──────────────────────────────────────────────────────────────────────┐  │
│  │  ProviderRegistry  │  HealthMonitor  │  CostTracker  │  BudgetManager │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────────────────────┐  │
│  │  ProviderSelector  │  FailoverHandler  │  CircuitBreaker  │  RetryPolicy│
│  └──────────────────────────────────────────────────────────────────────┘  │
│                                                                              │
│  Selection Strategies: Priority │ LeastCost │ FastestResponse │ RoundRobin  │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
            ┌─────────────┬───────────┼───────────┬─────────────┐
            ▼             ▼           ▼           ▼             ▼
      ┌─────────┐   ┌─────────┐ ┌─────────┐ ┌─────────┐   ┌─────────┐
      │ OpenAI  │   │Anthropic│ │ Gemini  │ │ Mistral │   │  Local  │
      │ GPT-4o  │   │ Claude  │ │         │ │         │   │  LLMs   │
      └─────────┘   └─────────┘ └─────────┘ └─────────┘   └─────────┘
```

## Request Flow

```
User Request
     │
     ▼
┌────────────────────┐
│   Fluent API       │ ─── Hazina.AskAsync("question")
└────────────────────┘
     │
     ▼
┌────────────────────┐
│  Fault Detection?  │ ─── If AskSafeAsync() or WithFaultDetection()
└────────────────────┘
     │ Yes
     ▼
┌────────────────────┐
│ Validation Context │ ─── Ground truth, format rules, confidence threshold
└────────────────────┘
     │
     ▼
┌────────────────────┐
│ Provider Selection │ ─── Strategy: Priority/LeastCost/FastestResponse
└────────────────────┘
     │
     ├──► Provider Healthy? ───► YES ──► Send Request
     │         │
     │        NO
     │         │
     │         ▼
     │    ┌────────────────┐
     └──► │ Failover Next  │ ─── Try next provider in priority
          └────────────────┘
                │
                ▼
          ┌────────────────┐
          │   LLM Call     │ ─── GetResponse() / GetResponseStream()
          └────────────────┘
                │
                ▼
          ┌────────────────┐
          │ Track Metrics  │ ─── Tokens, cost, latency, success/fail
          └────────────────┘
                │
                ▼
          ┌────────────────┐
          │  Validate?     │ ─── If fault detection enabled
          └────────────────┘
                │
        ┌───────┴───────┐
        │               │
       PASS            FAIL
        │               │
        ▼               ▼
    Return          Retry with
    Result          Refined Prompt
```

## Component Interactions

```
┌─────────────────────────────────────────────────────────────────┐
│                     EXTERNAL SYSTEMS                             │
├─────────────────────────────────────────────────────────────────┤
│  OpenAI API  │  Anthropic API  │  Google API  │  Local Models   │
└─────────────────────────────────────────────────────────────────┘
        ▲                ▲               ▲              ▲
        │                │               │              │
        └────────────────┴───────┬───────┴──────────────┘
                                 │
                    ┌────────────┴────────────┐
                    │    ILLMClient (6+)      │
                    │    implementations      │
                    └────────────┬────────────┘
                                 │
                    ┌────────────┴────────────┐
                    │   ProviderOrchestrator  │
                    │   (implements ILLMClient)│
                    └────────────┬────────────┘
                                 │
          ┌──────────────────────┼──────────────────────┐
          │                      │                      │
          ▼                      ▼                      ▼
┌─────────────────┐   ┌─────────────────┐   ┌─────────────────┐
│ AdaptiveFault   │   │  ContextManager │   │   Neurochain    │
│ Handler         │   │                 │   │   Orchestrator  │
└─────────────────┘   └─────────────────┘   └─────────────────┘
          │                      │                      │
          └──────────────────────┴──────────────────────┘
                                 │
                    ┌────────────┴────────────┐
                    │     HazinaBuilder       │
                    │     (Fluent API)        │
                    └────────────┬────────────┘
                                 │
                    ┌────────────┴────────────┐
                    │     Your Application    │
                    └─────────────────────────┘
```

## Key Files

| Component | File | Line |
|-----------|------|------|
| Fluent Entry | `Hazina.AI.FluentAPI/Core/Hazina.cs` | 1 |
| Builder | `Hazina.AI.FluentAPI/Core/HazinaBuilder.cs` | 1 |
| Quick Setup | `Hazina.AI.FluentAPI/Configuration/QuickSetup.cs` | 1 |
| Orchestrator | `Hazina.AI.Providers/Core/ProviderOrchestrator.cs` | 1 |
| Registry | `Hazina.AI.Providers/Core/ProviderRegistry.cs` | 1 |
| Selector | `Hazina.AI.Providers/Selection/ProviderSelector.cs` | 1 |
| Fault Handler | `Hazina.AI.FaultDetection/Core/AdaptiveFaultHandler.cs` | 1 |
| Hallucination | `Hazina.AI.FaultDetection/Detectors/BasicHallucinationDetector.cs` | 1 |
