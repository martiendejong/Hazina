# Hazina Repository

Hazina is a collection of agents, tooling, services, demos, and apps for LLM-powered workflows.

## Quick Start
- Restore dependencies: `dotnet restore Hazina.sln`
- Build everything: `dotnet build Hazina.sln`
- Run tests: `dotnet test Hazina.sln` (or per-project test csproj)

## Solution Organization

The solution is organized into logical categories to improve navigation and maintainability:

### 1. Agents
Core agent orchestration, factory patterns, and content generation engines.
- Hazina.AgentFactory
- Hazina.DynamicAPI
- Hazina.Generator

### 2. LLM Core
Foundation libraries for LLM interactions, including client abstractions, data contracts, and tool definitions.
- Hazina.LLMClientTools
- Hazina.LLMs.Classes
- Hazina.LLMs.Client
- Hazina.LLMs.Helpers
- Hazina.LLMs.Tools

### 3. LLM Providers
Concrete integrations with AI service providers and adapters.
- Hazina.LLMs.Anthropic
- Hazina.LLMs.Gemini
- Hazina.LLMs.HuggingFace
- Hazina.LLMs.Mistral
- Hazina.LLMs.OpenAI
- Hazina.LLMs.SemanticKernel

### 4. Storage & UI
Data persistence layers and shared user interface components.
- Hazina.Store.DocumentStore
- Hazina.Store.EmbeddingStore
- Hazina.ChatShared

### 5. Tools Foundation
Core utilities, extension methods, base types, and fundamental tool infrastructure.
- Hazina.Tools.AI.Agents
- Hazina.Tools.Core
- Hazina.Tools.Data
- Hazina.Tools.Extensions
- Hazina.Tools.Models
- Hazina.Tools.TextExtraction

### 6. Tools Common
Shared infrastructure and models used across multiple tool categories.
- Hazina.Tools.Common.Infrastructure.AspNetCore
- Hazina.Tools.Common.Models

### 7. Services - Core
Essential services for chat, embeddings, prompts, and storage operations.
- Hazina.Tools.Services
- Hazina.Tools.Services.Chat
- Hazina.Tools.Services.Embeddings
- Hazina.Tools.Services.Prompts
- Hazina.Tools.Services.Store

### 8. Services - Data
Data processing, ingestion, and file operation services.
- Hazina.Tools.Services.BigQuery
- Hazina.Tools.Services.ContentRetrieval
- Hazina.Tools.Services.DataGathering
- Hazina.Tools.Services.FileOps
- Hazina.Tools.Services.Intake

### 9. Services - Integration
External system connectors and third-party integrations.
- Hazina.Tools.Services.Social
- Hazina.Tools.Services.Web
- Hazina.Tools.Services.WordPress

### 10. Desktop Apps
Windows desktop applications for building, viewing, and managing Hazina workflows.
- Hazina.App.AppBuilder
- Hazina.App.EmbeddingsViewer
- Hazina.App.ExplorerIntegration
- Hazina.App.Windows

### 11. CLI & Web Apps
Command-line tools and web applications.
- Hazina.App.ClaudeCode
- Hazina.App.HtmlMockupGenerator

### 12. Demos
Example applications demonstrating Hazina capabilities.
- Hazina.Demo.Crosslink
- Hazina.Demo.FolderToPostgres
- Hazina.Demo.Llama
- Hazina.Demo.PDFMaker
- Hazina.Demo.Postgres

## Notes
- `.local` projects are developer-specific variants not referenced in the main solution file.
- XML docs are emitted on build under `bin/Debug/net8.0/*.xml` for library projects.