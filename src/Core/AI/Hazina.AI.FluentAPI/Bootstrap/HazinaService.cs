using System.Runtime.CompilerServices;
using Hazina.AgentFactory.Core;
using Hazina.AI.Providers.Core;

namespace Hazina.AI.FluentAPI.Bootstrap;

/// <summary>
/// Default implementation of <see cref="IHazinaService"/>.
/// Wraps <see cref="AgentManager"/> with async initialization and conversation management.
/// </summary>
internal sealed class HazinaService : IHazinaService
{
    private readonly AgentManager _manager;
    private readonly IProviderOrchestrator _orchestrator;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    /// <summary>
    /// Initializes a new instance of <see cref="HazinaService"/>.
    /// </summary>
    public HazinaService(AgentManager manager, IProviderOrchestrator orchestrator)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
    }

    /// <inheritdoc/>
    public async Task<HazinaServiceResponse> ChatAsync(
        string message,
        string? conversationId = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var agent = GetDefaultAgent();

        var response = await agent.Generator.GetResponse(
            message: message,
            cancel: cancellationToken,
            history: _manager.History,
            addRelevantDocuments: false,
            addFilesList: false,
            toolsContext: agent.Tools).ConfigureAwait(false);

        return new HazinaServiceResponse
        {
            Result = response.Result ?? string.Empty,
            ConversationId = conversationId,
            TokenUsage = response.TokenUsage is not null
                ? new HazinaTokenUsage
                {
                    TotalTokens = response.TokenUsage.TotalTokens,
                    TotalCost = response.TokenUsage.TotalCost,
                }
                : null,
        };
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> StreamChatAsync(
        string message,
        string? conversationId = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var agent = GetDefaultAgent();

        // DocumentGenerator.StreamResponse uses an Action<string> callback.
        // We bridge that to IAsyncEnumerable via a Channel.
        var channel = System.Threading.Channels.Channel.CreateUnbounded<string>();

        var streamTask = agent.Generator.StreamResponse(
            message: message,
            cancel: cancellationToken,
            onChunkReceived: chunk => channel.Writer.TryWrite(chunk),
            history: _manager.History,
            addRelevantDocuments: false,
            addFilesList: false,
            toolsContext: agent.Tools);

        // Complete the channel when streaming is done
        _ = streamTask.ContinueWith(
            _ => channel.Writer.TryComplete(),
            TaskScheduler.Default);

        await foreach (var chunk in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return chunk;
        }
    }

    /// <inheritdoc/>
    public HazinaServiceStatus GetStatus()
    {
        return new HazinaServiceStatus
        {
            IsReady = _initialized,
            AgentCount = _manager.Agents.Count,
            AgentNames = _manager.Agents.Select(a => a.Name).ToList(),
            Providers = ["openai", "anthropic"], // TODO: read from orchestrator when API is available
        };
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
            return;

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_initialized)
            {
                await _manager.LoadStoresAndAgents().ConfigureAwait(false);
                _initialized = true;
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    private HazinaAgent GetDefaultAgent()
    {
        var agent = _manager.Agents.FirstOrDefault()
            ?? throw new InvalidOperationException(
                "No agents are loaded. Verify your HazinaOptions.AgentsJson or AgentsJsonPath configuration.");
        return agent;
    }
}
