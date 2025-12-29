using Hazina.LLMs.GoogleADK.Agents;
using Hazina.LLMs.GoogleADK.Artifacts;
using Hazina.LLMs.GoogleADK.Artifacts.Models;
using Hazina.LLMs.GoogleADK.Artifacts.Storage;
using Hazina.LLMs.GoogleADK.Core;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Examples;

/// <summary>
/// Examples for Artifact Management (Step 9)
/// </summary>
public class ArtifactExamples
{
    /// <summary>
    /// Example 1: Basic artifact creation
    /// </summary>
    public static async Task BasicArtifactExample(ILogger logger)
    {
        var storage = new FileSystemArtifactStorage("./artifacts", logger);
        var manager = new ArtifactManager(storage, logger);

        // Create text artifact
        var textArtifact = await manager.CreateFromTextAsync(
            "This is a generated report",
            "report.txt",
            agentId: "report-agent"
        );

        Console.WriteLine($"Created artifact: {textArtifact.ArtifactId}");
        Console.WriteLine($"  Name: {textArtifact.Name}");
        Console.WriteLine($"  Size: {textArtifact.Size} bytes");
        Console.WriteLine($"  Type: {textArtifact.Type}");

        // Retrieve artifact
        var retrieved = await manager.GetArtifactTextAsync(textArtifact.ArtifactId);
        Console.WriteLine($"\nRetrieved content: {retrieved}");

        await manager.DisposeAsync();
    }

    /// <summary>
    /// Example 2: Agent producing artifacts
    /// </summary>
    public static async Task AgentProducingArtifactsExample(ILLMClient llmClient, ILogger logger)
    {
        var storage = new FileSystemArtifactStorage("./artifacts", logger);
        var manager = new ArtifactManager(storage, logger);

        var agent = new ArtifactEnabledAgent("CodeGenerator", llmClient, manager);
        await agent.InitializeAsync();

        // Agent produces code artifact
        var code = @"
public class HelloWorld
{
    public static void Main()
    {
        Console.WriteLine(""Hello, World!"");
    }
}";

        var artifact = await agent.ProduceTextArtifactAsync(
            code,
            "HelloWorld.cs",
            tags: new List<string> { "code", "csharp", "generated" }
        );

        Console.WriteLine($"Agent produced artifact: {artifact.Name}");
        Console.WriteLine($"  ID: {artifact.ArtifactId}");
        Console.WriteLine($"  Tags: {string.Join(", ", artifact.Tags)}");

        // Get all artifacts produced by agent
        var produced = await agent.GetProducedArtifactsAsync();
        Console.WriteLine($"\nTotal artifacts produced: {produced.Count}");

        await agent.DisposeAsync();
        await manager.DisposeAsync();
    }

    /// <summary>
    /// Example 3: File-based artifacts
    /// </summary>
    public static async Task FileArtifactExample(ILogger logger)
    {
        var storage = new FileSystemArtifactStorage("./artifacts", logger);
        var manager = new ArtifactManager(storage, logger);

        // Create a test file
        var testFilePath = "test-data.json";
        await File.WriteAllTextAsync(testFilePath, "{\"message\": \"test data\"}");

        // Create artifact from file
        var artifact = await manager.CreateFromFileAsync(
            testFilePath,
            name: "data.json",
            agentId: "data-agent"
        );

        Console.WriteLine($"Created file artifact: {artifact.Name}");
        Console.WriteLine($"  MIME Type: {artifact.MimeType}");
        Console.WriteLine($"  Size: {artifact.Size} bytes");

        // Export to different location
        var exportPath = "exported-data.json";
        await manager.ExportToFileAsync(artifact.ArtifactId, exportPath);

        Console.WriteLine($"Exported to: {exportPath}");

        // Cleanup
        File.Delete(testFilePath);
        File.Delete(exportPath);

        await manager.DisposeAsync();
    }

    /// <summary>
    /// Example 4: Searching artifacts
    /// </summary>
    public static async Task SearchArtifactsExample(ILogger logger)
    {
        var storage = new FileSystemArtifactStorage("./artifacts", logger);
        var manager = new ArtifactManager(storage, logger);

        // Create several artifacts
        await manager.CreateFromTextAsync("Report 1", "report1.txt", "agent-1");
        await manager.CreateFromTextAsync("Report 2", "report2.txt", "agent-1");
        await manager.CreateFromTextAsync("Data", "data.json", "agent-2");

        // Search by agent
        var agent1Artifacts = await manager.SearchAsync(new ArtifactQuery
        {
            AgentId = "agent-1"
        });

        Console.WriteLine($"Artifacts from agent-1: {agent1Artifacts.Count}");
        foreach (var artifact in agent1Artifacts)
        {
            Console.WriteLine($"  - {artifact.Name} ({artifact.Type})");
        }

        // Search by type
        var textArtifacts = await manager.SearchAsync(new ArtifactQuery
        {
            Type = ArtifactType.Text
        });

        Console.WriteLine($"\nText artifacts: {textArtifacts.Count}");

        await manager.DisposeAsync();
    }

    /// <summary>
    /// Example 5: Binary artifacts
    /// </summary>
    public static async Task BinaryArtifactExample(ILLMClient llmClient, ILogger logger)
    {
        var storage = new FileSystemArtifactStorage("./artifacts", logger);
        var manager = new ArtifactManager(storage, logger);
        var agent = new ArtifactEnabledAgent("ImageProcessor", llmClient, manager);

        await agent.InitializeAsync();

        // Simulate processing and producing binary data
        var imageData = new byte[1024]; // Simulated image data
        new Random().NextBytes(imageData);

        var artifact = await agent.ProduceBinaryArtifactAsync(
            imageData,
            "processed-image.png",
            mimeType: "image/png",
            tags: new List<string> { "image", "processed" }
        );

        Console.WriteLine($"Created binary artifact: {artifact.Name}");
        Console.WriteLine($"  Type: {artifact.Type}");
        Console.WriteLine($"  Size: {artifact.Size} bytes");
        Console.WriteLine($"  MIME: {artifact.MimeType}");

        // Retrieve binary data
        var retrievedData = await agent.GetArtifactDataAsync(artifact.ArtifactId);
        Console.WriteLine($"\nRetrieved {retrievedData?.Length} bytes");

        await agent.DisposeAsync();
        await manager.DisposeAsync();
    }

    /// <summary>
    /// Example 6: Agent consuming artifacts
    /// </summary>
    public static async Task ConsumeArtifactsExample(ILLMClient llmClient, ILogger logger)
    {
        var storage = new FileSystemArtifactStorage("./artifacts", logger);
        var manager = new ArtifactManager(storage, logger);

        var producer = new ArtifactEnabledAgent("Producer", llmClient, manager);
        var consumer = new ArtifactEnabledAgent("Consumer", llmClient, manager);

        await producer.InitializeAsync();
        await consumer.InitializeAsync();

        // Producer creates artifact
        var artifact = await producer.ProduceTextArtifactAsync(
            "Shared data between agents",
            "shared-data.txt",
            tags: new List<string> { "shared" }
        );

        Console.WriteLine($"Producer created: {artifact.Name}");

        // Consumer reads artifact
        var consumed = await consumer.ConsumeArtifactAsync(artifact.ArtifactId);
        var content = await consumer.GetArtifactTextAsync(artifact.ArtifactId);

        Console.WriteLine($"Consumer read: {consumed?.Name}");
        Console.WriteLine($"Content: {content}");

        await producer.DisposeAsync();
        await consumer.DisposeAsync();
        await manager.DisposeAsync();
    }
}
