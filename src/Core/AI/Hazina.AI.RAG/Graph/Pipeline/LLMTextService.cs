namespace Hazina.AI.RAG.Graph.Pipeline;

using Hazina.AI.Providers.Core;
using Hazina.LLMs;

/// <summary>
/// Simple text-in/text-out wrapper around IProviderOrchestrator.
/// </summary>
public class LLMTextService : ILLMTextService
{
    private readonly IProviderOrchestrator _orchestrator;

    public LLMTextService(IProviderOrchestrator orchestrator)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
    }

    public async Task<string> GetResponseAsync(
        string prompt,
        int maxTokens = 2000,
        float temperature = 0.3f,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<HazinaChatMessage>
        {
            new()
            {
                Role = HazinaMessageRole.User,
                Text = prompt
            }
        };

        var response = await _orchestrator.GetResponse(
            messages,
            HazinaChatResponseFormat.Text,
            toolsContext: null,
            images: null,
            cancellationToken);

        return response.Result ?? string.Empty;
    }
}
