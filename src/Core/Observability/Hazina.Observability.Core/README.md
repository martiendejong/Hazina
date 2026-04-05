# Hazina.Observability.Core

Comprehensive observability infrastructure for Hazina framework with structured logging, distributed tracing, metrics, and correlation tracking.

## Features

### 1. Structured Logging
- **Serilog Integration**: Production-ready logging with console and file sinks
- **Sensitive Data Redaction**: Automatic redaction of API keys, emails, credit cards, SSNs, phone numbers, IPs
- **Log Levels**: Configurable per-component log levels
- **Rolling Files**: Daily rolling with 30-day retention, 100MB size limit

### 2. Correlation Tracking
- **Ambient Context**: `CorrelationContext` works both inside and outside HTTP requests
- **AsyncLocal Support**: Correlation IDs flow across async boundaries automatically
- **HTTP Integration**: Seamless integration with ASP.NET Core middleware
- **Scoped Contexts**: Disposable scopes for nested operations

### 3. Distributed Tracing
- **OpenTelemetry**: Industry-standard distributed tracing
- **Activity Source**: `HazinaActivitySource` for LLM operations, NeuroChain, failovers
- **Cost Tracking**: Automatic token usage and cost recording
- **Error Tracking**: Exception details in traces

### 4. Metrics
- **Prometheus Integration**: Exportable metrics for monitoring
- **LLM Metrics**: Token usage, latency, error rates per provider
- **Health Checks**: Provider health status monitoring

## Quick Start

### Basic Setup

```csharp
using Hazina.Observability.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

var services = new ServiceCollection();
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

// Add complete observability stack
services.AddHazinaObservability(
    configuration,
    applicationName: "MyApp",
    configureOptions: options =>
    {
        options.Environment = "Production";
        options.EnableStructuredLogging = true;
        options.EnableDistributedTracing = true;
        options.EnableMetrics = true;
        options.OtlpEndpoint = "http://jaeger:4317"; // Optional
    });
```

### Using Correlation IDs

```csharp
using Hazina.Observability.Core.Correlation;

// Automatic correlation ID
var correlationId = CorrelationContext.GetOrCreateCorrelationId();

// Manual correlation scope
using (CorrelationContext.BeginScope("custom-correlation-id"))
{
    // All operations within this scope share the same correlation ID
    await DoSomethingAsync();
}

// Access current correlation ID
logger.LogInformation(
    "Processing request | CorrelationId: {CorrelationId}",
    CorrelationContext.CurrentCorrelationId);
```

### Sensitive Data Redaction

```csharp
using Hazina.Observability.Core.Logging;

var userInput = "My API key is sk_abc123def456 and email is user@example.com";
var redacted = SensitiveDataRedactor.RedactSensitiveData(userInput);
// Result: "My API key is [REDACTED] and email is [EMAIL]"

// Redact specific fields
var data = new Dictionary<string, object?>
{
    { "username", "john" },
    { "password", "secret123" },
    { "email", "john@example.com" }
};

var safe = SensitiveDataRedactor.RedactFields(
    data,
    SensitiveDataRedactor.DefaultSensitiveFields);
// password will be [REDACTED]
```

### LLM Logging

```csharp
using Hazina.Observability.LLMLogs;

// Wrap any ILLMClient with logging
var client = new OpenAIClient(apiKey);
var loggingClient = new LLMLoggingWrapper(
    client,
    logger,
    providerName: "OpenAI");

// All operations are automatically logged with:
// - Correlation IDs
// - Token usage
// - Latency
// - Redacted prompts/responses
// - Error details
var response = await loggingClient.GetResponse(prompt);
```

### Distributed Tracing

```csharp
using Hazina.Observability.Core.Tracing;
using System.Diagnostics;

// Start an activity
using var activity = HazinaActivitySource.StartLLMOperation(
    "ChatCompletion",
    provider: "OpenAI",
    model: "gpt-4");

try
{
    var response = await GetLLMResponse();

    // Record metrics
    HazinaActivitySource.RecordCost(
        activity,
        cost: 0.02m,
        inputTokens: 500,
        outputTokens: 150);
}
catch (Exception ex)
{
    HazinaActivitySource.RecordError(activity, ex);
    throw;
}
```

## Configuration

### appsettings.json

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning",
        "Hazina.AI": "Debug"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "restrictedToMinimumLevel": "Information"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/hazina-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId"]
  }
}
```

### Environment Variables

- `ASPNETCORE_ENVIRONMENT`: Development/Production
- `OTEL_EXPORTER_OTLP_ENDPOINT`: OpenTelemetry endpoint
- `HAZINA_LOG_LEVEL`: Override log level

## ASP.NET Core Integration

```csharp
using Hazina.Security.AspNetCore.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add observability
builder.Services.AddHazinaObservability(
    builder.Configuration,
    "MyAPI");

// Add correlation ID middleware
builder.Services.AddSingleton<CorrelationIdMiddleware>();

var app = builder.Build();

// Use correlation ID middleware (adds X-Correlation-ID header)
app.UseMiddleware<CorrelationIdMiddleware>();

app.MapGet("/api/test", (ILogger<Program> logger) =>
{
    var correlationId = CorrelationContext.CurrentCorrelationId;
    logger.LogInformation("Request received | CorrelationId: {CorrelationId}", correlationId);
    return Results.Ok(new { correlationId });
});

app.Run();
```

## Best Practices

1. **Always Use Correlation IDs**: Include correlation IDs in all log messages for request tracing
2. **Redact Sensitive Data**: Use `SensitiveDataRedactor` before logging user input or API responses
3. **Structured Logging**: Use structured logging properties instead of string interpolation
4. **Activity Tracking**: Wrap long-running operations in activities for distributed tracing
5. **Error Logging**: Always log exceptions with correlation IDs and context

## Log Message Format

All Hazina logs follow this pattern:

```
[{Timestamp}] [{Level}] [{Component}] {Message} | {Properties} | CorrelationId: {CorrelationId}
```

Example:
```
[2024-03-19 14:23:45.123] [INF] [LLM-REQUEST] Starting request | Provider: OpenAI | PromptLength: 1234 | CorrelationId: a1b2c3d4e5f6
```

## Monitoring

### Prometheus Metrics

Metrics are exposed at `/metrics` endpoint (when configured):

- `hazina_llm_requests_total`: Total LLM requests by provider
- `hazina_llm_tokens_total`: Total tokens consumed by provider
- `hazina_llm_latency_seconds`: Request latency histogram
- `hazina_llm_errors_total`: Error count by provider and type

### Jaeger Tracing

Configure OTLP endpoint to send traces to Jaeger:

```csharp
options.OtlpEndpoint = "http://jaeger:4317";
```

View traces at: http://localhost:16686

## Dependencies

- **Serilog**: Structured logging
- **OpenTelemetry**: Distributed tracing
- **Prometheus.NET**: Metrics
- **Microsoft.Extensions.Logging**: Logging abstractions

## License

Part of the Hazina framework.
