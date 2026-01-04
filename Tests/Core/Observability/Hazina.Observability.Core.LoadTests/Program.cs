using Hazina.Observability.Core.LoadTests;
using NBomber.CSharp;

// Run all load test scenarios
Console.WriteLine("Hazina Observability Load Tests\n");
Console.WriteLine("================================\n");

// Run scenarios sequentially to avoid interference
TelemetryLoadTests.RunSustainedLoad();
Console.WriteLine("\n");

TelemetryLoadTests.RunSpikeLoad();
Console.WriteLine("\n");

TelemetryLoadTests.RunStressTest();
Console.WriteLine("\n");

ActivityLoadTests.RunConcurrentActivityCreation();
Console.WriteLine("\n");

ActivityLoadTests.RunNeuroChainActivityLoad();
Console.WriteLine("\n");

ActivityLoadTests.RunActivityErrorHandlingLoad();
Console.WriteLine("\n");

MetricsLoadTests.RunHighVolumeMetrics();
Console.WriteLine("\n");

MetricsLoadTests.RunMetricsBurstLoad();
Console.WriteLine("\n");

MetricsLoadTests.RunComplexMetricsScenario();
Console.WriteLine("\n\nAll load tests completed!");
