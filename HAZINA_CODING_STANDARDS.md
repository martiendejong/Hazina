# Hazina Framework Coding Standards and Design Guidelines

**Version:** 2.0
**Last Updated:** 2026-01-21
**Status:** MANDATORY for all code changes
**Expert Panel:** 50 experts (Microsoft .NET team, C# language designers, enterprise architects, technical writers)

---

## Table of Contents

1. [Introduction & Philosophy](#1-introduction--philosophy)
2. [Code Organization Principles](#2-code-organization-principles)
3. [Complexity-Based Metrics (CORE)](#3-complexity-based-metrics-core)
4. [Exception Categories (Context-Aware Rules)](#4-exception-categories-context-aware-rules)
5. [Documentation Standards](#5-documentation-standards)
6. [Testing Standards (100% Coverage Goal)](#6-testing-standards-100-coverage-goal)
7. [Generic Code & Non-Redundancy](#7-generic-code--non-redundancy)
8. [Code Readability](#8-code-readability)
9. [Enforcement Mechanisms](#9-enforcement-mechanisms)
10. [Migration Strategy](#10-migration-strategy)
11. [Architectural Patterns](#11-architectural-patterns)
12. [Common Anti-Patterns](#12-common-anti-patterns)
13. [Performance Guidelines](#13-performance-guidelines)
14. [Security Best Practices](#14-security-best-practices)

---

## 1. Introduction & Philosophy

### 1.1 Framework Vision

**Hazina** is an enterprise-grade AI framework for building intelligent applications. Our quality objectives:

1. **Clarity Over Cleverness** - Code should be self-explanatory
2. **Maintainability First** - Optimize for long-term maintenance
3. **Testability by Design** - All code must be unit testable
4. **Performance When Needed** - Optimize based on profiler evidence
5. **Security by Default** - Security is non-negotiable

### 1.2 Why Coding Standards Matter for Hazina

**Current State Analysis (2026-01-21):**
- **938 C# files**, 40,627 lines of code
- **~50 files violate SRP** (Single Responsibility Principle)
- **15+ classes exceed 30 methods** (largest: WordPressProvider with 67 methods)
- **~20 methods exceed 50 lines** of code
- **Test coverage: <1%** (only 3 actual test files for 1,665 classes)
- **Documentation: 77.6%** (good baseline, targeting 100%)

**Problem Areas Identified:**
- `AgentFactory.cs` - God object with multiple responsibilities
- `WordPressProvider.cs` - 67 methods in single class
- `ToolExecutor.cs` - Complex branching, high cognitive load
- `RAGEngine.cs` - Mixed concerns (retrieval + generation + orchestration)

**Goal:** Transform Hazina into a model framework where every class, method, and line of code exemplifies best practices.

### 1.3 Relationship to Boy Scout Rule

These standards **extend** the Boy Scout Rule ("leave code better than you found it"):

- **Boy Scout Rule** = Incremental improvement mindset
- **Coding Standards** = Concrete targets for what "better" means

**Together they create:**
1. **Immediate value** - Standards define the destination
2. **Continuous improvement** - Boy Scout Rule provides the journey
3. **Sustainable quality** - Every change moves toward standards compliance

---

## 2. Code Organization Principles

### 2.1 One Primary Type Per File

**RULE:** Each file should contain ONE primary public type (class, interface, enum, struct).

**Rationale:** Improves discoverability, reduces cognitive load, enables faster navigation.

#### ✅ GOOD - Single primary type
```csharp
// File: UserService.cs
public class UserService
{
    private readonly IUserRepository _repository;
    // ... implementation
}
```

#### ❌ BAD - Multiple unrelated types
```csharp
// File: Services.cs
public class UserService { /* ... */ }
public class OrderService { /* ... */ }
public class PaymentService { /* ... */ }
```

#### ✅ ALLOWED EXCEPTION - Nested helper types
```csharp
// File: UserService.cs
public class UserService
{
    // Nested private class for internal use only
    private class UserValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; }
    }
}
```

#### ✅ ALLOWED EXCEPTION - Related DTOs/Models
```csharp
// File: UserModels.cs
public class CreateUserRequest { /* ... */ }
public class UpdateUserRequest { /* ... */ }
public class UserResponse { /* ... */ }
```

### 2.2 File Naming Conventions

| Type | File Name Pattern | Example |
|------|-------------------|---------|
| Class | `ClassName.cs` | `UserService.cs` |
| Interface | `IInterfaceName.cs` | `IUserRepository.cs` |
| Enum | `EnumName.cs` | `UserRole.cs` |
| Struct | `StructName.cs` | `Point.cs` |
| Record | `RecordName.cs` | `UserRecord.cs` |
| Multiple related DTOs | `PluralName.cs` or `EntityModels.cs` | `UserModels.cs` |

**RULE:** File name MUST match primary type name exactly (case-sensitive).

### 2.3 Namespace Organization

**RULE:** Namespace must reflect folder structure.

```
Project: Hazina.Tools.Services.Chat
Folder:  /Tools/Services/Chat/Providers/
File:    OpenAIProvider.cs

Namespace: Hazina.Tools.Services.Chat.Providers
```

**Namespace Depth Guidelines:**
- **Minimum:** 2 levels (`Hazina.Tools`)
- **Maximum:** 5 levels (`Hazina.Tools.Services.Chat.Providers.OpenAI`)
- **Recommended:** 3-4 levels

#### ❌ BAD - Inconsistent namespace
```csharp
// File: /Tools/Services/Chat/Providers/OpenAIProvider.cs
namespace Hazina.Chat.Stuff // Wrong!
{
    public class OpenAIProvider { }
}
```

#### ✅ GOOD - Consistent namespace
```csharp
// File: /Tools/Services/Chat/Providers/OpenAIProvider.cs
namespace Hazina.Tools.Services.Chat.Providers
{
    public class OpenAIProvider { }
}
```

### 2.4 Project Structure Guidelines

**Standard Hazina Project Structure:**

```
Hazina.Component/
├── Core/                    # Core business logic
│   ├── Interfaces/         # Public contracts
│   ├── Models/             # Domain models
│   └── Services/           # Core services
├── Infrastructure/         # External dependencies
│   ├── Persistence/        # Data access
│   ├── Configuration/      # Config classes
│   └── External/           # Third-party integrations
├── Extensions/             # Extension methods
├── Exceptions/             # Custom exceptions
└── Utilities/              # Helper classes
```

**Folder Naming:**
- Use PascalCase for all folders
- Pluralize collection folders (e.g., `Services`, `Models`, `Providers`)
- Use singular for concept folders (e.g., `Core`, `Infrastructure`)

---

## 3. Complexity-Based Metrics (CORE)

**REJECTED APPROACH:** Strict 7/7 Rule (max 7 methods per class, 7 lines per method)
**REASON:** Context-blind, penalizes legitimate patterns (DTOs, builders, repositories)

**ADOPTED APPROACH:** Complexity-based metrics with context-aware exceptions

### 3.1 Method Complexity Metrics

#### 3.1.1 Cyclomatic Complexity ≤10 (HARD LIMIT)

**Definition:** Count of independent paths through code (decision points).

**Calculation:** Count each:
- `if`, `else if`
- `while`, `for`, `foreach`, `do-while`
- `case` (in switch)
- `&&`, `||` (logical operators)
- `?:` (ternary operator)
- `catch` (exception handlers)
- `goto` (if you dare)

**Enforcement:**
```xml
<!-- .editorconfig -->
dotnet_diagnostic.CA1502.severity = error
dotnet_code_quality.CA1502.threshold = 10
```

#### ❌ BAD - Cyclomatic Complexity = 15
```csharp
public bool ValidateUser(User user, bool strict)
{
    if (user == null) return false;                    // +1
    if (string.IsNullOrEmpty(user.Email)) return false; // +1
    if (user.Email.Contains("@") == false) return false; // +1

    if (strict)                                         // +1
    {
        if (user.Age < 18) return false;                // +1
        if (user.Country == "US" && user.State == null) return false; // +2
        if (user.PhoneNumber == null || user.PhoneNumber.Length < 10) return false; // +2
    }

    if (user.Role == "Admin" || user.Role == "SuperAdmin") // +2
    {
        if (user.Permissions == null || user.Permissions.Count == 0) return false; // +2
    }

    return true;
}
// Total: 15 (EXCEEDS LIMIT)
```

#### ✅ GOOD - Refactored to Cyclomatic Complexity ≤10 per method
```csharp
public bool ValidateUser(User user, bool strict)
{
    if (!ValidateBasicFields(user)) return false;       // +1
    if (strict && !ValidateStrictRequirements(user)) return false; // +2
    if (IsAdminRole(user) && !ValidateAdminPermissions(user)) return false; // +2

    return true;
}
// Total: 5 (PASS)

private bool ValidateBasicFields(User user)
{
    if (user == null) return false;                    // +1
    if (string.IsNullOrEmpty(user.Email)) return false; // +1
    if (!user.Email.Contains("@")) return false;        // +1

    return true;
}
// Total: 3 (PASS)

private bool ValidateStrictRequirements(User user)
{
    if (user.Age < 18) return false;                    // +1
    if (user.Country == "US" && user.State == null) return false; // +2
    if (string.IsNullOrEmpty(user.PhoneNumber) || user.PhoneNumber.Length < 10) return false; // +2

    return true;
}
// Total: 5 (PASS)

private bool ValidateAdminPermissions(User user)
{
    return user.Permissions != null && user.Permissions.Count > 0; // +2
}
// Total: 2 (PASS)

private bool IsAdminRole(User user)
{
    return user.Role == "Admin" || user.Role == "SuperAdmin"; // +2
}
// Total: 2 (PASS)
```

#### 3.1.2 Cognitive Complexity ≤15 (ENFORCED via SonarQube)

**Definition:** Measures how difficult code is to understand (human-centric metric).

**Calculation:** More complex than cyclomatic complexity:
- Linear code flow: +0
- Nesting (each level): +1 per level
- Breaking structures (`break`, `continue`, `goto`, `return`): +1
- Recursion: +1
- Sequences of logical operators: +1 per sequence

**Why Cognitive > Cyclomatic:**
```csharp
// Same Cyclomatic Complexity, different Cognitive Complexity

// Example A - Low Cognitive (3)
if (a) return; // +1
if (b) return; // +1
if (c) return; // +1

// Example B - High Cognitive (8)
if (a)         // +1
{
    if (b)     // +2 (nested)
    {
        if (c) // +3 (doubly nested)
        {
            return; // +1 (break)
        }
    }
}
```

**Enforcement:** Via SonarQube (see section 9.2)

#### 3.1.3 Nesting Depth ≤3 (HARD LIMIT)

**RULE:** Maximum 3 levels of nesting (excluding class/method declarations).

**Enforcement:**
```xml
<!-- .editorconfig -->
dotnet_diagnostic.CA1505.severity = error
dotnet_code_quality.CA1505.threshold = 3
```

#### ❌ BAD - Nesting depth = 5
```csharp
public void ProcessOrder(Order order)
{
    if (order != null)                     // Level 1
    {
        if (order.Items.Any())             // Level 2
        {
            foreach (var item in order.Items) // Level 3
            {
                if (item.Quantity > 0)     // Level 4
                {
                    if (item.Price > 0)    // Level 5 - TOO DEEP!
                    {
                        ProcessItem(item);
                    }
                }
            }
        }
    }
}
```

#### ✅ GOOD - Reduced nesting with guard clauses
```csharp
public void ProcessOrder(Order order)
{
    if (order == null) return;                    // Guard clause
    if (!order.Items.Any()) return;               // Guard clause

    foreach (var item in order.Items)             // Level 1
    {
        if (item.Quantity <= 0 || item.Price <= 0) continue; // Guard within loop

        ProcessItem(item);                        // Level 1 (no deeper nesting)
    }
}
```

#### 3.1.4 Lines of Code ≤30 per Method (SOFT GUIDELINE)

**RULE:** Methods should not exceed 30 lines of code (excluding braces, blank lines, comments).

**Status:** Soft guideline (warning, not error)

**Enforcement:**
```xml
<!-- .editorconfig -->
dotnet_diagnostic.CA1502.severity = warning
dotnet_code_quality.max_statements_per_method = 30
```

**Exceptions:**
- Test methods (see section 4.8)
- EF Core configuration methods (see section 4.9)
- ASP.NET startup configuration (see section 4.10)

### 3.2 Class Complexity Metrics

#### 3.2.1 Public Methods ≤10 per Class (TRIGGERS REVIEW)

**RULE:** Classes with >10 public methods require architectural review.

**Rationale:** High method count indicates potential SRP violation.

**Thresholds:**
- **1-7 methods:** Ideal
- **8-10 methods:** Acceptable
- **11-15 methods:** Review required (document justification)
- **16+ methods:** Refactor required (exception categories apply)

**Enforcement:**
```xml
<!-- .editorconfig -->
dotnet_diagnostic.CA1505.severity = warning
dotnet_code_quality.CA1505.max_methods = 10
```

#### 3.2.2 Class Size ≤300 Lines (SOFT), ≤500 Lines (REQUIRES JUSTIFICATION)

**RULE:** Classes should be under 300 lines; exceeding 500 lines requires documented justification.

**Line Count:** Includes all code, comments, braces, whitespace.

**Thresholds:**
- **0-200 lines:** Ideal
- **201-300 lines:** Acceptable
- **301-500 lines:** Review required
- **501+ lines:** Refactor required (exception categories apply)

**Enforcement:**
```xml
<!-- .editorconfig -->
dotnet_diagnostic.CA1505.severity = warning
dotnet_code_quality.CA1505.max_lines = 300
```

#### ❌ BAD - WordPressProvider.cs (67 methods, 850 lines)
```csharp
public class WordPressProvider
{
    // 67 public methods violating SRP
    public Task<Post> CreatePost() { }
    public Task<Post> UpdatePost() { }
    public Task<Post> DeletePost() { }
    public Task<Post> GetPost() { }
    public Task<List<Post>> ListPosts() { }
    // ... 62 more methods covering posts, pages, media, users, comments, etc.
}
```

#### ✅ GOOD - Refactored to focused classes
```csharp
// WordPressClient.cs (orchestrator)
public class WordPressClient
{
    private readonly IWordPressPostService _postService;
    private readonly IWordPressMediaService _mediaService;
    private readonly IWordPressUserService _userService;

    public IWordPressPostService Posts => _postService;
    public IWordPressMediaService Media => _mediaService;
    public IWordPressUserService Users => _userService;
}

// WordPressPostService.cs (8 methods - focused)
public class WordPressPostService : IWordPressPostService
{
    public Task<Post> CreateAsync(CreatePostRequest request) { }
    public Task<Post> GetAsync(int id) { }
    public Task<Post> UpdateAsync(int id, UpdatePostRequest request) { }
    public Task DeleteAsync(int id) { }
    public Task<List<Post>> ListAsync(ListPostsQuery query) { }
    public Task<Post> PublishAsync(int id) { }
    public Task<Post> UnpublishAsync(int id) { }
    public Task<List<Post>> SearchAsync(string query) { }
}
```

#### 3.2.3 Parameters ≤4 per Method (USE PARAMETER OBJECTS)

**RULE:** Methods with >4 parameters should use parameter objects.

**Rationale:** Reduces cognitive load, improves maintainability, enables parameter validation.

#### ❌ BAD - 7 parameters
```csharp
public async Task<SearchResult> SearchAsync(
    string query,
    int page,
    int pageSize,
    string sortBy,
    bool ascending,
    DateTime? fromDate,
    DateTime? toDate)
{
    // Implementation
}
```

#### ✅ GOOD - Parameter object
```csharp
public class SearchQuery
{
    public string Query { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string SortBy { get; set; } = "relevance";
    public bool Ascending { get; set; } = true;
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    // Validation can be centralized
    public void Validate()
    {
        if (Page < 1) throw new ArgumentException("Page must be >= 1");
        if (PageSize < 1 || PageSize > 100) throw new ArgumentException("PageSize must be 1-100");
    }
}

public async Task<SearchResult> SearchAsync(SearchQuery query)
{
    query.Validate();
    // Implementation
}
```

---

## 4. Exception Categories (Context-Aware Rules)

**Philosophy:** Not all code is created equal. Complexity metrics apply differently based on code purpose.

### 4.1 DTOs and View Models (UNLIMITED PROPERTIES)

**Exemption:** DTOs/ViewModels/Request/Response objects are exempt from class complexity limits.

**Rationale:** Data transfer objects are inherently simple (getters/setters only).

**Allowed:**
- ✅ Unlimited properties
- ✅ Unlimited constructor parameters (for record types)
- ✅ Simple validation attributes

**Not Allowed:**
- ❌ Business logic
- ❌ Complex computed properties
- ❌ External dependencies

#### ✅ ALLOWED - Large DTO
```csharp
/// <summary>
/// Represents a comprehensive user profile for API responses.
/// EXEMPTION: DTOs are exempt from class size limits.
/// </summary>
public class UserProfileResponse
{
    // 30+ properties are fine for DTOs
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string PhoneNumber { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string Address { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string Country { get; set; }
    public string PostalCode { get; set; }
    // ... 20+ more properties
}
```

### 4.2 Configuration Classes (UNLIMITED PROPERTIES/STATEMENTS)

**Exemption:** Configuration classes (appsettings.json models, options patterns) are exempt.

**Rationale:** Configuration objects mirror external config files.

#### ✅ ALLOWED - Large configuration class
```csharp
/// <summary>
/// Application configuration settings.
/// EXEMPTION: Configuration classes exempt from complexity limits.
/// </summary>
public class AppSettings
{
    public DatabaseSettings Database { get; set; }
    public AuthenticationSettings Authentication { get; set; }
    public EmailSettings Email { get; set; }
    public LoggingSettings Logging { get; set; }
    public CacheSettings Cache { get; set; }
    // ... unlimited nested configuration objects
}
```

### 4.3 Builders and Fluent APIs (UNLIMITED FLUENT METHODS)

**Exemption:** Builder pattern classes exempt from method count limits.

**Rationale:** Fluent APIs intentionally have many small methods for readability.

#### ✅ ALLOWED - Large builder
```csharp
/// <summary>
/// Fluent builder for constructing AI agent configurations.
/// EXEMPTION: Builder pattern exempt from method count limits.
/// </summary>
public class AgentBuilder
{
    private readonly AgentConfiguration _config = new();

    // 20+ fluent methods are acceptable
    public AgentBuilder WithModel(string model) { _config.Model = model; return this; }
    public AgentBuilder WithTemperature(double temp) { _config.Temperature = temp; return this; }
    public AgentBuilder WithMaxTokens(int tokens) { _config.MaxTokens = tokens; return this; }
    public AgentBuilder WithSystemPrompt(string prompt) { _config.SystemPrompt = prompt; return this; }
    public AgentBuilder WithTools(params string[] tools) { _config.Tools = tools.ToList(); return this; }
    // ... 15+ more fluent configuration methods

    public Agent Build() => new Agent(_config);
}
```

### 4.4 Factories (UNLIMITED CREATION METHODS)

**Exemption:** Factory classes exempt from method count limits if methods are cohesive.

**Requirement:** Factory must have single responsibility (create objects of related type family).

#### ✅ ALLOWED - Factory with many creation methods
```csharp
/// <summary>
/// Factory for creating LLM provider instances.
/// EXEMPTION: Factory pattern with cohesive creation methods.
/// </summary>
public class LLMProviderFactory
{
    // 15+ creation methods acceptable if all create related types
    public ILLMProvider CreateOpenAI(OpenAIConfig config) { }
    public ILLMProvider CreateAnthropic(AnthropicConfig config) { }
    public ILLMProvider CreateGoogleGemini(GeminiConfig config) { }
    public ILLMProvider CreateMistral(MistralConfig config) { }
    public ILLMProvider CreateHuggingFace(HuggingFaceConfig config) { }
    // ... more LLM provider creation methods
}
```

#### ❌ NOT ALLOWED - God factory (mixed concerns)
```csharp
// VIOLATION: Factory creating unrelated types
public class ServiceFactory
{
    public IEmailService CreateEmailService() { }
    public IDatabaseService CreateDatabaseService() { }
    public IPaymentService CreatePaymentService() { }
    // WRONG: Factory should focus on single concern
}
```

### 4.5 Controllers (UNLIMITED ACTIONS, ONE PER ENDPOINT)

**Exemption:** ASP.NET controllers exempt from method count limits.

**Requirements:**
- ✅ Each method must be a single HTTP endpoint
- ✅ Minimal logic (delegate to services)
- ❌ No business logic in controllers

#### ✅ ALLOWED - Controller with many endpoints
```csharp
/// <summary>
/// RESTful API controller for user management.
/// EXEMPTION: Controllers with one action per endpoint.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    // 15+ endpoints are fine
    [HttpGet]
    public async Task<ActionResult<List<UserResponse>>> GetAll()
        => Ok(await _userService.GetAllAsync());

    [HttpGet("{id}")]
    public async Task<ActionResult<UserResponse>> GetById(int id)
        => Ok(await _userService.GetByIdAsync(id));

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create([FromBody] CreateUserRequest request)
        => CreatedAtAction(nameof(GetById), new { id = result.Id }, result);

    [HttpPut("{id}")]
    public async Task<ActionResult<UserResponse>> Update(int id, [FromBody] UpdateUserRequest request)
        => Ok(await _userService.UpdateAsync(id, request));

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _userService.DeleteAsync(id);
        return NoContent();
    }

    // ... more CRUD endpoints
}
```

### 4.6 Repositories (10-12 CRUD METHODS STANDARD)

**Exemption:** Repository pattern with standard CRUD operations.

**Standard Repository Methods:**
```csharp
public interface IRepository<T> where T : class
{
    // Read operations (5)
    Task<T> GetByIdAsync(int id);
    Task<List<T>> GetAllAsync();
    Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);
    Task<bool> ExistsAsync(int id);

    // Write operations (4)
    Task<T> AddAsync(T entity);
    Task<T> UpdateAsync(T entity);
    Task DeleteAsync(int id);
    Task<int> SaveChangesAsync();

    // Specialized queries (variable)
    Task<PagedResult<T>> GetPagedAsync(int page, int pageSize);
    Task<int> CountAsync(Expression<Func<T, bool>> predicate);
}
```

**Threshold:** 10-12 methods acceptable for full-featured repository.

### 4.7 Aggregate Roots (15-20 METHODS IF COHESIVE, DDD PATTERN)

**Exemption:** Domain-Driven Design aggregate roots with rich domain behavior.

**Requirements:**
- ✅ All methods must operate on aggregate state
- ✅ High cohesion (all methods related to single domain concept)
- ✅ Encapsulate business rules
- ❌ No external dependencies (use domain services instead)

#### ✅ ALLOWED - Rich aggregate root
```csharp
/// <summary>
/// Aggregate root representing an order in the system.
/// EXEMPTION: DDD aggregate root with cohesive domain behavior.
/// </summary>
public class Order
{
    // Rich domain model with 15-20 methods is acceptable
    public void AddItem(Product product, int quantity) { }
    public void RemoveItem(int itemId) { }
    public void UpdateItemQuantity(int itemId, int quantity) { }
    public void ApplyCoupon(Coupon coupon) { }
    public void RemoveCoupon() { }
    public void SetShippingAddress(Address address) { }
    public void SetBillingAddress(Address address) { }
    public void CalculateTotal() { }
    public void Submit() { }
    public void Approve() { }
    public void Reject(string reason) { }
    public void Ship() { }
    public void Deliver() { }
    public void Cancel(string reason) { }
    public void Refund(decimal amount) { }
    // ... all methods operate on Order state
}
```

### 4.8 Test Classes (UNLIMITED TEST METHODS)

**Exemption:** Test classes completely exempt from all complexity metrics.

**Rationale:** Each test method tests a single scenario.

**Requirements:**
- ✅ One test class per implementation class
- ✅ One test method per scenario/edge case
- ✅ Clear test names (`MethodName_Scenario_ExpectedBehavior`)

#### ✅ ALLOWED - Large test class
```csharp
/// <summary>
/// Unit tests for UserService.
/// EXEMPTION: Test classes exempt from all complexity limits.
/// </summary>
public class UserServiceTests
{
    // 50+ test methods are fine
    [Fact]
    public async Task GetUserAsync_ValidId_ReturnsUser() { }

    [Fact]
    public async Task GetUserAsync_InvalidId_ThrowsNotFoundException() { }

    [Fact]
    public async Task CreateUserAsync_ValidUser_CreatesUser() { }

    [Fact]
    public async Task CreateUserAsync_DuplicateEmail_ThrowsDuplicateException() { }

    [Fact]
    public async Task CreateUserAsync_NullUser_ThrowsArgumentNullException() { }

    // ... 45+ more test methods
}
```

### 4.9 EF Core Configuration (UNLIMITED CONFIGURATION STATEMENTS)

**Exemption:** Entity Framework configuration classes (fluent API).

**Rationale:** EF configuration is declarative, not imperative.

#### ✅ ALLOWED - Large EF configuration
```csharp
/// <summary>
/// Entity Framework configuration for User entity.
/// EXEMPTION: EF Core fluent configuration exempt from complexity limits.
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // 50+ configuration statements are fine
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(u => u.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        // ... 40+ more configuration statements
    }
}
```

### 4.10 ASP.NET Startup (UNLIMITED DI REGISTRATION)

**Exemption:** ASP.NET `Startup.cs` / `Program.cs` dependency injection configuration.

**Rationale:** Service registration is declarative.

#### ✅ ALLOWED - Large startup configuration
```csharp
/// <summary>
/// Application startup and dependency injection configuration.
/// EXEMPTION: Startup DI registration exempt from complexity limits.
/// </summary>
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // 100+ service registrations are fine
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IEmailService, EmailService>();
        // ... 90+ more service registrations

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(Configuration.GetConnectionString("Default")));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options => { /* config */ });

        // ... more middleware configuration
    }
}
```

---

## 5. Documentation Standards

**TARGET:** 100% XML documentation coverage for public APIs.

**CURRENT STATE:** 77.6% coverage (32,495 lines documented out of 40,627 total).

### 5.1 Required Documentation

#### 5.1.1 All Public Classes

**RULE:** Every public class MUST have a `<summary>` tag.

```csharp
/// <summary>
/// Manages user authentication and authorization operations.
/// </summary>
public class UserService
{
    // Implementation
}
```

**Required Content:**
- Clear description of class purpose (1-2 sentences)
- What the class does (not how it does it)
- High-level responsibility

#### 5.1.2 All Public Methods

**RULE:** Every public method MUST have:
- `<summary>` - What the method does
- `<param>` - For each parameter
- `<returns>` - What the method returns (if not void)
- `<exception>` - For expected exceptions

```csharp
/// <summary>
/// Retrieves a user by their unique identifier.
/// </summary>
/// <param name="userId">The unique identifier of the user to retrieve.</param>
/// <returns>The user object if found.</returns>
/// <exception cref="UserNotFoundException">Thrown when user ID does not exist.</exception>
public async Task<User> GetUserAsync(int userId)
{
    var user = await _repository.GetByIdAsync(userId);
    if (user == null)
        throw new UserNotFoundException($"User with ID {userId} not found");

    return user;
}
```

#### 5.1.3 All Public Properties (If Behavior Is Non-Obvious)

**RULE:** Document public properties that:
- Have side effects
- Perform computation
- Have non-obvious behavior
- Have constraints/validation

```csharp
/// <summary>
/// Gets or sets the user's email address.
/// Changing this value triggers an email verification workflow.
/// </summary>
/// <exception cref="ArgumentException">Thrown if email format is invalid.</exception>
public string Email
{
    get => _email;
    set
    {
        if (!IsValidEmail(value))
            throw new ArgumentException("Invalid email format");

        _email = value;
        TriggerEmailVerification();
    }
}
```

**Simple properties with obvious behavior can omit documentation:**
```csharp
// Documentation optional for obvious properties
public int Id { get; set; }
public string Name { get; set; }
```

#### 5.1.4 All Interfaces (COMPLETE CONTRACT DOCUMENTATION)

**RULE:** Interfaces must document:
- Purpose of the interface
- Implementation requirements
- Contract guarantees
- Usage examples (for complex interfaces)

```csharp
/// <summary>
/// Defines the contract for user data persistence operations.
/// </summary>
/// <remarks>
/// Implementations must:
/// - Support async operations for all methods
/// - Throw UserNotFoundException when user is not found
/// - Validate user data before persistence
/// - Ensure email uniqueness constraints
///
/// Example usage:
/// <code>
/// var user = await repository.GetByIdAsync(123);
/// user.Email = "new@email.com";
/// await repository.UpdateAsync(user);
/// </code>
/// </remarks>
public interface IUserRepository
{
    /// <summary>
    /// Retrieves a user by their unique identifier.
    /// </summary>
    /// <param name="userId">The unique user identifier.</param>
    /// <returns>The user object.</returns>
    /// <exception cref="UserNotFoundException">Thrown when user does not exist.</exception>
    Task<User> GetByIdAsync(int userId);
}
```

### 5.2 Documentation Quality Guidelines

#### 5.2.1 Explain "Why," Not Just "What"

#### ❌ BAD - States the obvious
```csharp
/// <summary>
/// Gets the user.
/// </summary>
public User GetUser() { }
```

#### ✅ GOOD - Explains purpose and behavior
```csharp
/// <summary>
/// Retrieves the currently authenticated user from the HTTP context.
/// Returns null if no user is authenticated.
/// </summary>
public User GetCurrentUser() { }
```

#### 5.2.2 Include Usage Examples for Complex APIs

```csharp
/// <summary>
/// Builds and executes a RAG (Retrieval-Augmented Generation) query.
/// </summary>
/// <remarks>
/// Example usage:
/// <code>
/// var result = await ragEngine
///     .WithQuery("What is Hazina?")
///     .WithDocumentFilter(doc => doc.Category == "Framework")
///     .WithMaxResults(5)
///     .WithReranking(true)
///     .ExecuteAsync();
///
/// Console.WriteLine(result.Answer);
/// foreach (var source in result.Sources)
/// {
///     Console.WriteLine($"- {source.Title}");
/// }
/// </code>
/// </remarks>
public class RAGQueryBuilder
{
    // Implementation
}
```

#### 5.2.3 Document Exceptions Thrown

```csharp
/// <summary>
/// Processes payment for an order.
/// </summary>
/// <param name="orderId">The order to process payment for.</param>
/// <param name="paymentMethod">The payment method to use.</param>
/// <exception cref="OrderNotFoundException">Thrown when order ID is invalid.</exception>
/// <exception cref="PaymentDeclinedException">Thrown when payment is declined by gateway.</exception>
/// <exception cref="InsufficientFundsException">Thrown when account has insufficient funds.</exception>
/// <exception cref="ArgumentNullException">Thrown when paymentMethod is null.</exception>
public async Task ProcessPaymentAsync(int orderId, PaymentMethod paymentMethod)
{
    // Implementation
}
```

#### 5.2.4 Document Thread Safety Considerations

```csharp
/// <summary>
/// In-memory cache for frequently accessed user data.
/// </summary>
/// <remarks>
/// Thread Safety: This class uses ConcurrentDictionary for thread-safe operations.
/// Multiple threads can safely read and write to the cache concurrently.
/// </remarks>
public class UserCache
{
    private readonly ConcurrentDictionary<int, User> _cache = new();

    // Implementation
}
```

#### 5.2.5 Document Performance Characteristics (If Relevant)

```csharp
/// <summary>
/// Generates embeddings for a collection of documents using the configured LLM provider.
/// </summary>
/// <param name="documents">The documents to generate embeddings for.</param>
/// <returns>A list of embedding vectors.</returns>
/// <remarks>
/// Performance: This method makes one API call per document batch (configurable batch size).
/// For 1000 documents with batch size of 100, expect ~10 API calls and ~30 seconds execution time.
/// Consider using batch processing for large document collections.
/// </remarks>
public async Task<List<float[]>> GenerateEmbeddingsAsync(List<Document> documents)
{
    // Implementation
}
```

### 5.3 Documentation Enforcement

**Pre-commit hook:**
```powershell
# Check for missing documentation
$violations = dotnet build /p:TreatWarningsAsErrors=true /p:DocumentationFile=docs.xml 2>&1 |
    Select-String "CS1591" # Missing XML documentation

if ($violations.Count -gt 0) {
    Write-Error "Missing XML documentation. Please document all public APIs."
    exit 1
}
```

**.editorconfig rules:**
```xml
# Enforce XML documentation
dotnet_diagnostic.CS1591.severity = error # Missing XML documentation
dotnet_diagnostic.SA1600.severity = error # Elements should be documented (StyleCop)
dotnet_diagnostic.SA1602.severity = error # Enumeration items should be documented
```

---

## 6. Testing Standards (100% Coverage Goal)

**CURRENT STATE:** <1% test coverage (3 test files for 1,665 classes)
**TARGET:** 100% coverage for all new code, 70% coverage for existing code (Boy Scout Rule)

### 6.1 Unit Testing Requirements

#### 6.1.1 One Test Class Per Implementation Class

**RULE:** Every implementation class must have a corresponding test class.

**Naming Convention:**
```
Implementation:  UserService.cs
Test Class:      UserServiceTests.cs
Location:        Tests/Services/UserServiceTests.cs
```

**Structure:**
```csharp
namespace Hazina.Tools.Services.Tests
{
    /// <summary>
    /// Unit tests for <see cref="UserService"/>.
    /// </summary>
    public class UserServiceTests
    {
        private readonly Mock<IUserRepository> _mockRepository;
        private readonly Mock<ILogger<UserService>> _mockLogger;
        private readonly UserService _sut; // System Under Test

        public UserServiceTests()
        {
            _mockRepository = new Mock<IUserRepository>();
            _mockLogger = new Mock<ILogger<UserService>>();
            _sut = new UserService(_mockRepository.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task GetUserAsync_ValidId_ReturnsUser()
        {
            // Arrange
            var expectedUser = new User { Id = 1, Email = "test@example.com" };
            _mockRepository.Setup(r => r.GetByIdAsync(1))
                .ReturnsAsync(expectedUser);

            // Act
            var result = await _sut.GetUserAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedUser.Id, result.Id);
            Assert.Equal(expectedUser.Email, result.Email);
        }
    }
}
```

#### 6.1.2 One Test Method Per Scenario/Edge Case

**RULE:** Each test method should test ONE specific scenario.

**Naming Convention:** `MethodName_Scenario_ExpectedBehavior`

**Examples:**
```csharp
[Fact]
public async Task GetUserAsync_ValidId_ReturnsUser() { }

[Fact]
public async Task GetUserAsync_InvalidId_ThrowsUserNotFoundException() { }

[Fact]
public async Task GetUserAsync_NegativeId_ThrowsArgumentException() { }

[Fact]
public async Task GetUserAsync_RepositoryThrowsException_PropagatesException() { }

[Fact]
public async Task CreateUserAsync_ValidUser_ReturnsCreatedUser() { }

[Fact]
public async Task CreateUserAsync_DuplicateEmail_ThrowsDuplicateEmailException() { }

[Fact]
public async Task CreateUserAsync_NullUser_ThrowsArgumentNullException() { }
```

#### 6.1.3 Minimum 70% Branch Coverage for New Code

**RULE:** All new code must achieve minimum 70% branch coverage.

**Measurement:** Use Coverlet + ReportGenerator

```xml
<!-- Test.csproj -->
<ItemGroup>
  <PackageReference Include="coverlet.collector" Version="6.0.0" />
  <PackageReference Include="coverlet.msbuild" Version="6.0.0" />
</ItemGroup>
```

**Run coverage:**
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:Threshold=70 /p:ThresholdType=branch
```

#### 6.1.4 100% Coverage for Critical Business Logic

**RULE:** Critical business logic requires 100% branch coverage.

**Critical Areas:**
- Payment processing
- Authentication/authorization
- Data validation
- Financial calculations
- Security-sensitive operations

#### 6.1.5 Arrange-Act-Assert Pattern

**RULE:** All tests must follow AAA pattern.

```csharp
[Fact]
public async Task ProcessPayment_ValidPayment_ReturnsSuccess()
{
    // Arrange - Set up test data and mocks
    var payment = new Payment { Amount = 100m, Method = "CreditCard" };
    var expectedResult = new PaymentResult { Success = true, TransactionId = "TXN123" };

    _mockGateway.Setup(g => g.ProcessAsync(payment))
        .ReturnsAsync(expectedResult);

    // Act - Execute the method under test
    var result = await _sut.ProcessPaymentAsync(payment);

    // Assert - Verify the outcome
    Assert.True(result.Success);
    Assert.Equal("TXN123", result.TransactionId);
    _mockGateway.Verify(g => g.ProcessAsync(payment), Times.Once);
}
```

### 6.2 Integration Testing Requirements

#### 6.2.1 Test All External Integration Points

**RULE:** Create integration tests for:
- Database operations (EF Core)
- External APIs (HTTP clients)
- Message queues (RabbitMQ, Azure Service Bus)
- File system operations
- Cloud services (Azure Storage, S3)

#### 6.2.2 Test All Database Operations

**RULE:** Integration tests for repositories must test:
- CRUD operations
- Complex queries
- Transactions
- Concurrency handling

**Example:**
```csharp
public class UserRepositoryIntegrationTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public UserRepositoryIntegrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AddAsync_ValidUser_InsertsIntoDatabase()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        var repository = new UserRepository(context);
        var user = new User { Email = "test@example.com", Name = "Test" };

        // Act
        var result = await repository.AddAsync(user);
        await context.SaveChangesAsync();

        // Assert
        Assert.True(result.Id > 0);

        // Verify in fresh context
        await using var verifyContext = _fixture.CreateContext();
        var savedUser = await verifyContext.Users.FindAsync(result.Id);
        Assert.NotNull(savedUser);
        Assert.Equal("test@example.com", savedUser.Email);
    }
}
```

#### 6.2.3 Test All HTTP Endpoints

**RULE:** API integration tests using WebApplicationFactory.

```csharp
public class UsersControllerIntegrationTests : IClassFixture<WebApplicationFactory<Startup>>
{
    private readonly HttpClient _client;

    public UsersControllerIntegrationTests(WebApplicationFactory<Startup> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetUser_ValidId_Returns200WithUser()
    {
        // Act
        var response = await _client.GetAsync("/api/users/1");

        // Assert
        response.EnsureSuccessStatusCode();
        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(user);
        Assert.Equal(1, user.Id);
    }
}
```

### 6.3 Test Naming Conventions

**Pattern:** `MethodName_Scenario_ExpectedBehavior`

**Guidelines:**
- Use underscores to separate parts (easier to read than PascalCase)
- Method name: Exact name of method being tested
- Scenario: Input conditions or context
- Expected behavior: What should happen

**Examples:**
```csharp
// Good test names
CreateUser_ValidUser_ReturnsCreatedUser()
CreateUser_DuplicateEmail_ThrowsDuplicateEmailException()
CreateUser_NullUser_ThrowsArgumentNullException()
ProcessOrder_EmptyCart_ThrowsInvalidOperationException()
CalculateDiscount_ValidCoupon_AppliesDiscount()
CalculateDiscount_ExpiredCoupon_ThrowsCouponExpiredException()

// Bad test names (avoid)
TestCreateUser() // Not descriptive
CreateUserTest1() // Meaningless number
CreateUser() // Missing scenario and expectation
Test_User_Creation() // Too vague
```

### 6.4 Test Organization

**Project Structure:**
```
Hazina.Tools.Services/
├── Services/
│   ├── UserService.cs
│   └── OrderService.cs
└── ...

Hazina.Tools.Services.Tests/
├── Services/
│   ├── UserServiceTests.cs        # Unit tests
│   └── OrderServiceTests.cs
└── Integration/
    ├── UserServiceIntegrationTests.cs
    └── OrderServiceIntegrationTests.cs
```

**Test Categories:**
```csharp
// Unit tests (fast, isolated)
[Trait("Category", "Unit")]
public class UserServiceTests { }

// Integration tests (slower, external dependencies)
[Trait("Category", "Integration")]
public class UserRepositoryIntegrationTests { }

// End-to-end tests (slowest, full stack)
[Trait("Category", "E2E")]
public class UserWorkflowE2ETests { }
```

**Run by category:**
```bash
# Run only unit tests (fast feedback)
dotnet test --filter "Category=Unit"

# Run unit + integration tests
dotnet test --filter "Category=Unit|Category=Integration"

# Run all tests
dotnet test
```

---

## 7. Generic Code & Non-Redundancy

**DRY Principle:** Don't Repeat Yourself

### 7.1 When to Create Abstractions

**3+ Uses Rule:** Create abstraction when code is duplicated 3+ times.

**Rationale:**
- 1 use: Concrete implementation
- 2 uses: Acceptable duplication (wait for third)
- 3+ uses: Extract to shared method/class

#### ❌ BAD - Copy-paste programming (4 duplicates)
```csharp
public class UserController
{
    public IActionResult GetUser(int id)
    {
        if (id <= 0)
            return BadRequest("Invalid ID");

        var user = _service.GetUser(id);
        if (user == null)
            return NotFound();

        return Ok(user);
    }
}

public class OrderController
{
    public IActionResult GetOrder(int id)
    {
        if (id <= 0)
            return BadRequest("Invalid ID");

        var order = _service.GetOrder(id);
        if (order == null)
            return NotFound();

        return Ok(order);
    }
}

// ... duplicated in ProductController, InvoiceController
```

#### ✅ GOOD - Extracted to generic base class
```csharp
public abstract class CrudControllerBase<T> : ControllerBase where T : class
{
    protected async Task<IActionResult> GetByIdAsync<TService>(
        int id,
        TService service,
        Func<TService, int, Task<T>> getter)
    {
        if (id <= 0)
            return BadRequest("Invalid ID");

        var entity = await getter(service, id);
        if (entity == null)
            return NotFound();

        return Ok(entity);
    }
}

public class UserController : CrudControllerBase<User>
{
    public async Task<IActionResult> GetUser(int id)
        => await GetByIdAsync(id, _service, (s, i) => s.GetUserAsync(i));
}

public class OrderController : CrudControllerBase<Order>
{
    public async Task<IActionResult> GetOrder(int id)
        => await GetByIdAsync(id, _service, (s, i) => s.GetOrderAsync(i));
}
```

### 7.2 Generic vs. Specific Implementations

**RULE:** Use generics when behavior is identical across types.

#### ✅ GOOD - Generic repository
```csharp
public interface IRepository<T> where T : class
{
    Task<T> GetByIdAsync(int id);
    Task<List<T>> GetAllAsync();
    Task<T> AddAsync(T entity);
    Task<T> UpdateAsync(T entity);
    Task DeleteAsync(int id);
}

public class Repository<T> : IRepository<T> where T : class
{
    private readonly DbContext _context;
    private readonly DbSet<T> _dbSet;

    public Repository(DbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<T> GetByIdAsync(int id)
        => await _dbSet.FindAsync(id);

    // ... other CRUD operations
}
```

#### ❌ BAD - Specific repository for every entity
```csharp
// Don't create these if logic is identical!
public class UserRepository { /* CRUD */ }
public class OrderRepository { /* CRUD */ }
public class ProductRepository { /* CRUD */ }
// ... 50 more repositories with identical code
```

#### ✅ GOOD - Specific repository only when needed
```csharp
// Use generic repository for simple CRUD
services.AddScoped<IRepository<User>, Repository<User>>();

// Create specific repository ONLY when custom logic is needed
public interface IOrderRepository : IRepository<Order>
{
    // Custom method beyond generic CRUD
    Task<List<Order>> GetPendingOrdersAsync();
    Task<decimal> GetTotalRevenueAsync(DateTime from, DateTime to);
}
```

### 7.3 Avoiding Premature Abstraction

**RULE:** Don't abstract until you have 3+ concrete cases.

**Anti-Pattern:** Creating abstractions "just in case."

#### ❌ BAD - Premature abstraction
```csharp
// Only one implementation exists, but already abstracted
public interface IEmailService { }
public interface IEmailSender { }
public interface IEmailProvider { }
public interface IEmailGateway { }
// Four layers of abstraction for single email provider!
```

#### ✅ GOOD - Concrete first, abstract when needed
```csharp
// Start with concrete implementation
public class SmtpEmailService
{
    public async Task SendAsync(Email email) { /* SMTP logic */ }
}

// When second provider is added, create abstraction
public interface IEmailService
{
    Task SendAsync(Email email);
}

public class SmtpEmailService : IEmailService { }
public class SendGridEmailService : IEmailService { }
```

### 7.4 Refactoring Triggers (Code Smells)

**Refactor when you see:**

1. **Duplicated Code** - Same logic in 3+ places
2. **Long Method** - Method exceeds 30 lines
3. **Long Parameter List** - Method has 5+ parameters
4. **Large Class** - Class exceeds 300 lines or 10 methods
5. **Shotgun Surgery** - One change requires modifying many files
6. **Feature Envy** - Method uses data from another class more than its own
7. **Data Clumps** - Same group of parameters appear together repeatedly
8. **Switch Statements** - Type-checking switches (use polymorphism instead)
9. **Temporary Fields** - Fields only used in certain scenarios
10. **Message Chains** - `a.getB().getC().getD().doSomething()`

**Example - Feature Envy:**
```csharp
// ❌ BAD - Method uses Customer data more than Order data
public class Order
{
    public decimal CalculateDiscount(Customer customer)
    {
        if (customer.IsPremium && customer.YearsActive > 5)
            return Total * 0.15m;
        else if (customer.IsPremium)
            return Total * 0.10m;
        else if (customer.YearsActive > 5)
            return Total * 0.05m;
        else
            return 0;
    }
}

// ✅ GOOD - Move to Customer class
public class Customer
{
    public decimal GetDiscountPercentage()
    {
        if (IsPremium && YearsActive > 5)
            return 0.15m;
        else if (IsPremium)
            return 0.10m;
        else if (YearsActive > 5)
            return 0.05m;
        else
            return 0m;
    }
}

public class Order
{
    public decimal CalculateDiscount(Customer customer)
    {
        return Total * customer.GetDiscountPercentage();
    }
}
```

---

## 8. Code Readability

### 8.1 Meaningful Names (No Abbreviations Except Standard Ones)

**RULE:** Use full words, not abbreviations (except universally recognized).

#### ❌ BAD - Cryptic abbreviations
```csharp
var usrSvc = new UsrSvc();
var ord = usrSvc.GetOrd(123);
var calc = new Calc();
var res = calc.CompDisc(ord, usr);
```

#### ✅ GOOD - Full, descriptive names
```csharp
var userService = new UserService();
var order = userService.GetOrder(123);
var calculator = new DiscountCalculator();
var result = calculator.ComputeDiscount(order, user);
```

**Allowed Standard Abbreviations:**
- `Id` (Identifier)
- `Url` (Uniform Resource Locator)
- `Html` (HyperText Markup Language)
- `Xml` (eXtensible Markup Language)
- `Json` (JavaScript Object Notation)
- `Dto` (Data Transfer Object)
- `Api` (Application Programming Interface)
- `Sql` (Structured Query Language)
- `Ui` (User Interface)
- `Uri` (Uniform Resource Identifier)
- `Io` (Input/Output)
- `Http` (HyperText Transfer Protocol)

### 8.2 Self-Documenting Code Principles

**RULE:** Code should explain itself without comments.

#### ❌ BAD - Requires comments to understand
```csharp
// Check if user is valid
if (u.a > 18 && u.s == 1 && u.v == true)
{
    // Process user
    p(u);
}
```

#### ✅ GOOD - Self-explanatory
```csharp
const int MinimumAge = 18;
const int ActiveStatus = 1;

if (user.Age >= MinimumAge &&
    user.Status == ActiveStatus &&
    user.IsVerified)
{
    ProcessUser(user);
}
```

**Better - Extract to named method:**
```csharp
if (IsEligibleForProcessing(user))
{
    ProcessUser(user);
}

private bool IsEligibleForProcessing(User user)
{
    const int MinimumAge = 18;
    const int ActiveStatus = 1;

    return user.Age >= MinimumAge &&
           user.Status == ActiveStatus &&
           user.IsVerified;
}
```

### 8.3 When to Extract Methods

**Extract method when:**

1. **Reuse** - Logic is used in multiple places
2. **Clarity** - Extraction improves readability by naming a concept
3. **Nesting Reduction** - Method is deeply nested (>3 levels)
4. **Length** - Method exceeds 30 lines
5. **Complexity** - Cyclomatic complexity exceeds 10

#### Example - Extract for Clarity
```csharp
// ❌ BAD - Unclear intent
public void ProcessOrder(Order order)
{
    if (order.Items.All(i => i.Quantity > 0) &&
        order.Items.All(i => i.Price > 0) &&
        order.Total == order.Items.Sum(i => i.Quantity * i.Price))
    {
        SaveOrder(order);
    }
}

// ✅ GOOD - Clear intent through named method
public void ProcessOrder(Order order)
{
    if (IsValidOrder(order))
    {
        SaveOrder(order);
    }
}

private bool IsValidOrder(Order order)
{
    return HasValidItems(order) && HasCorrectTotal(order);
}

private bool HasValidItems(Order order)
{
    return order.Items.All(item => item.Quantity > 0 && item.Price > 0);
}

private bool HasCorrectTotal(Order order)
{
    var expectedTotal = order.Items.Sum(item => item.Quantity * item.Price);
    return order.Total == expectedTotal;
}
```

### 8.4 Avoiding Over-Decomposition (Ravioli Code Anti-Pattern)

**RULE:** Don't extract methods that are only used once and don't improve clarity.

#### ❌ BAD - Over-decomposed (Ravioli Code)
```csharp
public void ProcessOrder(Order order)
{
    var user = GetUser(order);
    var total = GetTotal(order);
    var tax = GetTax(total);
    var finalTotal = GetFinalTotal(total, tax);
    Save(order, finalTotal);
}

private User GetUser(Order order) => _userService.GetUser(order.UserId);
private decimal GetTotal(Order order) => order.Total;
private decimal GetTax(decimal total) => total * 0.21m;
private decimal GetFinalTotal(decimal total, decimal tax) => total + tax;
private void Save(Order order, decimal finalTotal) => _repository.Save(order);

// 6 methods for simple calculation - harder to follow!
```

#### ✅ GOOD - Appropriate level of abstraction
```csharp
public void ProcessOrder(Order order)
{
    var user = _userService.GetUser(order.UserId);
    var finalTotal = CalculateFinalTotal(order.Total);

    order.FinalTotal = finalTotal;
    _repository.Save(order);
}

private decimal CalculateFinalTotal(decimal subtotal)
{
    const decimal TaxRate = 0.21m;
    var tax = subtotal * TaxRate;
    return subtotal + tax;
}

// 2 methods - easier to follow!
```

**Guideline:** Extract method only if:
- It's used 2+ times, OR
- It simplifies a complex method, OR
- It names a meaningful business concept

---

## 9. Enforcement Mechanisms

### 9.1 Roslyn Analyzer Configuration

**Install analyzers:**
```xml
<!-- Directory.Build.props (applies to all projects) -->
<Project>
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.NetAnalyzers" Version="8.0.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
    <PackageReference Include="StyleCop.Analyzers" Version="1.2.0-beta.507">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Roslynator.Analyzers" Version="4.7.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
  </ItemGroup>
</Project>
```

**Complete .editorconfig:** (See ROSLYN_ANALYZER_CONFIG.md for full configuration)

```ini
# Complexity Metrics
dotnet_diagnostic.CA1502.severity = error
dotnet_code_quality.CA1502.threshold = 10  # Cyclomatic complexity

dotnet_diagnostic.CA1505.severity = warning
dotnet_code_quality.CA1505.max_methods = 10  # Max methods per class
dotnet_code_quality.CA1505.max_lines = 300   # Max lines per class

dotnet_diagnostic.CA1506.severity = warning
dotnet_code_quality.CA1506.threshold = 3     # Nesting depth

# Documentation
dotnet_diagnostic.CS1591.severity = error    # Missing XML documentation
dotnet_diagnostic.SA1600.severity = error    # Elements should be documented

# Naming Conventions
dotnet_diagnostic.CA1707.severity = error    # Identifiers should not contain underscores
dotnet_diagnostic.CA1715.severity = error    # Identifiers should have correct prefix (I for interfaces)

# Code Quality
dotnet_diagnostic.CA1062.severity = warning  # Validate arguments of public methods
dotnet_diagnostic.CA1031.severity = warning  # Do not catch general exception types
dotnet_diagnostic.CA2007.severity = warning  # Consider calling ConfigureAwait
```

### 9.2 SonarQube Integration

**Install SonarQube Scanner:**
```bash
dotnet tool install --global dotnet-sonarscanner
```

**Run analysis:**
```bash
# Begin analysis
dotnet sonarscanner begin /k:"Hazina" /d:sonar.host.url="http://localhost:9000"

# Build
dotnet build

# End analysis
dotnet sonarscanner end
```

**Quality Gate Configuration:**
```yaml
# sonar-project.properties
sonar.projectKey=Hazina
sonar.projectName=Hazina Framework
sonar.sources=src
sonar.tests=Tests

# Complexity thresholds
sonar.cs.cyclomatic.max=10
sonar.cs.cognitive.max=15

# Coverage thresholds
sonar.coverage.exclusions=**/*Tests.cs,**/*TestData.cs
sonar.cs.opencover.reportsPaths=**/coverage.opencover.xml

# Quality Gate
sonar.qualitygate.wait=true
sonar.qualitygate.timeout=300
```

**Quality Gates:**
- Coverage on New Code ≥ 70%
- Maintainability Rating = A
- Reliability Rating = A
- Security Rating = A
- Duplicated Lines ≤ 3%
- Cognitive Complexity ≤ 15 per function

### 9.3 Pre-Commit Hooks

**Install Git hooks:**
```bash
# .git/hooks/pre-commit (or use Husky for .NET)
#!/bin/sh

# Run code formatting
dotnet format --verify-no-changes

# Run Roslyn analyzers
dotnet build /p:TreatWarningsAsErrors=true

# Run tests
dotnet test --filter "Category=Unit" --no-build

if [ $? -ne 0 ]; then
  echo "Pre-commit checks failed. Please fix errors before committing."
  exit 1
fi
```

**Using Husky.Net:**
```bash
dotnet new tool-manifest
dotnet tool install Husky
dotnet husky install

# Add pre-commit hook
dotnet husky add pre-commit -c "dotnet format --verify-no-changes"
dotnet husky add pre-commit -c "dotnet build /p:TreatWarningsAsErrors=true"
dotnet husky add pre-commit -c "dotnet test --filter Category=Unit --no-build"
```

### 9.4 Code Review Checklist

**Automated checks (enforced by CI):**
- ☐ Build succeeds with no warnings
- ☐ All unit tests pass
- ☐ Code coverage ≥ 70% for new code
- ☐ No code smells (SonarQube)
- ☐ Documentation coverage 100% for public APIs

**Manual code review (human reviewer):**
- ☐ Boy Scout Rule applied (code is better than before)
- ☐ Single Responsibility Principle followed
- ☐ Appropriate abstraction level (not over-decomposed)
- ☐ Meaningful variable/method names
- ☐ No magic numbers (extracted to constants)
- ☐ Appropriate use of exception categories
- ☐ Security considerations addressed
- ☐ Performance considerations documented (if relevant)
- ☐ Thread safety documented (if relevant)

### 9.5 Automated Quality Gates (CI/CD Pipeline)

**Azure DevOps / GitHub Actions:**
```yaml
# .github/workflows/quality-gate.yml
name: Quality Gate

on: [pull_request]

jobs:
  quality:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'

      - name: Restore dependencies
        run: dotnet restore

      - name: Check code formatting
        run: dotnet format --verify-no-changes

      - name: Build with analyzers
        run: dotnet build --no-restore /p:TreatWarningsAsErrors=true

      - name: Run unit tests
        run: dotnet test --no-build --filter "Category=Unit"

      - name: Run code coverage
        run: |
          dotnet test --no-build /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:Threshold=70 /p:ThresholdType=branch

      - name: SonarQube analysis
        run: |
          dotnet sonarscanner begin /k:"Hazina" /d:sonar.host.url="${{ secrets.SONAR_HOST_URL }}"
          dotnet build
          dotnet sonarscanner end

      - name: Quality Gate check
        run: |
          # Wait for SonarQube quality gate result
          # Fail if quality gate is not passed
```

---

## 10. Migration Strategy

**Goal:** Migrate 938 existing files to new standards without disrupting development.

### 10.1 New Code (IMMEDIATE ENFORCEMENT)

**RULE:** All new code MUST comply with standards from day one.

**Enforcement:**
- Pre-commit hooks block non-compliant code
- CI/CD pipeline fails on violations
- Code review checklist mandatory

**No exceptions for new code.**

### 10.2 Existing Code (BOY SCOUT RULE)

**RULE:** Fix when touching - leave code better than you found it.

**Boy Scout Approach:**
1. When editing existing file, scan entire file for violations
2. Fix violations in touched sections (5-10 minutes max)
3. Document remaining violations as technical debt
4. Prioritize high-impact violations (see section 10.4)

**Example:**
```csharp
// Before - Touching UpdateUser method
public class UserService
{
    // ❌ Missing documentation, poor naming
    public void UpdateUser(User u)
    {
        var x = repo.Get(u.Id); // ❌ Cryptic variable name
        x.Name = u.Name;
        repo.Save(x);
    }

    // ❌ Missing documentation, poor naming
    public void DeleteUser(int id)
    {
        repo.Delete(id);
    }
}

// After - Boy Scout Rule applied
/// <summary>
/// Manages user-related business operations.
/// </summary>
public class UserService
{
    private readonly IUserRepository _repository;

    /// <summary>
    /// Updates an existing user's information.
    /// </summary>
    /// <param name="user">The user with updated information.</param>
    /// <exception cref="UserNotFoundException">Thrown when user ID does not exist.</exception>
    public void UpdateUser(User user)
    {
        var existingUser = _repository.GetById(user.Id);
        if (existingUser == null)
            throw new UserNotFoundException($"User {user.Id} not found");

        existingUser.Name = user.Name;
        _repository.Save(existingUser);
    }

    // ❌ TODO: Add documentation and validation (tracked as technical debt)
    public void DeleteUser(int id)
    {
        _repository.Delete(id);
    }
}
```

### 10.3 Technical Debt Tracking

**RULE:** Track violations that can't be fixed immediately.

**Tracking Mechanism:**
```csharp
// In code (for small violations)
// TODO-STANDARDS: Extract method to reduce cyclomatic complexity (CC=12, target=10)
// TODO-STANDARDS: Add XML documentation
// TODO-STANDARDS: Split class (15 methods, target=10)

// In GitHub Issues (for large violations)
// Title: [TECH-DEBT] Refactor WordPressProvider (67 methods)
// Labels: technical-debt, complexity, refactoring
// Priority: High (blocks new features)
```

**Technical Debt Register:**
```markdown
# Technical Debt Register

## High Priority (Blocks new development)
- [ ] WordPressProvider.cs - 67 methods, 850 lines - Split into focused services
- [ ] AgentFactory.cs - God object, multiple responsibilities - Refactor to SRP
- [ ] RAGEngine.cs - Mixed concerns - Separate retrieval, generation, orchestration

## Medium Priority (Maintainability impact)
- [ ] ToolExecutor.cs - Cyclomatic complexity 15 - Extract decision logic
- [ ] 15 classes with >30 methods - Review and refactor

## Low Priority (Boy Scout Rule)
- [ ] ~20 methods with >50 lines - Extract when touching
- [ ] Missing XML documentation for ~23% of public APIs - Add when touching
```

### 10.4 Refactoring Prioritization

**Priority Matrix:**

| Priority | Criteria | Timeline |
|----------|----------|----------|
| **P0 - Critical** | Blocks new features, security risk, causes production bugs | ASAP (next sprint) |
| **P1 - High** | High complexity (CC>15), >500 lines, >20 methods, <50% test coverage for critical code | 1-2 sprints |
| **P2 - Medium** | Medium complexity (CC 11-15), 300-500 lines, 11-20 methods, missing documentation | Boy Scout Rule |
| **P3 - Low** | Minor violations, cosmetic issues | Boy Scout Rule (opportunistic) |

**Refactoring Approach:**

**For Critical/High Priority:**
1. Create GitHub issue with detailed scope
2. Write tests for existing behavior (if missing)
3. Refactor incrementally with test validation
4. Code review with two reviewers
5. Monitor production after deployment

**For Medium/Low Priority:**
6. Apply Boy Scout Rule when touching code
7. Track progress in Technical Debt Register
8. Celebrate incremental improvements

### 10.5 Migration Timeline

**Phase 1 (Months 1-2): Foundation**
- ✅ Establish standards (this document)
- ✅ Configure Roslyn analyzers
- ✅ Set up SonarQube
- ✅ Install pre-commit hooks
- ✅ Train team on standards

**Phase 2 (Months 3-4): High-Priority Refactoring**
- ☐ Refactor WordPressProvider (67 methods → 4 focused services)
- ☐ Refactor AgentFactory (God object → SRP-compliant classes)
- ☐ Refactor RAGEngine (mixed concerns → separated responsibilities)
- ☐ Refactor ToolExecutor (high complexity → extracted logic)

**Phase 3 (Months 5-6): Test Coverage Sprint**
- ☐ Achieve 70% coverage for critical paths
- ☐ Create integration tests for all repositories
- ☐ Create API tests for all endpoints
- ☐ Set up coverage quality gate

**Phase 4 (Months 7-12): Boy Scout Rule**
- ☐ Apply Boy Scout Rule to all touched files
- ☐ Reduce technical debt register by 50%
- ☐ Achieve 90% documentation coverage
- ☐ Achieve 80% test coverage

**Phase 5 (Month 12+): Maintenance**
- ☐ 100% compliance for new code
- ☐ <5% technical debt
- ☐ Continuous improvement culture

---

## 11. Architectural Patterns

### 11.1 Layered Architecture (MANDATORY)

**Hazina Framework Layers:**

```
┌─────────────────────────────────────┐
│   Presentation Layer               │  ASP.NET Controllers, Blazor Components
│   (UI, API, CLI)                    │
├─────────────────────────────────────┤
│   Application Layer                │  Services, Use Cases, DTOs
│   (Business Logic)                  │
├─────────────────────────────────────┤
│   Domain Layer                      │  Entities, Interfaces, Business Rules
│   (Core Business)                   │
├─────────────────────────────────────┤
│   Infrastructure Layer             │  Repositories, External APIs, Data Access
│   (External Dependencies)           │
└─────────────────────────────────────┘
```

**Dependency Flow:** Top → Down (upper layers depend on lower, never reverse)

**Example:**
```csharp
// ✅ GOOD - Controllers depend on Services
[ApiController]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService; // Application layer

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(int id)
    {
        var user = await _userService.GetUserAsync(id);
        return Ok(user);
    }
}

// ✅ GOOD - Services depend on Repositories
public class UserService : IUserService
{
    private readonly IUserRepository _repository; // Infrastructure layer

    public async Task<User> GetUserAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }
}

// ❌ BAD - Repository depending on Service (inverted dependency)
public class UserRepository
{
    private readonly IUserService _service; // WRONG! Infrastructure → Application
}
```

### 11.2 Repository Pattern

**Standard Implementation:**
```csharp
// Domain Layer - Interface
public interface IUserRepository
{
    Task<User> GetByIdAsync(int id);
    Task<List<User>> GetAllAsync();
    Task<User> AddAsync(User user);
    Task<User> UpdateAsync(User user);
    Task DeleteAsync(int id);
}

// Infrastructure Layer - Implementation
public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User> GetByIdAsync(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            throw new UserNotFoundException($"User {id} not found");

        return user;
    }

    // ... other CRUD operations
}
```

### 11.3 Service Layer Pattern

**Application Services (Orchestration):**
```csharp
public class UserService : IUserService
{
    private readonly IUserRepository _repository;
    private readonly IEmailService _emailService;
    private readonly ILogger<UserService> _logger;

    public async Task<User> RegisterUserAsync(CreateUserRequest request)
    {
        // Validation
        await ValidateUniqueEmailAsync(request.Email);

        // Business logic
        var user = new User
        {
            Email = request.Email,
            Name = request.Name,
            CreatedAt = DateTime.UtcNow
        };

        // Persistence
        await _repository.AddAsync(user);

        // Side effects
        await _emailService.SendWelcomeEmailAsync(user.Email);
        _logger.LogInformation("User {Email} registered", user.Email);

        return user;
    }

    private async Task ValidateUniqueEmailAsync(string email)
    {
        if (await _repository.EmailExistsAsync(email))
            throw new DuplicateEmailException($"Email {email} is already registered");
    }
}
```

### 11.4 Factory Pattern

**Use for:**
- Creating complex objects with many dependencies
- Creating objects based on runtime conditions
- Abstracting object creation logic

```csharp
public interface ILLMProviderFactory
{
    ILLMProvider Create(LLMProviderType type, LLMConfiguration config);
}

public class LLMProviderFactory : ILLMProviderFactory
{
    private readonly IServiceProvider _serviceProvider;

    public ILLMProvider Create(LLMProviderType type, LLMConfiguration config)
    {
        return type switch
        {
            LLMProviderType.OpenAI => new OpenAIProvider(config, _serviceProvider.GetRequiredService<ILogger<OpenAIProvider>>()),
            LLMProviderType.Anthropic => new AnthropicProvider(config, _serviceProvider.GetRequiredService<ILogger<AnthropicProvider>>()),
            LLMProviderType.GoogleGemini => new GeminiProvider(config, _serviceProvider.GetRequiredService<ILogger<GeminiProvider>>()),
            _ => throw new NotSupportedException($"Provider type {type} not supported")
        };
    }
}
```

### 11.5 Strategy Pattern

**Use for:**
- Algorithms that can be swapped at runtime
- Different behaviors based on configuration
- Replacing conditional logic with polymorphism

```csharp
public interface IDiscountStrategy
{
    decimal CalculateDiscount(Order order);
}

public class PremiumMemberDiscount : IDiscountStrategy
{
    public decimal CalculateDiscount(Order order)
    {
        return order.Total * 0.15m;
    }
}

public class RegularMemberDiscount : IDiscountStrategy
{
    public decimal CalculateDiscount(Order order)
    {
        return order.Total * 0.05m;
    }
}

public class OrderService
{
    public decimal CalculateOrderTotal(Order order, IDiscountStrategy discountStrategy)
    {
        var discount = discountStrategy.CalculateDiscount(order);
        return order.Subtotal - discount;
    }
}
```

---

## 12. Common Anti-Patterns

### 12.1 God Objects

**Problem:** Classes that know/do too much.

**Example:** WordPressProvider with 67 methods

**Solution:** Split into focused services following SRP.

### 12.2 Magic Numbers

**Problem:** Unexplained constants.

#### ❌ BAD
```csharp
if (user.Age < 18) { }
Task.Delay(5000);
var pageSize = 20;
```

#### ✅ GOOD
```csharp
private const int MinimumAge = 18;
private const int CacheRefreshIntervalMs = 5000;
private const int DefaultPageSize = 20;

if (user.Age < MinimumAge) { }
Task.Delay(CacheRefreshIntervalMs);
var pageSize = DefaultPageSize;
```

### 12.3 Primitive Obsession

**Problem:** Using primitive types instead of domain objects.

#### ❌ BAD
```csharp
public void SendEmail(string to, string subject, string body) { }
public void CreateUser(string email, string name, int age, string address) { }
```

#### ✅ GOOD
```csharp
public record Email(string To, string Subject, string Body);
public void SendEmail(Email email) { }

public record CreateUserRequest(string Email, string Name, int Age, Address Address);
public void CreateUser(CreateUserRequest request) { }
```

### 12.4 Leaky Abstractions

**Problem:** Implementation details exposed through interfaces.

#### ❌ BAD
```csharp
public interface IUserRepository
{
    User GetUser(int id); // Throws SqlException - leaky!
}
```

#### ✅ GOOD
```csharp
public interface IUserRepository
{
    User GetUser(int id); // Throws UserNotFoundException - domain exception
}

public class UserRepository : IUserRepository
{
    public User GetUser(int id)
    {
        try
        {
            return _context.Users.Find(id);
        }
        catch (SqlException ex)
        {
            throw new RepositoryException("Failed to retrieve user", ex);
        }
    }
}
```

### 12.5 Shotgun Surgery

**Problem:** One change requires modifying many files.

**Solution:** Improve cohesion - put related things together.

### 12.6 Feature Envy

**Problem:** Method uses another class's data more than its own.

#### ❌ BAD
```csharp
public class Order
{
    public bool IsEligibleForDiscount(Customer customer)
    {
        return customer.IsPremium && customer.YearsActive > 5;
    }
}
```

#### ✅ GOOD
```csharp
public class Customer
{
    public bool IsEligibleForDiscount()
    {
        return IsPremium && YearsActive > 5;
    }
}

public class Order
{
    public bool IsEligibleForDiscount(Customer customer)
    {
        return customer.IsEligibleForDiscount();
    }
}
```

---

## 13. Performance Guidelines

### 13.1 When to Optimize

**RULE:** Don't optimize prematurely. Optimize based on profiler evidence.

**Workflow:**
1. Write clear, maintainable code first
2. Measure with profiler (dotTrace, BenchmarkDotNet)
3. Identify actual bottlenecks
4. Optimize hotspots
5. Document optimization rationale

#### ✅ GOOD - Documented optimization
```csharp
/// <summary>
/// Processes large document batches with parallel processing.
/// </summary>
/// <remarks>
/// Performance: Uses Parallel.ForEach instead of sequential processing.
/// Profiler evidence: 5x speedup for 10,000+ documents (BenchmarkDotNet results attached).
/// Trade-off: Increased memory usage (~2GB for 10k documents) vs. 60% reduction in processing time.
/// </remarks>
public async Task ProcessDocumentBatchAsync(List<Document> documents)
{
    await Parallel.ForEachAsync(documents, async (doc, ct) =>
    {
        await ProcessDocumentAsync(doc, ct);
    });
}
```

### 13.2 Async/Await Best Practices

**RULE:** Use async all the way (no sync-over-async).

#### ❌ BAD - Sync-over-async (deadlock risk)
```csharp
public User GetUser(int id)
{
    return _repository.GetByIdAsync(id).Result; // Deadlock risk!
}
```

#### ✅ GOOD - Async all the way
```csharp
public async Task<User> GetUserAsync(int id)
{
    return await _repository.GetByIdAsync(id);
}
```

**ConfigureAwait(false):**
```csharp
// In library code (not UI), use ConfigureAwait(false) to avoid context capture
public async Task<User> GetUserAsync(int id)
{
    return await _repository.GetByIdAsync(id).ConfigureAwait(false);
}
```

### 13.3 LINQ Performance

**RULE:** Understand LINQ query execution (deferred vs. immediate).

#### ❌ BAD - Multiple enumerations
```csharp
var query = users.Where(u => u.IsActive);

if (query.Any())                    // Enumeration 1
{
    var count = query.Count();      // Enumeration 2
    var first = query.First();      // Enumeration 3
    // Query executed 3 times!
}
```

#### ✅ GOOD - Single enumeration
```csharp
var activeUsers = users.Where(u => u.IsActive).ToList(); // Enumerate once

if (activeUsers.Any())
{
    var count = activeUsers.Count;    // In-memory, no enumeration
    var first = activeUsers.First();  // In-memory, no enumeration
}
```

---

## 14. Security Best Practices

### 14.1 Input Validation

**RULE:** Validate ALL external input.

```csharp
public async Task<User> CreateUserAsync(CreateUserRequest request)
{
    // Validate
    if (request == null)
        throw new ArgumentNullException(nameof(request));

    if (string.IsNullOrWhiteSpace(request.Email))
        throw new ValidationException("Email is required");

    if (!IsValidEmail(request.Email))
        throw new ValidationException("Invalid email format");

    // Sanitize
    var sanitizedName = HtmlEncoder.Default.Encode(request.Name);

    // Process
    var user = new User { Email = request.Email, Name = sanitizedName };
    return await _repository.AddAsync(user);
}
```

### 14.2 SQL Injection Prevention

**RULE:** Always use parameterized queries.

#### ❌ BAD - SQL injection vulnerability
```csharp
var query = $"SELECT * FROM Users WHERE Email = '{email}'"; // UNSAFE!
```

#### ✅ GOOD - Parameterized query
```csharp
var users = await _context.Users
    .Where(u => u.Email == email) // EF Core parameterizes automatically
    .ToListAsync();
```

### 14.3 Secrets Management

**RULE:** Never hardcode secrets. Use configuration/key vault.

#### ❌ BAD
```csharp
var apiKey = "sk-1234567890abcdef"; // Hardcoded secret!
```

#### ✅ GOOD
```csharp
var apiKey = _configuration["OpenAI:ApiKey"]; // From appsettings or Azure Key Vault
```

---

## Conclusion

These coding standards represent the collective wisdom of 50 experts and analysis of Hazina's current state. They are:

- **Comprehensive** - Cover all aspects of code quality
- **Context-Aware** - Recognize that different code types have different needs
- **Enforceable** - Backed by automated tooling
- **Pragmatic** - Balance idealism with reality

**Success Metrics:**
- New code: 100% compliance from day one
- Existing code: 50% technical debt reduction in 12 months
- Test coverage: 70% overall, 100% for critical paths
- Documentation: 100% for public APIs
- Team satisfaction: Improved developer experience

**Remember:** These standards serve ONE goal: **Make Hazina the most maintainable, testable, and enjoyable codebase to work with.**

---

**Appendix:**
- [Quick Reference](./QUICK_REFERENCE.md) - 1-page cheat sheet
- [Roslyn Analyzer Config](./ROSLYN_ANALYZER_CONFIG.md) - Technical implementation
- [Migration Roadmap](./MIGRATION_ROADMAP.md) - Detailed refactoring plan
