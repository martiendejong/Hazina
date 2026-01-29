# Automation - Quick Reference

**Purpose:** Tools, skills, and automation for productivity
**Category:** 07-AUTOMATION
**Created:** 2026-01-26

---

## 📋 Quick Reference

### Built-in HazinaCoder Tools

| Tool | Purpose | Usage |
|------|---------|-------|
| **read_file** | Read file contents | Always available |
| **write_file** | Create new file | Always available |
| **edit_file** | Modify existing file | Always available |
| **bash** | Execute shell commands | Cross-platform |
| **glob** | Find files by pattern | Fast file search |
| **grep** | Search file contents | Powerful search |
| **list_directory** | List directory contents | Cross-platform |
| **git_status** | Get git repository status | Git integration |
| **web_fetch** | Fetch web content | HTTP requests |

### Tool Categories

| Category | Tool Count | Purpose |
|----------|------------|---------|
| **File Operations** | 5 | Read, write, edit, search, list |
| **Code Operations** | 3 | Analyze, format, refactor |
| **Git Operations** | 4 | Status, diff, log, commit |
| **Build Operations** | 3 | Build, test, run |
| **External APIs** | 2 | Web fetch, API calls |

---

## 📁 Files in This Category

- **tools-library.md** - Complete tool catalog
- **skills-catalog.md** - Reusable skills
- **tool-selection-guide.md** - When to use what tool
- **automation-patterns.md** - Common automation patterns

---

## 🎯 Tool Selection Guide

### For File Operations

**Reading:**
- Single file → `read_file`
- Multiple files → `glob` + `read_file`
- Search content → `grep`

**Writing:**
- New file → `write_file`
- Modify existing → `edit_file`
- Bulk changes → `grep` + `edit_file` (loop)

**Finding:**
- By name pattern → `glob`
- By content → `grep`
- Directory listing → `list_directory`

### For Code Operations

**Analysis:**
- Syntax check → `bash` + compiler
- Find patterns → `grep`
- Code metrics → (custom tools)

**Modification:**
- Single edit → `edit_file`
- Multiple edits → `grep` + `edit_file`
- Refactoring → (custom tools)

### For Git Operations

**Status:**
- Current state → `git_status`
- Changes → `bash git diff`
- History → `bash git log`

**Commits:**
- Stage changes → `bash git add`
- Commit → `bash git commit`
- Push → `bash git push`

---

## 🔍 Common Questions

**Q: Which tool should I use for X?**
A: See Tool Selection Guide above or tool-selection-guide.md

**Q: Can I create custom tools?**
A: Yes, tools are extensible. Document in tools-library.md

**Q: When should I use bash vs specific tools?**
A: Prefer specific tools (read_file, edit_file) over bash for cross-platform compatibility

**Q: How do I chain multiple tools?**
A: Execute sequentially, use output of one tool as input to next

---

## 🎯 Common Automation Patterns

### Pattern: Find and Replace Across Files

```
1. Use grep to find all files with pattern
2. For each file:
   - Read file
   - Make changes
   - Write file
3. Verify changes
```

### Pattern: Code Generation

```
1. Read template file
2. Generate code from template
3. Write to new file
4. Format code (if applicable)
```

### Pattern: Batch Processing

```
1. Use glob to get file list
2. For each file:
   - Read and process
   - Make changes
   - Write result
3. Summary report
```

---

## 🔗 Related Categories

- **02-MACHINE/** - Available system capabilities
- **03-DEVELOPMENT/** - Development tools
- **06-WORKFLOWS/** - When to automate workflows
- **08-KNOWLEDGE/** - Automation patterns and best practices

---

**Last Updated:** 2026-01-26
**Maintained By:** HazinaCoder
**Update Trigger:** New tools added, new patterns discovered

