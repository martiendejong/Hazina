# Hazina System Snapshot

> **30-second understanding**: AI framework for .NET with multi-provider LLM, RAG, agents, and production resilience.

---

## Architecture at a Glance

```
┌─────────────────────────────────────────────────────────────────┐
│                        YOUR APPLICATION                          │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  FLUENT API                                                      │
│  Hazina.AskAsync() / AskSafeAsync() / AskForJsonAsync()         │
│  4 lines to production AI                                        │
└─────────────────────────────────────────────────────────────────┘
                              │
          ┌───────────────────┼───────────────────┐
          ▼                   ▼                   ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│   NEUROCHAIN    │ │      RAG        │ │     AGENTS      │
│  Multi-layer    │ │  Document       │ │  Tool calling   │
│  reasoning      │ │  search         │ │  Workflows      │
│  95-99% conf.   │ │  Embeddings     │ │  Multi-agent    │
└─────────────────┘ └─────────────────┘ └─────────────────┘
          │                   │                   │
          └───────────────────┼───────────────────┘
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  PROVIDERS                                                       │
│  OpenAI │ Anthropic │ Gemini │ Mistral │ HuggingFace │ Local    │
│  + Failover + Health + Cost tracking + Circuit breaker           │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  STORAGE                                                         │
│  SQLite (local) │ PostgreSQL (prod) │ Supabase (cloud) │ Files  │
└─────────────────────────────────────────────────────────────────┘
```

---

## 5 Domains (What Lives Where)

| Domain | Folder | Purpose | Entry Point |
|--------|--------|---------|-------------|
| **AI** | `src/Core/AI/` | LLM orchestration, fault detection | `ENTRYPOINT.md` |
| **RAG** | `src/Core/AI/Hazina.AI.RAG/` | Document indexing, semantic search | `ENTRYPOINT.md` |
| **Agents** | `src/Core/AI/Hazina.AI.Agents/` | Tool calling, workflows | `ENTRYPOINT.md` |
| **Neurochain** | `src/Core/AI/Hazina.Neurochain.Core/` | Multi-layer reasoning | `ENTRYPOINT.md` |
| **Storage** | `src/Core/Storage/` | Embeddings, metadata, documents | `ENTRYPOINT.md` |

---

## Critical Projects (Don't Break These)

| Project | Why Critical |
|---------|--------------|
| `Hazina.LLMs.Client` | Core interface everything depends on |
| `Hazina.AI.Providers` | All LLM calls route through here |
| `Hazina.AI.FluentAPI` | Public API surface |
| `Hazina.Store.DocumentStore` | All document storage |
| `Hazina.Store.EmbeddingStore` | All vector storage |

---

## Quick Start Paths

| You Want To... | Start Here |
|----------------|------------|
| Make an AI call | `BOOTSTRAP.md` |
| Understand everything | `docs/ARCHITECTURE.md` |
| Add a feature | `CONTRIBUTING.md` |
| Choose a solution | `WHICH_SOLUTION.md` |
| Debug a domain | `src/Core/AI/ENTRYPOINT.md` |

---

## Key Numbers

| Metric | Value |
|--------|-------|
| Total projects | ~60 |
| Solution files | 7 |
| LLM providers | 6+ |
| Storage backends | 4 |
| Test projects | 20+ |

---

## One-Liner Summary

```
Hazina = ILLMClient abstraction + ProviderOrchestrator + RAG + Agents + Neurochain + Storage
```

*That's it. Now go build something.*
