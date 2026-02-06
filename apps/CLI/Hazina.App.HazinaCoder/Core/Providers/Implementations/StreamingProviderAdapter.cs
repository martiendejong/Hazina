using Hazina.LLMs;
using Hazina.App.HazinaCoder.Core.Events;
using Hazina.App.HazinaCoder.Core.Streaming;

namespace Hazina.App.HazinaCoder.Core.Providers.Implementations;

/// <summary>
/// Adapts LLM providers to streaming architecture
/// Iteration 26: Provider streaming integration
/// </summary>
public class StreamingProviderAdapter
{
    private readonly ILLMClient _client;
    private readonly EventBus _eventBus;
    private readonly StreamingOrchestrator _orchestrator;

    public StreamingProviderAdapter(
        ILLMClient client,
        EventBus eventBus,
        StreamingOrchestrator orchestrator)
    {
        _client = client;
        _eventBus = eventBus;
        _orchestrator = orchestrator;
    }

    /// <summary>
    /// Stream response with event publishing (using callback-based streaming)
    /// </summary>
    public async IAsyncEnumerable<string> StreamResponseAsync(
        List<HazinaChatMessage> messages,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var chunks = new List<string>();

        // Use callback-based streaming API
        await _client.GetResponseStream(
            messages,
            chunk => chunks.Add(chunk),
            HazinaChatResponseFormat.Text,
            null,
            null,
            cancellationToken
        );

        // Yield all collected chunks
        foreach (var chunk in chunks)
        {
            yield return chunk;
        }
    }

    /// <summary>
    /// Non-streaming fallback
    /// </summary>
    public async Task<string> GetResponseAsync(
        List<HazinaChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        var response = await _client.GetResponse(messages, HazinaChatResponseFormat.Text, null, null, cancellationToken);
        return response.Result;
    }
}

/// <summary>
/// Provider with automatic failover
/// Iteration 27: Failover logic
/// </summary>
public class FailoverProvider
{
    private readonly List<StreamingProviderAdapter> _providers;
    private int _currentIndex = 0;

    public FailoverProvider(List<StreamingProviderAdapter> providers)
    {
        _providers = providers;
    }

    /// <summary>
    /// Try providers in order until one succeeds
    /// </summary>
    public async IAsyncEnumerable<string> StreamWithFailoverAsync(
        List<HazinaChatMessage> messages,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var attempts = 0;
        var maxAttempts = _providers.Count;
        Exception? lastException = null;

        while (attempts < maxAttempts)
        {
            var provider = _providers[_currentIndex];
            var success = false;

            List<string>? tokens = null;
            try
            {
                tokens = new List<string>();
                await foreach (var token in provider.StreamResponseAsync(messages, cancellationToken))
                {
                    tokens.Add(token);
                }
                success = true;
            }
            catch (Exception ex)
            {
                lastException = ex;
                // Try next provider
                _currentIndex = (_currentIndex + 1) % _providers.Count;
                attempts++;
            }

            if (success && tokens != null)
            {
                foreach (var token in tokens)
                {
                    yield return token;
                }
                yield break;
            }
        }

        // All providers failed
        throw lastException ?? new InvalidOperationException("All providers failed");
    }
}
