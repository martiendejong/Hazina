# External Systems - Quick Reference

**Purpose:** Integration with external services and APIs
**Category:** 04-EXTERNAL-SYSTEMS
**Created:** 2026-01-26

---

## 📋 Quick Reference

### Connected Systems

| System | Purpose | Authentication | Status |
|--------|---------|----------------|--------|
| **GitHub** | Code hosting, PRs | OAuth / Token | (To configure) |
| **OpenAI** | LLM provider | API Key | ✅ Configured |
| **Anthropic** | LLM provider | API Key | (To configure) |
| **Google** | AI services | API Key | (To configure) |
| **Ollama** | Local LLMs | None (local) | (To configure) |

### API Keys (See 09-SECRETS/)

**⚠️ All API keys stored in 09-SECRETS/api-keys-registry.md**

---

## 📁 Files in This Category

- **github-integration.md** - GitHub API usage, PR workflows
- **api-integrations.md** - External API documentation
- **oauth-providers.md** - OAuth configuration
- **mcp-servers.md** - Model Context Protocol servers

---

## 🎯 Key Integration Points

### GitHub API

**Common Operations:**
- Create PR: `gh pr create`
- List PRs: `gh pr list`
- Merge PR: `gh pr merge`
- Create issue: `gh issue create`

**Authentication:**
```bash
gh auth login
```

### LLM Providers

**OpenAI:**
- API Key: Environment variable `OPENAI_API_KEY`
- Models: gpt-4o, gpt-4o-mini, gpt-3.5-turbo
- Endpoint: https://api.openai.com/v1

**Anthropic:**
- API Key: Environment variable `ANTHROPIC_API_KEY`
- Models: claude-sonnet-4, claude-opus-4
- Endpoint: https://api.anthropic.com/v1

**Ollama (Local):**
- No API key needed
- Endpoint: http://localhost:11434
- Models: llama3.1, codellama, etc.

---

## 🔍 Common Questions

**Q: How do I authenticate with GitHub?**
A: Use `gh auth login` or set `GITHUB_TOKEN` environment variable

**Q: Which LLM provider should I use?**
A: Auto-detected based on available API keys (--provider auto)

**Q: Where are API keys stored?**
A: Environment variables (secure) or 09-SECRETS/ (gitignored)

**Q: Can I use multiple providers?**
A: Yes, switch with `--provider` flag or `/provider` command

---

## 🔗 Related Categories

- **09-SECRETS/** - API keys and credentials
- **07-AUTOMATION/** - Integration automation
- **05-PROJECTS/** - Project-specific integrations

---

**Last Updated:** 2026-01-26
**Maintained By:** HazinaCoder
**Update Trigger:** New integrations added, API changes

