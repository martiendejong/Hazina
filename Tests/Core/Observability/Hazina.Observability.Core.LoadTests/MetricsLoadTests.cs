using Hazina.Observability.Core.Metrics;
using NBomber.CSharp;

namespace Hazina.Observability.Core.LoadTests;

public static class MetricsLoadTests
{
    public static void RunHighVolumeMetrics()
    {
        var scenario = Scenario.Create("high_volume_metrics", async context =>
        {
            var providerIndex = Random.Shared.Next(3);
            var provider = providerIndex == 0 ? "openai" :
                          providerIndex == 1 ? "anthropic" : "gemini";
            var success = Random.Shared.Next(100) < 95; // 95% success rate

            // Increment operations
            HazinaMetrics.OperationsTotal.WithLabels(provider, success.ToString().ToLower()).Inc();

            // Record duration
            var duration = Random.Shared.Next(50, 500);
            HazinaMetrics.OperationDuration.WithLabels(provider).Observe(duration);

            // Track cost
            var cost = 0.001m * Random.Shared.Next(1, 10);
            HazinaMetrics.TotalCost.WithLabels(provider).Inc((double)cost);

            // Track tokens
            var inputTokens = Random.Shared.Next(50, 200);
            var outputTokens = Random.Shared.Next(25, 100);
            HazinaMetrics.TokensUsed.WithLabels(provider, "input").Inc(inputTokens);
            HazinaMetrics.TokensUsed.WithLabels(provider, "output").Inc(outputTokens);

            // Update provider health
            var health = success ? 1.0 : 0.0;
            HazinaMetrics.ProviderHealth.WithLabels(provider).Set(health);

            return Response.Ok();
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            // High volume metric recording
            Simulation.Inject(rate: 500, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
        );

        NBomberRunner
            .RegisterScenarios(scenario)
            .Run();
    }

    public static void RunMetricsBurstLoad()
    {
        var scenario = Scenario.Create("metrics_burst_load", async context =>
        {
            var provider = "openai";

            // Burst of metric updates
            for (int i = 0; i < 10; i++)
            {
                HazinaMetrics.OperationsTotal.WithLabels(provider, "true").Inc();
                HazinaMetrics.OperationDuration.WithLabels(provider).Observe(Random.Shared.Next(50, 500));
                HazinaMetrics.TotalCost.WithLabels(provider).Inc(0.001);
                HazinaMetrics.TokensUsed.WithLabels(provider, "input").Inc(100);
                HazinaMetrics.TokensUsed.WithLabels(provider, "output").Inc(50);
            }

            return Response.Ok();
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.Inject(rate: 100, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(10))
        );

        NBomberRunner
            .RegisterScenarios(scenario)
            .Run();
    }

    public static void RunComplexMetricsScenario()
    {
        var scenario = Scenario.Create("complex_metrics_scenario", async context =>
        {
            var provider = Random.Shared.Next(2) == 0 ? "openai" : "anthropic";
            var success = Random.Shared.Next(100) < 90; // 90% success rate

            // Standard operation metrics
            HazinaMetrics.OperationsTotal.WithLabels(provider, success.ToString().ToLower()).Inc();
            HazinaMetrics.OperationDuration.WithLabels(provider).Observe(Random.Shared.Next(100, 300));
            HazinaMetrics.TotalCost.WithLabels(provider).Inc(0.002);
            HazinaMetrics.TokensUsed.WithLabels(provider, "input").Inc(150);
            HazinaMetrics.TokensUsed.WithLabels(provider, "output").Inc(75);

            // Occasionally track special events
            if (Random.Shared.Next(100) < 10) // 10% hallucination detection
            {
                var hallucinationTypes = new[] { "factual_error", "inconsistency", "contradiction" };
                var type = hallucinationTypes[Random.Shared.Next(hallucinationTypes.Length)];
                HazinaMetrics.HallucinationsDetected.WithLabels(type).Inc();
            }

            if (Random.Shared.Next(100) < 5) // 5% failover rate
            {
                var fallbackProvider = provider == "openai" ? "anthropic" : "openai";
                HazinaMetrics.ProviderFailovers.WithLabels(provider, fallbackProvider).Inc();
            }

            if (Random.Shared.Next(100) < 15) // 15% NeuroChain usage
            {
                var layers = Random.Shared.Next(2, 5).ToString();
                var complexity = Random.Shared.Next(2) == 0 ? "high" : "medium";
                HazinaMetrics.NeuroChainLayersUsed.WithLabels(layers, complexity).Inc();
            }

            if (Random.Shared.Next(100) < 8) // 8% fault detection
            {
                var faultTypes = new[] { "timeout", "rate_limit", "invalid_response" };
                var faultType = faultTypes[Random.Shared.Next(faultTypes.Length)];
                var corrected = Random.Shared.Next(2) == 0 ? "true" : "false";
                HazinaMetrics.FaultsDetected.WithLabels(faultType, corrected).Inc();
            }

            // Update health
            var healthValue = success ? Random.Shared.NextDouble() * 0.2 + 0.8 : Random.Shared.NextDouble() * 0.5;
            HazinaMetrics.ProviderHealth.WithLabels(provider).Set(healthValue);

            return Response.Ok();
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            // Sustained complex metrics
            Simulation.Inject(rate: 150, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
        );

        NBomberRunner
            .RegisterScenarios(scenario)
            .Run();
    }
}
