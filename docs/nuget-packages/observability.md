# Hazina Observability

Three packages ship together to cover the observability surface:

| Package                              | Purpose                                                                                  |
| ------------------------------------ | ---------------------------------------------------------------------------------------- |
| `Hazina.Observability.Core`          | OpenTelemetry tracing/metrics, Serilog logging, Prometheus, health checks, abstractions  |
| `Hazina.Observability.AspNetCore`    | Request/response middleware, Prometheus scrape endpoint, ASP.NET Core DI extensions      |
| `Hazina.Observability.LLMLogs`       | SQLite-backed persistence of prompts, responses, token usage, latency — with replay APIs |

## Install

```bash
dotnet add package Hazina.Observability.Core
dotnet add package Hazina.Observability.AspNetCore   # web apps
dotnet add package Hazina.Observability.LLMLogs      # capture LLM calls
```

## Bootstrap an ASP.NET Core app

```csharp
using Hazina.Observability;
using Hazina.Observability.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHazinaObservability(o =>
{
    o.ServiceName    = "my-service";
    o.ServiceVersion = "1.0.0";
    o.OtlpEndpoint   = builder.Configuration["Otel:Endpoint"]; // e.g. http://otel-collector:4317
    o.EnableConsoleExporter = builder.Environment.IsDevelopment();
});

builder.Services.AddHazinaAspNetCoreObservability();

var app = builder.Build();

app.UseHazinaRequestTracing();
app.MapHazinaPrometheusScrapeEndpoint("/metrics");
app.MapHealthChecks("/health");
```

## Capture every LLM call

`Hazina.Observability.LLMLogs` plugs into the LLM client pipeline and writes
every prompt, response, model, latency, and token usage row into a SQLite
database — useful for cost analysis, regression testing, and prompt/response
replay during evals.

```csharp
using Hazina.Observability.LLMLogs;

builder.Services.AddHazinaLLMLogs(o =>
{
    o.DatabasePath = "data/llm-logs.db";
    o.RedactPromptValues = builder.Environment.IsProduction();
});
```

Inspect captured calls programmatically:

```csharp
public sealed class CostReportService(ILLMLogReader reader)
{
    public async Task<decimal> GetSpendAsync(DateTime since) =>
        (await reader.QueryAsync(q => q.Where(c => c.Timestamp >= since)))
            .Sum(c => c.EstimatedCostUsd ?? 0m);
}
```

## Correlating LLM logs with traces

Every captured LLM call records the active OpenTelemetry `TraceId` and
`SpanId`, so you can cross-reference a call from your tracing backend (Jaeger,
Tempo, Honeycomb, Datadog) into the SQLite log to see the full prompt and
response. This pattern is also how the eval suite replays production failures.

See `docs/examples/05-agent-orchestration` for a runnable observability
configuration.
