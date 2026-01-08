# Hazina Testing Strategy

## Overview

Hazina uses a multi-layer testing approach to ensure reliability:

| Layer | Purpose | Location | Tools |
|-------|---------|----------|-------|
| Unit Tests | Test individual components | `Tests/*/` | xUnit, Moq |
| Integration Tests | Test component interactions | `Tests/*/` | xUnit, TestContainers |
| Architecture Tests | Enforce dependency rules | `Tests/Architecture/` | ArchUnit.NET |
| Performance Tests | Benchmark critical paths | `Tests/Performance/` | BenchmarkDotNet |

---

## Quick Start

### Run All Tests
```bash
dotnet test Hazina.sln
```

### Run Specific Project
```bash
dotnet test Tests/Core/Hazina.LLMs.Client.Tests
```

### Run with Coverage
```bash
dotnet test --collect:"XPlat Code Coverage"
```

### Run with Filter
```bash
dotnet test --filter "FullyQualifiedName~Provider"
```

---

## Naming Conventions

### Test Class Names
```
{ClassUnderTest}Tests.cs

Examples:
- ProviderOrchestratorTests.cs
- RAGEngineTests.cs
- DocumentStoreTests.cs
```

### Test Method Names
```
{Method}_{Scenario}_{ExpectedResult}

Examples:
- GetResponse_WhenProviderHealthy_ReturnsResponse
- Search_WhenNoResults_ReturnsEmptyList
- Store_WhenDiskFull_ThrowsIOException
```

### Alternative (BDD-style)
```
Should_{ExpectedBehavior}_When_{Scenario}

Examples:
- Should_ReturnResponse_When_ProviderHealthy
- Should_Failover_When_PrimaryProviderDown
- Should_ThrowException_When_NoProvidersRegistered
```

---

## Test Organization

### Directory Structure
```
Tests/
├── Core/
│   ├── Hazina.LLMs.Client.Tests/
│   │   ├── ILLMClientTests.cs
│   │   ├── ToolsContextTests.cs
│   │   └── TestData/
│   │       └── sample_messages.json
│   │
│   ├── Hazina.AI.Providers.Tests/
│   │   ├── ProviderOrchestratorTests.cs
│   │   ├── ProviderSelectorTests.cs
│   │   ├── CostTrackerTests.cs
│   │   └── Mocks/
│   │       └── MockLLMClient.cs
│   │
│   └── Hazina.Store.*.Tests/
│
├── Tools/
│   ├── Hazina.Tools.Data.Tests/
│   └── Hazina.Tools.Services.Chat.Tests/
│
└── Integration/
    ├── Hazina.Integration.OpenAI.Tests/
    └── Hazina.Integration.Storage.Tests/
```

### Test Data
- Place test data in `TestData/` folder within each test project
- Use JSON for structured test data
- Name files descriptively: `valid_chat_messages.json`, `malformed_response.json`

---

## Mocking Guidelines

### What to Mock
- External APIs (OpenAI, Anthropic, etc.)
- Database connections
- File system operations
- Time-dependent operations
- Network calls

### What NOT to Mock
- Pure functions
- Value objects
- DTOs
- Internal helper methods

### Mock Setup Pattern
```csharp
public class ProviderOrchestratorTests
{
    private readonly Mock<ILLMClient> _mockClient;
    private readonly Mock<IProviderRegistry> _mockRegistry;
    private readonly ProviderOrchestrator _sut; // System Under Test

    public ProviderOrchestratorTests()
    {
        _mockClient = new Mock<ILLMClient>();
        _mockRegistry = new Mock<IProviderRegistry>();

        _mockRegistry
            .Setup(r => r.GetProvider("openai"))
            .Returns(_mockClient.Object);

        _sut = new ProviderOrchestrator(_mockRegistry.Object);
    }

    [Fact]
    public async Task GetResponse_WhenProviderHealthy_ReturnsResponse()
    {
        // Arrange
        var expected = new LLMResponse<string> { Result = "Hello" };
        _mockClient
            .Setup(c => c.GetResponse(It.IsAny<List<HazinaChatMessage>>(),
                                       It.IsAny<HazinaChatResponseFormat>(),
                                       It.IsAny<IToolsContext>(),
                                       It.IsAny<List<ImageData>>(),
                                       It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _sut.GetResponse(new List<HazinaChatMessage>(),
                                            HazinaChatResponseFormat.Text,
                                            null, null, CancellationToken.None);

        // Assert
        Assert.Equal("Hello", result.Result);
    }
}
```

---

## Test Categories

### Unit Tests
```csharp
[Fact]
public void CostTracker_RecordUsage_UpdatesTotalCost()
{
    // Pure unit test - no external dependencies
    var tracker = new CostTracker();

    tracker.RecordUsage("openai", 100, 50, 0.01m, 0.03m);

    Assert.Equal(0.0025m, tracker.GetTotalCost());
}
```

### Integration Tests
```csharp
[Trait("Category", "Integration")]
[Fact]
public async Task RAGEngine_IndexAndSearch_ReturnsRelevantResults()
{
    // Uses real SQLite database
    using var db = new SqliteConnection("DataSource=:memory:");
    await db.OpenAsync();

    var store = new SqliteEmbeddingStore(db);
    var rag = new RAGEngine(store, mockLLM.Object);

    await rag.IndexDocumentsAsync(testDocuments);
    var results = await rag.SearchAsync("test query");

    Assert.NotEmpty(results);
}
```

### Slow Tests
```csharp
[Trait("Category", "Slow")]
[Fact]
public async Task Migration_LargeDataset_CompletesWithinTimeout()
{
    // Long-running test, excluded from quick builds
    // ...
}
```

---

## Running Tests by Category

```bash
# Run only unit tests (fast)
dotnet test --filter "Category!=Integration&Category!=Slow"

# Run integration tests
dotnet test --filter "Category=Integration"

# Run all tests including slow
dotnet test
```

---

## Test Fixtures

### Shared Context
```csharp
public class DatabaseFixture : IDisposable
{
    public SqliteConnection Connection { get; }

    public DatabaseFixture()
    {
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();
        // Initialize schema
    }

    public void Dispose() => Connection.Dispose();
}

public class DatabaseTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public DatabaseTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test_Something()
    {
        // Use _fixture.Connection
    }
}
```

---

## Assertions

### Preferred Assertion Style
```csharp
// Good - specific assertions
Assert.Equal(expected, actual);
Assert.NotNull(result);
Assert.Contains("error", message);
Assert.Throws<InvalidOperationException>(() => action());

// Avoid - vague assertions
Assert.True(result != null); // Use Assert.NotNull instead
Assert.True(list.Count > 0); // Use Assert.NotEmpty instead
```

### Collection Assertions
```csharp
Assert.NotEmpty(results);
Assert.All(results, r => Assert.NotNull(r.Key));
Assert.Contains(results, r => r.Similarity > 0.9);
Assert.DoesNotContain(results, r => r.Key == "excluded");
```

### Exception Assertions
```csharp
var exception = await Assert.ThrowsAsync<InvalidOperationException>(
    () => orchestrator.GetResponse(messages));

Assert.Contains("no providers", exception.Message);
```

---

## Test Data Builders

### Builder Pattern for Test Data
```csharp
public class MessageBuilder
{
    private HazinaMessageRole _role = HazinaMessageRole.User;
    private string _content = "Default message";

    public MessageBuilder WithRole(HazinaMessageRole role)
    {
        _role = role;
        return this;
    }

    public MessageBuilder WithContent(string content)
    {
        _content = content;
        return this;
    }

    public HazinaChatMessage Build() => new(_role, _content);

    public List<HazinaChatMessage> BuildList() => new() { Build() };
}

// Usage
var messages = new MessageBuilder()
    .WithRole(HazinaMessageRole.User)
    .WithContent("Hello")
    .BuildList();
```

---

## CI/CD Integration

### GitHub Actions
```yaml
# .github/workflows/test.yml
name: Tests
on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      - run: dotnet test --configuration Release --logger "trx"
      - uses: actions/upload-artifact@v4
        with:
          name: test-results
          path: '**/*.trx'
```

---

## Coverage Requirements

### Targets
| Component | Target | Current |
|-----------|--------|---------|
| Core (LLMs, Storage) | 80% | TBD |
| AI (Providers, RAG) | 70% | TBD |
| Tools | 60% | TBD |
| Apps | 50% | TBD |

### Generating Reports
```bash
dotnet test --collect:"XPlat Code Coverage"
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coverage-report
```

---

## Best Practices

1. **One assertion per test** (when practical)
2. **Arrange-Act-Assert** structure
3. **Descriptive test names** that document behavior
4. **Independent tests** - no shared state between tests
5. **Fast tests** - unit tests should run in < 100ms each
6. **Deterministic** - no flaky tests allowed
7. **Test edge cases** - nulls, empty collections, boundaries
