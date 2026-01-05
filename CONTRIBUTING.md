# Contributing to Hazina

Thank you for your interest in contributing to Hazina! This guide will help you get started.

## 🚀 Quick Start for Contributors

### 1. Fork and Clone

```bash
# Fork on GitHub, then clone your fork
git clone https://github.com/YOUR_USERNAME/Hazina.git
cd Hazina

# Add upstream remote
git remote add upstream https://github.com/martiendejong/Hazina.git
```

### 2. Choose Your Solution File

**New to the codebase?** Start with a focused solution:

```bash
# First-time contributors - QuickStart solution
dotnet restore Hazina.QuickStart.sln
dotnet build Hazina.QuickStart.sln

# Working on AI features?
dotnet restore Hazina.AI.sln

# Working on tools/services?
dotnet restore Hazina.Tools.sln
```

See [SOLUTIONS.md](SOLUTIONS.md) for detailed guidance on which solution to use.

### 3. Create a Branch

```bash
# Create feature branch
git checkout -b feature/your-feature-name

# Or bugfix branch
git checkout -b fix/issue-number-description
```

### 4. Make Your Changes

- Write clean, readable code
- Follow existing code style
- Add tests for new features
- Update documentation

### 5. Test Your Changes

```bash
# Run tests for specific projects
dotnet test

# Or test a specific solution
dotnet test Hazina.AI.sln
```

### 6. Commit and Push

```bash
# Stage changes
git add .

# Commit with clear message
git commit -m "feat: add semantic search optimization

- Implement multi-strategy ranking
- Add relevance scoring
- Update RAG guide with examples"

# Push to your fork
git push origin feature/your-feature-name
```

### 7. Create Pull Request

- Go to GitHub and create a Pull Request
- Fill in the PR template
- Link any related issues
- Wait for review

---

## 📋 Commit Message Guidelines

We follow [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <subject>

<body>

<footer>
```

### Types

- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation only
- `style`: Code style changes (formatting, no logic change)
- `refactor`: Code refactoring
- `perf`: Performance improvement
- `test`: Adding/updating tests
- `chore`: Build process, tooling, dependencies

### Examples

```bash
# Feature
git commit -m "feat(rag): add hybrid search with keyword boosting"

# Bug fix
git commit -m "fix(providers): handle rate limit errors correctly"

# Documentation
git commit -m "docs(agents): add workflow coordination examples"

# Performance
git commit -m "perf(neurochain): optimize layer execution with caching"
```

---

## 🏗️ Repository Structure

```
Hazina/
├── src/
│   ├── Core/
│   │   ├── AI/              # AI features (RAG, Agents, Neurochain)
│   │   ├── LLMs/            # LLM providers and interfaces
│   │   ├── LLMs.Providers/  # Provider implementations
│   │   ├── Storage/         # Document and embedding stores
│   │   ├── Security/        # Security components
│   │   └── Observability/   # Monitoring and logging
│   └── Tools/
│       ├── Foundation/      # Core tools (Data, Extensions, etc.)
│       ├── Services/        # Service implementations
│       └── Production/      # Production tooling
├── apps/
│   ├── CLI/                 # Command-line applications
│   ├── Desktop/             # Desktop applications
│   ├── Web/                 # Web applications
│   └── Demos/               # Demo applications
├── Tests/                   # All test projects
└── docs/                    # Documentation

Solution Files:
├── Hazina.sln               # Full solution (all 62 projects)
├── Hazina.QuickStart.sln    # Top 10 essential projects
├── Hazina.Core.sln          # Core infrastructure
├── Hazina.AI.sln            # AI features
├── Hazina.Tools.sln         # Tools and services
└── Hazina.Apps.sln          # Applications
```

---

## 🎯 Areas for Contribution

### High Priority

1. **Documentation**
   - Tutorial improvements
   - Code examples
   - Architecture guides

2. **Tests**
   - Unit tests for new features
   - Integration tests
   - Performance benchmarks

3. **Bug Fixes**
   - Check [GitHub Issues](https://github.com/martiendejong/Hazina/issues)
   - Look for `good first issue` label

### Feature Development

Check [TECHNICAL_ROADMAP.md](TECHNICAL_ROADMAP.md) for planned features:

- Real-time collaboration (WebSockets)
- Advanced code analysis
- Intelligent test generation
- Performance profiler
- Multi-language support

### Current Focus Areas

See [CLAUDE.md](CLAUDE.md) for:
- Recently completed work
- Current implementation status
- Next steps

---

## 🧪 Testing Guidelines

### Writing Tests

```csharp
// Place tests in Tests/ directory matching src/ structure
// Tests/Core/AI/Hazina.AI.RAG.Tests/

using Xunit;

public class RAGEngineTests
{
    [Fact]
    public async Task QueryAsync_WithRelevantDocs_ReturnsAccurateAnswer()
    {
        // Arrange
        var rag = CreateTestRAGEngine();
        await IndexTestDocuments(rag);

        // Act
        var result = await rag.QueryAsync("test question");

        // Assert
        Assert.NotNull(result.Answer);
        Assert.True(result.Confidence > 0.7);
    }
}
```

### Running Tests

```bash
# All tests
dotnet test Hazina.sln

# Specific project
dotnet test Tests/Core/AI/Hazina.AI.RAG.Tests/

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

---

## 📝 Code Style

### C# Guidelines

- Follow Microsoft's [C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use meaningful variable names
- Add XML documentation for public APIs
- Keep methods focused and small

### Example

```csharp
/// <summary>
/// Retrieves documents relevant to the query using semantic search.
/// </summary>
/// <param name="query">The search query.</param>
/// <param name="topK">Number of results to return.</param>
/// <returns>List of relevant documents with similarity scores.</returns>
public async Task<List<ScoredDocument>> RetrieveAsync(string query, int topK = 5)
{
    // Implementation
}
```

---

## 🔍 Code Review Process

### What We Look For

1. **Correctness**: Does it work as intended?
2. **Tests**: Are there tests? Do they pass?
3. **Documentation**: Is it documented?
4. **Performance**: Is it efficient?
5. **Style**: Does it follow conventions?

### Review Timeline

- Initial review: 1-3 days
- Follow-up reviews: 1-2 days
- Merge after approval: same day

---

## 🚨 Reporting Issues

### Before Reporting

1. Search existing issues
2. Try latest version
3. Check documentation

### Creating an Issue

Use our issue templates:

**Bug Report:**
```
**Describe the bug**
A clear description of what the bug is.

**To Reproduce**
Steps to reproduce:
1. ...
2. ...

**Expected behavior**
What you expected to happen.

**Environment**
- OS: [e.g. Windows 11, Ubuntu 22.04]
- .NET Version: [e.g. .NET 9.0]
- Hazina Version: [e.g. 1.0.0]
```

**Feature Request:**
```
**Is your feature request related to a problem?**
A clear description of the problem.

**Describe the solution you'd like**
What you want to happen.

**Describe alternatives you've considered**
Other solutions you've thought about.
```

---

## 💬 Communication

- **GitHub Issues**: Bug reports, feature requests
- **GitHub Discussions**: Questions, ideas, general discussion
- **Pull Requests**: Code contributions

---

## 📚 Helpful Resources

- [.NET Documentation](https://docs.microsoft.com/en-us/dotnet/)
- [SOLUTIONS.md](SOLUTIONS.md) - Solution file guide
- [ARCHITECTURE.md](docs/ARCHITECTURE.md) - System architecture
- [MONOREPO_QUICK_WINS_PLAN.md](MONOREPO_QUICK_WINS_PLAN.md) - Monorepo optimization

---

## ✅ Checklist for Pull Requests

Before submitting your PR:

- [ ] Code follows the project's code style
- [ ] Tests added/updated and passing
- [ ] Documentation updated (README, XML docs, guides)
- [ ] Commit messages follow conventional commits
- [ ] Branch is up to date with main
- [ ] PR description filled out
- [ ] No merge conflicts
- [ ] Build succeeds locally

---

## 🎉 Recognition

Contributors are recognized in:

- [README.md](README.md) Contributors section
- GitHub Contributors page
- Release notes for significant contributions

Thank you for helping make Hazina better! 🚀
