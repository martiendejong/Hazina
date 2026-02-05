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
    private readonly AgentEventBus _eventBus;
    private readonly StreamingOrchestrator _orchestrator;

    public StreamingProviderAdapter(
        ILLMClient client,
        AgentEventBus eventBus,
        StreamingOrchestrator orchestrator)
    {
        _client = client;
        _eventBus = eventBus;
        _orchestrator = orchestrator;
    }

    /// <summary>
    /// Stream response with event publishing
    /// </summary>
    public async IAsyncEnumerable<string> StreamResponseAsync(
        List<HazinaChatMessage> messages,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _eventBus.Publish(new AgentEvent
        {
            Type = "StreamStarted",
            Timestamp = DateTime.UtcNow
        });

        var tokenCount = 0;

        try
        {
            var interaction = _client.StartChatInteraction(messages);

            await foreach (var token in interaction.StreamResponseAsync(cancellationToken))
            {
                tokenCount++;

                // Publish token event
                _eventBus.Publish(new AgentEvent
                {
                    Type = "TokenReceived",
                    Data = token,
                    Timestamp = DateTime.UtcNow
                });

                yield return token;

                // Progress updates every 10 tokens
                if (tokenCount % 10 == 0)
                {
                    _eventBus.Publish(new AgentEvent
                    {
                        Type = "Progress",
                        Data = $"{tokenCount} tokens",
                        Timestamp = DateTime.UtcNow
                    });
                }
            }

            _eventBus.Publish(new AgentEvent
            {
                Type = "StreamCompleted",
                Data = $"Total: {tokenCount} tokens",
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _eventBus.Publish(new AgentEvent
            {
                Type = "StreamError",
                Data = ex.Message,
                Timestamp = DateTime.UtcNow
            });

            throw;
        }
    }

    /// <summary>
    /// Non-streaming fallback
    /// </summary>
    public async Task<string> GetResponseAsync(
        List<HazinaChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        var interaction = _client.StartChatInteraction(messages);
        return await interaction.GetResponseAsync(cancellationToken);
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

        while (attempts < maxAttempts)
        {
            var provider = _providers[_currentIndex];

            try
            {
                await foreach (var token in provider.StreamResponseAsync(messages, cancellationToken))
                {
                    yield return token;
                }

                yield break; // Success - exit
            }
            catch (Exception)
            {
                // Try next provider
                _currentIndex = (_currentIndex + 1) % _providers.Count;
                attempts++;

                if (attempts >= maxAttempts)
                    throw; // All providers failed
            }
        }
    }
}
