# Contributing to HazinaCoder

Thanks for your interest in improving HazinaCoder!

## Quick Start

```bash
# Fork and clone
git clone https://github.com/your-fork/hazina.git
cd hazina/apps/CLI/Hazina.App.HazinaCoder

# Create branch
git checkout -b feature/your-feature

# Make changes, add tests
dotnet test

# Commit
git commit -m "feat: your feature"

# Push and create PR
git push origin feature/your-feature
```

## Development Guidelines

### Code Style
- Follow .editorconfig settings
- XML documentation on all public APIs
- Keep methods under 50 lines
- One class per file

### Testing
- ✅ Unit tests required for new features
- ✅ Integration tests for complex flows
- ✅ Target: 80%+ code coverage

### Commit Messages
Use conventional commits:
- `feat:` New features
- `fix:` Bug fixes
- `docs:` Documentation
- `test:` Tests
- `refactor:` Code changes (no behavior change)

### Pull Requests
- Link to issue or describe problem
- Include tests
- Update documentation
- Ensure CI passes

## Architecture

See [POC1-ARCHITECTURE.md](docs/POC1-ARCHITECTURE.md) for system design.

Key principles:
- Event-driven architecture
- Feature flags for gradual rollout
- Dependency injection
- Fail-safe defaults

## Need Help?

- 📖 [README](README.md)
- 🏗️ [Architecture](docs/POC1-ARCHITECTURE.md)
- 💬 [Discussions](https://github.com/hazina-ai/hazina/discussions)
- 🐛 [Issues](https://github.com/hazina-ai/hazina/issues)

**Happy coding!**
