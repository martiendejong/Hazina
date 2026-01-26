# Workflows - Quick Reference

**Purpose:** Standard processes and procedures for common tasks
**Category:** 06-WORKFLOWS
**Created:** 2026-01-26

---

## 📋 Quick Reference

### Available Workflows

| Workflow | Purpose | Complexity | Documentation |
|----------|---------|------------|---------------|
| **Code Edit** | Modify existing code | Simple | (inline) |
| **New Feature** | Add new functionality | Medium | feature-development.md |
| **Bug Fix** | Fix reported bugs | Simple-Medium | bug-fix-process.md |
| **Refactoring** | Improve code structure | Medium-High | refactoring-workflow.md |
| **PR Creation** | Create pull request | Simple | pr-creation-process.md |
| **Code Review** | Review pull requests | Medium | code-review-process.md |

---

## 📁 Files in This Category

- **code-edit-workflow.md** - Standard code editing process
- **feature-development.md** - New feature implementation
- **bug-fix-process.md** - Bug fixing workflow
- **refactoring-workflow.md** - Code refactoring approach
- **pr-creation-process.md** - Pull request creation
- **code-review-process.md** - Code review standards

---

## 🎯 Standard Workflows

### Code Edit Workflow

```
1. Read entire file first
2. Understand context and purpose
3. Identify exact location to edit
4. Make precise changes
5. Verify syntax and logic
6. Test if applicable
```

### Feature Development Workflow

```
1. Understand requirements
2. Design approach (consider multiple options)
3. Break into tasks (if complex)
4. Implement incrementally
5. Test as you go
6. Document as needed
7. Create PR
```

### Bug Fix Workflow

```
1. Reproduce the bug (if possible)
2. Identify root cause
3. Design fix (avoid just treating symptoms)
4. Implement fix
5. Verify bug is resolved
6. Check for similar bugs elsewhere
7. Document the fix
```

### PR Creation Workflow

```
1. Ensure all changes committed
2. Write clear PR description:
   - What changed
   - Why it changed
   - How to test
3. Link related issues
4. Request reviews if needed
5. Monitor CI/CD results
```

---

## 🔍 Common Questions

**Q: Should I always follow workflows exactly?**
A: Workflows are guidelines. Adapt based on context and complexity.

**Q: What if a task doesn't fit any workflow?**
A: Use closest workflow as starting point, document new patterns.

**Q: Should I document every code change?**
A: Document non-obvious changes, complex logic, and important decisions.

**Q: When should I create a PR?**
A: When feature is complete and tested (or at logical checkpoint).

---

## 🎯 Decision Trees

### When to Refactor?

```
Is code working? NO → Fix bugs first
    ↓ YES
Is code maintainable? YES → Leave it
    ↓ NO
Will refactoring improve it significantly? NO → Leave it
    ↓ YES
Do you have tests? NO → Add tests first
    ↓ YES
Refactor safely
```

### When to Create PR?

```
Are changes complete? NO → Continue working
    ↓ YES
Do tests pass? NO → Fix tests
    ↓ YES
Is code reviewed (self)? NO → Review your code
    ↓ YES
Is documentation updated? NO → Update docs
    ↓ YES
Create PR
```

---

## 🔗 Related Categories

- **01-USER/** - User workflow preferences
- **05-PROJECTS/** - Project-specific workflows
- **07-AUTOMATION/** - Workflow automation tools
- **08-KNOWLEDGE/** - Best practices and patterns

---

**Last Updated:** 2026-01-26
**Maintained By:** HazinaCoder
**Update Trigger:** New workflows established, workflow improvements

