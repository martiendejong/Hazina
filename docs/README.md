# Hazina

Hazina is an agentic framework for .NET that lets you build, run, and orchestrate AI agents over your own documents and source code. It combines retrieval‑augmented generation (RAG), tool calling, and safe file modifications so agents can search, read, reason, and update your repositories with context.

It ships with:
- A set of core libraries (also packaged for NuGet) for embeddings, document stores, tool contexts, LLM client wrappers, and agent orchestration.
- A Windows desktop app to author Stores, Agents, and Flows, and to chat with them.
- Sample utilities (EmbeddingsViewer, Crosslink, PDFMaker, HtmlMockupGenerator) demonstrating the building blocks in practical scenarios.

Made by Martien de Jong — https://martiendejong.nl

---

## What This Tool Is For

Hazina helps you create autonomous or semi‑autonomous AI agents that can:
- Index your documents and code into lightweight, local embedding stores.
- Find the most relevant files for a prompt and feed them to an LLM.
- Call developer‑oriented tools (e.g., `git`, `dotnet`, `npm`), data tools (e.g., Google BigQuery), and custom tools.
- Modify files deterministically through a strongly typed “update store” response format.
- Collaborate via multi‑agent “flows,” where agents call other agents and exchange context.

Common use cases:
- Code assistants that read/update local repositories with explicit write permissions per store.
- Knowledge assistants that answer questions using your docs, knowledge base, or customer data.
- Data helpers that explore and query datasets (e.g., BigQuery) and synthesize insights.

---

## High‑Level Architecture

Core concepts:
- Stores: A `DocumentStore` combines a text store (files), an embedding store (vectors), and a parts store (chunk index). Stores are named, can be read‑only or writable, and power RAG.
- Agents: A `HazinaAgent` pairs a `DocumentGenerator` (LLM + RAG) with a `ToolsContextBase` (tool calls). Agents are configured with a system prompt, a set of stores, and an allowed toolset.
- Flows: A `HazinaFlow` is a lightweight orchestration over agents. A flow describes which agents can be called in sequence or on demand.
- LLM Client: `ILLMClient` abstracts LLM providers. `Hazina.LLMs.OpenAI` provides an OpenAI implementation (chat, streaming, images, embeddings).
- Tools: Tool definitions (functions + typed parameters) the model can call during a conversation. Tools are attached to an agent via its `ToolsContextBase`.

Key libraries and roles:
- Hazina.Classes: Shared contracts (chat messages, tool calls, typed responses like `UpdateStoreResponse`).
- Hazina.Helpers: Utilities (document splitting, partial JSON parser, token counting, checksum, store helpers).
- Hazina.EmbeddingStore: Embedding backends (file‑based and in‑memory) with a simple JSON format.
- Hazina.DocumentStore: `DocumentStore` composition + RAG helpers (relevant items, listing, move/remove, etc.).
- Hazina.LLMClient: The provider‑agnostic LLM interface.
- Hazina.OpenAI: Concrete OpenAI implementation (chat, streaming, images, embeddings) + response parsing.
- Hazina.LLMClientTools: Tool context abstraction and helper tools (e.g., `WebPageScraper`).
- Hazina.Generator: `DocumentGenerator` that assembles messages with relevant context and can safely apply file updates.
- Hazina.AgentFactory: Agent/store/flow creation, config format helpers, built‑in toolsets (read/write/list/relevancy, git/dotnet/npm/build, BigQuery, email, WordPress placeholder).
- Hazina.ChatShared: Shared WPF chat UI (`ChatWindow`) and `IChatController` abstraction so multiple apps can reuse a single chat experience.

Applications:
- Windows: WPF app to author Stores/Agents/Flows (as text or cards) and chat with selected agent/flow.
- Hazina.ExplorerIntegration: WPF utility that integrates with Windows Explorer workflows to embed a folder and chat over its contents; uses the shared chat component.
- EmbeddingsViewer: WPF tool to inspect `.embed` files and list their keys.
- Crosslink: Console sample showing semantic matching of a CV to job postings using stores.
- PDFMaker, HtmlMockupGenerator: Samples that show how to compose Hazina components.

---

## Repository Structure (Selected)

- Hazina.AgentFactory — Agent orchestration, config parsing, and built‑in tools.
- Hazina.Classes — Message/response models, tool metadata, image data.
- Hazina.DocumentStore — Store composition and RAG helpers.
- Hazina.EmbeddingStore — File/in‑memory embedding backends.
- Hazina.Generator — `DocumentGenerator` for responses and safe file updates.
- Hazina.Helpers — Splitting, token counting, partial JSON parsing, store helpers.
- Hazina.LLMClient — Provider‑agnostic LLM interface.
- Hazina.OpenAI — OpenAI implementation (chat, stream, images, embeddings).
- Hazina.LLMClientTools — Tool context and helper tools (e.g., web page scraping).
- Hazina.ChatShared — Shared WPF chat UI and controller abstraction used by Windows and ExplorerIntegration.
- Windows — WPF desktop authoring and chat app for Hazina.
- Hazina.ExplorerIntegration — Explorer‑focused WPF utility to embed and chat over a selected folder, using the shared chat window.
- EmbeddingsViewer — WPF embedding file inspector.
- Crosslink — Console sample for semantic matching.

Local packages (for testing): `local_packages/*.nupkg` for the Hazina libraries.

---

## Configuration: Stores, Agents, Flows

Hazina supports both JSON and a simple `.hazina` text format for configuration. The Windows app can edit either format. At runtime, the loader auto‑detects the format.

Examples:

stores.hazina
```
Name: hazina_sourcecode
Description: Alle projectcode, inclusief tests, CI/CD scripts, infra en devopsconfiguraties.
Path: C:\Projects\Hazina
FileFilters: *.cs,*.ts,*.js,*.py,*.sh,*.yml,*.yaml,*.json,*.csproj,*.sln,Dockerfile
SubDirectory: 
ExcludePattern: bin,obj,node_modules,dist
```

agents.hazina
```
Name: hazina_simpleagent
Description:
Prompt: Handle the instruction.
Stores: hazina_sourcecode|False
Functions: 
CallsAgents: 
CallsFlows: 
ExplicitModify: False
```

Key fields:
- Store `Write` flag: controls whether an agent may call write/delete tools on that store.
- Functions: opt‑in to tool sets (e.g., `git`, `dotnet`, `npm`, `build`, `bigquery`, `email`, `wordpress`, `custom`).
- CallsAgents/CallsFlows: allow cross‑agent or flow invocations from within an agent.

---

## How Responses Are Generated

`DocumentGenerator` assembles the LLM message list as:
- Recent conversation history (sliding window).
- Retrieved relevant document snippets from the agent’s writable store plus any extra read‑only stores.
- A list of files in the writable store (optional, provides global context).
- The agent’s system prompt and the user input.

Agents can request tool calls (e.g., list/read files, search by relevancy, run `git`/`dotnet`/`npm`, query BigQuery). The OpenAI wrapper handles tool streaming and merges tool outputs back into the conversation.

For safe code changes, the model is asked to respond in the strongly typed `UpdateStoreResponse` shape. Hazina parses and applies:
- Modifications: write updated file contents.
- Deletions: remove files.
- Moves: rename files.

This enforces full‑file writes and avoids “partial edits” that would yield broken code.

---

## Windows App (WPF)

The Windows app lets you:
- Open/edit/save Stores, Agents, and Flows (as text or card views).
- Start a chat window bound to an agent or flow.
- See interim tool output messages and final replies.

Required settings:
- OpenAI API key and model selection are read from `appsettings.json` (see OpenAIConfig below).
- Some tools (e.g., BigQuery) require credentials (`googleaccount.json` next to the app executable).

---

## OpenAI Configuration

`Hazina.OpenAI` reads configuration via `OpenAIConfig`:

appsettings.json
```
{
  "OpenAI": {
    "ApiKey": "sk-...",
    "Model": "gpt-4.1",
    "ImageModel": "gpt-image-1",
    "EmbeddingModel": "text-embedding-ada-002",
    "LogPath": "C:\\projects\\Hazina\\logs.txt"
  }
}
```

Or construct `OpenAIConfig` in code and pass it to `OpenAIClientWrapper`.

---

## Using Hazina In Your Code

Minimal agent from code:
```
var openAI = new OpenAIConfig(apiKey);
var llm = new OpenAIClientWrapper(openAI);

var store = new DocumentStore(
    new EmbeddingFileStore(@"C:\\myproj\\repo.embed", llm),
    new TextFileStore(@"C:\\myproj"),
    new DocumentPartFileStore(@"C:\\myproj\\repo.parts"),
    llm);
await store.UpdateEmbeddings();

var baseMsgs = new List<HazinaChatMessage> {
  new HazinaChatMessage { Role = HazinaMessageRole.System, Text = "You are a helpful assistant." }
};
var generator = new DocumentGenerator(store, baseMsgs, llm, new List<IDocumentStore>());
var tools = new ToolsContextBase();

// Optional: add read tools for the store
// (See AgentFactory.AddReadTools for reference or use AgentFactory directly)

var agent = new HazinaAgent("assistant", generator, tools);
var reply = await agent.Generator.GetResponse("What files relate to authentication?", default);
```

Using configuration files via AgentManager:
```
var mgr = new AgentManager(
  storesJsonPath: "stores.hazina",
  agentsJsonPath: "agents.hazina",
  flowsJsonPath:  "flows.hazina",
  openAIApiKey: apiKey,
  logFilePath:   "C:\\logs\\hazina.log");
await mgr.LoadStoresAndAgents();
var response = await mgr.SendMessage("Explain how build works", default, agentName: "hazina_simpleagent");
```

---

## NuGet Packages (Main)

The following libraries are intended for packaging and reuse in Visual Studio projects:
- Hazina.Classes — Shared contracts and types.
- Hazina.Helpers — Generic helpers and parsing utilities.
- Hazina.EmbeddingStore — Pluggable embedding backends.
- Hazina.DocumentStore — Store composition and RAG helpers.
- Hazina.LLMClient — Provider abstraction.
- Hazina.OpenAI — OpenAI client implementation.
- Hazina.HuggingFace — HuggingFace client implementation.
- Hazina.LLMClientTools — Tooling context and helpers.
- Hazina.Generator — Response generation and update‑store pipeline.
- Hazina.AgentFactory — Agent/flow creation and built‑in tools.

All packages follow synchronized semantic versioning. See [NUGET-VERSIONING.md](NUGET-VERSIONING.md) for details on publishing and version management.

Local builds of these packages exist under `local_packages/`.

---

## Proposal: Radically Improve Quality and Usability

Focus areas and actions per main library (NuGet packages):

Hazina.Classes
- Stabilize public API with XML docs and examples.
- Add analyzers and nullable annotations consistently across types.
- Provide consistent naming (HazinaChatMessage, HazinaChatTool, etc.).

Hazina.Helpers
- Extract token counting and parsing into cohesive namespaces.
- Harden `PartialJsonParser` with formal streaming/JSON repair strategies and tests.
- Add benchmarks for splitter/token counter to tune defaults.

Hazina.Store.EmbeddingStore
- Unify file format, add schema version, and safe persistence (temp + atomic replace).
- Add compaction and integrity verification commands.
- Expose asynchronous batch APIs for indexing.

Hazina.Store.DocumentStore
- Enforce consistent path normalization, case rules, and separators.
- Add transactional update API for multi‑file edits and rollbacks on failure.
- Provide adapters for alternative storage (e.g., SQLite/Faiss/PGVector via plugin pattern).

Hazina.LLMs.Client
- Keep provider‑neutral with explicit capabilities (chat, stream, tools, images, embeddings).
- Add retry/backoff and rate‑limit policies in the abstraction (pluggable strategies).
- Introduce cancellation guidance and timeouts per call.

Hazina.LLMs.OpenAI
- Centralize model configuration and safety prompts per operation.
- Improve streaming tool‑call assembly and error surfaces (clear exceptions when partial tool data is malformed).
- Add structured logging hooks, correlation IDs, and redaction utilities.

Hazina.LLMs.Tools
- Formalize a Tool Provider pattern to register tool sets (fs, git, dotnet, npm, http, webscrape, email, bigquery) with discovery and capability flags.
- Add input validation and guardrails for each tool (timeouts, allowlists, working dirs).
- Provide mocks/fakes for offline tests.

Hazina.Generator
- Refactor message assembly pipeline into pluggable “message enrichers” (history window, relevant snippets, file list, extra stores) with ordering and limits.
- Expose safe policies for UpdateStore (e.g., max file size, extension allowlist, diff preview mode).
- Add optional dry‑run and patch preview generation.

Hazina.AgentFactory
- Separate config parsing from construction; expose typed validation with diagnostics.
- Remove hardcoded paths; inject `IClock`, `IFileSystem` to ease testing.
- Make tool sets explicitly opt‑in by name and document them in generated schema.

Cross‑cutting
- Add unit and integration tests throughout; create small sample repos for repeatable tests.
- Add CI (build, test, package) and publish signed NuGet packages.
- Provide sandboxes and safety defaults (no write tools unless explicitly configured).
- Write end‑to‑end samples: “Code Assistant,” “Docs Q&A,” “Data Analyst with BigQuery.”

---

## What’s Needed For VS Developer Usability

To make Hazina drop‑in for typical Visual Studio projects:
- Publish NuGet packages with semantic versioning and clear release notes.
- Provide QuickStart templates/snippets for common scenarios (one‑agent, multi‑agent flow, RAG only).
- Ship a minimal `AgentManager` bootstrapper with JSON config support out‑of‑the‑box.
- Document `appsettings.json` for OpenAIConfig and environment variable overrides.
- Provide a sample `stores.hazina`/`agents.hazina` for a standard .NET solution.
- Offer a “no‑write by default” configuration with clear steps to enable safe writes.
- Add a `Hazina.Tools.FileSystem` module with explicit root allowlists and path guards for Windows/Linux.
- Ensure all public APIs target `net8.0` and consider `netstandard2.1` where feasible for wider reuse.
- Provide a Visual Studio “Connected Service” or item template for adding Hazina quickly (optional).

---

## Task List (Status: TODO)

- TODO Stabilize and document public APIs across all NuGet libraries.
- TODO Add XML docs and samples to each package (Classes, Helpers, EmbeddingStore, DocumentStore, LLMClient, OpenAI, LLMClientTools, Generator, AgentFactory).
- TODO Introduce analyzers, nullable reference types, and consistent coding style (EditorConfig).
- TODO Refactor `PartialJsonParser` with robust streaming JSON repair and test coverage.
- TODO Add transactional update and dry‑run support to `DocumentStore` and `DocumentGenerator`.
- TODO Implement atomic file writes and integrity checks in `EmbeddingFileStore` and `DocumentPartFileStore`.
- TODO Extract tool sets into a formal Tool Provider with validation/guards and tests.
- TODO Remove hardcoded paths; inject file system/clock abstractions for testability.
- TODO Add retry/backoff policies and timeouts at the `ILLMClient` level.
- TODO Improve streaming tool‑call assembly and error reporting in `Hazina.LLMs.OpenAI`.
- TODO Add unit/integration tests with small sample repositories and BigQuery mocks.
- TODO Set up CI (build, test, pack, sign) and publish to NuGet.
- TODO Provide QuickStart templates and minimal examples for VS users.
- TODO Document configuration formats and generate JSON schema for Stores/Agents/Flows.
- TODO Add safety defaults: read‑only by default, explicit write capability per store, size/extension limits.
- TODO Provide migration guides and versioned release notes.
- TODO Add additional provider adapters (optional): Azure OpenAI, Ollama/local.

---

## License

Add an OSS license of your choice if you plan to publish. If this remains private, clarify usage restrictions in your organization.

---

## Consumer Projects and Multi-Repository Setup

This Hazina repository is part of a multi-repository development environment. Consumer projects reference these libraries either as NuGet packages (production) or as local project references (development).

### Known Consumer Projects

#### Client Manager (HazinaStoreAPI)
- **Location**: `C:\projects\client-manager`
- **Purpose**: .NET 8 API application for AI-powered content generation and management
- **Integration**: Uses Hazina LLM libraries (Classes, Helpers, Client, OpenAI, Anthropic, etc.) along with HazinaTools libraries

The client-manager project demonstrates two integration patterns:

1. **Production Pattern** (`ClientManager.sln`):
   - Uses published NuGet packages
   - Suitable for CI/CD and production deployments
   - Cannot step through library source code when debugging

2. **Development Pattern** (`ClientManager.local.sln`):
   - Uses local project references to this repository
   - Full debugging support with symbols
   - Ideal for library development and troubleshooting

### Local Development Workflow

When working on Hazina libraries with a consumer project:

1. **Directory Structure**:
   ```
   C:\projects\
   ├── devgpt\              (this repository)
   ├── hazina\         (companion tools repository)
   └── client-manager\      (consumer API project)
   ```

2. **Making Changes**:
   - Make changes to Hazina libraries in `C:\projects\devgpt`
   - Open consumer project using its `.local.sln` file
   - Rebuild the consumer solution - changes are immediately reflected
   - Debug with full symbol support

3. **Publishing Updates**:
   ```bash
   cd C:\projects\devgpt
   # Update version numbers in .csproj files
   ./bump-and-publish.ps1  # Windows
   ```
   
   Then update consumer project's `packages.config` or `.csproj` to use new versions.

### Debugging Symbol Loading

**Important**: Consumer projects must use the `.local.sln` solution file to debug into Hazina library code. The standard `.sln` file references NuGet packages which don't include full debugging symbols or source code.

**Symptom**: "Symbols not loaded" message when trying to step into Hazina methods during debugging.

**Solution**: Open the consumer project's `.local.sln` file in Visual Studio, rebuild, and start debugging.

### Related Repositories

- **Hazina** (this repo): Core agentic framework, LLM clients, stores, and generator
- **HazinaTools**: Content generation services, BigQuery integration, WordPress, etc.
- **Client Manager**: Consumer API that integrates both Hazina and HazinaTools

See `C:\projects\client-manager\README.md` for detailed documentation on the multi-repository development workflow.
# HazinaTools

A collection of .NET libraries for building AI-powered content generation tools.

## Overview

HazinaTools is a comprehensive suite of libraries designed to support AI-driven content generation, text extraction, and various service integrations. All projects are published as NuGet packages for easy integration into other applications.

## Projects

### Common Libraries
- **Hazina.Tools.Common.Models** - Shared data models and DTOs
- **Hazina.Tools.Common.Utilities** - Common utilities and extensions
- **Hazina.Tools.Common.Infrastructure.AspNetCore** - ASP.NET Core infrastructure components

### Core Libraries
- **Hazina.Tools.Core** - Core functionality
- **Hazina.Tools.Models** - Domain models
- **Hazina.Tools.Data** - Data access layer
- **Hazina.Tools.AI.Agents** - AI agent implementations

### Services
- **Hazina.Tools.Services** - Main service orchestration
- **Hazina.Tools.Services.BigQuery** - Google BigQuery integration
- **Hazina.Tools.Services.Chat** - Chat services
- **Hazina.Tools.Services.ContentRetrieval** - Content retrieval services
- **Hazina.Tools.Services.Embeddings** - Vector embeddings services
- **Hazina.Tools.Services.FileOps** - File operations
- **Hazina.Tools.Services.Intake** - Content intake services
- **Hazina.Tools.Services.Prompts** - Prompt management
- **Hazina.Tools.Services.Social** - Social media integrations
- **Hazina.Tools.Services.Store** - Storage services
- **Hazina.Tools.Services.Web** - Web scraping and interaction
- **Hazina.Tools.Services.WordPress** - WordPress integration

### Text Extraction
- **Hazina.Tools.TextExtraction** - Text extraction from various file formats (PDF, Word, Excel, images)

## Building the Solution

```bash
# Restore dependencies
dotnet restore HazinaTools.sln

# Build all projects
dotnet build HazinaTools.sln

# Build in Release mode
dotnet build HazinaTools.sln -c Release
```

## Publishing NuGet Packages

All projects are configured to build NuGet packages. The publish script automatically publishes to NuGet.org if you have set the API key.

### Setup (One-time)

Set your NuGet API key as an environment variable:

**Windows (PowerShell):**
```powershell
setx NUGET_API_KEY "your-api-key-here"
```

**Linux/macOS:**
```bash
export NUGET_API_KEY="your-api-key-here"
# Add to ~/.bashrc or ~/.zshrc for persistence
echo 'export NUGET_API_KEY="your-api-key-here"' >> ~/.bashrc
```

Get your API key from: https://www.nuget.org/account/apikeys

### Usage

**Windows (PowerShell):**
```powershell
./publish-nuget.ps1
```

**Linux/macOS (Bash):**
```bash
./publish-nuget.sh
```

The script will:
1. Build all projects in Release mode
2. Create NuGet packages in `./nupkgs` directory
3. **Automatically publish to NuGet.org** (if NUGET_API_KEY is set)

If the API key is not set, packages are built but not published.

## For AI Assistants / LLMs

If you're an AI assistant working on this codebase, please read [LLM-INSTRUCTIONS.md](LLM-INSTRUCTIONS.md) for important guidelines about:
- NuGet package publishing requirements
- Project structure conventions
- Version management
- Testing procedures

## Requirements

- .NET 8.0 SDK or later
- Windows (projects target net8.0-windows)

## License

[Specify your license here]

---

## Consumer Projects and Multi-Repository Setup

This HazinaTools repository is part of a multi-repository development environment. Consumer projects reference these libraries either as NuGet packages (production) or as local project references (development).

### Known Consumer Projects

#### Client Manager (HazinaStoreAPI)
- **Location**: `C:\projects\client-manager`
- **Purpose**: .NET 8 API application for AI-powered content generation and management
- **Integration**: Uses all HazinaTools services (Store, Chat, BigQuery, WordPress, etc.) along with Hazina LLM libraries

The client-manager project demonstrates two integration patterns:

1. **Production Pattern** (`ClientManager.sln`):
   - Uses published NuGet packages (version 1.0.16)
   - Suitable for CI/CD and production deployments
   - Cannot step through library source code when debugging

2. **Development Pattern** (`ClientManager.local.sln`):
   - Uses local project references to this repository
   - Full debugging support with symbols
   - Ideal for library development and troubleshooting

### Local Development Workflow

When working on HazinaTools libraries with a consumer project:

1. **Directory Structure**:
   ```
   C:\projects\
   ├── devgpt\              (companion LLM framework)
   ├── hazina\         (this repository)
   └── client-manager\      (consumer API project)
   ```

2. **Making Changes**:
   - Make changes to HazinaTools libraries in `C:\projects\hazina`
   - Open consumer project using its `.local.sln` file
   - Rebuild the consumer solution - changes are immediately reflected
   - Debug with full symbol support

3. **Publishing Updates**:
   ```bash
   cd C:\projects\hazina
   # Update version numbers in .csproj files
   ./publish-nuget.ps1  # Windows
   # or
   ./publish-nuget.sh   # Linux/macOS
   ```
   
   Then update consumer project's `.csproj` to use new package versions.

### Project Reference Structure

When using local development mode, the consumer project references all HazinaTools projects:

**Common Libraries**:
- Hazina.Tools.Common.Infrastructure.AspNetCore
- Hazina.Tools.Common.Models

**Core Libraries**:
- Hazina.Tools.Core
- Hazina.Tools.Models
- Hazina.Tools.Data
- Hazina.Tools.AI.Agents

**Service Libraries**:
- Hazina.Tools.Services (main orchestration)
- Hazina.Tools.Services.BigQuery
- Hazina.Tools.Services.Chat
- Hazina.Tools.Services.ContentRetrieval
- Hazina.Tools.Services.FileOps
- Hazina.Tools.Services.Intake
- Hazina.Tools.Services.Prompts
- Hazina.Tools.Services.Social
- Hazina.Tools.Services.Store
- Hazina.Tools.Services.Web
- Hazina.Tools.Services.WordPress

All these projects are referenced with `.local.csproj` variants in the consumer's local solution.

### Debugging Symbol Loading Issue

**Important**: Consumer projects must use the `.local.sln` solution file to debug into HazinaTools library code.

**Problem**: When debugging HazinaStoreAPI using `ClientManager.sln`, symbols for `Hazina.Tools.Services.Store` and other libraries don't load.

**Solution**: 
1. Close the standard solution in Visual Studio
2. Open `C:\projects\client-manager\ClientManager.local.sln`
3. Rebuild the entire solution to ensure all local projects build with symbols
4. Start debugging - symbols will now load properly

**Why**: 
- Standard `.sln` files reference compiled NuGet package DLLs without source code or full debugging symbols
- `.local.sln` files reference actual source code projects with full PDB symbols
- The `.local` solution pattern was specifically created for local development with debugging support

### Related Repositories

- **Hazina**: Core agentic framework, LLM clients, stores, and generator (`C:\projects\devgpt`)
- **HazinaTools** (this repo): Content generation services and integrations
- **Client Manager**: Consumer API that integrates both Hazina and HazinaTools (`C:\projects\client-manager`)

See `C:\projects\client-manager\README.md` for detailed documentation on the multi-repository development workflow.

### For AI Assistants Working on This Codebase

When working with consumer projects:
- **Always check all three repository locations** (`C:\projects\devgpt`, `C:\projects\hazina`, `C:\projects\client-manager`)
- **Use the `.local.sln` files** for development and debugging
- **Understand the dual solution pattern**: standard for production, local for development
- **Reference the LLM-INSTRUCTIONS.md** in this repository for NuGet publishing guidelines
- **Test changes locally** before publishing new package versions

