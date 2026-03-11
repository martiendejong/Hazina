using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hazina.AI.LocalLLM;

/// <summary>
/// Stub implementation for demo. Recognizes a few hardcoded patterns.
/// TODO: Replace with actual llama.cpp integration (8-12 hours estimated).
/// </summary>
public class StubIntentParser : IIntentParser
{
    private readonly ILogger<StubIntentParser> _logger;

    public StubIntentParser(ILogger<StubIntentParser>? logger = null)
    {
        _logger = logger ?? NullLogger<StubIntentParser>.Instance;
    }

    public Task<ParsedIntent?> ParseAsync(string userInput, CancellationToken ct = default)
    {
        var input = userInput.ToLowerInvariant().Trim();

        // Pattern: "show files" → query files
        if (input.Contains("show") && input.Contains("file"))
        {
            return Task.FromResult<ParsedIntent?>(new ParsedIntent
            {
                Type = "query",
                Target = "files",
                Confidence = 0.95,
                OriginalInput = userInput
            });
        }

        // Pattern: "help"
        if (input.Contains("help"))
        {
            return Task.FromResult<ParsedIntent?>(new ParsedIntent
            {
                Type = "help",
                Confidence = 1.0,
                OriginalInput = userInput
            });
        }

        // Unknown → low confidence
        return Task.FromResult<ParsedIntent?>(new ParsedIntent
        {
            Type = "unknown",
            Confidence = 0.3,
            OriginalInput = userInput
        });
    }
}
