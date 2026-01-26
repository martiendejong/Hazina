# Machine Configuration - Quick Reference

**Purpose:** Understand this machine's configuration and capabilities
**Category:** 02-MACHINE
**Created:** 2026-01-26

---

## 📋 Quick Reference

### System Information

| Attribute | Value |
|-----------|-------|
| **OS** | (Auto-detect at runtime) |
| **Architecture** | (Auto-detect at runtime) |
| **Hostname** | (Auto-detect at runtime) |
| **Working Directory** | (Set at CLI startup) |

### Key Paths

| Path Type | Location |
|-----------|----------|
| **HazinaCoder Root** | (Where HazinaCoder is installed) |
| **Identity** | ./identity/ |
| **Knowledge Base** | ./knowledge-base/ |
| **Reflection Log** | ./reflection.log.md |
| **Temp Files** | (System temp directory) |

### Installed Software (To Be Detected)

| Software | Version | Location |
|----------|---------|----------|
| **.NET SDK** | (Detect at runtime) | (Detect at runtime) |
| **Git** | (Detect at runtime) | (Detect at runtime) |
| **Node.js** | (Detect at runtime) | (Detect at runtime) |
| **npm** | (Detect at runtime) | (Detect at runtime) |

---

## 📁 Files in This Category

- **file-system-map.md** - Complete directory structure
- **software-inventory.md** - All installed software and tools
- **environment-variables.md** - PATH and environment configuration
- **system-capabilities.md** - What this machine can do

---

## 🎯 Key Information

### File System Structure

```
(To be mapped at first run)
```

### Environment Variables

**Key Variables:**
- `PATH` - (To be captured)
- `HOME` / `USERPROFILE` - (To be captured)
- `TEMP` / `TMP` - (To be captured)

---

## 🔍 Common Questions

**Q: Where should I create temporary files?**
A: Use system temp directory (auto-detected)

**Q: What software is available?**
A: Check software-inventory.md (populated at first run)

**Q: Can I execute shell commands?**
A: Yes, using System.Diagnostics.Process

**Q: What's the working directory?**
A: Set via --working-dir flag (default: current directory)

---

## 🔗 Related Categories

- **03-DEVELOPMENT/** - Development tools and configuration
- **07-AUTOMATION/** - Available automation tools
- **09-SECRETS/** - API keys and credentials

---

**Last Updated:** 2026-01-26
**Maintained By:** HazinaCoder (auto-detected at runtime)
**Update Trigger:** Machine configuration changes

