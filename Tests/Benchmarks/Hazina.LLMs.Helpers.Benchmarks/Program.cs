using BenchmarkDotNet.Running;
using Hazina.LLMs.Helpers.Benchmarks;

// Run all benchmarks
Console.WriteLine("Hazina LLM Helpers Benchmarks");
Console.WriteLine("==============================");
Console.WriteLine();
Console.WriteLine("Select benchmark to run:");
Console.WriteLine("1. Document Splitter Benchmarks");
Console.WriteLine("2. TokensPerPart Configuration Benchmarks");
Console.WriteLine("3. Token Counter Benchmarks");
Console.WriteLine("4. Token Counter Comparison Benchmarks");
Console.WriteLine("5. String Processing Benchmarks");
Console.WriteLine("6. All Benchmarks");
Console.WriteLine();
Console.Write("Enter choice (1-6): ");

var choice = Console.ReadLine();

switch (choice)
{
    case "1":
        BenchmarkRunner.Run<DocumentSplitterBenchmarks>();
        break;
    case "2":
        BenchmarkRunner.Run<TokensPerPartBenchmarks>();
        break;
    case "3":
        BenchmarkRunner.Run<TokenCounterBenchmarks>();
        break;
    case "4":
        BenchmarkRunner.Run<TokenCounterComparisonBenchmarks>();
        break;
    case "5":
        BenchmarkRunner.Run<StringProcessingBenchmarks>();
        break;
    case "6":
        Console.WriteLine("\nRunning all benchmarks...\n");
        BenchmarkRunner.Run<DocumentSplitterBenchmarks>();
        BenchmarkRunner.Run<TokensPerPartBenchmarks>();
        BenchmarkRunner.Run<TokenCounterBenchmarks>();
        BenchmarkRunner.Run<TokenCounterComparisonBenchmarks>();
        BenchmarkRunner.Run<StringProcessingBenchmarks>();
        break;
    default:
        Console.WriteLine("Invalid choice. Running all benchmarks...");
        BenchmarkRunner.Run<DocumentSplitterBenchmarks>();
        BenchmarkRunner.Run<TokensPerPartBenchmarks>();
        BenchmarkRunner.Run<TokenCounterBenchmarks>();
        BenchmarkRunner.Run<TokenCounterComparisonBenchmarks>();
        BenchmarkRunner.Run<StringProcessingBenchmarks>();
        break;
}

Console.WriteLine("\nBenchmark complete. Results saved to BenchmarkDotNet.Artifacts/");
