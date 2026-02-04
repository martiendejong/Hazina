using System.Text;
using Hazina.App.HazinaCoder.Core.Events;
using Hazina.App.HazinaCoder.Core.Providers;
using Hazina.App.HazinaCoder.Core.State;
using Hazina.App.HazinaCoder.Core.Vision;

namespace Hazina.App.HazinaCoder.Core.API;

/// <summary>
/// Programmatic API for HazinaCoder
/// Use HazinaCoder as a library in your applications
/// </summary>
public class HazinaCoderAPI : IDisposable
{
    private readonly EventBus _eventBus;
    private readonly ProviderRouter _providerRouter;
    private readonly VisionAnalyzer? _visionAnalyzer;
    private readonly SnapshotManager _snapshotManager;
    private readonly Dictionary<string, AgentSession> _sessions = new();

    public HazinaCoderAPI(HazinaCoderConfig? config = null)
    {
        config ??= HazinaCoderConfig.Default();

        _eventBus = new EventBus();

        // Initialize providers
        var providers = new List<ILLMProvider>();

        if (config.EnableAnthropic)
        {
            providers.Add(new Providers.Implementations.AnthropicProvider());
        }

        if (config.EnableOpenAI)
        {
            providers.Add(new Providers.Implementations.OpenAIProvider());
        }

        if (!providers.Any())
        {
            throw new InvalidOperationException(
                "No LLM providers configured. Enable at least one provider in config.");
        }

        _providerRouter = new ProviderRouter(providers, config.RoutingStrategy, _eventBus);
        _snapshotManager = new SnapshotManager(eventBus: _eventBus);

        if (config.EnableVision)
        {
            var visionProvider = new Vision.Providers.ClaudeVisionProvider();
            var visionCache = new VisionCache();
            _visionAnalyzer = new VisionAnalyzer(visionProvider, visionCache, _eventBus);
        }
    }

    /// <summary>
    /// Create a new agent session
    /// </summary>
    public AgentSession CreateSession(string? sessionId = null)
    {
        sessionId ??= Guid.NewGuid().ToString("N");

        var session = new AgentSession(
            sessionId,
            _providerRouter,
            _visionAnalyzer,
            _snapshotManager,
            _eventBus);

        _sessions[sessionId] = session;

        _eventBus.Publish(new SessionCreatedEvent(sessionId));

        return session;
    }

    /// <summary>
    /// Get existing session
    /// </summary>
    public AgentSession? GetSession(string sessionId)
    {
        return _sessions.GetValueOrDefault(sessionId);
    }

    /// <summary>
    /// End and remove session
    /// </summary>
    public async Task EndSessionAsync(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            await session.DisposeAsync();
            _sessions.Remove(sessionId);
            _eventBus.Publish(new SessionEndedEvent(sessionId));
        }
    }

    /// <summary>
    /// List all active sessions
    /// </summary>
    public List<string> ListSessions()
    {
        return _sessions.Keys.ToList();
    }

    /// <summary>
    /// Subscribe to events
    /// </summary>
    public IDisposable SubscribeToEvents<T>(Action<T> handler) where T : IEvent
    {
        return _eventBus.Subscribe(handler);
    }

    /// <summary>
    /// Get event observable
    /// </summary>
    public IObservable<T> GetEventObservable<T>() where T : IEvent
    {
        return _eventBus.GetObservable<T>();
    }

    /// <summary>
    /// Check provider health
    /// </summary>
    public async Task<Dictionary<string, ProviderHealth>> CheckProvidersHealthAsync()
    {
        return await _providerRouter.CheckAllHealthAsync();
    }

    public void Dispose()
    {
        foreach (var session in _sessions.Values)
        {
            session.DisposeAsync().AsTask().Wait();
        }
        _sessions.Clear();
        _eventBus.Dispose();
    }
}

/// <summary>
/// HazinaCoder configuration
/// </summary>
public class HazinaCoderConfig
{
    public bool EnableAnthropic { get; set; } = true;
    public bool EnableOpenAI { get; set; } = true;
    public bool EnableVision { get; set; } = true;
    public ProviderRoutingStrategy RoutingStrategy { get; set; } = ProviderRoutingStrategy.Balanced;
    public string? WorkingDirectory { get; set; }
    public string? SnapshotDirectory { get; set; }

    public static HazinaCoderConfig Default()
    {
        return new HazinaCoderConfig
        {
            EnableAnthropic = !string.IsNullOrEmpty(
                Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")),
            EnableOpenAI = !string.IsNullOrEmpty(
                Environment.GetEnvironmentVariable("OPENAI_API_KEY")),
            EnableVision = true,
            RoutingStrategy = ProviderRoutingStrategy.Balanced,
            WorkingDirectory = Directory.GetCurrentDirectory()
        };
    }
}

/// <summary>
/// Agent session
/// </summary>
public class AgentSession : IAsyncDisposable
{
    public string SessionId { get; }

    private readonly ProviderRouter _providerRouter;
    private readonly VisionAnalyzer? _visionAnalyzer;
    private readonly SnapshotManager _snapshotManager;
    private readonly IEventBus _eventBus;
    private AgentState _state;

    public AgentSession(
        string sessionId,
        ProviderRouter providerRouter,
        VisionAnalyzer? visionAnalyzer,
        SnapshotManager snapshotManager,
        IEventBus eventBus)
    {
        SessionId = sessionId;
        _providerRouter = providerRouter;
        _visionAnalyzer = visionAnalyzer;
        _snapshotManager = snapshotManager;
        _eventBus = eventBus;
        _state = new AgentState
        {
            WorkingDirectory = Directory.GetCurrentDirectory(),
            LastActivity = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Send message to agent
    /// </summary>
    public async Task<string> SendMessageAsync(
        string message,
        LLMRequest? request = null)
    {
        _state.LastActivity = DateTime.UtcNow;
        _state.IterationCount++;

        // Add message to conversation history
        _state.ConversationHistory.Add(new ConversationMessage
        {
            Role = "user",
            Content = message,
            Timestamp = DateTime.UtcNow
        });

        // Build LLM request
        request ??= new LLMRequest
        {
            Model = "claude-3-5-sonnet-20241022",
            MaxTokens = 4000,
            Temperature = 0.7,
            Messages = _state.ConversationHistory.Select(m => new LLMMessage
            {
                Role = m.Role,
                Content = m.Content
            }).ToList()
        };

        // Generate response
        var response = await _providerRouter.GenerateAsync(request);

        if (response.Success)
        {
            // Add response to conversation history
            _state.ConversationHistory.Add(new ConversationMessage
            {
                Role = "assistant",
                Content = response.Content,
                Timestamp = DateTime.UtcNow
            });

            return response.Content;
        }
        else
        {
            throw new Exception($"Agent response failed: {response.Error}");
        }
    }

    /// <summary>
    /// Send message with streaming
    /// </summary>
    public async IAsyncEnumerable<string> SendMessageStreamAsync(
        string message,
        LLMRequest? request = null)
    {
        _state.LastActivity = DateTime.UtcNow;
        _state.IterationCount++;

        // Add message to conversation history
        _state.ConversationHistory.Add(new ConversationMessage
        {
            Role = "user",
            Content = message,
            Timestamp = DateTime.UtcNow
        });

        // Build LLM request
        request ??= new LLMRequest
        {
            Model = "claude-3-5-sonnet-20241022",
            MaxTokens = 4000,
            Temperature = 0.7,
            Messages = _state.ConversationHistory.Select(m => new LLMMessage
            {
                Role = m.Role,
                Content = m.Content
            }).ToList()
        };

        var fullResponse = new StringBuilder();

        // Stream response
        await foreach (var chunk in _providerRouter.GenerateStreamAsync(request))
        {
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                fullResponse.Append(chunk.Content);
                yield return chunk.Content;
            }

            if (chunk.IsComplete)
            {
                // Add complete response to conversation history
                _state.ConversationHistory.Add(new ConversationMessage
                {
                    Role = "assistant",
                    Content = fullResponse.ToString(),
                    Timestamp = DateTime.UtcNow
                });
                break;
            }
        }
    }

    /// <summary>
    /// Analyze image
    /// </summary>
    public async Task<string> AnalyzeImageAsync(string imagePath, string query)
    {
        if (_visionAnalyzer == null)
        {
            throw new InvalidOperationException("Vision is not enabled in this configuration.");
        }

        var result = await _visionAnalyzer.AnalyzeImageAsync(imagePath, query);

        if (result.Success)
        {
            return result.Analysis;
        }
        else
        {
            throw new Exception($"Vision analysis failed: {result.Error}");
        }
    }

    /// <summary>
    /// Save session snapshot
    /// </summary>
    public async Task<string> SaveSnapshotAsync(string name)
    {
        var snapshot = await _snapshotManager.SaveSnapshotAsync(name, _state);
        return snapshot.Id;
    }

    /// <summary>
    /// Restore session from snapshot
    /// </summary>
    public async Task RestoreSnapshotAsync(string snapshotId)
    {
        _state = await _snapshotManager.RestoreSnapshotAsync(snapshotId);
    }

    /// <summary>
    /// Get session state
    /// </summary>
    public AgentState GetState()
    {
        return _state;
    }

    /// <summary>
    /// Clear conversation history
    /// </summary>
    public void ClearHistory()
    {
        _state.ConversationHistory.Clear();
        _state.IterationCount = 0;
    }

    public async ValueTask DisposeAsync()
    {
        await Task.CompletedTask;
    }
}

/// <summary>
/// Session created event
/// </summary>
public record SessionCreatedEvent(string SessionId) : BaseEvent;

/// <summary>
/// Session ended event
/// </summary>
public record SessionEndedEvent(string SessionId) : BaseEvent;
