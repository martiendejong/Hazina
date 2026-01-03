# Quick Start Implementation Guide
## Get Results This Week

**Goal:** Have observable, tested, documented code in 5 days (12 hours total)

---

## Day 1: Telemetry (2 hours)

### Create Observability Project
```bash
cd C:/projects/hazina/src/Core
mkdir -p Observability/Hazina.Observability.Core
cd Observability/Hazina.Observability.Core
dotnet new classlib -f net9.0
dotnet add package Microsoft.Extensions.Logging
```

### Create TelemetrySystem.cs
```csharp
// src/Core/Observability/Hazina.Observability.Core/TelemetrySystem.cs
using Microsoft.Extensions.Logging;

namespace Hazina.Observability.Core;

public interface ITelemetrySystem
{
    void TrackOperation(string operationId, string provider, TimeSpan duration, bool success);
    void TrackHallucination(string operationId, string type, double confidence);
    void TrackProviderFailover(string from, string to, string reason);
}

public class TelemetrySystem : ITelemetrySystem
{
    private readonly ILogger<TelemetrySystem> _logger;

    public TelemetrySystem(ILogger<TelemetrySystem> logger)
    {
        _logger = logger;
    }

    public void TrackOperation(string operationId, string provider, TimeSpan duration, bool success)
    {
        _logger.LogInformation(
            "[TELEMETRY] Operation: {OperationId} | Provider: {Provider} | Duration: {Duration}ms | Success: {Success}",
            operationId, provider, duration.TotalMilliseconds, success);
    }

    public void TrackHallucination(string operationId, string type, double confidence)
    {
        _logger.LogWarning(
            "[TELEMETRY] Hallucination detected | Operation: {OperationId} | Type: {Type} | Confidence: {Confidence:P0}",
            operationId, type, confidence);
    }

    public void TrackProviderFailover(string from, string to, string reason)
    {
        _logger.LogWarning(
            "[TELEMETRY] Provider failover | From: {From} → To: {To} | Reason: {Reason}",
            from, to, reason);
    }
}
```

### Integrate into ProviderOrchestrator
```csharp
// src/Core/AI/Hazina.AI.Providers/Core/ProviderOrchestrator.cs
// Add to constructor:
private readonly ITelemetrySystem? _telemetry;

public ProviderOrchestrator(ITelemetrySystem? telemetry = null)
{
    _telemetry = telemetry;
}

// In SendAsync method:
var stopwatch = Stopwatch.StartNew();
try
{
    var result = await provider.SendAsync(messages, cancellationToken);

    _telemetry?.TrackOperation(
        operationId: Guid.NewGuid().ToString(),
        provider: selectedProvider,
        duration: stopwatch.Elapsed,
        success: true
    );

    return result;
}
catch (Exception ex)
{
    _telemetry?.TrackOperation(
        operationId: Guid.NewGuid().ToString(),
        provider: selectedProvider,
        duration: stopwatch.Elapsed,
        success: false
    );
    throw;
}
```

**✅ Done:** All operations now logged with structured data

---

## Day 2: Prometheus Metrics (3 hours)

### Add Prometheus Package
```bash
cd C:/projects/hazina/src/Core/Observability/Hazina.Observability.Core
dotnet add package prometheus-net
```

### Create HazinaMetrics.cs
```csharp
// src/Core/Observability/Hazina.Observability.Core/Metrics/HazinaMetrics.cs
using Prometheus;

namespace Hazina.Observability.Core.Metrics;

public static class HazinaMetrics
{
    // Operation duration histogram
    public static readonly Histogram OperationDuration = Metrics.CreateHistogram(
        "hazina_operation_duration_ms",
        "Duration of AI operations in milliseconds",
        new HistogramConfiguration
        {
            LabelNames = new[] { "provider", "operation_type", "success" },
            Buckets = Histogram.ExponentialBuckets(10, 2, 10) // 10ms to 5s
        }
    );

    // Provider health gauge
    public static readonly Gauge ProviderHealth = Metrics.CreateGauge(
        "hazina_provider_health",
        "Provider health score (0-1)",
        new GaugeConfiguration { LabelNames = new[] { "provider" } }
    );

    // Hallucination detection counter
    public static readonly Counter HallucinationsDetected = Metrics.CreateCounter(
        "hazina_hallucinations_detected_total",
        "Total hallucinations detected",
        new CounterConfiguration { LabelNames = new[] { "type" } }
    );

    // Cost tracking
    public static readonly Counter TotalCost = Metrics.CreateCounter(
        "hazina_cost_usd_total",
        "Total cost in USD",
        new CounterConfiguration { LabelNames = new[] { "provider" } }
    );

    // Success rate counter
    public static readonly Counter OperationsTotal = Metrics.CreateCounter(
        "hazina_operations_total",
        "Total operations",
        new CounterConfiguration { LabelNames = new[] { "provider", "success" } }
    );
}
```

### Enhanced TelemetrySystem with Metrics
```csharp
public class TelemetrySystem : ITelemetrySystem
{
    private readonly ILogger<TelemetrySystem> _logger;

    public void TrackOperation(string operationId, string provider, TimeSpan duration, bool success)
    {
        // Log
        _logger.LogInformation(
            "[TELEMETRY] Operation: {OperationId} | Provider: {Provider} | Duration: {Duration}ms | Success: {Success}",
            operationId, provider, duration.TotalMilliseconds, success);

        // Metrics
        HazinaMetrics.OperationDuration
            .WithLabels(provider, "chat", success.ToString())
            .Observe(duration.TotalMilliseconds);

        HazinaMetrics.OperationsTotal
            .WithLabels(provider, success.ToString())
            .Inc();
    }

    public void TrackHallucination(string operationId, string type, double confidence)
    {
        _logger.LogWarning(
            "[TELEMETRY] Hallucination detected | Operation: {OperationId} | Type: {Type} | Confidence: {Confidence:P0}",
            operationId, type, confidence);

        HazinaMetrics.HallucinationsDetected
            .WithLabels(type)
            .Inc();
    }

    public void TrackCost(string provider, decimal cost)
    {
        HazinaMetrics.TotalCost
            .WithLabels(provider)
            .Inc((double)cost);
    }
}
```

### Expose Metrics Endpoint
```csharp
// In an ASP.NET Core app (or create a minimal API for testing)
// Program.cs
app.UseMetricServer(); // Exposes /metrics endpoint

// OR for testing without web server:
var metricServer = new MetricServer(port: 9090);
metricServer.Start();
```

### Test Locally
```bash
# Run your app
dotnet run

# In another terminal, check metrics
curl http://localhost:9090/metrics

# Should see:
# hazina_operation_duration_ms_bucket{provider="openai",operation_type="chat",success="true",le="10"} 0
# hazina_operations_total{provider="openai",success="true"} 42
# ...
```

**✅ Done:** Prometheus metrics exposed and scrapable

---

## Day 3: Grafana Dashboard (2 hours)

### Option 1: Grafana Cloud (Easiest)
1. Sign up at https://grafana.com/auth/sign-up (free tier)
2. Add Prometheus data source pointing to your app
3. Import dashboard from JSON below

### Option 2: Local Grafana
```bash
# Using Docker
docker run -d -p 3000:3000 --name=grafana grafana/grafana

# Open http://localhost:3000
# Default login: admin/admin
```

### Create Dashboard JSON
Save as `grafana-dashboard-operations.json`:

```json
{
  "dashboard": {
    "title": "Hazina Operations",
    "panels": [
      {
        "title": "Operations per Second",
        "targets": [
          {
            "expr": "rate(hazina_operations_total[5m])"
          }
        ],
        "type": "graph"
      },
      {
        "title": "Success Rate",
        "targets": [
          {
            "expr": "sum(rate(hazina_operations_total{success=\"true\"}[5m])) / sum(rate(hazina_operations_total[5m]))"
          }
        ],
        "type": "singlestat"
      },
      {
        "title": "P95 Latency",
        "targets": [
          {
            "expr": "histogram_quantile(0.95, rate(hazina_operation_duration_ms_bucket[5m]))"
          }
        ],
        "type": "graph"
      },
      {
        "title": "Provider Distribution",
        "targets": [
          {
            "expr": "sum by (provider) (rate(hazina_operations_total[5m]))"
          }
        ],
        "type": "piechart"
      }
    ]
  }
}
```

### Import to Grafana
1. In Grafana: Dashboards → Import
2. Upload `grafana-dashboard-operations.json`
3. Select Prometheus data source
4. Click Import

**✅ Done:** Live dashboard showing operation metrics

---

## Day 4: Unit Tests (3 hours)

### Create Test Project
```bash
cd C:/projects/hazina/tests
mkdir -p Unit/Hazina.AI.FaultDetection.Tests
cd Unit/Hazina.AI.FaultDetection.Tests
dotnet new xunit -f net9.0
dotnet add package FluentAssertions
dotnet add package Moq
dotnet add reference ../../../src/Core/AI/Hazina.AI.FaultDetection/Hazina.AI.FaultDetection.csproj
```

### Write Tests for HallucinationDetector
```csharp
// tests/Unit/Hazina.AI.FaultDetection.Tests/HallucinationDetectorTests.cs
using FluentAssertions;
using Hazina.AI.FaultDetection.Detectors;
using Xunit;

namespace Hazina.AI.FaultDetection.Tests;

public class HallucinationDetectorTests
{
    private readonly BasicHallucinationDetector _detector;

    public HallucinationDetectorTests()
    {
        _detector = new BasicHallucinationDetector();
    }

    [Fact]
    public async Task DetectAsync_ObviousFabrication_DetectsHallucination()
    {
        // Arrange
        var response = "The capital of Mars is definitely New York City.";
        var context = new ValidationContext();

        // Act
        var result = await _detector.DetectAsync(response, context);

        // Assert
        result.IsHallucination.Should().BeTrue();
        result.Type.Should().Be(HallucinationType.FabricatedFact);
        result.Explanation.Should().Contain("Mars");
    }

    [Theory]
    [InlineData("The sky is blue", false)]
    [InlineData("2 + 2 = 5", true)]
    [InlineData("Water freezes at 100 degrees Celsius", true)]
    public async Task DetectAsync_FactualStatements_CorrectlyClassifies(
        string response,
        bool expectedHallucination)
    {
        // Arrange
        var context = new ValidationContext();

        // Act
        var result = await _detector.DetectAsync(response, context);

        // Assert
        result.IsHallucination.Should().Be(expectedHallucination);
    }

    [Fact]
    public async Task DetectAsync_ContradictionWithGroundTruth_DetectsHallucination()
    {
        // Arrange
        var response = "The company was founded in 2019.";
        var context = new ValidationContext
        {
            GroundTruths = new Dictionary<string, string>
            {
                ["company_founded"] = "2020"
            }
        };

        // Act
        var result = await _detector.DetectAsync(response, context);

        // Assert
        result.IsHallucination.Should().BeTrue();
        result.Type.Should().Be(HallucinationType.Contradiction);
    }
}
```

### Write Tests for AdaptiveFaultHandler
```csharp
// tests/Unit/Hazina.AI.FaultDetection.Tests/AdaptiveFaultHandlerTests.cs
using FluentAssertions;
using Hazina.AI.FaultDetection.Core;
using Moq;
using Xunit;

namespace Hazina.AI.FaultDetection.Tests;

public class AdaptiveFaultHandlerTests
{
    [Fact]
    public async Task ExecuteWithFaultDetectionAsync_HighConfidence_NoRetry()
    {
        // Arrange
        var mockOrchestrator = new Mock<IProviderOrchestrator>();
        mockOrchestrator
            .Setup(o => o.SendAsync(It.IsAny<List<HazinaChatMessage>>(), default))
            .ReturnsAsync(new LLMResponse { Content = "Valid response", Confidence = 0.95 });

        var handler = new AdaptiveFaultHandler(
            mockOrchestrator.Object,
            new BasicResponseValidator(),
            new BasicHallucinationDetector(),
            new BasicErrorPatternRecognizer(),
            new BasicConfidenceScorer()
        );

        var messages = new List<HazinaChatMessage>
        {
            new() { Role = HazinaMessageRole.User, Text = "Test" }
        };

        // Act
        var result = await handler.ExecuteWithFaultDetectionAsync(
            messages,
            new ValidationContext(),
            CancellationToken.None
        );

        // Assert
        result.Response.Should().Be("Valid response");
        result.Confidence.Should().Be(0.95);
        mockOrchestrator.Verify(
            o => o.SendAsync(It.IsAny<List<HazinaChatMessage>>(), default),
            Times.Once // Should only call once, no retry
        );
    }

    [Fact]
    public async Task ExecuteWithFaultDetectionAsync_LowConfidence_RetriesWithRefinedPrompt()
    {
        // Arrange
        var mockOrchestrator = new Mock<IProviderOrchestrator>();
        mockOrchestrator
            .SetupSequence(o => o.SendAsync(It.IsAny<List<HazinaChatMessage>>(), default))
            .ReturnsAsync(new LLMResponse { Content = "Uncertain response", Confidence = 0.5 })
            .ReturnsAsync(new LLMResponse { Content = "Better response", Confidence = 0.9 });

        var handler = new AdaptiveFaultHandler(
            mockOrchestrator.Object,
            new BasicResponseValidator(),
            new BasicHallucinationDetector(),
            new BasicErrorPatternRecognizer(),
            new BasicConfidenceScorer()
        );

        // Act
        var result = await handler.ExecuteWithFaultDetectionAsync(
            new List<HazinaChatMessage> { new() { Role = HazinaMessageRole.User, Text = "Test" } },
            new ValidationContext(),
            CancellationToken.None
        );

        // Assert
        result.Response.Should().Be("Better response");
        mockOrchestrator.Verify(
            o => o.SendAsync(It.IsAny<List<HazinaChatMessage>>(), default),
            Times.Exactly(2) // Should retry once
        );
    }
}
```

### Run Tests
```bash
cd tests/Unit/Hazina.AI.FaultDetection.Tests
dotnet test

# Check coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

**✅ Done:** 10+ tests written and passing

---

## Day 5: Getting Started Guide (2 hours)

### Create docs/GettingStarted.md
```markdown
# Getting Started with Hazina

**Get up and running in 15 minutes**

---

## Quick Install

```bash
dotnet add package Hazina.AI.FluentAPI
dotnet add package Hazina.AI.FaultDetection
```

## Hello World (2 minutes)

```csharp
using Hazina.AI.FluentAPI;

// Setup with your API keys
QuickSetup.SetupAndConfigure(
    openAIKey: "sk-...",
    anthropicKey: "sk-ant-..."  // Optional for failover
);

// Ask a question
var result = await Hazina.AI()
    .Ask("What is the capital of France?")
    .ExecuteAsync();

Console.WriteLine(result.Response);
// Output: "The capital of France is Paris."
```

## With Fault Detection (5 minutes)

Hazina automatically detects and corrects hallucinations:

```csharp
var result = await Hazina.AI()
    .WithFaultDetection(confidence: 0.9)  // Require 90% confidence
    .Ask("What is 2 + 2?")
    .ExecuteAsync();

Console.WriteLine($"Answer: {result.Response}");
Console.WriteLine($"Confidence: {result.Confidence:P0}");
Console.WriteLine($"Hallucinations detected: {result.HallucinationsDetected}");
```

## Multi-Layer Reasoning (8 minutes)

For complex questions requiring deep reasoning:

```csharp
using Hazina.Neurochain.Core;

var orchestrator = QuickSetup.SetupWithFailover("sk-...", "sk-ant-...");
var neurochain = new NeuroChainOrchestrator(orchestrator);

var result = await neurochain.ProcessAsync(
    "Explain quantum entanglement in simple terms",
    complexity: TaskComplexity.Complex
);

Console.WriteLine($"Answer: {result.Answer}");
Console.WriteLine($"Layers used: {string.Join(" → ", result.LayersUsed)}");
Console.WriteLine($"Confidence: {result.Confidence:P0}");
```

## Multi-Provider Failover

Hazina automatically switches providers if one fails:

```csharp
// If OpenAI fails, automatically uses Anthropic
var result = await Hazina.AI()
    .Ask("Hello")
    .ExecuteAsync();

Console.WriteLine($"Responded via: {result.Provider}");
```

## Next Steps

- [Tutorial: Building a RAG System](tutorials/BuildingRAG.md)
- [Tutorial: Code Generation](tutorials/CodeGeneration.md)
- [API Reference](api/README.md)
- [Examples](../examples/README.md)

## Need Help?

- GitHub Issues: https://github.com/hazina/hazina/issues
- Documentation: https://docs.hazina.dev
```

### Create examples/01-HelloWorld/Program.cs
```csharp
using Hazina.AI.FluentAPI;

// Simple setup
QuickSetup.SetupAndConfigure(
    openAIKey: Environment.GetEnvironmentVariable("OPENAI_API_KEY")!
);

// Basic question
Console.WriteLine("Ask me anything:");
var question = Console.ReadLine();

var result = await Hazina.AI()
    .Ask(question ?? "What is AI?")
    .ExecuteAsync();

Console.WriteLine($"\nAnswer: {result.Response}");
Console.WriteLine($"Provider: {result.Provider}");
```

**✅ Done:** New users can be productive in 15 minutes

---

## Results After 5 Days

### You Now Have:

✅ **Observability**
- Structured logging of all operations
- Prometheus metrics exposed
- Live Grafana dashboard
- Visible proof of system health

✅ **Testing**
- 10+ unit tests with 70%+ coverage on key modules
- Test framework established
- Can prove code quality

✅ **Documentation**
- Getting started guide (15-minute onboarding)
- Working examples
- Clear path for new users

### Impact:

- **Production Credibility:** Can show live metrics proving reliability
- **Quality Assurance:** Tests prove core functionality works
- **User Adoption:** Clear docs enable others to use Hazina
- **CV Claims:** Can now demonstrate "95% uptime" with real data

---

## Next Week: Continue with Remaining Items

Once you have these fundamentals:

1. **Week 2:** Complete testing (integration tests, benchmarks, load tests)
2. **Week 3:** Start code generation pipeline
3. **Week 4:** More documentation and tutorials
4. **Week 5:** Polish and publish

---

## Quick Reference Commands

```bash
# Run metrics server
dotnet run --project your-app

# Check metrics
curl http://localhost:9090/metrics

# Run tests
dotnet test

# Check coverage
dotnet test /p:CollectCoverage=true

# View Grafana
open http://localhost:3000
```

---

**Start with Day 1 tomorrow and you'll have results by Friday!**
