# Bootstrap Hazina from Zero

**Time to first AI call: 5 minutes**

---

## Prerequisites

| Requirement | Version | Check Command |
|-------------|---------|---------------|
| .NET SDK | 9.0+ | `dotnet --version` |
| Git | Any | `git --version` |
| API Key | OpenAI or Anthropic | See Step 3 |

---

## 5-Minute Quick Start

### Step 1: Clone
```bash
git clone https://github.com/your-org/hazina.git
cd hazina
```

### Step 2: Open Solution
```bash
# Recommended for first time:
start Hazina.QuickStart.sln

# Or from command line:
dotnet restore Hazina.QuickStart.sln
```

### Step 3: Set API Key
```bash
# Windows (cmd)
set OPENAI_API_KEY=sk-your-key-here

# Windows (PowerShell)
$env:OPENAI_API_KEY = "sk-your-key-here"

# Or use Anthropic
set ANTHROPIC_API_KEY=sk-ant-your-key-here
```

### Step 4: Run Demo
```bash
dotnet run --project apps/Demos/Hazina.Demo.ConfigurationShowcase
```

### Step 5: Your First AI Call
Create a new console app or add to existing:

```csharp
using Hazina.AI.FluentAPI.Configuration;
using Hazina.AI.FluentAPI.Core;

// One-time setup
QuickSetup.SetupOpenAI(Environment.GetEnvironmentVariable("OPENAI_API_KEY")!);

// Ask anything
var answer = await Hazina.AskAsync("What is the meaning of life?");
Console.WriteLine(answer);
```

---

## Verification Checklist

After setup, verify each component:

| Component | Test Command | Expected |
|-----------|--------------|----------|
| Build | `dotnet build Hazina.QuickStart.sln` | 0 errors |
| Tests | `dotnet test Hazina.QuickStart.sln` | All pass |
| Demo | `dotnet run --project apps/Demos/...` | Output shown |

---

## Common Setup Scenarios

### Scenario A: Just Exploring
```
1. Open Hazina.QuickStart.sln
2. Read src/Core/AI/ENTRYPOINT.md
3. Run any demo project
```

### Scenario B: Building an AI App
```
1. Open Hazina.AI.sln
2. Add reference to Hazina.AI.FluentAPI
3. Use QuickSetup.SetupAndConfigure()
4. Call Hazina.AskAsync() or build orchestrator
```

### Scenario C: Using RAG (Document Search)
```
1. Open Hazina.AI.sln
2. Configure storage (SQLite recommended):
   - Set SqliteSettings.Enabled = true
   - Set SqliteSettings.DatabasePath = "./hazina.db"
3. Index documents with RAGEngine
4. Query with RAGEngine.AskWithContextAsync()
```

### Scenario D: Production Deployment
```
1. Open Hazina.sln (full solution)
2. Configure production storage (PostgreSQL/Supabase)
3. Set up monitoring (Hazina.Production.Monitoring)
4. Enable health checks
5. Deploy with your preferred method
```

---

## Environment Variables

| Variable | Purpose | Required |
|----------|---------|----------|
| `OPENAI_API_KEY` | OpenAI API access | Yes* |
| `ANTHROPIC_API_KEY` | Anthropic API access | Yes* |
| `SUPABASE_URL` | Supabase project URL | For cloud storage |
| `SUPABASE_CONNECTION_STRING` | Database connection | For cloud storage |

*At least one LLM API key required

---

## Troubleshooting

### "API key not found"
```bash
# Verify key is set
echo %OPENAI_API_KEY%    # Windows cmd
echo $env:OPENAI_API_KEY # PowerShell
```

### "Project not found"
```bash
# Restore packages
dotnet restore Hazina.QuickStart.sln
```

### "Build errors"
```bash
# Clean and rebuild
dotnet clean Hazina.QuickStart.sln
dotnet build Hazina.QuickStart.sln
```

### Still stuck?
1. Check `docs/CONFIGURATION_GUIDE.md`
2. Check `docs/ARCHITECTURE.md`
3. Open an issue on GitHub

---

## Next Steps

| Goal | Read |
|------|------|
| Understand architecture | `docs/ARCHITECTURE.md` |
| Learn AI features | `docs/NEUROCHAIN_GUIDE.md` |
| Use RAG | `docs/RAG_GUIDE.md` |
| Build agents | `docs/AGENTS_GUIDE.md` |
| Contribute | `CONTRIBUTING.md` |

---

*Bootstrap complete. Happy building!*
