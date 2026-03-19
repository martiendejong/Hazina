# Hazina Code Examples

**Complete, runnable examples demonstrating Hazina's capabilities**

## Overview

This directory contains production-ready code examples organized by feature area. Each example is fully functional and can be run independently.

## Quick Navigation

### Getting Started
- [Hello World](01-hello-world/) - Your first Hazina application
- [Interactive Chat](02-interactive-chat/) - Conversation loop with history
- [Multi-Provider Setup](03-multi-provider/) - Provider failover and selection

### RAG (Retrieval-Augmented Generation)
- [Basic RAG](04-basic-rag/) - Simple document Q&A
- [Production RAG with PostgreSQL](05-production-rag/) - Scalable RAG with database
- [RAG without Embeddings](06-rag-no-embeddings/) - Metadata-first search
- [High-Confidence RAG](07-high-confidence-rag/) - Neurochain integration

### Agents and Tools
- [Simple Agent](08-simple-agent/) - Autonomous agent basics
- [Agent with Custom Tools](09-agent-tools/) - Tool-using agents
- [Multi-Agent System](10-multi-agent/) - Agent coordination

### Advanced Patterns
- [Dynamic Provider Selection](11-dynamic-providers/) - Task-based routing
- [Cost Optimization](12-cost-optimization/) - Budget management and caching
- [Circuit Breaker Pattern](13-circuit-breaker/) - Fault tolerance
- [Context Engineering](14-context-engineering/) - Dynamic context management

### Production Features
- [Monitoring and Observability](15-monitoring/) - Metrics and logging
- [Tool Provider System](16-tool-providers/) - Custom tool development
- [Hierarchical Agents](17-hierarchical-agents/) - Supervisor pattern
- [Custom Vector Store](18-custom-vector-store/) - Redis implementation

## Prerequisites

All examples require:
- .NET 8.0 or higher (.NET 10.0 recommended)
- At least one LLM API key (OpenAI recommended)

Optional for specific examples:
- PostgreSQL for production RAG examples
- Redis for custom vector store example

## Running Examples

### Option 1: Individual Examples

Each example is a standalone console application:

```bash
cd examples/01-hello-world
dotnet run
```

### Option 2: Example Solution

Build all examples at once:

```bash
dotnet build Hazina.Examples.sln
dotnet run --project examples/04-basic-rag
```

## Configuration

Most examples use environment variables for configuration:

```bash
# Required
export OPENAI_API_KEY=sk-your-key-here

# Optional (for multi-provider examples)
export ANTHROPIC_API_KEY=sk-ant-your-key-here

# Optional (for database examples)
export DATABASE_URL=postgresql://user:password@localhost:5432/hazina
```

## Example Structure

Each example includes:

- **Program.cs** - Main application code
- **README.md** - Detailed explanation and instructions
- **.csproj** - Project configuration with dependencies
- **appsettings.json** - Configuration (when applicable)

## Learning Path

### Beginner (Start Here)
1. [Hello World](01-hello-world/) - Understand basic setup
2. [Interactive Chat](02-interactive-chat/) - Learn message handling
3. [Basic RAG](04-basic-rag/) - Add document context

### Intermediate
4. [Multi-Provider Setup](03-multi-provider/) - Provider management
5. [Agent with Tools](09-agent-tools/) - Tool integration
6. [Production RAG](05-production-rag/) - Database integration

### Advanced
7. [High-Confidence RAG](07-high-confidence-rag/) - Neurochain reasoning
8. [Multi-Agent System](10-multi-agent/) - Agent coordination
9. [Circuit Breaker](13-circuit-breaker/) - Fault tolerance
10. [Hierarchical Agents](17-hierarchical-agents/) - Complex workflows

### Production-Ready
11. [Cost Optimization](12-cost-optimization/) - Budget management
12. [Monitoring](15-monitoring/) - Observability
13. [Custom Vector Store](18-custom-vector-store/) - Custom integrations
14. [Context Engineering](14-context-engineering/) - Performance optimization

## Feature Matrix

| Example | RAG | Agents | Tools | Multi-Provider | Monitoring | Database |
|---------|-----|--------|-------|----------------|------------|----------|
| 01-hello-world | | | | | | |
| 02-interactive-chat | | | | | | |
| 03-multi-provider | | | | ✓ | | |
| 04-basic-rag | ✓ | | | | | |
| 05-production-rag | ✓ | | | | | ✓ |
| 06-rag-no-embeddings | ✓ | | | | | |
| 07-high-confidence-rag | ✓ | | | | | ✓ |
| 08-simple-agent | | ✓ | | | | |
| 09-agent-tools | | ✓ | ✓ | | | |
| 10-multi-agent | | ✓ | | | | |
| 11-dynamic-providers | | | | ✓ | | |
| 12-cost-optimization | | | | ✓ | ✓ | |
| 13-circuit-breaker | | | | ✓ | ✓ | |
| 14-context-engineering | ✓ | | | | | |
| 15-monitoring | | | | ✓ | ✓ | |
| 16-tool-providers | | ✓ | ✓ | | | |
| 17-hierarchical-agents | | ✓ | | | | |
| 18-custom-vector-store | ✓ | | | | | ✓ |

## Common Patterns

### Setup Pattern
```csharp
using Hazina.AI.FluentAPI.Configuration;

var ai = QuickSetup.SetupOpenAI(
    Environment.GetEnvironmentVariable("OPENAI_API_KEY")!
);
```

### Conversation Pattern
```csharp
var messages = new List<HazinaChatMessage>
{
    new() { Role = HazinaMessageRole.User, Text = "Hello!" }
};

var response = await ai.GetResponse(messages);
Console.WriteLine(response.Content.Text);
```

### RAG Pattern
```csharp
var vectorStore = new InMemoryVectorStore();
var rag = new RAGEngine(ai, vectorStore);

await rag.IndexDocumentsAsync(documents);
var response = await rag.QueryAsync("question");
```

### Agent Pattern
```csharp
var agent = new Agent("Assistant", "Helpful agent", ai);
var result = await agent.ExecuteAsync("task");
```

## Troubleshooting

### API Key Issues
```bash
# Check if environment variable is set
echo $OPENAI_API_KEY  # Linux/Mac
echo %OPENAI_API_KEY%  # Windows

# Set temporarily for this session
export OPENAI_API_KEY=your-key  # Linux/Mac
set OPENAI_API_KEY=your-key     # Windows
```

### Package Restore Issues
```bash
# Clear NuGet cache
dotnet nuget locals all --clear

# Restore packages
dotnet restore
```

### Database Connection Issues
```bash
# Test PostgreSQL connection
psql $DATABASE_URL

# Install pgvector extension
CREATE EXTENSION IF NOT EXISTS vector;
```

## Contributing Examples

We welcome additional examples! Guidelines:

1. **Self-contained** - Each example should run independently
2. **Well-documented** - Include detailed README.md
3. **Production-ready** - Use best practices and error handling
4. **Environment-based config** - No hardcoded secrets
5. **Clear purpose** - Demonstrate one concept well

See [Contributing Guide](../../CONTRIBUTING.md) for details.

## Additional Resources

### Documentation
- [Getting Started Guide](../GETTING_STARTED.md)
- [Advanced Scenarios](../ADVANCED_SCENARIOS.md)
- [RAG Guide](../RAG_GUIDE.md)
- [Agents Guide](../AGENTS_GUIDE.md)

### Reference
- [API Documentation](../apidoc/api/index.html)
- [Architecture Guide](../ARCHITECTURE.md)
- [Package Registry](../../PACKAGES_REGISTRY.md)

### Community
- [GitHub Discussions](https://github.com/martiendejong/Hazina/discussions)
- [Issue Tracker](https://github.com/martiendejong/Hazina/issues)
- [Contributing Guidelines](../../CONTRIBUTING.md)

---

**Ready to learn? Start with [Hello World](01-hello-world/)!**
