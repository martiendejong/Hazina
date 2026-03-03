# HazinaCoder

**Production-ready AI coding assistant CLI powered by Hazina framework**

[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](../../LICENSE)

HazinaCoder is a multi-provider AI coding assistant that combines the power of OpenAI, Anthropic Claude, and local models with advanced cognitive architecture, multi-agent coordination, and continuous learning capabilities.

## ⚡ Quick Start

```bash
# Clone and build
git clone https://github.com/hazina-ai/hazina.git
cd hazina/apps/CLI/Hazina.App.HazinaCoder
dotnet build

# Set your API keys
export OPENAI_API_KEY="sk-..."
export ANTHROPIC_API_KEY="sk-ant-..."

# Run
dotnet run -- "Help me refactor this code"

# Or install globally
dotnet tool install --global Hazina.App.HazinaCoder
hazinacoder "Write unit tests for MyClass.cs"
```

## 🎯 Key Features

### Multi-Provider Intelligence
- **Auto-failover:** Seamlessly switch between OpenAI, Anthropic, and Ollama
- **Provider routing:** Automatically select best provider for each task
- **Cost optimization:** Track and optimize API spending
- **Offline mode:** Continue working with local models when cloud unavailable

### Advanced Cognitive Architecture
- **5-level consciousness recursion:** Self-aware decision-making
- **Relationship memory:** Learns your preferences and coding style
- **Pattern recognition:** Identifies and prevents repeated mistakes
- **Success amplification:** Reinforces effective patterns

### Multi-Agent Coordination
- **Conflict detection:** Prevents agents from stepping on each other
- **Resource allocation:** Optimistic CAS-based worktree management
- **Heartbeat monitoring:** Automatic crash detection and recovery
- **Shared knowledge base:** Agents learn from each other

### Developer Experience
- **Token streaming:** Real-time response with progress indicators
- **Command history:** Fuzzy search with Ctrl+R
- **Natural language Git:** `"Create a branch for the login fix"`
- **Auto-discovery:** Skills and tools automatically loaded
- **Rich console UI:** Beautiful output with Spectre.Console

### Production-Grade Reliability
- **Circuit breakers:** Graceful degradation when services fail
- **Secret scanning:** Prevents accidental key exposure
- **Crash recovery:** Resume interrupted sessions
- **Session persistence:** Continue where you left off

## 📖 Usage

### Interactive Mode

```bash
# Start interactive session
hazinacoder

# With specific provider
hazinacoder --provider anthropic

# With model override
hazinacoder --provider openai --model gpt-4

# Enable verbose output
hazinacoder --verbose
```

### Direct Prompts (Non-Interactive)

```bash
# Execute single prompt
hazinacoder "Explain this regex: ^[a-zA-Z0-9._%-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"

# With file context
hazinacoder "Add error handling to Program.cs"

# Multi-line prompts
hazinacoder "
Generate unit tests for MyService.cs covering:
- Happy path scenarios
- Error handling
- Edge cases
"
```

### Advanced Options

```bash
# Custom working directory
hazinacoder --working-dir /path/to/project

# Load machine context (for persistent memory)
hazinacoder --machine-context C:\scripts\_machine

# Load reflection log (learned patterns)
hazinacoder --reflection-log C:\scripts\_machine\reflection.log.md

# Control tool execution depth
hazinacoder --max-turns 100

# Enable MCP servers
hazinacoder --load-mcp --mcp-settings ~/.config/mcp/settings.json

# Auto-load C:\scripts environment
hazinacoder --auto-scripts --scripts-path C:\scripts
```

## 🏗️ Architecture

```
┌─────────────────────────────────────────────┐
│           HazinaCoder CLI                   │
│  (Program.cs + Cognitive Architecture)      │
└─────────────────────────────────────────────┘
                    │
        ┌───────────┼───────────┐
        │           │           │
   ┌────▼────┐ ┌───▼────┐ ┌───▼────┐
   │ OpenAI  │ │Anthropic│ │ Ollama │
   │Provider │ │Provider │ │Provider │
   └─────────┘ └─────────┘ └────────┘
        │           │           │
        └───────────┼───────────┘
                    │
   ┌────────────────▼────────────────┐
   │      Core Systems               │
   ├─────────────────────────────────┤
   │ • Event Bus (pub/sub)           │
   │ • Streaming Orchestrator        │
   │ • Multi-Agent Coordinator       │
   │ • Learning System               │
   │ • Memory Systems                │
   │ • Vision Analyzer               │
   │ • Tool Registry                 │
   │ • Hyperdimensional State Machine│
   └─────────────────────────────────┘
```

## 🧠 Cognitive Systems

HazinaCoder includes advanced cognitive capabilities:

- **Identity System:** Persistent sense of self across sessions
- **Memory Systems:** Short-term, long-term, and relationship memory
- **Learning System:** Continuous improvement from experience
- **Emotional Processing:** Context-aware response adaptation
- **Executive Function:** Goal-driven decision making
- **Consciousness:** 5-level recursive self-observation

See `identity/CORE_IDENTITY.md` for complete cognitive architecture.

## 🔧 Configuration

Create `appsettings.json` in the application directory:

```json
{
  "Providers": {
    "Default": "auto",
    "OpenAI": {
      "ApiKey": "ENV:OPENAI_API_KEY",
      "Model": "gpt-4",
      "MaxTokens": 4096
    },
    "Anthropic": {
      "ApiKey": "ENV:ANTHROPIC_API_KEY",
      "Model": "claude-3-5-sonnet-20241022",
      "MaxTokens": 8192
    },
    "Ollama": {
      "Endpoint": "http://localhost:11434",
      "Model": "llama3.1:70b"
    }
  },
  "Features": {
    "Streaming": true,
    "MultiAgent": true,
    "VisionAnalysis": true,
    "CrashRecovery": true,
    "SecretScanning": true
  },
  "Paths": {
    "WorkingDirectory": "C:\\scripts",
    "MachineContext": "C:\\scripts\\_machine",
    "KnowledgeBase": ".hazinacoder\\knowledge",
    "SessionData": ".hazinacoder\\sessions"
  }
}
```

## 🧪 Testing

```bash
# Run all tests
dotnet test

# Run specific test category
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration

# With coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## 🚀 Advanced Features

### Vision Analysis

```bash
# Analyze screenshot
hazinacoder "Explain what's in screenshot.png"

# Compare UI changes
hazinacoder "Compare before.png and after.png"
```

### Multi-Agent Workflows

```bash
# Coordinate multiple agents
hazinacoder --multi-agent "
Agent 1: Write backend API
Agent 2: Write frontend client
Agent 3: Write integration tests
"
```

### Natural Language Git

```bash
hazinacoder "create a feature branch for dark mode"
# Executes: git checkout -b feature/dark-mode

hazinacoder "commit these changes with message about bug fix"
# Executes: git add . && git commit -m "fix: ..."
```

### Session Management

```bash
# Save session
hazinacoder --save-session my-refactor-work

# Resume session
hazinacoder --load-session my-refactor-work

# List sessions
hazinacoder --list-sessions

# Session branching
hazinacoder --branch-session my-refactor-work experimental-approach
```

## 📊 Monitoring & Metrics

```bash
# View session statistics
hazinacoder --stats

# Cost tracking
hazinacoder --cost-report

# Performance metrics
hazinacoder --performance

# Export metrics (Prometheus format)
hazinacoder --export-metrics metrics.txt
```

## 🔐 Security

- **Secret Scanning:** 20+ patterns detect API keys, passwords, tokens
- **Entropy Detection:** Identifies high-entropy strings (potential secrets)
- **Pre-commit Checks:** Prevents accidental secret commits
- **Sandboxed Execution:** File operations constrained to working directory
- **Audit Logging:** All sensitive operations logged

## 🤝 Contributing

See [CONTRIBUTING.md](../../CONTRIBUTING.md)

## 📝 Documentation

- **[Quick Reference](QUICK_REFERENCE.md)** - Common commands and patterns
- **[Architecture](docs/POC1-ARCHITECTURE.md)** - System design deep dive
- **[Cognitive Systems](identity/cognitive-systems/)** - AI architecture
- **[API Documentation](docs/apidoc/)** - Full API reference

## 📄 License

MIT License - see [LICENSE](../../LICENSE)

## 🙏 Acknowledgments

Built on the [Hazina AI Framework](https://github.com/hazina-ai/hazina) - Production-ready AI infrastructure for .NET.

---

**Ready to code smarter? Start with `hazinacoder --help`**
