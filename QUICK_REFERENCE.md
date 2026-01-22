# Hazina Coding Standards - Quick Reference

**Version:** 2.0
**Last Updated:** 2026-01-21
**Full Documentation:** [HAZINA_CODING_STANDARDS.md](./HAZINA_CODING_STANDARDS.md)

---

## 🎯 Top 10 Rules (Non-Negotiable)

1. **Cyclomatic Complexity ≤10** per method (HARD LIMIT)
2. **100% XML documentation** for public APIs (HARD REQUIREMENT)
3. **Boy Scout Rule** - Always leave code better than you found it
4. **One primary type per file** (with documented exceptions)
5. **No magic numbers** - Extract to named constants
6. **Methods ≤30 lines** (soft guideline)
7. **Classes ≤10 public methods** (triggers review if exceeded)
8. **Parameters ≤4 per method** (use parameter objects otherwise)
9. **DRY - 3+ uses rule** - Abstract when repeated 3+ times
10. **Async all the way** - No sync-over-async

---

## 📊 Complexity Metrics Quick Table

| Metric | Ideal | Max (Soft) | Max (Hard) | Enforcement |
|--------|-------|------------|------------|-------------|
| **Cyclomatic Complexity** | 1-4 | 8 | 10 | Compiler Error |
| **Cognitive Complexity** | 1-10 | 12 | 15 | SonarQube Error |
| **Nesting Depth** | 1-2 | 2 | 3 | Compiler Error |
| **Lines per Method** | 5-15 | 20 | 30 | Compiler Warning |
| **Methods per Class** | 3-7 | 8-10 | 15 | Code Review |
| **Lines per Class** | 50-200 | 200-300 | 500 | Code Review |
| **Parameters per Method** | 1-3 | 4 | 6 | Use Parameter Object |
| **Test Coverage (New Code)** | 80%+ | 70% | 60% | CI/CD Blocker |
| **Documentation Coverage** | 100% | 100% | 90% | Pre-commit Hook |

---

## ✅ Exception Categories (Context-Aware Rules)

| Category | Exemptions | Requirements |
|----------|------------|--------------|
| **DTOs/ViewModels** | Unlimited properties | ✅ No business logic<br>✅ Getters/setters only |
| **Configuration Classes** | Unlimited properties | ✅ Mirror config files<br>✅ No logic |
| **Builders (Fluent API)** | Unlimited fluent methods | ✅ Return `this` for chaining<br>✅ Single responsibility |
| **Factories** | Unlimited creation methods | ✅ All methods create related types<br>❌ No mixed concerns |
| **Controllers** | Unlimited HTTP actions | ✅ One method per endpoint<br>✅ Delegate to services<br>❌ No business logic |
| **Repositories** | 10-12 CRUD methods | ✅ Standard CRUD operations<br>✅ Specialized queries |
| **Aggregate Roots (DDD)** | 15-20 methods if cohesive | ✅ All methods operate on aggregate<br>✅ High cohesion<br>❌ No external dependencies |
| **Test Classes** | Unlimited test methods | ✅ One test per scenario<br>✅ AAA pattern |
| **EF Configuration** | Unlimited config statements | ✅ Fluent API configuration<br>✅ Declarative only |
| **Startup/DI Registration** | Unlimited registrations | ✅ Service configuration<br>✅ Middleware setup |

---

## 📋 Code Review Checklist (1-Minute Scan)

### Automated (CI/CD)
- ☐ Build succeeds (no warnings)
- ☐ All tests pass
- ☐ Coverage ≥70% for new code
- ☐ No code smells (SonarQube)
- ☐ Documentation 100% for public APIs

### Manual (Human Review)
- ☐ Boy Scout Rule applied?
- ☐ Meaningful names (no abbreviations)?
- ☐ No magic numbers?
- ☐ Single Responsibility Principle?
- ☐ Appropriate abstraction level?
- ☐ Exceptions documented?
- ☐ Thread safety documented (if relevant)?
- ☐ Performance considerations noted (if relevant)?

---

## 🏛️ Common Patterns

### ✅ GOOD - Guard Clauses (Reduce Nesting)
```csharp
public void ProcessOrder(Order order)
{
    if (order == null) return;              // Guard
    if (!order.Items.Any()) return;         // Guard

    foreach (var item in order.Items)       // Level 1
    {
        if (item.Quantity <= 0) continue;   // Guard

        ProcessItem(item);                  // No deep nesting!
    }
}
```

### ✅ GOOD - Parameter Object (Reduce Parameters)
```csharp
public record SearchQuery(
    string Query,
    int Page = 1,
    int PageSize = 20,
    string SortBy = "relevance"
);

public Task<SearchResult> SearchAsync(SearchQuery query) { }
```

### ✅ GOOD - Extract Method for Clarity
```csharp
// Instead of complex condition
if (user.Age >= 18 && user.Status == 1 && user.IsVerified)

// Extract to named method
if (IsEligibleForProcessing(user))

private bool IsEligibleForProcessing(User user) =>
    user.Age >= 18 && user.Status == 1 && user.IsVerified;
```

---

## 🚫 Common Anti-Patterns to Avoid

| Anti-Pattern | Problem | Solution |
|--------------|---------|----------|
| **God Object** | Class does everything (67 methods) | Split into focused classes (SRP) |
| **Magic Numbers** | `if (age < 18)` | `const int MinimumAge = 18` |
| **Primitive Obsession** | `SendEmail(string to, string subject, string body)` | `SendEmail(Email email)` |
| **Shotgun Surgery** | One change → many files | Improve cohesion |
| **Copy-Paste** | Duplicated code | Extract to shared method (DRY) |
| **Leaky Abstraction** | `SqlException` leaks from repository | Throw domain exceptions |
| **Sync-over-Async** | `.Result` or `.Wait()` | Async all the way |
| **Feature Envy** | Method uses other class's data | Move to appropriate class |
| **Ravioli Code** | Over-decomposed (6 methods for simple calc) | Appropriate abstraction level |

---

## 📐 Decision Tree: When to Extract Method?

```
Should I extract this code to a method?

├─ Is it used 2+ times?
│  └─ YES → Extract (DRY principle)
│  └─ NO → Continue ↓

├─ Does extraction improve clarity by naming a concept?
│  └─ YES → Extract (Self-documenting code)
│  └─ NO → Continue ↓

├─ Is nesting depth >3 levels?
│  └─ YES → Extract (Reduce complexity)
│  └─ NO → Continue ↓

├─ Is method >30 lines?
│  └─ YES → Extract (Method too long)
│  └─ NO → Continue ↓

├─ Is Cyclomatic Complexity >10?
│  └─ YES → Extract (Too complex)
│  └─ NO → DON'T EXTRACT (avoid Ravioli Code)
```

---

## 🧪 Testing Quick Guide

### Test Naming
```
Pattern: MethodName_Scenario_ExpectedBehavior

Examples:
✅ GetUserAsync_ValidId_ReturnsUser
✅ GetUserAsync_InvalidId_ThrowsNotFoundException
✅ CreateUserAsync_DuplicateEmail_ThrowsDuplicateException
❌ TestGetUser (not descriptive)
❌ GetUserTest1 (meaningless number)
```

### Test Structure (AAA)
```csharp
[Fact]
public async Task ProcessPayment_ValidPayment_ReturnsSuccess()
{
    // Arrange - Setup
    var payment = new Payment { Amount = 100m };
    _mockGateway.Setup(g => g.ProcessAsync(payment))
        .ReturnsAsync(new PaymentResult { Success = true });

    // Act - Execute
    var result = await _sut.ProcessPaymentAsync(payment);

    // Assert - Verify
    Assert.True(result.Success);
    _mockGateway.Verify(g => g.ProcessAsync(payment), Times.Once);
}
```

### Coverage Targets
- **Critical code (payments, auth, security):** 100%
- **New code:** 70% minimum
- **Existing code (Boy Scout):** Improve when touching

---

## 📝 Documentation Template

### Class Documentation
```csharp
/// <summary>
/// [What the class does - 1-2 sentences]
/// </summary>
/// <remarks>
/// [Optional: Usage examples, thread safety, performance notes]
/// </remarks>
public class UserService { }
```

### Method Documentation
```csharp
/// <summary>
/// [What the method does - 1 sentence]
/// </summary>
/// <param name="userId">[Parameter description]</param>
/// <returns>[Return value description]</returns>
/// <exception cref="UserNotFoundException">[When thrown]</exception>
/// <remarks>
/// [Optional: Performance notes, thread safety, examples]
/// </remarks>
public async Task<User> GetUserAsync(int userId) { }
```

### Complex API with Example
```csharp
/// <summary>
/// Builds and executes a RAG query with configurable options.
/// </summary>
/// <remarks>
/// Example usage:
/// <code>
/// var result = await ragEngine
///     .WithQuery("What is Hazina?")
///     .WithMaxResults(5)
///     .ExecuteAsync();
/// </code>
/// </remarks>
```

---

## 🔧 Enforcement Quick Setup

### .editorconfig (Essential Rules)
```ini
# Cyclomatic Complexity ≤10
dotnet_diagnostic.CA1502.severity = error
dotnet_code_quality.CA1502.threshold = 10

# Max nesting depth = 3
dotnet_diagnostic.CA1506.severity = error
dotnet_code_quality.CA1506.threshold = 3

# XML documentation required
dotnet_diagnostic.CS1591.severity = error
```

### Pre-commit Hook (Husky.Net)
```bash
dotnet format --verify-no-changes
dotnet build /p:TreatWarningsAsErrors=true
dotnet test --filter "Category=Unit" --no-build
```

### Run Code Coverage
```bash
dotnet test /p:CollectCoverage=true \
            /p:CoverletOutputFormat=cobertura \
            /p:Threshold=70 \
            /p:ThresholdType=branch
```

---

## 🎯 Migration Strategy (Boy Scout Rule)

| Priority | Criteria | Action |
|----------|----------|--------|
| **New Code** | All new files/methods | 100% compliance (pre-commit blocks violations) |
| **High Priority** | WordPressProvider, AgentFactory, RAGEngine | Dedicated refactoring sprint |
| **Touching Existing** | Any file you edit | Fix violations in touched sections (5-10 min) |
| **Low Priority** | Untouched legacy code | Track as technical debt, fix opportunistically |

**Boy Scout Protocol:**
1. Before editing: Scan entire file for violations
2. During editing: Fix violations in touched sections
3. After editing: Document remaining violations as TODO-STANDARDS

---

## 🎓 Remember

**Philosophy:** Code is read 10x more than written. Optimize for clarity.

**Priority Order:**
1. Understandability (junior dev can follow in 6 months?)
2. Maintainability (easy to change when requirements shift?)
3. Testability (can unit test without mocking half the framework?)
4. Performance (only optimize with profiler evidence)

**Goal:** Make Hazina the most maintainable, testable, and enjoyable codebase to work with.

---

**Need more detail?** See [HAZINA_CODING_STANDARDS.md](./HAZINA_CODING_STANDARDS.md) (complete 30-50 page guide)

**Technical implementation?** See [ROSLYN_ANALYZER_CONFIG.md](./ROSLYN_ANALYZER_CONFIG.md) (analyzer setup)
