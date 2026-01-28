# Hazina Git Hooks - Local Quality Gate

This directory contains git hooks that enforce code quality locally before commits are allowed.

## Quick Setup

```powershell
# Install pre-commit hooks
.\.hooks\setup.ps1

# Uninstall if needed
.\.hooks\setup.ps1 -Uninstall
```

Or manually:
```bash
git config core.hooksPath .hooks
```

## What Gets Checked

The pre-commit hook runs the following checks:

1. **Build Verification** - Ensures the AgentFactory project builds successfully
2. **Unit Tests** - Runs all AgentFactory.Tests to ensure no regressions

## Bypassing Hooks (Emergency Only)

If you need to bypass hooks temporarily:
```bash
git commit --no-verify -m "message"
```

**Note:** This should only be used in emergencies. The CI pipeline will still enforce these checks.

## Extending the Hooks

To add more checks, edit `.hooks/pre-commit` and add your verification commands.

Example additions:
- Code formatting checks
- Linting
- Security scanning
- Additional test projects
