# Hazina Logging Guide

## Overview

Hazina uses `Microsoft.Extensions.Logging` for structured logging across all components. This guide establishes patterns for consistent, production-ready logging.

---

## Quick Start

### 1. Add Package Reference

```xml
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.0" />
```

### 2. Add Logger to Class

```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public class MyService
{
    private readonly ILogger<MyService> _logger;

    // Optional logger with NullLogger fallback (backward compatible)
    public MyService(ILogger<MyService>? logger = null)
    {
        _logger = logger ?? NullLogger<MyService>.Instance;
    }
}
```

### 3. Log Messages

```csharp
_logger.LogDebug("Processing item: {ItemId}", itemId);
_logger.LogInformation("Request completed in {Duration}ms", duration);
_logger.LogWarning("Rate limit approaching: {Usage}%", usage);
_logger.LogError(exception, "Failed to process: {Message}", message);
```

---

## Log Levels

| Level | Usage | Example |
|-------|-------|---------|
| `Trace` | Fine-grained debugging | Method entry/exit, loop iterations |
| `Debug` | Development diagnostics | Tool calls, intermediate results |
| `Information` | Normal operations | Requests completed, operations started |
| `Warning` | Unexpected but handled | Budget alerts, retry attempts |
| `Error` | Failures requiring attention | API failures, validation errors |
| `Critical` | System-wide failures | Startup failures, data corruption |

### Decision Tree

```
Is it a failure?
├── Yes → Is it recoverable?
│   ├── Yes → Warning (if user action needed) or Debug (if auto-recovered)
│   └── No → Error (component failure) or Critical (system failure)
└── No → Is it useful for production monitoring?
    ├── Yes → Information
    └── No → Debug or Trace
```

---

## Structured Logging Best Practices

### DO: Use Message Templates

```csharp
// GOOD - Structured, searchable
_logger.LogInformation("User {UserId} created order {OrderId}", userId, orderId);

// BAD - String interpolation breaks structure
_logger.LogInformation($"User {userId} created order {orderId}");
```

### DO: Use Semantic Property Names

```csharp
// GOOD - Clear, consistent names
_logger.LogWarning("Budget Alert: {Message} - Provider: {Provider}, Cost: ${Cost:F2}",
    alert.Message, provider, cost);

// BAD - Generic names
_logger.LogWarning("Alert: {p1} - {p2}, {p3}", alert.Message, provider, cost);
```

### DO: Include Context

```csharp
// GOOD - Includes correlation ID
_logger.LogError(ex, "Request {RequestId} failed for user {UserId}", requestId, userId);

// BAD - No context for debugging
_logger.LogError(ex, "Request failed");
```

### DON'T: Log Sensitive Data

```csharp
// GOOD - Redacted
_logger.LogDebug("Authenticating user {UserId}", userId);

// BAD - Exposes credentials
_logger.LogDebug("Authenticating with password {Password}", password);
```

---

## Migration from Console.WriteLine

### Pattern 1: Debug Output

```csharp
// Before
Console.WriteLine($"[Debug] Processing {itemCount} items");

// After
_logger.LogDebug("Processing {ItemCount} items", itemCount);
```

### Pattern 2: Error Reporting

```csharp
// Before
Console.WriteLine($"Error: {ex.Message}");
Console.WriteLine(ex.StackTrace);

// After
_logger.LogError(ex, "Operation failed: {Message}", ex.Message);
```

### Pattern 3: Progress Updates

```csharp
// Before
Console.WriteLine($"Progress: {percent}%");

// After
_logger.LogInformation("Progress: {Percent}%", percent);
// Or for frequent updates:
_logger.LogTrace("Progress: {Percent}%", percent);
```

### Pattern 4: Alerts/Warnings

```csharp
// Before
Console.WriteLine($"[WARNING] Budget at {usage}%!");

// After
_logger.LogWarning("Budget utilization at {Usage}%", usage);
```

---

## Common Scenarios

### Provider Operations

```csharp
_logger.LogInformation("Selecting provider {Provider} using strategy {Strategy}",
    providerName, strategy);

_logger.LogWarning("Provider {Provider} unhealthy, failing over to {FallbackProvider}",
    primaryProvider, fallbackProvider);

_logger.LogError(ex, "Provider {Provider} request failed after {Retries} retries",
    providerName, retryCount);
```

### Tool Execution

```csharp
_logger.LogDebug("Executing tool {ToolName} with arguments: {Arguments}",
    tool.Name, JsonSerializer.Serialize(args));

_logger.LogDebug("Tool {ToolName} completed in {Duration}ms with result length {Length}",
    tool.Name, duration, result.Length);
```

### Cost Tracking

```csharp
_logger.LogInformation("Request cost: ${Cost:F6} ({InputTokens} input, {OutputTokens} output)",
    cost, inputTokens, outputTokens);

_logger.LogWarning("Budget alert: {AlertMessage} - Current spend: ${CurrentCost:F2} ({Utilization:F1}%)",
    alertMessage, currentCost, utilization);
```

---

## Configuration

### In ASP.NET Core

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Hazina": "Debug",
      "Hazina.AI.Providers": "Information"
    }
  }
}
```

### In Console Apps

```csharp
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .SetMinimumLevel(LogLevel.Debug)
        .AddConsole();
});

var logger = loggerFactory.CreateLogger<MyService>();
var service = new MyService(logger);
```

---

## Files Requiring Migration

The following production code files still use `Console.Write`:

### High Priority (Core Components)
- [ ] `AgentFactory.cs` - Error handling uses Console
- [ ] `AgentManager.cs` - Status output uses Console
- [ ] `ChatService.cs` - Various debug output
- [ ] `ChatStreamService.cs` - Stream progress
- [ ] `EmbeddingsService.cs` - Processing updates

### Medium Priority (Infrastructure)
- [ ] `StoreProvider.cs` - Configuration output
- [ ] `SqliteStoreProvider.cs` - Initialization messages
- [ ] `DocumentRepository.cs` - CRUD operations

### Low Priority (Can Keep Console)
- Example files (`*Example*.cs`) - Appropriate for demos
- Migration CLI tools - Console output is expected
- Test utilities - Console is appropriate

---

## Testing with Loggers

```csharp
public class MyServiceTests
{
    [Fact]
    public void MyService_LogsExpectedMessages()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<MyService>>();
        var service = new MyService(mockLogger.Object);

        // Act
        service.DoSomething();

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("expected")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
```

---

## See Also

- [Microsoft Logging Documentation](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging)
- [Structured Logging Best Practices](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/)
- [Serilog (alternative sink)](https://serilog.net/)

---

*Last Updated: 2026-01-08*
*Part of: Clean Code Initiative (C30)*
