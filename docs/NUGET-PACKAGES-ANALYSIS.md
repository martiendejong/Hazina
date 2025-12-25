# Hazina NuGet Packages Analysis

## Executive Summary

This document provides a comprehensive analysis of the Hazina project structure to identify all projects that are configured to build NuGet packages. The analysis is intended to support the creation of a dedicated `Hazina.NuGet.sln` solution file containing only the library projects that produce NuGet packages.

**Analysis Date:** 2025-11-10
**Current Version:** 1.1.3
**Total Projects:** 38
**NuGet Package Projects:** 17

---

## NuGet Package Projects (Identified for Hazina.NuGet.sln)

### Core Library Projects

#### 1. Hazina.LLMs.Classes
- **Path:** `LLMs\Classes\Hazina.LLMs.Classes.csproj`
- **Version:** 1.1.3
- **Description:** Core data models and contracts for the Hazina ecosystem. Provides chat message models, LLM response wrappers with token usage tracking, tool definitions, image generation models, and shared interfaces used across all Hazina packages.
- **Dependencies:**
  - System.Memory.Data (6.0.0)
- **Tags:** llm, ai, language-model, chat, openai, anthropic, claude, gpt, tokens, tool-calling

#### 2. Hazina.LLMs.Helpers
- **Path:** `LLMs\Helpers\Hazina.LLMs.Helpers.csproj`
- **Version:** 1.1.3
- **Description:** Utility functions for document and token processing. Includes TokenCounter for GPT token counting, DocumentSplitter for chunking by token limits, PartialJsonParser for streaming JSON, and helpers for checksums, file trees, and embeddings.
- **Dependencies:**
  - MathNet.Numerics (5.0.0)
  - SharpToken (2.0.3)
- **Tags:** llm, tokens, document-splitting, chunking, utilities, helpers, json-parser

#### 3. Hazina.LLMs.Client
- **Path:** `LLMs\Client\Hazina.LLMs.Client.csproj`
- **Version:** 1.1.3
- **Description:** Provider-agnostic interface for LLM interactions. Defines the ILLMClient interface with support for chat completions, streaming, structured JSON responses, image generation, text-to-speech, embeddings, and tool calling.
- **Dependencies:**
  - HtmlAgilityPack (1.12.0)
  - OpenAI (2.1.0)
  - Hazina.LLMs.Classes (project reference)
  - Hazina.LLMs.Helpers (project reference)
- **Tags:** llm, ai, interface, abstraction, chat, streaming, embeddings, tool-calling, provider-agnostic

### LLM Provider Implementations

#### 4. Hazina.LLMs.OpenAI
- **Path:** `LLMs\OpenAI\Hazina.LLMs.OpenAI.csproj`
- **Version:** 1.1.3
- **Description:** OpenAI implementation of ILLMClient for Hazina. Provides access to GPT models including chat completions, streaming, image generation (DALL-E), embeddings, and structured JSON outputs.
- **Dependencies:**
  - Microsoft.Extensions.Configuration (9.0.4)
  - Microsoft.Extensions.Configuration.Abstractions (9.0.4)
  - Microsoft.Extensions.Configuration.Binder (9.0.4)
  - Microsoft.Extensions.Configuration.Json (9.0.4)
  - Hazina.LLMs.Client (project reference)
- **Tags:** openai, gpt, chatgpt, dall-e, llm, ai, chat, embeddings, streaming, dalle

#### 5. Hazina.LLMs.Anthropic
- **Path:** `LLMs\Anthropic\Hazina.LLMs.Anthropic.csproj`
- **Version:** 1.1.3
- **Description:** Anthropic Claude implementation of ILLMClient for Hazina. Provides access to Claude models (Opus, Sonnet, Haiku) with chat completions, streaming, structured JSON outputs, and token usage tracking.
- **Dependencies:**
  - Hazina.LLMs.Client (project reference)
- **Tags:** anthropic, claude, llm, ai, chat, streaming, sonnet, opus, haiku, claude-3

#### 6. Hazina.LLMs.HuggingFace
- **Path:** `LLMs\HuggingFace\Hazina.LLMs.HuggingFace.csproj`
- **Version:** 1.1.3
- **Description:** HuggingFace implementation of ILLMClient for Hazina. Provides access to open-source models via HuggingFace Inference API including Llama, Mixtral, and others.
- **Dependencies:**
  - Hazina.LLMs.Client (project reference)
  - Hazina.LLMs.Classes (project reference)
  - Hazina.LLMs.Helpers (project reference)
- **Tags:** huggingface, llm, ai, open-source, llama, mixtral, stable-diffusion, embeddings, transformers

#### 7. Hazina.LLMs.Gemini
- **Path:** `LLMs\Gemini\Hazina.LLMs.Gemini.csproj`
- **Version:** 1.1.3
- **Description:** Google Gemini implementation of ILLMClient for Hazina. Provides access to Gemini models with chat completions, streaming, system instructions, structured JSON outputs, and token usage tracking.
- **Dependencies:**
  - Microsoft.Extensions.Configuration (9.0.4)
  - Microsoft.Extensions.Configuration.Binder (9.0.4)
  - Microsoft.Extensions.Configuration.Json (9.0.4)
  - Hazina.LLMs.Client (project reference)
  - Hazina.LLMs.Classes (project reference)
  - Hazina.LLMs.Helpers (project reference)
- **Tags:** google, gemini, llm, ai, chat, streaming, gemini-pro, google-ai

#### 8. Hazina.LLMs.Mistral
- **Path:** `LLMs\Mistral\Hazina.LLMs.Mistral.csproj`
- **Version:** 1.1.3
- **Description:** Mistral AI implementation of ILLMClient for Hazina. Provides access to Mistral language models with chat completions, streaming, token usage tracking, and structured JSON outputs.
- **Dependencies:**
  - Microsoft.Extensions.Configuration (9.0.4)
  - Microsoft.Extensions.Configuration.Binder (9.0.4)
  - Microsoft.Extensions.Configuration.Json (9.0.4)
  - Hazina.LLMs.Client (project reference)
  - Hazina.LLMs.Classes (project reference)
  - Hazina.LLMs.Helpers (project reference)
- **Tags:** mistral, llm, ai, chat, streaming, mistral-ai

#### 9. Hazina.LLMs.SemanticKernel
- **Path:** `LLMs\SemanticKernel\Hazina.LLMs.SemanticKernel.csproj`
- **Version:** 1.1.3
- **Description:** Semantic Kernel implementation of ILLMClient for Hazina. Provides multi-provider LLM support (OpenAI, Azure OpenAI, Anthropic, Ollama) through Microsoft Semantic Kernel integration.
- **Dependencies:**
  - Microsoft.SemanticKernel (1.31.0)
  - Newtonsoft.Json (13.0.3)
  - Microsoft.Extensions.Configuration (9.0.4)
  - Microsoft.Extensions.Configuration.Abstractions (9.0.4)
  - Microsoft.Extensions.Configuration.Binder (9.0.4)
  - Microsoft.Extensions.Configuration.Json (9.0.4)
  - Hazina.LLMs.Client (project reference)
  - Hazina.LLMs.Classes (project reference)
  - Hazina.LLMs.Helpers (project reference)
  - Hazina.Store.DocumentStore (project reference)
- **Tags:** semantic-kernel, llm, ai, chat, multi-provider, openai, azure, anthropic, ollama, plugins

### Tool and Extension Libraries

#### 10. Hazina.LLMs.Tools
- **Path:** `LLMs\ClientTools\Hazina.LLMs.Tools.csproj`
- **Version:** 1.1.3
- **Description:** Tool calling extensions for Hazina LLM clients. Provides reusable tools that LLMs can invoke including Claude CLI execution, web page scraping, and tool context base classes.
- **Dependencies:**
  - HtmlAgilityPack (1.12.0)
  - Hazina.LLMs.Classes (project reference)
  - Hazina.LLMs.Client (project reference)
- **Tags:** llm, tools, function-calling, web-scraping, claude-cli, ai-agents

### Storage and RAG Libraries

#### 11. Hazina.Store.EmbeddingStore
- **Path:** `Store\EmbeddingStore\Hazina.Store.EmbeddingStore.csproj`
- **Version:** 1.1.3
- **Description:** Vector embedding storage for semantic search in Hazina. Provides IEmbeddingStore interface with PostgreSQL/pgvector backend, batch operations, similarity matching, and embedding generation service.
- **Dependencies:**
  - Npgsql (8.0.3)
  - Pgvector (0.2.0)
  - Hazina.LLMs.Helpers (project reference)
  - Hazina.LLMs.Client (project reference)
- **Tags:** embeddings, vector-store, semantic-search, pgvector, postgresql, rag, similarity

#### 12. Hazina.Store.DocumentStore
- **Path:** `Store\DocumentStore\Hazina.Store.DocumentStore.csproj`
- **Version:** 1.1.3
- **Description:** Document storage and retrieval system for RAG (Retrieval-Augmented Generation) in Hazina. Provides IDocumentStore interface with support for text/binary documents, chunking, metadata management, and relevancy matching.
- **Dependencies:**
  - Npgsql (8.0.3)
  - Hazina.Store.EmbeddingStore (project reference)
  - Hazina.LLMs.Helpers (project reference)
- **Tags:** rag, document-store, retrieval, storage, chunking, embeddings, semantic-search

### High-Level Orchestration Libraries

#### 13. Hazina.Generator
- **Path:** `Hazina.Generator\Hazina.Generator.csproj`
- **Version:** 1.1.3
- **Description:** Document-augmented LLM response orchestration for Hazina. Provides IDocumentGenerator for composing RAG context with prompts, streaming responses, and safe file modification handling.
- **Dependencies:**
  - Hazina.Store.DocumentStore (project reference)
  - Hazina.LLMs.OpenAI (project reference)
- **Tags:** rag, document-generation, llm-orchestration, context-injection, streaming

#### 14. Hazina.AgentFactory
- **Path:** `Hazina.AgentFactory\Hazina.AgentFactory.csproj`
- **Version:** 1.1.3
- **Description:** High-level agent factory for building autonomous AI agents in Hazina. Provides configuration parsing, built-in tool collections (file ops, git, dotnet, npm, BigQuery, email, WordPress), and multi-agent flow orchestration.
- **Dependencies:**
  - Google.Cloud.BigQuery.V2 (3.11.0)
  - MailKit (4.13.0)
  - Hazina.LLMs.Classes (project reference)
  - Hazina.Store.DocumentStore (project reference)
  - Hazina.Store.EmbeddingStore (project reference)
  - Hazina.Generator (project reference)
  - Hazina.LLMs.Helpers (project reference)
  - Hazina.LLMs.Client (project reference)
  - Hazina.LLMs.OpenAI (project reference)
  - Hazina.LLMs.SemanticKernel (project reference)
- **Tags:** ai-agents, agent-factory, orchestration, tools, bigquery, email, wordpress, git, automation

### API and Integration Libraries

#### 15. Hazina.DynamicAPI
- **Path:** `Hazina.DynamicAPI\Hazina.DynamicAPI.csproj`
- **Version:** 1.1.3
- **Description:** Dynamic API client for Hazina that calls any HTTP API without pre-configuration. Includes credential management, automatic authentication injection, and LLM tool integration.
- **Dependencies:**
  - Newtonsoft.Json (13.0.3)
  - System.Text.Json (8.0.5)
  - Hazina.LLMs.Classes (project reference)
  - Hazina.LLMs.Client (project reference)
- **Tags:** api, http, dynamic, credentials, oauth, api-integration, rest-api, tool-calling

### UI/Shared Components

#### 16. Hazina.ChatShared
- **Path:** `App\ChatShared\Hazina.ChatShared.csproj`
- **Version:** 1.1.3
- **Target Framework:** net8.0-windows
- **Description:** Shared WPF chat UI components for Hazina applications. Provides reusable ChatWindow XAML component, IChatController interface, and chat message display models.
- **Dependencies:** (No external NuGet packages, WPF-based)
- **Tags:** wpf, chat-ui, windows, xaml, ui-components, chat-interface
- **Note:** This is a Windows-specific package (net8.0-windows)

---

## Dependency Hierarchy

The projects have the following dependency hierarchy (from lowest to highest level):

### Layer 1: Foundation
1. **Hazina.LLMs.Classes** - No project dependencies
2. **Hazina.LLMs.Helpers** - No project dependencies

### Layer 2: Core Abstractions
3. **Hazina.LLMs.Client** → Classes, Helpers

### Layer 3: Provider Implementations
4. **Hazina.LLMs.OpenAI** → Client
5. **Hazina.LLMs.Anthropic** → Client
6. **Hazina.LLMs.HuggingFace** → Client, Classes, Helpers
7. **Hazina.LLMs.Gemini** → Client, Classes, Helpers
8. **Hazina.LLMs.Mistral** → Client, Classes, Helpers

### Layer 4: Tools and Storage
9. **Hazina.LLMs.Tools** → Classes, Client
10. **Hazina.Store.EmbeddingStore** → Helpers, Client
11. **Hazina.Store.DocumentStore** → EmbeddingStore, Helpers

### Layer 5: Orchestration
12. **Hazina.Generator** → DocumentStore, OpenAI
13. **Hazina.DynamicAPI** → Classes, Client
14. **Hazina.LLMs.SemanticKernel** → Client, Classes, Helpers, DocumentStore

### Layer 6: High-Level Agents
15. **Hazina.AgentFactory** → Classes, DocumentStore, EmbeddingStore, Generator, Helpers, Client, OpenAI, SemanticKernel

### Layer 7: UI Components
16. **Hazina.ChatShared** - Independent WPF component

---

## Projects NOT Included (Application/Test Projects)

The following projects are NOT configured as NuGet packages and should NOT be included in Hazina.NuGet.sln:

### Application Projects (App folder)
- **AppBuilder** - WPF application
- **ClaudeCode** - WPF application
- **Crosslink** - Console sample application
- **EmbeddingsViewer** - WPF utility application
- **Hazina.App.ExplorerIntegration** - WPF Explorer integration utility
- **FolderToPostgres** - Console utility
- **HtmlMockupGenerator** - Utility application
- **LlamaDemo** - Demo application
- **PDFMaker** - Utility application
- **PostgresDemo** - Demo application
- **Windows** - Main WPF desktop application

### Test Projects (Tests folder)
- **Hazina.AgentFactory.Tests**
- **Hazina.DynamicAPI.Tests**
- **Hazina.Generator.Tests**
- **Hazina.LLMs.Anthropic.Tests**
- **Hazina.LLMs.Classes.Tests**
- **Hazina.LLMs.Client.Tests**
- **Hazina.LLMs.HuggingFace.Tests**
- **Hazina.LLMs.OpenAI.Tests**
- **Hazina.Store.DocumentStore.Tests**
- **Hazina.Store.EmbeddingStore.Tests**
- **Hazina.LLMs.OpenAI.IntegrationTests**

---

## Version Synchronization

All NuGet packages currently use **synchronized versioning at 1.1.3**. This is documented in the repository's NUGET-VERSIONING.md file and follows these principles:

- All Hazina.* packages share the same version number
- Version increments are synchronized across all packages
- Breaking changes trigger major version bumps across all packages
- Non-breaking feature additions trigger minor version bumps
- Bug fixes trigger patch version bumps

---

## Package Publication Status

Based on the `local_packages` directory:
- All 16 library packages have been built and packaged locally
- Package versions range from 1.0.0 to 1.1.3
- Latest version across all packages: **1.1.3**
- Packages are stored in: `C:\Projects\devgpt\local_packages\`

---

## Recommendations for Hazina.NuGet.sln

1. **Include all 16 library projects** listed above in the NuGet solution
2. **Exclude all application and test projects** to keep the solution focused
3. **Organize projects in solution folders** matching the dependency hierarchy:
   - Foundation (Classes, Helpers)
   - Core (Client)
   - Providers (OpenAI, Anthropic, HuggingFace, Gemini, Mistral, SemanticKernel)
   - Tools (ClientTools)
   - Storage (EmbeddingStore, DocumentStore)
   - Orchestration (Generator, DynamicAPI)
   - Agents (AgentFactory)
   - UI (ChatShared)

4. **Consider separate build configurations** for different package subsets:
   - Core packages only (Classes, Helpers, Client)
   - With specific providers (e.g., OpenAI-only build)
   - Full build (all packages)

5. **Maintain version synchronization** across all packages as currently implemented

---

## Technical Notes

### Target Framework
- All packages target **.NET 8.0** (net8.0)
- Exception: Hazina.ChatShared targets **net8.0-windows** (WPF dependency)

### License
- All packages use **MIT License** (PackageLicenseExpression)

### Repository Information
- Repository URL: https://github.com/prospergenics/devgpt
- Repository Type: git
- Project URL: https://github.com/prospergenics/devgpt

### Author
- Author: Prospergenics

### Build Configuration
- LanguageVersion: latest
- ImplicitUsings: enabled
- Nullable: enabled

---

## Next Steps

1. Create `Hazina.NuGet.sln` with the 16 identified library projects
2. Organize projects into logical solution folders
3. Verify all project references resolve correctly
4. Test solution build in both Debug and Release configurations
5. Consider creating automated build scripts for NuGet package generation
6. Document the package publishing workflow

---

**Document Version:** 1.0
**Last Updated:** 2025-11-10
