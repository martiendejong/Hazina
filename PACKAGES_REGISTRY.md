# Hazina Package Registry
Complete overview of all Hazina packages, services, and tools.
**Total Packages:** 114
**Last Updated:** 2026-02-28

---
## Table of Contents
- [AI Core](#ai-core) (18)
- [APIs](#apis) (2)
- [Agents](#agents) (5)
- [Applications](#applications) (22)
- [Authentication](#authentication) (2)
- [Brain](#brain) (1)
- [CLI Tools](#cli-tools) (1)
- [Code Generation](#code-generation) (1)
- [Enterprise](#enterprise) (1)
- [Evaluation](#evaluation) (1)
- [LLM Providers](#llm-providers) (13)
- [Neurochain](#neurochain) (1)
- [Observability](#observability) (3)
- [Other](#other) (8)
- [Production](#production) (1)
- [Security](#security) (2)
- [Services](#services) (17)
- [Storage](#storage) (4)
- [Tools](#tools) (11)

---
## Quick Reference
| Package | Category | Description |
|---------|----------|-------------|
| `Hazina.AI.Agents` | AI Core | (No description) |
| `Hazina.AI.CognitivePipeline` | AI Core | SCP Cognitive Pipeline - Sensory/Organization/Filter/Reflective/Decision/Memory ... |
| `Hazina.AI.Compression` | AI Core | (No description) |
| `Hazina.AI.ContextEngineering` | AI Core | Context engineering layer with multi-retriever fusion for intelligent context as... |
| `Hazina.AI.Core` | AI Core | Core interfaces and models for Hazina's native AI stack. Defines abstractions fo... |
| `Hazina.AI.FaultDetection` | AI Core | (No description) |
| `Hazina.AI.FluentAPI` | AI Core | Developer-first Fluent API for Hazina AI Framework - Multi-provider LLM orchestr... |
| `Hazina.AI.Guardrails` | AI Core | (No description) |
| `Hazina.AI.Inference` | AI Core | ONNX Runtime model inference for Hazina. Run any ONNX model (V-JEPA, BERT, CLIP,... |
| `Hazina.AI.LocalLLM` | AI Core | Local LLM inference for Hazina using LLamaSharp. Run Llama, Phi, DeepSeek, Qwen,... |
| `Hazina.AI.Memory` | AI Core | Memory architecture for agents with working, episodic, and semantic memory layer... |
| `Hazina.AI.Orchestration` | AI Core | (No description) |
| `Hazina.AI.PromptManagement` | AI Core | Hazina Prompt Management System - Template versioning, evaluation, and self-lear... |
| `Hazina.AI.Providers` | AI Core | Multi-provider abstraction layer for Hazina AI Framework - Seamless LLM provider... |
| `Hazina.AI.RAG` | AI Core | Retrieval-Augmented Generation (RAG) with vector search and embeddings |
| `Hazina.AI.Training` | AI Core | Model training capabilities for Hazina using TorchSharp. Includes LoRA fine-tuni... |
| `Hazina.AI.Vision` | AI Core | Vision and video understanding pipelines for Hazina. Includes support for V-JEPA... |
| `Hazina.AI.Workflows` | AI Core | (No description) |
| `Hazina.API.Generic` | APIs | Generic Entity API framework for Hazina - Convention-over-configuration CRUD con... |
| `Hazina.API.Search` | APIs | Complete RAG Search API for Hazina - Production Ready |
| `Hazina.Agent.API` | Agents | (No description) |
| `Hazina.AgentFactory` | Agents | High-level agent factory for building autonomous AI agents in Hazina. Provides c... |
| `Hazina.AgenticOrchestration` | Agents | (No description) |
| `Hazina.Agents.Coding` | Agents | GLM-based deterministic coding agent with plan-act-observe loop |
| `Hazina.Agents.Tools` | Agents | Claude Code-style tools for file operations and command execution |
| `Hazina.App.AIImage` | Applications | (No description) |
| `Hazina.App.AppBuilder` | Applications | (No description) |
| `Hazina.App.ClaudeCode` | Applications | (No description) |
| `Hazina.App.EmbeddingsViewer` | Applications | (No description) |
| `Hazina.App.ExplorerIntegration` | Applications | (No description) |
| `Hazina.App.HazinaCoder` | Applications | HazinaCoder - Multi-provider coding assistant CLI powered by Hazina AI framework |
| `Hazina.App.HazinaCoder.Tests` | Applications | (No description) |
| `Hazina.App.HtmlMockupGenerator` | Applications | (No description) |
| `Hazina.App.Windows` | Applications | (No description) |
| `Hazina.Auth.Core` | Authentication | Core models, interfaces and DTOs for Hazina authentication system |
| `Hazina.Auth.Identity` | Authentication | ASP.NET Identity implementation for Hazina authentication system with JWT and OA... |
| `Hazina.Brain` | Brain | Persistent episodic memory and fact distillation module for Hazina agents |
| `Hazina.CLI` | CLI Tools | (No description) |
| `Hazina.ChatShared` | Other | Shared WPF chat UI components for Hazina applications. Provides reusable ChatWin... |
| `Hazina.CodeGeneration.Core` | Code Generation | (No description) |
| `Hazina.CodeIntelligence` | Other | (No description) |
| `Hazina.Core.Plugins` | Other | (No description) |
| `Hazina.Demo.AgenticOrchestration` | Applications | Desktop tray application for managing Claude Code CLI instances |
| `Hazina.Demo.ConfigurationShowcase` | Applications | (No description) |
| `Hazina.Demo.Crosslink` | Applications | (No description) |
| `Hazina.Demo.FolderToPostgres` | Applications | (No description) |
| `Hazina.Demo.GenericApi` | Applications | Demo API showing Hazina.API.Generic with document storage and semantic search |
| `Hazina.Demo.LayeredImage` | Applications | (No description) |
| `Hazina.Demo.Llama` | Applications | (No description) |
| `Hazina.Demo.PDFMaker` | Applications | (No description) |
| `Hazina.Demo.PDOK` | Applications | (No description) |
| `Hazina.Demo.Postgres` | Applications | (No description) |
| `Hazina.Demo.SmartLayeredImage` | Applications | (No description) |
| `Hazina.Demo.Supabase` | Applications | (No description) |
| `Hazina.Demo.ZeroCode` | Applications | (No description) |
| `Hazina.DynamicAPI` | Other | Dynamic API client for Hazina that calls any HTTP API without pre-configuration.... |
| `Hazina.Enterprise.Core` | Enterprise | (No description) |
| `Hazina.Evals` | Evaluation | Evaluation harness for retrieval and RAG pipeline quality assessment |
| `Hazina.Generator` | Other | Document-augmented LLM response orchestration for Hazina. Provides IDocumentGene... |
| `Hazina.IntegrationTests.OpenAI` | Other | (No description) |
| `Hazina.LLMClientTools` | Other | Tool calling extensions for Hazina LLM clients. Provides reusable tools that LLM... |
| `Hazina.LLMs.Anthropic` | LLM Providers | Anthropic Claude implementation of ILLMClient for Hazina. Provides access to Cla... |
| `Hazina.LLMs.Classes` | LLM Providers | Core data models and contracts for the Hazina ecosystem. Provides chat message m... |
| `Hazina.LLMs.Client` | LLM Providers | Provider-agnostic interface for LLM interactions. Defines the ILLMClient interfa... |
| `Hazina.LLMs.Gemini` | LLM Providers | Google Gemini implementation of ILLMClient for Hazina. Provides access to Gemini... |
| `Hazina.LLMs.GoogleADK` | LLM Providers | Google Agent Development Kit (ADK) implementation for Hazina. Provides agent arc... |
| `Hazina.LLMs.Helpers` | LLM Providers | Utility functions for document and token processing. Includes TokenCounter for G... |
| `Hazina.LLMs.HuggingFace` | LLM Providers | HuggingFace implementation of ILLMClient for Hazina. Provides access to open-sou... |
| `Hazina.LLMs.Mistral` | LLM Providers | Mistral AI implementation of ILLMClient for Hazina. Provides access to Mistral l... |
| `Hazina.LLMs.Ollama` | LLM Providers | Ollama implementation of ILLMClient for Hazina. Provides local LLM support via O... |
| `Hazina.LLMs.OpenAI` | LLM Providers | OpenAI implementation of ILLMClient for Hazina. Provides access to GPT models in... |
| `Hazina.LLMs.Registry` | LLM Providers | Configurable provider registry for LLM clients with JSON-based definitions and f... |
| `Hazina.LLMs.SemanticKernel` | LLM Providers | Semantic Kernel implementation of ILLMClient for Hazina. Provides multi-provider... |
| `Hazina.LLMs.Tools` | LLM Providers | (No description) |
| `Hazina.LongContext` | Other | Recursive long-context orchestrator for handling queries over massive context th... |
| `Hazina.Neurochain.Core` | Neurochain | (No description) |
| `Hazina.Observability.AspNetCore` | Observability | (No description) |
| `Hazina.Observability.Core` | Observability | (No description) |
| `Hazina.Observability.LLMLogs` | Observability | (No description) |
| `Hazina.Production.Monitoring` | Production | (No description) |
| `Hazina.Security.AspNetCore` | Security | (No description) |
| `Hazina.Security.Core` | Security | (No description) |
| `Hazina.Store.DocumentStore` | Storage | Document storage and retrieval system for RAG (Retrieval-Augmented Generation) i... |
| `Hazina.Store.EmbeddingStore` | Storage | Vector embedding storage for semantic search in Hazina. Provides IEmbeddingStore... |
| `Hazina.Store.FactsStore` | Storage | Facts storage for compact, relevant context facts in context engineering |
| `Hazina.Store.Sqlite` | Storage | SQLite-based storage backend for Hazina. Provides single-file database storage w... |
| `Hazina.Tools.AI.Agents` | Tools | (No description) |
| `Hazina.Tools.Common.Infrastructure.AspNetCore` | Tools | ASP.NET Core infrastructure components for Hazina tools |
| `Hazina.Tools.Common.Models` | Tools | Shared models and DTOs for Hazina tools |
| `Hazina.Tools.ContextCompression` | Tools | Context compression and optimization for LLM requests |
| `Hazina.Tools.Core` | Tools | Core functionality for Hazina generation tools |
| `Hazina.Tools.Data` | Tools | Data access layer for Hazina generation tools |
| `Hazina.Tools.Extensions` | Tools | (No description) |
| `Hazina.Tools.Migration` | Tools | Data migration tools for Hazina - migrate from file-based storage to SQLite or o... |
| `Hazina.Tools.Models` | Tools | Domain models for Hazina content generation tools |
| `Hazina.Tools.Services` | Tools | Main service orchestration for Hazina generation tools |
| `Hazina.Tools.Services.BigQuery` | Services | (No description) |
| `Hazina.Tools.Services.Chat` | Services | (No description) |
| `Hazina.Tools.Services.ContentRetrieval` | Services | (No description) |
| `Hazina.Tools.Services.DataGathering` | Services | Data gathering services for extracting and storing structured information from c... |
| `Hazina.Tools.Services.Database` | Services | (No description) |
| `Hazina.Tools.Services.Embeddings` | Services | (No description) |
| `Hazina.Tools.Services.FileOps` | Services | (No description) |
| `Hazina.Tools.Services.GoogleDrive` | Services | (No description) |
| `Hazina.Tools.Services.Images` | Services | (No description) |
| `Hazina.Tools.Services.Intake` | Services | (No description) |
| `Hazina.Tools.Services.PDOK` | Services | PDOK (Publieke Dienstverlening Op de Kaart) integration for Hazina - Access Dutc... |
| `Hazina.Tools.Services.Prompts` | Services | (No description) |
| `Hazina.Tools.Services.Social` | Services | (No description) |
| `Hazina.Tools.Services.Store` | Services | (No description) |
| `Hazina.Tools.Services.ToolAgent` | Services | (No description) |
| `Hazina.Tools.Services.Web` | Services | (No description) |
| `Hazina.Tools.Services.WordPress` | Services | (No description) |
| `Hazina.Tools.TextExtraction` | Tools | Text extraction utilities for PDF, Word, Excel and other document formats |

---
## AI Core
**Count:** 18

### `Hazina.AI.Agents`
**Description:** (No description)

**Version:** N/A

**Path:** `src\Core\AI\Hazina.AI.Agents\Hazina.AI.Agents.csproj`

**Dependencies:**
- `Hazina.AI.Providers`
- `Hazina.AI.RAG`
- `Hazina.LLMs.Classes`
- `Hazina.LLMs.Client`
- `Hazina.Neurochain.Core`

---

### `Hazina.AI.CognitivePipeline`
**Description:** SCP Cognitive Pipeline - Sensory/Organization/Filter/Reflective/Decision/Memory stages for structured AI reasoning

**Version:** N/A

**Path:** `src\Core\AI\Hazina.AI.CognitivePipeline\Hazina.AI.CognitivePipeline.csproj`

**Dependencies:**
- `Hazina.Neurochain.Core`

---

### `Hazina.AI.Compression`
**Description:** (No description)

**Version:** N/A

**Path:** `src\Core\AI\Hazina.AI.Compression\Hazina.AI.Compression.csproj`

**Dependencies:**
- `Hazina.LLMs.Classes`

**Key NuGet Packages:**
- Microsoft.Extensions.Logging.Abstractions
- SharpToken

---

### `Hazina.AI.ContextEngineering`
**Description:** Context engineering layer with multi-retriever fusion for intelligent context assembly

**Version:** N/A

**Path:** `src\Core\AI\Hazina.AI.ContextEngineering\Hazina.AI.ContextEngineering.csproj`

**Dependencies:**
- `Hazina.AI.RAG`
- `Hazina.Store.DocumentStore`
- `Hazina.Store.FactsStore`

**Key NuGet Packages:**
- Microsoft.Extensions.DependencyInjection.Abstractions
- Microsoft.Extensions.Logging.Abstractions

---

### `Hazina.AI.Core`
**Description:** Core interfaces and models for Hazina's native AI stack. Defines abstractions for model inference, training, and vision pipelines.

**Version:** 1.0.0

**Path:** `src\Core\AI\Hazina.AI.Core\Hazina.AI.Core.csproj`

**Key NuGet Packages:**
- Microsoft.Extensions.Logging.Abstractions

---

### `Hazina.AI.FaultDetection`
**Description:** (No description)

**Version:** N/A

**Path:** `src\Core\AI\Hazina.AI.FaultDetection\Hazina.AI.FaultDetection.csproj`

**Dependencies:**
- `Hazina.AI.Providers`
- `Hazina.LLMs.Classes`
- `Hazina.LLMs.Client`

---

### `Hazina.AI.FluentAPI`
**Description:** Developer-first Fluent API for Hazina AI Framework - Multi-provider LLM orchestration made simple

**Version:** 1.0.0

**Path:** `src\Core\AI\Hazina.AI.FluentAPI\Hazina.AI.FluentAPI.csproj`

**Dependencies:**
- `Hazina.AI.FaultDetection`
- `Hazina.AI.Orchestration`
- `Hazina.AI.Providers`
- `Hazina.LLMs.Anthropic`
- `Hazina.LLMs.Classes`
- `Hazina.LLMs.Client`
- `Hazina.LLMs.OpenAI`

---

### `Hazina.AI.Guardrails`
**Description:** (No description)

**Version:** N/A

**Path:** `src\Core\AI\Hazina.AI.Guardrails\Hazina.AI.Guardrails.csproj`

**Dependencies:**
- `Hazina.AgentFactory`

---

### `Hazina.AI.Inference`
**Description:** ONNX Runtime model inference for Hazina. Run any ONNX model (V-JEPA, BERT, CLIP, etc.) with CPU, CUDA, DirectML, or TensorRT acceleration.

**Version:** 1.0.0

**Path:** `src\Core\AI\Hazina.AI.Inference\Hazina.AI.Inference.csproj`

**Dependencies:**
- `Hazina.AI.Core`

**Key NuGet Packages:**
- Microsoft.Extensions.Logging.Abstractions
- Microsoft.ML.OnnxRuntime

---

### `Hazina.AI.LocalLLM`
**Description:** Local LLM inference for Hazina using LLamaSharp. Run Llama, Phi, DeepSeek, Qwen, and other GGUF models locally without API costs. Implements ILLMClient for seamless integration with existing Hazina patterns.

**Version:** 1.0.0

**Path:** `src\Core\AI\Hazina.AI.LocalLLM\Hazina.AI.LocalLLM.csproj`

**Dependencies:**
- `Hazina.AI.Core`
- `Hazina.LLMs.Classes`
- `Hazina.LLMs.Client`

**Key NuGet Packages:**
- LLamaSharp
- Microsoft.Extensions.Logging.Abstractions

---

### `Hazina.AI.Memory`
**Description:** Memory architecture for agents with working, episodic, and semantic memory layers

**Version:** N/A

**Path:** `src\Core\AI\Hazina.AI.Memory\Hazina.AI.Memory.csproj`

**Dependencies:**
- `Hazina.AI.Agents`
- `Hazina.Store.EmbeddingStore`

---

### `Hazina.AI.Orchestration`
**Description:** (No description)

**Version:** N/A

**Path:** `src\Core\AI\Hazina.AI.Orchestration\Hazina.AI.Orchestration.csproj`

**Dependencies:**
- `Hazina.AI.Compression`
- `Hazina.AI.FaultDetection`
- `Hazina.AI.Providers`
- `Hazina.LLMs.Classes`
- `Hazina.LLMs.Client`

---

### `Hazina.AI.PromptManagement`
**Description:** Hazina Prompt Management System - Template versioning, evaluation, and self-learning capabilities

**Version:** 1.0.0

**Path:** `src\Core\AI\Hazina.AI.PromptManagement\Hazina.AI.PromptManagement.csproj`

**Dependencies:**
- `Hazina.LLMs.Classes`
- `Hazina.LLMs.Client`

**Key NuGet Packages:**
- Dapper
- Handlebars.Net
- Npgsql
- System.Text.Json

---

### `Hazina.AI.Providers`
**Description:** Multi-provider abstraction layer for Hazina AI Framework - Seamless LLM provider switching with failover, health monitoring, and cost tracking

**Version:** 1.0.0

**Path:** `src\Core\AI\Hazina.AI.Providers\Hazina.AI.Providers.csproj`

**Dependencies:**
- `Hazina.LLMs.Classes`
- `Hazina.LLMs.Client`

**Key NuGet Packages:**
- Microsoft.Extensions.Logging.Abstractions
- Polly

---

### `Hazina.AI.RAG`
**Description:** Retrieval-Augmented Generation (RAG) with vector search and embeddings

**Version:** N/A

**Path:** `src\Core\AI\Hazina.AI.RAG\Hazina.AI.RAG.csproj`

**Dependencies:**
- `Hazina.AI.Providers`
- `Hazina.LLMs.Classes`
- `Hazina.LLMs.Client`
- `Hazina.Neurochain.Core`
- `Hazina.Store.DocumentStore`
- `Hazina.Store.EmbeddingStore`

**Key NuGet Packages:**
- Microsoft.Data.Sqlite
- Microsoft.Extensions.Configuration.Abstractions
- Microsoft.Extensions.Configuration.Binder

---

### `Hazina.AI.Training`
**Description:** Model training capabilities for Hazina using TorchSharp. Includes LoRA fine-tuning, training loops, and checkpoint management.

**Version:** 1.0.0

**Path:** `src\Core\AI\Hazina.AI.Training\Hazina.AI.Training.csproj`

**Dependencies:**
- `Hazina.AI.Core`

**Key NuGet Packages:**
- Microsoft.Extensions.Logging.Abstractions
- TorchSharp

---

### `Hazina.AI.Vision`
**Description:** Vision and video understanding pipelines for Hazina. Includes support for V-JEPA 2 video embeddings, image encoding, and multi-modal AI integration.

**Version:** 1.0.0

**Path:** `src\Core\AI\Hazina.AI.Vision\Hazina.AI.Vision.csproj`

**Dependencies:**
- `Hazina.AI.Core`
- `Hazina.AI.Inference`

**Key NuGet Packages:**
- FFMpegCore
- Microsoft.Extensions.Logging.Abstractions
- SixLabors.ImageSharp

---

### `Hazina.AI.Workflows`
**Description:** (No description)

**Version:** N/A

**Path:** `src\Core\AI\Hazina.AI.Workflows\Hazina.AI.Workflows.csproj`

**Dependencies:**
- `Hazina.AI.Agents`
- `Hazina.AI.Guardrails`
- `Hazina.AgentFactory`

---

## APIs
**Count:** 2

### `Hazina.API.Generic`
**Description:** Generic Entity API framework for Hazina - Convention-over-configuration CRUD controllers that eliminate boilerplate

**Version:** 1.0.0

**Path:** `src\Core\API\Hazina.API.Generic\Hazina.API.Generic.csproj`

**Dependencies:**
- `Hazina.Tools.Data`

**Key NuGet Packages:**
- Microsoft.EntityFrameworkCore
- Swashbuckle.AspNetCore
- Swashbuckle.AspNetCore.Annotations
- YamlDotNet

---

### `Hazina.API.Search`
**Description:** Complete RAG Search API for Hazina - Production Ready

**Version:** 1.0.0

**Path:** `apps\Web\Hazina.API.Search\Hazina.API.Search.csproj`

**Dependencies:**
- `Hazina.AI.Providers`
- `Hazina.AI.RAG`
- `Hazina.LLMs.Client`
- `Hazina.LLMs.OpenAI`
- `Hazina.Store.DocumentStore`
- `Hazina.Store.EmbeddingStore`

---

## Agents
**Count:** 5

### `Hazina.Agent.API`
**Description:** (No description)

**Version:** N/A

**Path:** `src\Hazina.Agent.API\Hazina.Agent.API.csproj`

**Key NuGet Packages:**
- Microsoft.AspNetCore.OpenApi
- OpenAI
- Swashbuckle.AspNetCore

---

### `Hazina.AgentFactory`
**Description:** High-level agent factory for building autonomous AI agents in Hazina. Provides configuration parsing, built-in tool collections (file ops, git, dotnet, npm, BigQuery, email, WordPress), and multi-agent flow orchestration. Simplifies creating production-ready AI agents.

**Version:** 2.0.0

**Path:** `src\Core\Agents\Hazina.AgentFactory\Hazina.AgentFactory.csproj`

**Dependencies:**
- `Hazina.Generator`
- `Hazina.LLMs.Classes`
- `Hazina.LLMs.Client`
- `Hazina.LLMs.Helpers`
- `Hazina.LLMs.OpenAI`
- `Hazina.LLMs.SemanticKernel`
- `Hazina.Store.DocumentStore`
- `Hazina.Store.EmbeddingStore`

**Key NuGet Packages:**
- Google.Cloud.BigQuery.V2
- MailKit

---

### `Hazina.AgenticOrchestration`
**Description:** (No description)

**Version:** N/A

**Path:** `src\Hazina.AgenticOrchestration\Hazina.AgenticOrchestration.csproj`

**Dependencies:**
- `Hazina.LLMs.Classes`
- `Hazina.LLMs.Client`
- `Hazina.LLMs.OpenAI`
- `Hazina.Tools.AI.Agents`
- `Hazina.Tools.Data`
- `Hazina.Tools.Models`

**Key NuGet Packages:**
- Microsoft.Data.Sqlite
- System.Data.SQLite.Core

---

### `Hazina.Agents.Coding`
**Description:** GLM-based deterministic coding agent with plan-act-observe loop

**Version:** N/A

**Path:** `src\Hazina.Agents.Coding\Hazina.Agents.Coding.csproj`

**Dependencies:**
- `Hazina.AI.Providers`

**Key NuGet Packages:**
- System.Text.Json

---

### `Hazina.Agents.Tools`
**Description:** Claude Code-style tools for file operations and command execution

**Version:** N/A

**Path:** `src\Hazina.Agents.Tools\Hazina.Agents.Tools.csproj`

**Dependencies:**
- `Hazina.LLMs.Classes`
- `Hazina.LLMs.Client`

**Key NuGet Packages:**
- Spectre.Console

---

## Applications
**Count:** 22

### `Hazina.App.AIImage`
**Description:** (No description)

**Version:** N/A

**Path:** `apps\CLI\Hazina.App.AIImage\Hazina.App.AIImage.csproj`

**Dependencies:**
- `Hazina.Tools.Models`

**Key NuGet Packages:**
- Microsoft.Extensions.Configuration.Json
- OpenAI
- Spectre.Console
- System.CommandLine

---

### `Hazina.App.AppBuilder`
**Description:** (No description)

**Version:** N/A

**Path:** `apps\Desktop\Hazina.App.AppBuilder\Hazina.App.AppBuilder.csproj`

---

### `Hazina.App.ClaudeCode`
**Description:** (No description)

**Version:** N/A

**Path:** `apps\CLI\Hazina.App.ClaudeCode\Hazina.App.ClaudeCode.csproj`

**Dependencies:**
- `Hazina.Agents.Tools`
- `Hazina.LLMs.Classes`
- `Hazina.LLMs.Client`
- `Hazina.LLMs.OpenAI`

---

### `Hazina.App.EmbeddingsViewer`
**Description:** (No description)

**Version:** N/A

**Path:** `apps\Desktop\Hazina.App.EmbeddingsViewer\Hazina.App.EmbeddingsViewer.csproj`

**Dependencies:**
- `Hazina.Store.EmbeddingStore`

**Key NuGet Packages:**
- System.Text.Json

---

### `Hazina.App.ExplorerIntegration`
**Description:** (No description)

**Version:** N/A

**Path:** `apps\Desktop\Hazina.App.ExplorerIntegration\Hazina.App.ExplorerIntegration.csproj`

**Dependencies:**
- `Hazina.AgentFactory`
- `Hazina.ChatShared`

---

### `Hazina.App.HazinaCoder`
**Description:** HazinaCoder - Multi-provider coding assistant CLI powered by Hazina AI framework

**Version:** 1.0.0

**Path:** `apps\CLI\Hazina.App.HazinaCoder\Hazina.App.HazinaCoder.csproj`

**Dependencies:**
- `Hazina.Agents.Tools`
- `Hazina.LLMs.Anthropic`
- `Hazina.LLMs.Ollama`
- `Hazina.LLMs.OpenAI`

---

### `Hazina.App.HazinaCoder.Tests`
**Description:** (No description)

**Version:** N/A

**Path:** `apps\CLI\Hazina.App.HazinaCoder.Tests\Hazina.App.HazinaCoder.Tests.csproj`

**Dependencies:**
- `Hazina.App.HazinaCoder`

**Key NuGet Packages:**
- FluentAssertions
- Microsoft.NET.Test.Sdk
- Moq
- coverlet.collector
- xunit
- xunit.runner.visualstudio

---

### `Hazina.App.HtmlMockupGenerator`
**Description:** (No description)

**Version:** N/A

**Path:** `apps\Web\Hazina.App.HtmlMockupGenerator\Hazina.App.HtmlMockupGenerator.csproj`

**Dependencies:**
- `Hazina.AgentFactory`
- `Hazina.Generator`
- `Hazina.LLMClientTools`
- `Hazina.LLMs.OpenAI`

**Key NuGet Packages:**
- Microsoft.AspNetCore.Authentication.Google
- Microsoft.EntityFrameworkCore.Design
- Microsoft.EntityFrameworkCore.Sqlite

---

### `Hazina.App.Windows`
**Description:** (No description)

**Version:** N/A

**Path:** `apps\Desktop\Hazina.App.Windows\Hazina.App.Windows.csproj`

**Dependencies:**
- `Hazina.AgentFactory`
- `Hazina.ChatShared`
- `Hazina.DynamicAPI`

**Key NuGet Packages:**
- MSTest.TestFramework
- Microsoft.Extensions.Configuration
- Microsoft.Extensions.Configuration.Binder
- Microsoft.Extensions.Configuration.Json
- Microsoft.Windows.Compatibility

---

### `Hazina.Demo.AgenticOrchestration`
**Description:** Desktop tray application for managing Claude Code CLI instances

**Version:** 1.0.0

**Path:** `apps\Demos\Hazina.Demo.AgenticOrchestration\Hazina.Demo.AgenticOrchestration.csproj`

**Dependencies:**
- `Hazina.AI.Providers`
- `Hazina.API.Generic`
- `Hazina.AgenticOrchestration`
- `Hazina.LLMs.Classes`
- `Hazina.LLMs.Client`
- `Hazina.LLMs.OpenAI`

**Key NuGet Packages:**
- Microsoft.AspNetCore.OpenApi
- Swashbuckle.AspNetCore

---

### `Hazina.Demo.ConfigurationShowcase`
**Description:** (No description)

**Version:** N/A

**Path:** `apps\Demos\Hazina.Demo.ConfigurationShowcase\Hazina.Demo.ConfigurationShowcase.csproj`

**Dependencies:**
- `Hazina.AI.Agents`
- `Hazina.AI.FluentAPI`
- `Hazina.AI.Providers`
- `Hazina.AI.RAG`
- `Hazina.LLMs.Anthropic`
- `Hazina.LLMs.Ollama`
- `Hazina.LLMs.OpenAI`
- `Hazina.Neurochain.Core`
- `Hazina.Production.Monitoring`
- `Hazina.Tools.Data`

**Key NuGet Packages:**
- Microsoft.Extensions.Configuration
- Microsoft.Extensions.Configuration.EnvironmentVariables
- Microsoft.Extensions.Configuration.Json

---

### `Hazina.Demo.Crosslink`
**Description:** (No description)

**Version:** N/A

**Path:** `apps\Demos\Hazina.Demo.Crosslink\Hazina.Demo.Crosslink.csproj`

**Dependencies:**
- `Hazina.Generator`
- `Hazina.LLMs.Classes`
- `Hazina.LLMs.Client`
- `Hazina.LLMs.Helpers`
- `Hazina.LLMs.OpenAI`
- `Hazina.Store.DocumentStore`
- `Hazina.Store.EmbeddingStore`

**Key NuGet Packages:**
- Microsoft.Extensions.Configuration
- Microsoft.Extensions.Configuration.Binder
- Microsoft.Extensions.Configuration.Json

---

### `Hazina.Demo.FolderToPostgres`
**Description:** (No description)

**Version:** N/A

**Path:** `apps\Demos\Hazina.Demo.FolderToPostgres\Hazina.Demo.FolderToPostgres.csproj`

**Dependencies:**
- `Hazina.LLMs.Client`
- `Hazina.LLMs.Helpers`
- `Hazina.LLMs.OpenAI`
- `Hazina.Store.DocumentStore`
- `Hazina.Store.EmbeddingStore`

---

### `Hazina.Demo.GenericApi`
**Description:** Demo API showing Hazina.API.Generic with document storage and semantic search

**Version:** N/A

**Path:** `apps\Demos\Hazina.Demo.GenericApi\Hazina.Demo.GenericApi.csproj`

**Dependencies:**
- `Hazina.AI.Core`
- `Hazina.API.Generic`
- `Hazina.Store.EmbeddingStore`

**Key NuGet Packages:**
- Microsoft.EntityFrameworkCore.Sqlite
- Swashbuckle.AspNetCore

---

### `Hazina.Demo.LayeredImage`
**Description:** (No description)

**Version:** N/A

**Path:** `apps\Demos\Hazina.Demo.LayeredImage\Hazina.Demo.LayeredImage.csproj`

**Dependencies:**
- `Hazina.Tools.Data`
- `Hazina.Tools.Services.Chat`
- `Hazina.Tools.Services.Images`

---

### `Hazina.Demo.Llama`
**Description:** (No description)

**Version:** N/A

**Path:** `apps\Demos\Hazina.Demo.Llama\Hazina.Demo.Llama.csproj`

**Key NuGet Packages:**
- LLamaSharp
- LLamaSharp.Backend.Cpu

---

### `Hazina.Demo.PDFMaker`
**Description:** (No description)

**Version:** N/A

**Path:** `apps\Demos\Hazina.Demo.PDFMaker\Hazina.Demo.PDFMaker.csproj`

---

### `Hazina.Demo.PDOK`
**Description:** (No description)

**Version:** N/A

**Path:** `apps\Demos\Hazina.Demo.PDOK\Hazina.Demo.PDOK.csproj`

**Dependencies:**
- `Hazina.Tools.Services.PDOK`

---

### `Hazina.Demo.Postgres`
**Description:** (No description)

**Version:** N/A

**Path:** `apps\Demos\Hazina.Demo.Postgres\Hazina.Demo.Postgres.csproj`

**Dependencies:**
- `Hazina.LLMs.Client`
- `Hazina.LLMs.Helpers`
- `Hazina.Store.DocumentStore`
- `Hazina.Store.EmbeddingStore`

---

### `Hazina.Demo.SmartLayeredImage`
**Description:** (No description)

**Version:** N/A

**Path:** `apps\Demos\Hazina.Demo.SmartLayeredImage\Hazina.Demo.SmartLayeredImage.csproj`

**Dependencies:**
- `Hazina.LLMs.OpenAI`
- `Hazina.Tools.Data`
- `Hazina.Tools.Services.Chat`
- `Hazina.Tools.Services.Images`

---

### `Hazina.Demo.Supabase`
**Description:** (No description)

**Version:** N/A

**Path:** `apps\Demos\Hazina.Demo.Supabase\Hazina.Demo.Supabase.csproj`

**Dependencies:**
- `Hazina.LLMs.Client`
- `Hazina.Store.DocumentStore`
- `Hazina.Store.EmbeddingStore`
- `Hazina.Tools.Core`
- `Hazina.Tools.Data`

---

### `Hazina.Demo.ZeroCode`
**Description:** (No description)

**Version:** N/A

**Path:** `apps\Demos\Hazina.Demo.ZeroCode\Hazina.Demo.ZeroCode.csproj`

**Dependencies:**
- `Hazina.API.Generic`
- `Hazina.LLMs.Client`
- `Hazina.LLMs.Ollama`

**Key NuGet Packages:**
- Swashbuckle.AspNetCore
- Swashbuckle.AspNetCore.Annotations

---

## Authentication
**Count:** 2

### `Hazina.Auth.Core`
**Description:** Core models, interfaces and DTOs for Hazina authentication system

**Version:** 1.0.0

**Path:** `src\Infrastructure\Auth\Hazina.Auth.Core\Hazina.Auth.Core.csproj`

**Key NuGet Packages:**
- Microsoft.AspNetCore.Identity.EntityFrameworkCore

---

### `Hazina.Auth.Identity`
**Description:** ASP.NET Identity implementation for Hazina authentication system with JWT and OAuth support

**Version:** 1.0.0

**Path:** `src\Infrastructure\Auth\Hazina.Auth.Identity\Hazina.Auth.Identity.csproj`

**Dependencies:**
- `Hazina.Auth.Core`

**Key NuGet Packages:**
- Microsoft.AspNetCore.Authentication.Google
- Microsoft.AspNetCore.Authentication.JwtBearer
- Microsoft.AspNetCore.Authentication.MicrosoftAccount
- Microsoft.AspNetCore.Identity.EntityFrameworkCore
- Microsoft.EntityFrameworkCore.Sqlite
- System.IdentityModel.Tokens.Jwt

---

## Brain
**Count:** 1

### `Hazina.Brain`
**Description:** Persistent episodic memory and fact distillation module for Hazina agents

**Version:** N/A

**Path:** `src\Core\AI\Hazina.Brain\Hazina.Brain.csproj`

**Dependencies:**
- `Hazina.AI.Memory`
- `Hazina.LLMs.Client`
- `Hazina.Store.EmbeddingStore`

**Key NuGet Packages:**
- Microsoft.EntityFrameworkCore
- Microsoft.EntityFrameworkCore.Sqlite
- Microsoft.Extensions.Hosting.Abstractions
- Microsoft.Extensions.Options.ConfigurationExtensions
- Npgsql.EntityFrameworkCore.PostgreSQL

---

## CLI Tools
**Count:** 1

### `Hazina.CLI`
**Description:** (No description)

**Version:** N/A

**Path:** `apps\CLI\Hazina.CLI\Hazina.CLI.csproj`

**Dependencies:**
- `Hazina.AI.Agents`
- `Hazina.AI.FaultDetection`
- `Hazina.AI.FluentAPI`
- `Hazina.AI.Providers`
- `Hazina.AI.RAG`
- `Hazina.LLMs.Anthropic`
- `Hazina.LLMs.Client`
- `Hazina.LLMs.OpenAI`
- `Hazina.Neurochain.Core`
- `Hazina.Store.DocumentStore`
- `Hazina.Store.EmbeddingStore`
- `Hazina.Tools.Services.Intake`
- `Hazina.Tools.TextExtraction`

**Key NuGet Packages:**
- Microsoft.Extensions.Configuration.Json
- Spectre.Console
- System.CommandLine

---

## Code Generation
**Count:** 1

### `Hazina.CodeGeneration.Core`
**Description:** (No description)

**Version:** N/A

**Path:** `src\Core\CodeGeneration\Hazina.CodeGeneration.Core\Hazina.CodeGeneration.Core.csproj`

**Key NuGet Packages:**
- Microsoft.Extensions.Logging.Abstractions

---

## Enterprise
**Count:** 1

### `Hazina.Enterprise.Core`
**Description:** (No description)

**Version:** N/A

**Path:** `src\Core\Enterprise\Hazina.Enterprise.Core\Hazina.Enterprise.Core.csproj`

**Key NuGet Packages:**
- Microsoft.Extensions.Logging.Abstractions

---

## Evaluation
**Count:** 1

### `Hazina.Evals`
**Description:** Evaluation harness for retrieval and RAG pipeline quality assessment

**Version:** N/A

**Path:** `src\Core\AI\Hazina.Evals\Hazina.Evals.csproj`

**Dependencies:**
- `Hazina.AI.RAG`

---

## LLM Providers
**Count:** 13

### `Hazina.LLMs.Anthropic`
**Description:** Anthropic Claude implementation of ILLMClient for Hazina. Provides access to Claude models (Opus, Sonnet, Haiku) with chat completions, streaming, structured JSON outputs, and token usage tracking. Supports Claude 3 and 3.5 series models via the Anthropic Messages API.

**Version:** 2.0.0

**Path:** `src\Core\LLMs.Providers\Hazina.LLMs.Anthropic\Hazina.LLMs.Anthropic.csproj`

**Dependencies:**
- `Hazina.LLMs.Client`

---

### `Hazina.LLMs.Classes`
**Description:** Core data models and contracts for the Hazina ecosystem. Provides chat message models, LLM response wrappers with token usage tracking, tool definitions, image generation models, and shared interfaces used across all Hazina packages.

**Version:** 2.0.0

**Path:** `src\Core\LLMs\Hazina.LLMs.Classes\Hazina.LLMs.Classes.csproj`

**Key NuGet Packages:**
- System.Memory.Data

---

### `Hazina.LLMs.Client`
**Description:** Provider-agnostic interface for LLM interactions. Defines the ILLMClient interface with support for chat completions, streaming, structured JSON responses, image generation, text-to-speech, embeddings, and tool calling. Enables swapping between OpenAI, Anthropic, Gemini, and other providers.

**Version:** 2.0.0

**Path:** `src\Core\LLMs\Hazina.LLMs.Client\Hazina.LLMs.Client.csproj`

**Dependencies:**
- `Hazina.LLMs.Classes`
- `Hazina.LLMs.Helpers`

**Key NuGet Packages:**
- HtmlAgilityPack
- Microsoft.Extensions.Configuration
- Microsoft.Extensions.Configuration.Binder
- Microsoft.Extensions.Configuration.Json
- Microsoft.Extensions.Logging.Abstractions
- OpenAI

---

### `Hazina.LLMs.Gemini`
**Description:** Google Gemini implementation of ILLMClient for Hazina. Provides access to Gemini models with chat completions, streaming, system instructions, structured JSON outputs, and token usage tracking. Supports Gemini Pro and other Google AI models.

**Version:** 2.0.0

**Path:** `src\Core\LLMs.Providers\Hazina.LLMs.Gemini\Hazina.LLMs.Gemini.csproj`

**Dependencies:**
- `Hazina.LLMs.Classes`
- `Hazina.LLMs.Client`
- `Hazina.LLMs.Helpers`

**Key NuGet Packages:**
- Microsoft.Extensions.Configuration
- Microsoft.Extensions.Configuration.Binder
- Microsoft.Extensions.Configuration.Json

---

### `Hazina.LLMs.GoogleADK`
**Description:** Google Agent Development Kit (ADK) implementation for Hazina. Provides agent architecture with BaseAgent, LlmAgent, workflow agents, session management, and event system aligned with Google ADK patterns.

**Version:** 1.0.0

**Path:** `src\Core\LLMs.Providers\Hazina.LLMs.GoogleADK\Hazina.LLMs.GoogleADK.csproj`

**Dependencies:**
- `Hazina.LLMs.Classes`
- `Hazina.LLMs.Client`
- `Hazina.LLMs.Gemini`
- `Hazina.LLMs.Helpers`

**Key NuGet Packages:**
- Microsoft.Extensions.Configuration
- Microsoft.Extensions.Configuration.Binder
- Microsoft.Extensions.Configuration.Json
- Microsoft.Extensions.DependencyInjection
- Microsoft.Extensions.Logging.Abstractions

---

### `Hazina.LLMs.Helpers`
**Description:** Utility functions for document and token processing. Includes TokenCounter for GPT token counting, DocumentSplitter for chunking by token limits, PartialJsonParser for streaming JSON, and helpers for checksums, file trees, and embeddings.

**Version:** 2.0.0

**Path:** `src\Core\LLMs\Hazina.LLMs.Helpers\Hazina.LLMs.Helpers.csproj`

**Key NuGet Packages:**
- MathNet.Numerics
- SharpToken

---

### `Hazina.LLMs.HuggingFace`
**Description:** HuggingFace implementation of ILLMClient for Hazina. Provides access to open-source models via HuggingFace Inference API including Llama, Mixtral, and others. Supports chat completions, embeddings (sentence transformers), and image generation (Stable Diffusion).

**Version:** 2.0.0

**Path:** `src\Core\LLMs.Providers\Hazina.LLMs.HuggingFace\Hazina.LLMs.HuggingFace.csproj`

**Dependencies:**
- `Hazina.LLMs.Classes`
- `Hazina.LLMs.Client`
- `Hazina.LLMs.Helpers`

---

### `Hazina.LLMs.Mistral`
**Description:** Mistral AI implementation of ILLMClient for Hazina. Provides access to Mistral language models with chat completions, streaming, token usage tracking, and structured JSON outputs. Supports Mistral's latest models via their API.

**Version:** 2.0.0

**Path:** `src\Core\LLMs.Providers\Hazina.LLMs.Mistral\Hazina.LLMs.Mistral.csproj`

**Dependencies:**
- `Hazina.LLMs.Classes`
- `Hazina.LLMs.Client`
- `Hazina.LLMs.Helpers`

**Key NuGet Packages:**
- Microsoft.Extensions.Configuration
- Microsoft.Extensions.Configuration.Binder
- Microsoft.Extensions.Configuration.Json

---

### `Hazina.LLMs.Ollama`
**Description:** Ollama implementation of ILLMClient for Hazina. Provides local LLM support via Ollama API with chat completions, streaming, and embeddings. Enables running LLMs locally without cloud API dependencies.

**Version:** 1.0.0

**Path:** `src\Core\LLMs.Providers\Hazina.LLMs.Ollama\Hazina.LLMs.Ollama.csproj`

**Dependencies:**
- `Hazina.LLMs.Client`

**Key NuGet Packages:**
- Microsoft.Extensions.Configuration
- Microsoft.Extensions.Configuration.Abstractions
- Microsoft.Extensions.Configuration.Binder
- Microsoft.Extensions.Configuration.Json

---

### `Hazina.LLMs.OpenAI`
**Description:** OpenAI implementation of ILLMClient for Hazina. Provides access to GPT models including chat completions, streaming, image generation (DALL-E), embeddings, and structured JSON outputs. Includes token cost calculation and configuration management for OpenAI's API.

**Version:** 2.0.0

**Path:** `src\Core\LLMs.Providers\Hazina.LLMs.OpenAI\Hazina.LLMs.OpenAI.csproj`

**Dependencies:**
- `Hazina.LLMs.Client`

**Key NuGet Packages:**
- Microsoft.Extensions.Configuration
- Microsoft.Extensions.Configuration.Abstractions
- Microsoft.Extensions.Configuration.Binder
- Microsoft.Extensions.Configuration.Json

---

### `Hazina.LLMs.Registry`
**Description:** Configurable provider registry for LLM clients with JSON-based definitions and factory pattern

**Version:** N/A

**Path:** `src\Core\LLMs\Hazina.LLMs.Registry\Hazina.LLMs.Registry.csproj`

**Dependencies:**
- `Hazina.LLMs.Client`

**Key NuGet Packages:**
- Microsoft.Extensions.Configuration
- Microsoft.Extensions.Configuration.Binder
- Microsoft.Extensions.Configuration.Json
- Microsoft.Extensions.DependencyInjection.Abstractions

---

### `Hazina.LLMs.SemanticKernel`
**Description:** Semantic Kernel implementation of ILLMClient for Hazina. Provides multi-provider LLM support (OpenAI, Azure OpenAI, Anthropic, Ollama) through Microsoft Semantic Kernel integration. Supports advanced orchestration, plugins, and planners while maintaining Hazina's RAG and safe file modification features.

**Version:** 2.0.0

**Path:** `src\Core\LLMs.Providers\Hazina.LLMs.SemanticKernel\Hazina.LLMs.SemanticKernel.csproj`

**Dependencies:**
- `Hazina.LLMs.Classes`
- `Hazina.LLMs.Client`
- `Hazina.LLMs.Helpers`
- `Hazina.Store.DocumentStore`

**Key NuGet Packages:**
- Microsoft.Extensions.Configuration
- Microsoft.Extensions.Configuration.Abstractions
- Microsoft.Extensions.Configuration.Binder
- Microsoft.Extensions.Configuration.Json
- Microsoft.SemanticKernel
- Newtonsoft.Json
- OpenAI

---

### `Hazina.LLMs.Tools`
**Description:** (No description)

**Version:** N/A

**Path:** `src\Core\LLMs\Hazina.LLMs.Tools\Hazina.LLMs.Tools.csproj`

---

## Neurochain
**Count:** 1

### `Hazina.Neurochain.Core`
**Description:** (No description)

**Version:** N/A

**Path:** `src\Core\AI\Hazina.Neurochain.Core\Hazina.Neurochain.Core.csproj`

**Dependencies:**
- `Hazina.AI.FaultDetection`
- `Hazina.AI.Orchestration`
- `Hazina.AI.Providers`
- `Hazina.LLMs.Classes`
- `Hazina.LLMs.Client`

---

## Observability
**Count:** 3

### `Hazina.Observability.AspNetCore`
**Description:** (No description)

**Version:** N/A

**Path:** `src\Core\Observability\Hazina.Observability.AspNetCore\Hazina.Observability.AspNetCore.csproj`

**Dependencies:**
- `Hazina.Observability.Core`

**Key NuGet Packages:**
- Microsoft.AspNetCore.Http.Abstractions
- Microsoft.AspNetCore.Routing
- OpenTelemetry.Exporter.Console
- OpenTelemetry.Extensions.Hosting
- prometheus-net.AspNetCore

---

### `Hazina.Observability.Core`
**Description:** (No description)

**Version:** N/A

**Path:** `src\Core\Observability\Hazina.Observability.Core\Hazina.Observability.Core.csproj`

**Dependencies:**
- `Hazina.AI.Providers`
- `Hazina.Neurochain.Core`

---

### `Hazina.Observability.LLMLogs`
**Description:** (No description)

**Version:** N/A

**Path:** `src\Core\Observability\Hazina.Observability.LLMLogs\Hazina.Observability.LLMLogs.csproj`

**Dependencies:**
- `Hazina.LLMs.Classes`
- `Hazina.LLMs.Client`

**Key NuGet Packages:**
- Microsoft.Data.Sqlite
- Microsoft.Extensions.Configuration.Abstractions
- Microsoft.Extensions.DependencyInjection
- Microsoft.Extensions.DependencyInjection.Abstractions
- Microsoft.Extensions.Hosting.Abstractions
- Microsoft.Extensions.Options
- Microsoft.Extensions.Options.ConfigurationExtensions
- Scrutor

---

## Other
**Count:** 8

### `Hazina.ChatShared`
**Description:** Shared WPF chat UI components for Hazina applications. Provides reusable ChatWindow XAML component, IChatController interface, and chat message display models. Enables consistent chat experience across multiple Hazina Windows applications.

**Version:** 2.0.0

**Path:** `src\Core\UI\Hazina.ChatShared\Hazina.ChatShared.csproj`

---

### `Hazina.CodeIntelligence`
**Description:** (No description)

**Version:** N/A

**Path:** `src\Core\AI\Hazina.CodeIntelligence\Hazina.CodeIntelligence.csproj`

**Dependencies:**
- `Hazina.AI.Providers`
- `Hazina.LLMs.Classes`
- `Hazina.LLMs.Client`
- `Hazina.Neurochain.Core`

---

### `Hazina.Core.Plugins`
**Description:** (No description)

**Version:** N/A

**Path:** `src\Core\Plugins\Hazina.Core.Plugins\Hazina.Core.Plugins.csproj`

**Dependencies:**
- `Hazina.AI.Agents`

**Key NuGet Packages:**
- Microsoft.CodeAnalysis.CSharp.Scripting
- Microsoft.Extensions.DependencyInjection.Abstractions
- Microsoft.Extensions.Logging.Abstractions

---

### `Hazina.DynamicAPI`
**Description:** Dynamic API client for Hazina that calls any HTTP API without pre-configuration. Includes credential management, automatic authentication injection, and LLM tool integration. Enables AI agents to interact with external APIs on-the-fly with support for API keys, OAuth, and custom headers.

**Version:** 2.0.0

**Path:** `src\Core\Agents\Hazina.DynamicAPI\Hazina.DynamicAPI.csproj`

**Dependencies:**
- `Hazina.LLMs.Classes`
- `Hazina.LLMs.Client`

**Key NuGet Packages:**
- Newtonsoft.Json
- System.Text.Json

---

### `Hazina.Generator`
**Description:** Document-augmented LLM response orchestration for Hazina. Provides IDocumentGenerator for composing RAG context with prompts, streaming responses, and safe file modification handling. Core component for context-aware AI applications.

**Version:** 2.0.0

**Path:** `src\Core\Agents\Hazina.Generator\Hazina.Generator.csproj`

**Dependencies:**
- `Hazina.LLMs.OpenAI`
- `Hazina.Store.DocumentStore`

---

### `Hazina.IntegrationTests.OpenAI`
**Description:** (No description)

**Version:** N/A

**Path:** `apps\Testing\Hazina.IntegrationTests.OpenAI\Hazina.IntegrationTests.OpenAI.csproj`

**Dependencies:**
- `Hazina.LLMs.Classes`
- `Hazina.LLMs.OpenAI`

---

### `Hazina.LLMClientTools`
**Description:** Tool calling extensions for Hazina LLM clients. Provides reusable tools that LLMs can invoke including Claude CLI execution, web page scraping, and tool context base classes. Enables function calling and external system integration for AI agents.

**Version:** 2.0.0

**Path:** `src\Core\LLMs\Hazina.LLMClientTools\Hazina.LLMClientTools.csproj`

**Dependencies:**
- `Hazina.LLMs.Classes`
- `Hazina.LLMs.Client`

**Key NuGet Packages:**
- HtmlAgilityPack

---

### `Hazina.LongContext`
**Description:** Recursive long-context orchestrator for handling queries over massive context through query decomposition and hierarchical retrieval

**Version:** N/A

**Path:** `src\Core\AI\Hazina.LongContext\Hazina.LongContext.csproj`

**Dependencies:**
- `Hazina.AI.Compression`
- `Hazina.AI.ContextEngineering`
- `Hazina.AI.Orchestration`
- `Hazina.AI.Providers`
- `Hazina.AI.RAG`
- `Hazina.LLMs.Client`
- `Hazina.Store.EmbeddingStore`

**Key NuGet Packages:**
- Microsoft.Extensions.DependencyInjection.Abstractions
- Microsoft.Extensions.Options
- Microsoft.Extensions.Options.ConfigurationExtensions

---

## Production
**Count:** 1

### `Hazina.Production.Monitoring`
**Description:** (No description)

**Version:** N/A

**Path:** `src\Tools\Production\Hazina.Production.Monitoring\Hazina.Production.Monitoring.csproj`

**Dependencies:**
- `Hazina.AI.Providers`
- `Hazina.Neurochain.Core`

**Key NuGet Packages:**
- System.Diagnostics.PerformanceCounter

---

## Security
**Count:** 2

### `Hazina.Security.AspNetCore`
**Description:** (No description)

**Version:** N/A

**Path:** `src\Core\Security\Hazina.Security.AspNetCore\Hazina.Security.AspNetCore.csproj`

**Dependencies:**
- `Hazina.Security.Core`

**Key NuGet Packages:**
- Microsoft.Extensions.Logging.Abstractions

---

### `Hazina.Security.Core`
**Description:** (No description)

**Version:** N/A

**Path:** `src\Core\Security\Hazina.Security.Core\Hazina.Security.Core.csproj`

**Key NuGet Packages:**
- Microsoft.AspNetCore.DataProtection
- Microsoft.AspNetCore.DataProtection.Extensions
- Microsoft.Extensions.Configuration.Abstractions
- Microsoft.Extensions.DependencyInjection.Abstractions
- Microsoft.Extensions.Logging.Abstractions

---

## Services
**Count:** 17

### `Hazina.Tools.Services.BigQuery`
**Description:** (No description)

**Version:** 1.0.17

**Path:** `src\Tools\Services\Hazina.Tools.Services.BigQuery\Hazina.Tools.Services.BigQuery.csproj`

**Dependencies:**
- `Hazina.Tools.Data`
- `Hazina.Tools.Models`
- `Hazina.Tools.Services.FileOps`

**Key NuGet Packages:**
- Google.Cloud.BigQuery.V2

---

### `Hazina.Tools.Services.Chat`
**Description:** (No description)

**Version:** 1.0.17

**Path:** `src\Tools\Services\Hazina.Tools.Services.Chat\Hazina.Tools.Services.Chat.csproj`

**Dependencies:**
- `Hazina.Observability.LLMLogs`
- `Hazina.Tools.AI.Agents`
- `Hazina.Tools.Core`
- `Hazina.Tools.Data`
- `Hazina.Tools.Models`
- `Hazina.Tools.Services.DataGathering`

**Key NuGet Packages:**
- Mscc.GenerativeAI
- SixLabors.ImageSharp

---

### `Hazina.Tools.Services.ContentRetrieval`
**Description:** (No description)

**Version:** 1.0.17

**Path:** `src\Tools\Services\Hazina.Tools.Services.ContentRetrieval\Hazina.Tools.Services.ContentRetrieval.csproj`

**Dependencies:**
- `Hazina.Tools.Common.Models`
- `Hazina.Tools.Core`
- `Hazina.Tools.Data`
- `Hazina.Tools.Models`
- `Hazina.Tools.Services.FileOps`
- `Hazina.Tools.Services.WordPress`

---

### `Hazina.Tools.Services.DataGathering`
**Description:** Data gathering services for extracting and storing structured information from chat conversations.

**Version:** 1.0.17

**Path:** `src\Tools\Services\Hazina.Tools.Services.DataGathering\Hazina.Tools.Services.DataGathering.csproj`

**Dependencies:**
- `Hazina.LLMClientTools`
- `Hazina.Tools.AI.Agents`
- `Hazina.Tools.Core`
- `Hazina.Tools.Data`
- `Hazina.Tools.Extensions`
- `Hazina.Tools.Models`
- `Hazina.Tools.Services.Store`

**Key NuGet Packages:**
- Microsoft.Extensions.Configuration.Abstractions
- Microsoft.Extensions.Logging.Abstractions
- System.Text.Json

---

### `Hazina.Tools.Services.Database`
**Description:** (No description)

**Version:** 1.0.0

**Path:** `src\Tools\Services\Hazina.Tools.Services.Database\Hazina.Tools.Services.Database.csproj`

**Dependencies:**
- `Hazina.Tools.AI.Agents`
- `Hazina.Tools.Core`
- `Hazina.Tools.Data`
- `Hazina.Tools.Models`
- `Hazina.Tools.Services.Chat`
- `Hazina.Tools.Services.Embeddings`

**Key NuGet Packages:**
- Microsoft.Data.Sqlite

---

### `Hazina.Tools.Services.Embeddings`
**Description:** (No description)

**Version:** 2.0.0

**Path:** `src\Tools\Services\Hazina.Tools.Services.Embeddings\Hazina.Tools.Services.Embeddings.csproj`

**Dependencies:**
- `Hazina.Tools.Core`
- `Hazina.Tools.Data`
- `Hazina.Tools.Models`
- `Hazina.Tools.Services.FileOps`
- `Hazina.Tools.Services.Store`

**Key NuGet Packages:**
- OpenAI

---

### `Hazina.Tools.Services.FileOps`
**Description:** (No description)

**Version:** 2.0.0

**Path:** `src\Tools\Services\Hazina.Tools.Services.FileOps\Hazina.Tools.Services.FileOps.csproj`

**Dependencies:**
- `Hazina.Tools.Core`
- `Hazina.Tools.Data`
- `Hazina.Tools.Models`
- `Hazina.Tools.TextExtraction`

**Key NuGet Packages:**
- Serilog
- Serilog.Sinks.File

---

### `Hazina.Tools.Services.GoogleDrive`
**Description:** (No description)

**Version:** 1.0.0

**Path:** `src\Tools\Services\Hazina.Tools.Services.GoogleDrive\Hazina.Tools.Services.GoogleDrive.csproj`

**Dependencies:**
- `Hazina.Tools.Data`
- `Hazina.Tools.Models`
- `Hazina.Tools.Services.Embeddings`

**Key NuGet Packages:**
- Google.Apis.Auth
- Google.Apis.Drive.v3
- Microsoft.Data.Sqlite
- Microsoft.EntityFrameworkCore.Sqlite

---

### `Hazina.Tools.Services.Images`
**Description:** (No description)

**Version:** 2.0.0

**Path:** `src\Tools\Services\Hazina.Tools.Services.Images\Hazina.Tools.Services.Images.csproj`

**Dependencies:**
- `Hazina.Tools.Core`
- `Hazina.Tools.Data`
- `Hazina.Tools.Models`
- `Hazina.Tools.Services.Chat`
- `Hazina.Tools.Services.FileOps`

**Key NuGet Packages:**
- OpenAI
- SixLabors.ImageSharp
- SixLabors.ImageSharp.Drawing

---

### `Hazina.Tools.Services.Intake`
**Description:** (No description)

**Version:** 1.0.17

**Path:** `src\Tools\Services\Hazina.Tools.Services.Intake\Hazina.Tools.Services.Intake.csproj`

**Dependencies:**
- `Hazina.AgentFactory`
- `Hazina.Generator`
- `Hazina.LLMClientTools`
- `Hazina.LLMs.OpenAI`
- `Hazina.Tools.Core`
- `Hazina.Tools.Data`
- `Hazina.Tools.Models`
- `Hazina.Tools.Services`

**Key NuGet Packages:**
- OpenAI

---

### `Hazina.Tools.Services.PDOK`
**Description:** PDOK (Publieke Dienstverlening Op de Kaart) integration for Hazina - Access Dutch Kadaster open data including BAG, BRK, BGT, and geocoding services

**Version:** 1.0.0

**Path:** `src\Tools\Services\Hazina.Tools.Services.PDOK\Hazina.Tools.Services.PDOK.csproj`

**Dependencies:**
- `Hazina.Tools.Core`
- `Hazina.Tools.Models`

**Key NuGet Packages:**
- NetTopologySuite
- NetTopologySuite.IO.GeoJSON
- System.Text.Json

---

### `Hazina.Tools.Services.Prompts`
**Description:** (No description)

**Version:** 1.0.17

**Path:** `src\Tools\Services\Hazina.Tools.Services.Prompts\Hazina.Tools.Services.Prompts.csproj`

**Dependencies:**
- `Hazina.Tools.Core`
- `Hazina.Tools.Data`
- `Hazina.Tools.Models`

---

### `Hazina.Tools.Services.Social`
**Description:** (No description)

**Version:** 1.0.18

**Path:** `src\Tools\Services\Hazina.Tools.Services.Social\Hazina.Tools.Services.Social.csproj`

**Dependencies:**
- `Hazina.Store.EmbeddingStore`
- `Hazina.Tools.Data`
- `Hazina.Tools.Models`
- `Hazina.Tools.Services.Chat`

**Key NuGet Packages:**
- Microsoft.Data.Sqlite
- Polly

---

### `Hazina.Tools.Services.Store`
**Description:** (No description)

**Version:** 2.0.0

**Path:** `src\Tools\Services\Hazina.Tools.Services.Store\Hazina.Tools.Services.Store.csproj`

**Dependencies:**
- `Hazina.Tools.Core`
- `Hazina.Tools.Data`
- `Hazina.Tools.Models`
- `Hazina.Tools.Services.BigQuery`
- `Hazina.Tools.Services.FileOps`
- `Hazina.Tools.Services.Web`

**Key NuGet Packages:**
- Google.Cloud.BigQuery.V2
- OpenAI

---

### `Hazina.Tools.Services.ToolAgent`
**Description:** (No description)

**Version:** N/A

**Path:** `src\Tools\Services\Hazina.Tools.Services.ToolAgent\Hazina.Tools.Services.ToolAgent.csproj`

**Dependencies:**
- `Hazina.LLMClientTools`
- `Hazina.LLMs.Classes`
- `Hazina.Tools.Models`

**Key NuGet Packages:**
- Microsoft.Extensions.Logging.Abstractions

---

### `Hazina.Tools.Services.Web`
**Description:** (No description)

**Version:** 2.0.0

**Path:** `src\Tools\Services\Hazina.Tools.Services.Web\Hazina.Tools.Services.Web.csproj`

**Dependencies:**
- `Hazina.Tools.Common.Models`
- `Hazina.Tools.Core`
- `Hazina.Tools.Data`
- `Hazina.Tools.Models`
- `Hazina.Tools.Services.ContentRetrieval`
- `Hazina.Tools.Services.FileOps`
- `Hazina.Tools.Services.WordPress`

---

### `Hazina.Tools.Services.WordPress`
**Description:** (No description)

**Version:** 2.0.0

**Path:** `src\Tools\Services\Hazina.Tools.Services.WordPress\Hazina.Tools.Services.WordPress.csproj`

**Dependencies:**
- `Hazina.LLMs.Client`
- `Hazina.LLMs.SemanticKernel`
- `Hazina.Tools.Core`
- `Hazina.Tools.Data`
- `Hazina.Tools.Models`
- `Hazina.Tools.Services.FileOps`

**Key NuGet Packages:**
- HtmlAgilityPack
- Microsoft.Extensions.Http.Polly
- Polly

---

## Storage
**Count:** 4

### `Hazina.Store.DocumentStore`
**Description:** Document storage and retrieval system for RAG (Retrieval-Augmented Generation) in Hazina. Provides IDocumentStore interface with support for text/binary documents, chunking, metadata management, and relevancy matching. Includes file-based and memory-based backends.

**Version:** 2.0.0

**Path:** `src\Core\Storage\Hazina.Store.DocumentStore\Hazina.Store.DocumentStore.csproj`

**Dependencies:**
- `Hazina.AI.Providers`
- `Hazina.LLMs.Helpers`
- `Hazina.Store.EmbeddingStore`

**Key NuGet Packages:**
- Npgsql
- Supabase

---

### `Hazina.Store.EmbeddingStore`
**Description:** Vector embedding storage for semantic search in Hazina. Provides IEmbeddingStore interface with PostgreSQL/pgvector backend, batch operations, similarity matching, and embedding generation service. Essential for RAG implementations.

**Version:** 2.0.0

**Path:** `src\Core\Storage\Hazina.Store.EmbeddingStore\Hazina.Store.EmbeddingStore.csproj`

**Dependencies:**
- `Hazina.LLMs.Client`
- `Hazina.LLMs.Helpers`

**Key NuGet Packages:**
- Npgsql
- OpenAI
- Pgvector
- Supabase

---

### `Hazina.Store.FactsStore`
**Description:** Facts storage for compact, relevant context facts in context engineering

**Version:** N/A

**Path:** `src\Core\Storage\Hazina.Store.FactsStore\Hazina.Store.FactsStore.csproj`

**Key NuGet Packages:**
- Microsoft.Data.Sqlite
- Microsoft.Extensions.Configuration.Abstractions
- Microsoft.Extensions.DependencyInjection.Abstractions

---

### `Hazina.Store.Sqlite`
**Description:** SQLite-based storage backend for Hazina. Provides single-file database storage with FTS5 full-text search, vector similarity search, metadata querying, and file checksum tracking for rebuildability. Ideal for local development and embedded scenarios.

**Version:** 2.0.0

**Path:** `src\Core\Storage\Hazina.Store.Sqlite\Hazina.Store.Sqlite.csproj`

**Dependencies:**
- `Hazina.LLMs.Client`
- `Hazina.LLMs.Helpers`
- `Hazina.Store.DocumentStore`
- `Hazina.Store.EmbeddingStore`

**Key NuGet Packages:**
- Microsoft.Data.Sqlite
- Microsoft.Extensions.Logging.Abstractions

---

## Tools
**Count:** 11

### `Hazina.Tools.AI.Agents`
**Description:** (No description)

**Version:** 2.0.0

**Path:** `src\Tools\Foundation\Hazina.Tools.AI.Agents\Hazina.Tools.AI.Agents.csproj`

**Dependencies:**
- `Hazina.Observability.LLMLogs`
- `Hazina.Tools.Core`
- `Hazina.Tools.Data`
- `Hazina.Tools.Models`
- `Hazina.Tools.Services.Embeddings`
- `Hazina.Tools.Services.FileOps`
- `Hazina.Tools.Services.Store`
- `Hazina.Tools.Services.Web`

**Key NuGet Packages:**
- OpenAI
- Serilog
- Serilog.Sinks.File

---

### `Hazina.Tools.Common.Infrastructure.AspNetCore`
**Description:** ASP.NET Core infrastructure components for Hazina tools

**Version:** 2.0.0

**Path:** `src\Tools\Common\Hazina.Tools.Common.Infrastructure.AspNetCore\Hazina.Tools.Common.Infrastructure.AspNetCore.csproj`

**Dependencies:**
- `Hazina.AgentFactory`

**Key NuGet Packages:**
- Microsoft.AspNetCore.Identity.EntityFrameworkCore
- Microsoft.Extensions.Identity.Core

---

### `Hazina.Tools.Common.Models`
**Description:** Shared models and DTOs for Hazina tools

**Version:** 2.0.0

**Path:** `src\Tools\Common\Hazina.Tools.Common.Models\Hazina.Tools.Common.Models.csproj`

**Dependencies:**
- `Hazina.AgentFactory`

---

### `Hazina.Tools.ContextCompression`
**Description:** Context compression and optimization for LLM requests

**Version:** 1.0.0

**Path:** `src\Tools\Foundation\Hazina.Tools.ContextCompression\Hazina.Tools.ContextCompression.csproj`

**Dependencies:**
- `Hazina.Tools.Models`

**Key NuGet Packages:**
- LibGit2Sharp
- Microsoft.CodeAnalysis.CSharp

---

### `Hazina.Tools.Core`
**Description:** Core functionality for Hazina generation tools

**Version:** 2.0.0

**Path:** `src\Tools\Foundation\Hazina.Tools.Core\Hazina.Tools.Core.csproj`

**Dependencies:**
- `Hazina.Tools.Models`

**Key NuGet Packages:**
- Microsoft.Extensions.Configuration.Abstractions
- Microsoft.Extensions.Configuration.Binder
- Microsoft.Extensions.Logging.Abstractions

---

### `Hazina.Tools.Data`
**Description:** Data access layer for Hazina generation tools

**Version:** 2.0.0

**Path:** `src\Tools\Foundation\Hazina.Tools.Data\Hazina.Tools.Data.csproj`

**Dependencies:**
- `Hazina.LLMClientTools`
- `Hazina.Store.DocumentStore`
- `Hazina.Store.Sqlite`
- `Hazina.Tools.Common.Models`
- `Hazina.Tools.Core`
- `Hazina.Tools.Models`

**Key NuGet Packages:**
- Microsoft.EntityFrameworkCore
- Supabase

---

### `Hazina.Tools.Extensions`
**Description:** (No description)

**Version:** N/A

**Path:** `src\Tools\Foundation\Hazina.Tools.Extensions\Hazina.Tools.Extensions.csproj`

**Dependencies:**
- `Hazina.Tools.Data`

---

### `Hazina.Tools.Migration`
**Description:** Data migration tools for Hazina - migrate from file-based storage to SQLite or other backends

**Version:** 2.0.0

**Path:** `src\Tools\Migration\Hazina.Tools.Migration\Hazina.Tools.Migration.csproj`

**Dependencies:**
- `Hazina.Store.DocumentStore`
- `Hazina.Store.EmbeddingStore`
- `Hazina.Store.Sqlite`
- `Hazina.Tools.Core`
- `Hazina.Tools.Data`

**Key NuGet Packages:**
- Microsoft.Extensions.Logging.Abstractions
- System.Text.Json

---

### `Hazina.Tools.Models`
**Description:** Domain models for Hazina content generation tools

**Version:** 2.0.0

**Path:** `src\Tools\Foundation\Hazina.Tools.Models\Hazina.Tools.Models.csproj`

**Dependencies:**
- `Hazina.Tools.Common.Models`

---

### `Hazina.Tools.Services`
**Description:** Main service orchestration for Hazina generation tools

**Version:** 2.0.0

**Path:** `src\Tools\Services\Hazina.Tools.Services\Hazina.Tools.Services.csproj`

**Dependencies:**
- `Hazina.Tools.AI.Agents`
- `Hazina.Tools.Core`
- `Hazina.Tools.Data`
- `Hazina.Tools.Models`
- `Hazina.Tools.Services.ContentRetrieval`
- `Hazina.Tools.Services.FileOps`
- `Hazina.Tools.Services.Store`
- `Hazina.Tools.Services.Web`
- `Hazina.Tools.Services.WordPress`
- `Hazina.Tools.TextExtraction`

**Key NuGet Packages:**
- Google.Cloud.BigQuery.V2
- HtmlAgilityPack
- OpenAI

---

### `Hazina.Tools.TextExtraction`
**Description:** Text extraction utilities for PDF, Word, Excel and other document formats

**Version:** 2.0.0

**Path:** `src\Tools\Foundation\Hazina.Tools.TextExtraction\Hazina.Tools.TextExtraction.csproj`

**Dependencies:**
- `Hazina.AgentFactory`
- `Hazina.LLMClientTools`
- `Hazina.LLMs.Client`
- `Hazina.LLMs.OpenAI`

**Key NuGet Packages:**
- ClosedXML
- DocumentFormat.OpenXml
- Google.Ads.GoogleAds
- MimeTypesMap
- OpenAI
- PDFiumCore
- SixLabors.ImageSharp
- System.Memory.Data

---

