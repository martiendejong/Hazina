# Hazina Observability Guide

This guide shows how to integrate Hazina's observability features into your applications.

## Components

### 1. **Hazina.Observability.Core**
Core observability features including telemetry, metrics, and health checks.

**Features:**
- `ITelemetrySystem` - Structured logging and metrics tracking
- `HazinaMetrics` - Prometheus metrics for all Hazina operations
- `HazinaProviderHealthCheck` - ASP.NET Core health check for provider orchestrator
- `HazinaNeuroChainHealthCheck` - ASP.NET Core health check for NeuroChain

**Metrics tracked:**
- `hazina_operation_duration_ms` - Operation latency histogram
- `hazina_operations_total` - Total operations counter
- `hazina_provider_health` - Provider health gauge
- `hazina_hallucinations_detected_total` - Hallucinations detected
- `hazina_provider_failovers_total` - Provider failover events
- `hazina_cost_usd_total` - Total cost in USD
- `hazina_tokens_used_total` - Token usage (input/output)
- `hazina_neurochain_layers_used_total` - NeuroChain layers usage
- `hazina_faults_detected_total` - Faults detected and corrected

### 2. **Hazina.Observability.AspNetCore**
ASP.NET Core integration for exposing metrics and health endpoints.

**Features:**
- Simple registration with `AddHazinaObservability()`
- Automatic Prometheus metrics endpoint at `/metrics`
- Health check endpoints:
  - `/health/live` - Liveness probe (always healthy if running)
  - `/health/ready` - Readiness probe (checks dependencies)
  - `/health` - Full health check with JSON response

## Quick Start

### Step 1: Add Package References

```xml
<ItemGroup>
  <ProjectReference Include="path/to/Hazina.Observability.Core/Hazina.Observability.Core.csproj" />
  <ProjectReference Include="path/to/Hazina.Observability.AspNetCore/Hazina.Observability.AspNetCore.csproj" />
</ItemGroup>
```

### Step 2: Configure Services

```csharp
using Hazina.Observability.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add Hazina observability
builder.Services.AddHazinaObservability(options =>
{
    options.EnableProviderHealthChecks = true;
    options.EnableNeuroChainHealthChecks = true;
});

// Register your Hazina components
builder.Services.AddSingleton<IProviderOrchestrator>(sp =>
{
    var orchestrator = new ProviderOrchestrator();
    // Configure providers...
    return orchestrator;
});

builder.Services.AddSingleton<NeuroChainOrchestrator>(sp =>
{
    var neuroChain = new NeuroChainOrchestrator();
    // Configure layers...
    return neuroChain;
});

var app = builder.Build();

// Use Hazina observability endpoints
app.UseHazinaObservability(options =>
{
    options.EnableMetrics = true;
    options.EnableHealthChecks = true;
    options.MetricsPath = "/metrics";
    options.HealthPath = "/health";
    options.LivenessPath = "/health/live";
    options.ReadinessPath = "/health/ready";
});

app.Run();
```

### Step 3: Use Telemetry in Your Code

```csharp
using Hazina.Observability.Core;

public class MyService
{
    private readonly ITelemetrySystem _telemetry;
    private readonly IProviderOrchestrator _orchestrator;

    public MyService(ITelemetrySystem telemetry, IProviderOrchestrator orchestrator)
    {
        _telemetry = telemetry;
        _orchestrator = orchestrator;
    }

    public async Task<string> ProcessRequestAsync(string prompt)
    {
        var operationId = Guid.NewGuid().ToString();
        var sw = Stopwatch.StartNew();
        bool success = false;

        try
        {
            var result = await _orchestrator.GetResponse(
                new List<HazinaChatMessage>
                {
                    new() { Role = "user", Text = prompt }
                },
                new HazinaChatResponseFormat(),
                null,
                null,
                CancellationToken.None
            );

            success = true;

            // Track cost
            _telemetry.TrackCost(
                "openai",
                0.0001m,
                result.TokenUsage.InputTokens,
                result.TokenUsage.OutputTokens
            );

            return result.Result;
        }
        finally
        {
            sw.Stop();

            // Track operation
            _telemetry.TrackOperation(
                operationId,
                "openai",
                sw.Elapsed,
                success,
                "chat_completion"
            );
        }
    }
}
```

## Accessing Endpoints

Once configured, you can access:

**Prometheus Metrics:**
```bash
curl http://localhost:5000/metrics
```

**Health Checks:**
```bash
# Liveness (always returns 200 if app is running)
curl http://localhost:5000/health/live

# Readiness (checks if dependencies are ready)
curl http://localhost:5000/health/ready

# Full health with JSON response
curl http://localhost:5000/health
```

Example health response:
```json
{
  "status": "Healthy",
  "duration": 45.2,
  "checks": [
    {
      "name": "hazina_providers",
      "status": "Healthy",
      "description": "All 2 providers healthy",
      "duration": 12.5,
      "data": {
        "total_providers": 2,
        "healthy_providers": 2,
        "degraded_providers": 0,
        "unhealthy_providers": 0,
        "overall_success_rate": 0.98,
        "total_cost": 0.0523,
        "provider_openai_state": "Healthy",
        "provider_openai_success_rate": 0.97,
        "provider_anthropic_state": "Healthy",
        "provider_anthropic_success_rate": 0.99
      }
    },
    {
      "name": "hazina_neurochain",
      "status": "Healthy",
      "description": "NeuroChain orchestrator is configured and ready",
      "duration": 5.1,
      "data": {
        "orchestrator_configured": true,
        "health_check_mode": "configuration_only"
      }
    }
  ]
}
```

## Kubernetes Integration

### Deployment with Health Probes

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: hazina-app
spec:
  replicas: 3
  template:
    metadata:
      annotations:
        prometheus.io/scrape: "true"
        prometheus.io/port: "8080"
        prometheus.io/path: "/metrics"
    spec:
      containers:
      - name: app
        image: hazina-app:latest
        ports:
        - containerPort: 8080
        livenessProbe:
          httpGet:
            path: /health/live
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 10
          timeoutSeconds: 5
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 8080
          initialDelaySeconds: 5
          periodSeconds: 5
          timeoutSeconds: 3
```

### ServiceMonitor for Prometheus Operator

```yaml
apiVersion: monitoring.coreos.com/v1
kind: ServiceMonitor
metadata:
  name: hazina-metrics
spec:
  selector:
    matchLabels:
      app: hazina
  endpoints:
  - port: http
    path: /metrics
    interval: 30s
```

## Grafana Dashboards

Coming soon! We'll provide pre-built Grafana dashboards for:
- Provider performance and failover tracking
- NeuroChain multi-layer reasoning insights
- Cost analysis and budget alerts
- Hallucination detection trends
- Fault detection and recovery metrics

## Advanced Configuration

### Custom Telemetry Implementation

```csharp
public class CustomTelemetrySystem : ITelemetrySystem
{
    private readonly ILogger<CustomTelemetrySystem> _logger;
    private readonly IMetricCollector _customMetrics;

    public CustomTelemetrySystem(
        ILogger<CustomTelemetrySystem> logger,
        IMetricCollector customMetrics)
    {
        _logger = logger;
        _customMetrics = customMetrics;
    }

    public void TrackOperation(string operationId, string provider, TimeSpan duration, bool success, string? operationType = null)
    {
        // Custom implementation
        _logger.LogInformation("Operation {Id} completed in {Duration}ms", operationId, duration.TotalMilliseconds);
        _customMetrics.RecordOperation(provider, duration, success);

        // Still update Prometheus metrics
        HazinaMetrics.OperationDuration
            .WithLabels(provider, operationType ?? "unknown", success.ToString().ToLower())
            .Observe(duration.TotalMilliseconds);
    }

    // Implement other methods...
}

// Register custom implementation
builder.Services.AddSingleton<ITelemetrySystem, CustomTelemetrySystem>();
```

### Selective Health Checks

```csharp
builder.Services.AddHazinaObservability(options =>
{
    // Only enable provider health checks
    options.EnableProviderHealthChecks = true;
    options.EnableNeuroChainHealthChecks = false;
});
```

## Best Practices

1. **Always track costs** - Use `TrackCost()` after every LLM operation
2. **Track failures** - Use `TrackProviderFailover()` when providers fail
3. **Monitor hallucinations** - Integrate hallucination detection and track with `TrackHallucination()`
4. **Set up alerts** - Configure Prometheus alerts for:
   - High error rates (`hazina_operations_total{success="false"}`)
   - Excessive costs (`hazina_cost_usd_total`)
   - Provider health degradation (`hazina_provider_health < 0.5`)
5. **Use readiness probes** - Don't route traffic to pods that aren't ready

## Troubleshooting

**Metrics not showing up:**
- Ensure `app.UseHazinaObservability()` is called
- Check that `/metrics` endpoint is accessible
- Verify Prometheus scrape config

**Health checks failing:**
- Check that `IProviderOrchestrator` is registered in DI
- Verify providers are actually configured and running
- Check logs for detailed error messages

**High memory usage:**
- Prometheus metrics can accumulate - consider using histogram buckets wisely
- Use `_metrics.Clear()` periodically if needed (not recommended in production)

## Next Steps

- Set up Grafana dashboards (see `GRAFANA_DASHBOARDS.md` - coming soon)
- Configure distributed tracing with OpenTelemetry
- Integrate with your existing observability stack
