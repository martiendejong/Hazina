# Hazina Repository

Hazina is a collection of agents, tooling, services, demos, and apps for LLM-powered workflows.

## Quick Start
- Restore dependencies: `dotnet restore Hazina.sln`
- Build everything: `dotnet build Hazina.sln`
- Run tests: `dotnet test Hazina.sln` (or per-project test csproj)

## Project Catalog
- `apps/CLI/Hazina.App.ClaudeCode/Hazina.App.ClaudeCode.csproj` - CLI integration for driving Hazina via Claude Code.
- `apps/Demos/Hazina.Demo.Crosslink/Hazina.Demo.Crosslink.csproj` - Demo app showing crosslinking/relationship generation.
- `apps/Demos/Hazina.Demo.FolderToPostgres/Hazina.Demo.FolderToPostgres.csproj` - Demo that ingests folder content into Postgres.
- `apps/Demos/Hazina.Demo.Llama/Hazina.Demo.Llama.csproj` - Demo wiring Hazina against a LLaMA provider.
- `apps/Demos/Hazina.Demo.PDFMaker/Hazina.Demo.PDFMaker.csproj` - Demo creating PDFs with Hazina pipelines.
- `apps/Demos/Hazina.Demo.Postgres/Hazina.Demo.Postgres.csproj` - Demo of Postgres-backed Hazina flows.
- `apps/Desktop/Hazina.App.AppBuilder/Hazina.App.AppBuilder.csproj` - Desktop app for building and orchestrating Hazina agents.
- `apps/Desktop/Hazina.App.EmbeddingsViewer/Hazina.App.EmbeddingsViewer.csproj` - Desktop viewer for embeddings produced by Hazina.
- `apps/Desktop/Hazina.App.ExplorerIntegration/Hazina.App.ExplorerIntegration.csproj` - Windows Explorer integration shell extension harness.
- `apps/Desktop/Hazina.App.Windows/Hazina.App.Windows.csproj` - Windows desktop shell for Hazina.
- `apps/Testing/Hazina.IntegrationTests.OpenAI/Hazina.IntegrationTests.OpenAI.csproj` - Integration tests for the OpenAI provider.
- `apps/Web/Hazina.App.HtmlMockupGenerator/Hazina.App.HtmlMockupGenerator.csproj` - Web app that generates HTML mockups with Hazina.
- `src/Core/Agents/Hazina.AgentFactory/Hazina.AgentFactory.csproj` - Factory and dependency wiring for generator agents.
- `src/Core/Agents/Hazina.DynamicAPI/Hazina.DynamicAPI.csproj` - Dynamic API surface for Hazina agents.
- `src/Core/Agents/Hazina.Generator/Hazina.Generator.csproj` - Core generator engine for producing documents/content.
- `src/Core/LLMs/Hazina.LLMClientTools/Hazina.LLMClientTools.csproj` - Shared LLM client utilities and abstractions.
- `src/Core/LLMs/Hazina.LLMs.Classes/Hazina.LLMs.Classes.csproj` - Common LLM data contracts (messages, roles, payloads).
- `src/Core/LLMs/Hazina.LLMs.Client/Hazina.LLMs.Client.csproj` - Client wrapper for LLM interactions.
- `src/Core/LLMs/Hazina.LLMs.Helpers/Hazina.LLMs.Helpers.csproj` - Helper utilities for LLM operations.
- `src/Core/LLMs/Hazina.LLMs.Tools/Hazina.LLMs.Tools.csproj` - LLM tool/function-calling abstractions.
- `src/Core/LLMs.Providers/Hazina.LLMs.Anthropic/Hazina.LLMs.Anthropic.csproj` - Anthropic chat/completions provider for Hazina.
- `src/Core/LLMs.Providers/Hazina.LLMs.Gemini/Hazina.LLMs.Gemini.csproj` - Gemini (Google) provider for Hazina.
- `src/Core/LLMs.Providers/Hazina.LLMs.HuggingFace/Hazina.LLMs.HuggingFace.csproj` - HuggingFace inference provider integration.
- `src/Core/LLMs.Providers/Hazina.LLMs.Mistral/Hazina.LLMs.Mistral.csproj` - Mistral provider integration.
- `src/Core/LLMs.Providers/Hazina.LLMs.OpenAI/Hazina.LLMs.OpenAI.csproj` - OpenAI provider integration.
- `src/Core/LLMs.Providers/Hazina.LLMs.SemanticKernel/Hazina.LLMs.SemanticKernel.csproj` - Semantic Kernel adapter utilities for providers.
- `src/Core/Storage/Hazina.Store.DocumentStore/Hazina.Store.DocumentStore.csproj` - Document store backend abstractions and implementations.
- `src/Core/Storage/Hazina.Store.EmbeddingStore/Hazina.Store.EmbeddingStore.csproj` - Embedding store implementations and adapters.
- `src/Core/UI/Hazina.ChatShared/Hazina.ChatShared.csproj` - Shared UI components and models for chat experiences.
- `src/Tools/Common/Hazina.Tools.Common.Infrastructure.AspNetCore/Hazina.Tools.Common.Infrastructure.AspNetCore.csproj` - ASP.NET Core infrastructure helpers for Hazina tools.
- `src/Tools/Common/Hazina.Tools.Common.Models/Hazina.Tools.Common.Models.csproj` - Common models shared across Hazina tools.
- `src/Tools/Foundation/Hazina.Tools.AI.Agents/Hazina.Tools.AI.Agents.csproj` - Agent abstractions and base implementations for tools.
- `src/Tools/Foundation/Hazina.Tools.Core/Hazina.Tools.Core.csproj` - Core utilities for Hazina tools.
- `src/Tools/Foundation/Hazina.Tools.Data/Hazina.Tools.Data.csproj` - Data-access utilities and helpers for tools.
- `src/Tools/Foundation/Hazina.Tools.Extensions/Hazina.Tools.Extensions.csproj` - Extension methods shared across Hazina tooling.
- `src/Tools/Foundation/Hazina.Tools.Models/Hazina.Tools.Models.csproj` - Shared domain models for Hazina tools.
- `src/Tools/Foundation/Hazina.Tools.TextExtraction/Hazina.Tools.TextExtraction.csproj` - Text extraction utilities for ingestion.
- `src/Tools/Services/Hazina.Tools.Services/Hazina.Tools.Services.csproj` - Shared service primitives for Hazina tools.
- `src/Tools/Services/Hazina.Tools.Services.BigQuery/Hazina.Tools.Services.BigQuery.csproj` - BigQuery integration service.
- `src/Tools/Services/Hazina.Tools.Services.Chat/Hazina.Tools.Services.Chat.csproj` - Chat orchestration service for Hazina tooling.
- `src/Tools/Services/Hazina.Tools.Services.ContentRetrieval/Hazina.Tools.Services.ContentRetrieval.csproj` - Content retrieval pipeline service.
- `src/Tools/Services/Hazina.Tools.Services.DataGathering/Hazina.Tools.Services.DataGathering.csproj` - Data gathering and analysis field generation service.
- `src/Tools/Services/Hazina.Tools.Services.Embeddings/Hazina.Tools.Services.Embeddings.csproj` - Embedding generation and storage service.
- `src/Tools/Services/Hazina.Tools.Services.FileOps/Hazina.Tools.Services.FileOps.csproj` - File operations service.
- `src/Tools/Services/Hazina.Tools.Services.Intake/Hazina.Tools.Services.Intake.csproj` - Intake/ingestion service.
- `src/Tools/Services/Hazina.Tools.Services.Prompts/Hazina.Tools.Services.Prompts.csproj` - Prompt management service.
- `src/Tools/Services/Hazina.Tools.Services.Social/Hazina.Tools.Services.Social.csproj` - Social network integration service.
- `src/Tools/Services/Hazina.Tools.Services.Store/Hazina.Tools.Services.Store.csproj` - Store services for Hazina tooling.
- `src/Tools/Services/Hazina.Tools.Services.Web/Hazina.Tools.Services.Web.csproj` - Web content services.
- `src/Tools/Services/Hazina.Tools.Services.WordPress/Hazina.Tools.Services.WordPress.csproj` - WordPress integration service.
- `Tests/Core/Hazina.AgentFactory.Tests/Hazina.AgentFactory.Tests.csproj` - Test suite for Hazina.AgentFactory.
- `Tests/Core/Hazina.DynamicAPI.Tests/Hazina.DynamicAPI.Tests.csproj` - Test suite for Hazina.DynamicAPI.
- `Tests/Core/Hazina.Generator.Tests/Hazina.Generator.Tests.csproj` - Test suite for Hazina.Generator.
- `Tests/Core/Hazina.LLMs.Anthropic.Tests/Hazina.LLMs.Anthropic.Tests.csproj` - Test suite for Hazina.LLMs.Anthropic.
- `Tests/Core/Hazina.LLMs.Classes.Tests/Hazina.LLMs.Classes.Tests.csproj` - Test suite for Hazina.LLMs.Classes.
- `Tests/Core/Hazina.LLMs.Client.Tests/Hazina.LLMs.Client.Tests.csproj` - Test suite for Hazina.LLMs.Client.
- `Tests/Core/Hazina.LLMs.HuggingFace.Tests/Hazina.LLMs.HuggingFace.Tests.csproj` - Test suite for Hazina.LLMs.HuggingFace.
- `Tests/Core/Hazina.LLMs.OpenAI.Tests/Hazina.LLMs.OpenAI.Tests.csproj` - Test suite for Hazina.LLMs.OpenAI.
- `Tests/Core/Hazina.Store.DocumentStore.Tests/Hazina.Store.DocumentStore.Tests.csproj` - Test suite for Hazina.Store.DocumentStore.
- `Tests/Core/Hazina.Store.EmbeddingStore.Tests/Hazina.Store.EmbeddingStore.Tests.csproj` - Test suite for Hazina.Store.EmbeddingStore.
- `Tests/Tools/Hazina.Tools.Common.Models.Tests/Hazina.Tools.Common.Models.Tests.csproj` - Test suite for Hazina.Tools.Common.Models.
- `Tests/Tools/Hazina.Tools.Core.Tests/Hazina.Tools.Core.Tests.csproj` - Test suite for Hazina.Tools.Core.
- `Tests/Tools/Hazina.Tools.Data.Tests/Hazina.Tools.Data.Tests.csproj` - Test suite for Hazina.Tools.Data.
- `Tests/Tools/Hazina.Tools.Models.Tests/Hazina.Tools.Models.Tests.csproj` - Test suite for Hazina.Tools.Models.
- `Tests/Tools/Hazina.Tools.Services.BigQuery.Tests/Hazina.Tools.Services.BigQuery.Tests.csproj` - Test suite for Hazina.Tools.Services.BigQuery.
- `Tests/Tools/Hazina.Tools.Services.Chat.Tests/Hazina.Tools.Services.Chat.Tests.csproj` - Test suite for Hazina.Tools.Services.Chat.
- `Tests/Tools/Hazina.Tools.Services.Embeddings.Tests/Hazina.Tools.Services.Embeddings.Tests.csproj` - Test suite for Hazina.Tools.Services.Embeddings.
- `Tests/Tools/Hazina.Tools.Services.FileOps.Tests/Hazina.Tools.Services.FileOps.Tests.csproj` - Test suite for Hazina.Tools.Services.FileOps.
- `Tests/Tools/Hazina.Tools.Services.Store.Tests/Hazina.Tools.Services.Store.Tests.csproj` - Test suite for Hazina.Tools.Services.Store.
- `Tests/Tools/Hazina.Tools.TextExtraction.Tests/Hazina.Tools.TextExtraction.Tests.csproj` - Test suite for Hazina.Tools.TextExtraction.

## Notes
- `.local` projects are developer-specific variants not referenced in the main solution file.
- XML docs are emitted on build under `bin/Debug/net8.0/*.xml` for library projects.