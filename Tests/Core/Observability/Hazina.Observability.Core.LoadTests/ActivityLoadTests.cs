using Hazina.Observability.Core;
using Hazina.Observability.Core.Tracing;
using NBomber.CSharp;

namespace Hazina.Observability.Core.LoadTests;

public static class ActivityLoadTests
{
    public static void RunConcurrentActivityCreation()
    {
        var scenario = Scenario.Create("concurrent_activity_creation", async context =>
        {
            var operationType = "chat_completion";
            var providerIndex = Random.Shared.Next(3);
            var provider = providerIndex == 0 ? "openai" :
                          providerIndex == 1 ? "anthropic" : "gemini";
            var model = "gpt-4";

            // Create activity
            using var activity = HazinaActivitySource.StartLLMOperation(operationType, provider, model);

            // Simulate work
            await Task.Delay(Random.Shared.Next(10, 100));

            // Record cost
            var cost = 0.001m * Random.Shared.Next(1, 10);
            var inputTokens = Random.Shared.Next(50, 200);
            var outputTokens = Random.Shared.Next(25, 100);
            HazinaActivitySource.RecordCost(activity, cost, inputTokens, outputTokens);

            // Randomly add hallucination or error
            if (Random.Shared.Next(100) < 5)
            {
                HazinaActivitySource.RecordHallucination(activity, "factual_error", Random.Shared.NextDouble());
            }

            if (Random.Shared.Next(100) < 2)
            {
                HazinaActivitySource.RecordError(activity, new Exception("Simulated error"));
            }

            return Response.Ok();
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            // Test concurrent activity creation
            Simulation.Inject(rate: 200, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
        );

        NBomberRunner
            .RegisterScenarios(scenario)
            .Run();
    }

    public static void RunNeuroChainActivityLoad()
    {
        var scenario = Scenario.Create("neurochain_activity_load", async context =>
        {
            var prompt = "Analyze user sentiment and generate response";
            var layers = Random.Shared.Next(2, 5);

            // Create NeuroChain operation
            using var activity = HazinaActivitySource.StartNeuroChainOperation(prompt, layers);

            // Simulate work for each layer
            for (int i = 0; i < layers; i++)
            {
                await Task.Delay(Random.Shared.Next(20, 80));
            }

            // Record cost for the entire chain
            var totalCost = 0.001m * layers * Random.Shared.Next(1, 5);
            var totalInputTokens = Random.Shared.Next(100, 300) * layers;
            var totalOutputTokens = Random.Shared.Next(50, 150) * layers;
            HazinaActivitySource.RecordCost(activity, totalCost, totalInputTokens, totalOutputTokens);

            return Response.Ok();
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.Inject(rate: 50, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
        );

        NBomberRunner
            .RegisterScenarios(scenario)
            .Run();
    }

    public static void RunActivityErrorHandlingLoad()
    {
        var scenario = Scenario.Create("activity_error_handling", async context =>
        {
            var operationType = "chat_completion";
            var provider = "openai";

            using var activity = HazinaActivitySource.StartLLMOperation(operationType, provider);

            await Task.Delay(Random.Shared.Next(10, 50));

            // Simulate high error rate
            if (Random.Shared.Next(100) < 20) // 20% error rate
            {
                var errorTypes = new[] { "timeout", "rate_limit", "invalid_request", "service_unavailable" };
                var errorType = errorTypes[Random.Shared.Next(errorTypes.Length)];
                HazinaActivitySource.RecordError(activity, new Exception($"Simulated {errorType} error"));
            }
            else
            {
                HazinaActivitySource.RecordCost(activity, 0.001m, 100, 50);
            }

            return Response.Ok();
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.Inject(rate: 100, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(20))
        );

        NBomberRunner
            .RegisterScenarios(scenario)
            .Run();
    }
}
