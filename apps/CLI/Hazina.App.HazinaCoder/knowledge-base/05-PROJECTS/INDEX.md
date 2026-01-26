# Projects - Quick Reference

**Purpose:** Deep understanding of project architecture and structure
**Category:** 05-PROJECTS
**Created:** 2026-01-26

---

## 📋 Quick Reference

### Active Projects

| Project | Type | Language | Framework | Status |
|---------|------|----------|-----------|--------|
| **Hazina Framework** | Library | C# | .NET 9 | ✅ Active |
| **HazinaCoder** | CLI Tool | C# | .NET 9, Hazina | ✅ Active |
| *(User Projects)* | (Various) | (Various) | (Various) | (To discover) |

---

## 📁 Files in This Category

- **hazina-framework.md** - Hazina architecture, patterns, conventions
- **hazinacoder-project.md** - This project (HazinaCoder) architecture
- **user-projects/** - User's project documentation (created on demand)

---

## 🎯 Project: Hazina Framework

### Architecture

**Core Components:**
- `Hazina.LLMs` - LLM provider abstractions
- `Hazina.LLMs.OpenAI` - OpenAI integration
- `Hazina.LLMs.Anthropic` - Anthropic Claude integration
- `Hazina.LLMs.Ollama` - Ollama (local LLM) integration
- `Hazina.Agents.Tools` - Tool system for agents

### Key Interfaces

```csharp
ILLMClient - Base interface for all LLM providers
IToolsContext - Tool registry and execution
```

### Conventions

- Async/await for all I/O operations
- Nullable reference types enabled
- Streaming support for real-time responses
- Tool calling via function calling APIs

---

## 🎯 Project: HazinaCoder

### Architecture

**Core Systems:**
- `Core/Identity/` - Cognitive architecture, persistent identity
- `Core/Memory/` - Memory systems (episodic, semantic, procedural)
- `Core/State/` - Session state management
- `identity/` - Identity documentation (markdown)
- `knowledge-base/` - This knowledge base
- `Program.cs` - Main CLI entry point

### Key Features

- Multi-provider LLM support (OpenAI, Anthropic, Ollama)
- Persistent identity across sessions
- Memory and learning systems
- Tool calling capabilities
- Session state persistence

---

## 🔍 Common Questions

**Q: What is Hazina?**
A: A .NET framework for building LLM-powered agents with multi-provider support

**Q: What is HazinaCoder?**
A: A coding assistant CLI built on Hazina, inspired by Claude Code

**Q: How do I add a new LLM provider?**
A: Implement `ILLMClient` interface and register in provider factory

**Q: Where is project documentation?**
A: See files in this category (05-PROJECTS/)

---

## 🔗 Related Categories

- **03-DEVELOPMENT/** - Development tools and build systems
- **06-WORKFLOWS/** - Project workflows
- **07-AUTOMATION/** - Project-specific tools

---

**Last Updated:** 2026-01-26
**Maintained By:** HazinaCoder
**Update Trigger:** Project structure changes, new projects added

