using Hazina.LLMs;
using Hazina.LLMs.OpenAI;
using Hazina.Tools.Services.Images.LayeredImage;
using Hazina.Tools.Services.Images.LayeredImage.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

Console.WriteLine("=== Hazina Smart Layered Image Demo ===\n");

// Get API key
var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
if (string.IsNullOrEmpty(apiKey))
{
    Console.Write("Enter your OpenAI API key: ");
    apiKey = Console.ReadLine();
}

if (string.IsNullOrEmpty(apiKey))
{
    Console.WriteLine("Error: OpenAI API key is required.");
    return;
}

// Get prompt
string prompt;
if (args.Length > 0 && args[0].StartsWith("--prompt="))
{
    prompt = args[0].Substring("--prompt=".Length).Trim('"');
}
else
{
    Console.WriteLine("Describe the layered image you want to create:");
    Console.WriteLine("Example: 'Een luxe straat met een villa, een witte Opel Combo met SCP logo op de zijkant geparkeerd op de weg'");
    Console.Write("\nYour prompt: ");
    prompt = Console.ReadLine() ?? "";
}

if (string.IsNullOrWhiteSpace(prompt))
{
    Console.WriteLine("Error: No prompt provided.");
    return;
}

// Build services
var services = new ServiceCollection();

// Add logging
services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

// Add LLM client
var openAiConfig = new OpenAIConfig(apiKey, "gpt-4o");
services.AddSingleton<ILLMClient>(new OpenAIClientWrapper(openAiConfig));

// Add layered image services
services.AddLayeredImageServices();

// Note: Full setup requires IChatImageService and repositories
// For this demo, we'll show the expected usage pattern

var serviceProvider = services.BuildServiceProvider();

Console.WriteLine("\n--- Service Setup ---");
Console.WriteLine("Note: This demo shows the usage pattern.");
Console.WriteLine("Full integration requires IChatImageService for AI image generation.");

Console.WriteLine("\n--- Expected Usage ---");
Console.WriteLine(@"
// In your application with full DI:

var smartService = serviceProvider.GetRequiredService<ISmartLayeredImageService>();

var result = await smartService.GenerateFromPromptAsync(
    prompt: ""Een luxe straat met een villa, een witte Opel Combo met SCP op de zijkant"",
    width: 1024,
    height: 1024,
    format: ""pdn"");

if (result.Success)
{
    // Save the layered file
    await File.WriteAllBytesAsync(""output.pdn"", result.LayeredImageData!);

    // Save the preview
    await File.WriteAllBytesAsync(""preview.png"", result.CompositePreview!);

    Console.WriteLine($""Generated {result.Layers.Count} layers"");
    Console.WriteLine($""Composition reasoning: {result.CompositionReasoning}"");
}
");

Console.WriteLine("--- Prompt Received ---");
Console.WriteLine($"Your prompt: \"{prompt}\"");
Console.WriteLine("\nTo run the full integration, use the SmartLayeredImageService through the Hazina API.");
