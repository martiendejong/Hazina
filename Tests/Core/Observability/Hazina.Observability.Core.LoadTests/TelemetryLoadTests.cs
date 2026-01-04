using Hazina.Observability.Core;
using Hazina.Observability.Core.Tracing;
using Microsoft.Extensions.Logging.Abstractions;
using NBomber.CSharp;

namespace Hazina.Observability.Core.LoadTests;

public static class TelemetryLoadTests
{
    public static void RunSustainedLoad()
    {
        var logger = NullLogger<TelemetrySystem>.Instance;
        var telemetry = new TelemetrySystem(logger);

        var scenario = Scenario.Create("telemetry_sustained_load", async context =>
        {
            var operationId = Guid.NewGuid().ToString();
            var provider = Random.Shared.Next(2) == 0 ? "openai" : "anthropic";
            var duration = TimeSpan.FromMilliseconds(Random.Shared.Next(50, 500));

            // Simulate typical telemetry operations
            telemetry.TrackOperation(operationId, provider, duration, true, "chat_completion");
            telemetry.TrackCost(provider, 0.001m * Random.Shared.Next(1, 10),
                Random.Shared.Next(50, 200),
                Random.Shared.Next(25, 100));

            return Response.Ok();
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.Inject(rate: 100, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
        );

        NBomberRunner
            .RegisterScenarios(scenario)
            .Run();
    }

    public static void RunSpikeLoad()
    {
        var logger = NullLogger<TelemetrySystem>.Instance;
        var telemetry = new TelemetrySystem(logger);

        var scenario = Scenario.Create("telemetry_spike_load", async context =>
        {
            var operationId = Guid.NewGuid().ToString();
            var provider = "openai";
            var duration = TimeSpan.FromMilliseconds(100);

            telemetry.TrackOperation(operationId, provider, duration, true, "chat_completion");
            telemetry.TrackHallucination(operationId, "factual_error", Random.Shared.NextDouble());
            telemetry.TrackCost(provider, 0.001m, 100, 50);

            return Response.Ok();
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            // Normal load
            Simulation.Inject(rate: 50, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(10)),
            // Spike
            Simulation.Inject(rate: 500, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(5)),
            // Back to normal
            Simulation.Inject(rate: 50, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(10))
        );

        NBomberRunner
            .RegisterScenarios(scenario)
            .Run();
    }

    public static void RunStressTest()
    {
        var logger = NullLogger<TelemetrySystem>.Instance;
        var telemetry = new TelemetrySystem(logger);

        var scenario = Scenario.Create("telemetry_stress_test", async context =>
        {
            var operationId = Guid.NewGuid().ToString();
            var providerIndex = Random.Shared.Next(3);
            var provider = providerIndex == 0 ? "openai" :
                          providerIndex == 1 ? "anthropic" : "gemini";

            // Simulate all telemetry operations
            telemetry.TrackOperation(operationId, provider, TimeSpan.FromMilliseconds(100), true, "chat_completion");
            telemetry.TrackCost(provider, 0.001m, 100, 50);
            telemetry.TrackNeuroChainLayers(operationId, 3, "high");
            telemetry.TrackFaultDetection(operationId, "timeout", true);

            if (Random.Shared.Next(100) < 5) // 5% hallucination rate
            {
                telemetry.TrackHallucination(operationId, "factual_error", Random.Shared.NextDouble());
            }

            if (Random.Shared.Next(100) < 2) // 2% failover rate
            {
                telemetry.TrackProviderFailover(provider, "fallback", "rate_limit");
            }

            return Response.Ok();
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            // Gradually increase load to find breaking point
            Simulation.RampingInject(rate: 100, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(10)),
            Simulation.RampingInject(rate: 300, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(10)),
            Simulation.RampingInject(rate: 500, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(10))
        );

        NBomberRunner
            .RegisterScenarios(scenario)
            .Run();
    }
}
