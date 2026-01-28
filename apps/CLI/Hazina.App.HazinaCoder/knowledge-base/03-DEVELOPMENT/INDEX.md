# Development Environment - Quick Reference

**Purpose:** Understand development tools and configuration
**Category:** 03-DEVELOPMENT
**Created:** 2026-01-26

---

## 📋 Quick Reference

### Git Configuration

| Setting | Value |
|---------|-------|
| **User Name** | (Detect from git config) |
| **User Email** | (Detect from git config) |
| **Default Branch** | main or develop |
| **Remote Origin** | (Project-specific) |

### IDEs & Editors

| Tool | Configuration | Notes |
|------|---------------|-------|
| **VS Code** | (Detect if available) | Preferred for TypeScript/JS |
| **Visual Studio** | (Detect if available) | Preferred for C# |
| **Other** | (Detect at runtime) | |

### Build Systems

| System | Project Types | Command |
|--------|---------------|---------|
| **.NET CLI** | C# projects | `dotnet build` |
| **npm** | Node.js projects | `npm run build` |
| **MSBuild** | C# projects | `msbuild` |

---

## 📁 Files in This Category

- **git-repositories.md** - All git repos on this machine
- **ide-configuration.md** - IDE settings and preferences
- **build-systems.md** - Build tools and configurations
- **testing-infrastructure.md** - Test frameworks and runners

---

## 🎯 Key Information

### Git Workflow

**Branch Strategy:**
- `main` / `develop` - Main branch
- `feature/*` - Feature branches
- `bugfix/*` - Bug fix branches

**Common Commands:**
```bash
git status
git add .
git commit -m "message"
git push origin branch-name
```

### Build Commands by Project Type

**C# (.NET):**
```bash
dotnet restore
dotnet build
dotnet test
dotnet run
```

**TypeScript/JavaScript:**
```bash
npm install
npm run build
npm test
npm start
```

---

## 🔍 Common Questions

**Q: How do I detect the project type?**
A: Check for files: `*.csproj` (C#), `package.json` (Node.js), `*.sln` (Visual Studio)

**Q: What's the default build command?**
A: Depends on project type (see Build Commands above)

**Q: Should I run builds automatically?**
A: Only when explicitly requested or after code changes

**Q: How do I check git status?**
A: Use `git status --porcelain` for machine-readable output

---

## 🔗 Related Categories

- **02-MACHINE/** - Installed software and tools
- **05-PROJECTS/** - Specific project configurations
- **06-WORKFLOWS/** - Development workflows
- **07-AUTOMATION/** - Build automation tools

---

**Last Updated:** 2026-01-26
**Maintained By:** HazinaCoder (detected at runtime)
**Update Trigger:** New repos added, config changes

