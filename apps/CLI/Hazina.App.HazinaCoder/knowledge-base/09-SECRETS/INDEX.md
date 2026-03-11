# Secrets & Credentials - Quick Reference

**Purpose:** Secure storage of API keys, tokens, and credentials
**Category:** 09-SECRETS
**Created:** 2026-01-26
**⚠️ SECURITY:** All files in this directory are gitignored

---

## 📋 Quick Reference

### API Keys Storage

| Provider | Environment Variable | Alternative | Status |
|----------|---------------------|-------------|--------|
| **OpenAI** | `OPENAI_API_KEY` | api-keys-registry.md | (To configure) |
| **Anthropic** | `ANTHROPIC_API_KEY` | api-keys-registry.md | (To configure) |
| **Google AI** | `GOOGLE_API_KEY` | api-keys-registry.md | (To configure) |
| **GitHub** | `GITHUB_TOKEN` | gh auth | (To configure) |

### Security Practices

| Practice | Status | Notes |
|----------|--------|-------|
| **Gitignore** | ✅ Active | All 09-SECRETS/ files excluded |
| **Environment Variables** | ✅ Preferred | Most secure method |
| **File-based (encrypted)** | ⏳ Future | For additional security |
| **Never in Code** | ✅ Enforced | Never hardcode secrets |

---

## 📁 Files in This Category

**⚠️ ALL FILES GITIGNORED:**
- **api-keys-registry.md** - API keys and credentials (DO NOT COMMIT)
- **oauth-tokens.md** - OAuth tokens and refresh tokens (DO NOT COMMIT)
- **connection-strings.md** - Database connections (DO NOT COMMIT)
- **.gitignore** - Ensures secrets not committed

---

## 🎯 How to Store Secrets

### Method 1: Environment Variables (RECOMMENDED)

**Pros:**
- Most secure
- No risk of accidental commit
- Easy to manage per environment

**Setup:**
```bash
# Linux/macOS
export OPENAI_API_KEY="sk-..."
export ANTHROPIC_API_KEY="sk-ant-..."

# Windows (PowerShell)
$env:OPENAI_API_KEY="sk-..."
$env:ANTHROPIC_API_KEY="sk-ant-..."

# Windows (cmd)
set OPENAI_API_KEY=sk-...
set ANTHROPIC_API_KEY=sk-ant-...
```

### Method 2: File-based (GITIGNORED)

**Pros:**
- Persistent across sessions
- Easy to view and edit

**Cons:**
- Must ensure .gitignore works
- Risk if .gitignore misconfigured

**Usage:**
- Store in `api-keys-registry.md`
- HazinaCoder reads on startup (if env vars not set)

### Method 3: System Keychain (FUTURE)

**Pros:**
- OS-level security
- Encrypted storage

**Cons:**
- Platform-specific
- More complex setup

---

## 🔍 Common Questions

**Q: Where should I store API keys?**
A: Environment variables (most secure) or api-keys-registry.md (gitignored)

**Q: How do I check if API key is set?**
A: HazinaCoder will prompt at startup if required keys missing

**Q: What if I accidentally commit a secret?**
A: Immediately rotate the key, never reuse compromised credentials

**Q: Can I share secrets between projects?**
A: Yes, use environment variables (system-wide or profile-level)

---

## 🎯 API Key Management

### Getting API Keys

**OpenAI:**
1. Visit https://platform.openai.com/api-keys
2. Create new secret key
3. Copy and store securely
4. Set as `OPENAI_API_KEY`

**Anthropic:**
1. Visit https://console.anthropic.com/settings/keys
2. Create new key
3. Copy and store securely
4. Set as `ANTHROPIC_API_KEY`

**GitHub:**
```bash
gh auth login
# Follow prompts
```

### Rotating Keys

**When to rotate:**
- Suspected compromise
- Regular schedule (90 days recommended)
- After team member leaves
- Best practice maintenance

**How to rotate:**
1. Generate new key
2. Update environment variable / registry
3. Test new key works
4. Revoke old key

---

## 🔒 Security Checklist

**✅ Verify Security:**
- [ ] All secrets in environment variables OR gitignored files
- [ ] .gitignore includes `09-SECRETS/*.md` (except INDEX.md)
- [ ] No secrets in code files
- [ ] No secrets in git history
- [ ] API keys have minimum required permissions
- [ ] Regular key rotation scheduled

---

## 🔗 Related Categories

- **04-EXTERNAL-SYSTEMS/** - Systems that need credentials
- **02-MACHINE/** - Environment variable configuration
- **06-WORKFLOWS/** - Secret management workflows

---

## ⚠️ CRITICAL REMINDERS

1. **NEVER commit secrets to git**
2. **ALWAYS use environment variables when possible**
3. **ROTATE compromised keys immediately**
4. **USE minimum required permissions**
5. **VERIFY .gitignore is working**

---

**Last Updated:** 2026-01-26
**Maintained By:** HazinaCoder + User
**Update Trigger:** New APIs integrated, keys rotated

