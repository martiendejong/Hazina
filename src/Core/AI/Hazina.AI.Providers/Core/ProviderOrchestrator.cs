using Hazina.AI.Providers.Cost;
using Hazina.AI.Providers.Health;
using Hazina.AI.Providers.Resilience;
using Hazina.AI.Providers.Selection;

namespace Hazina.AI.Providers.Core;

/// <summary>
/// Main orchestrator for multi-provider LLM management
/// Implements ILLMClient to provide transparent multi-provider support with failover, health monitoring, and cost tracking
/// </summary>
public class ProviderOrchestrator : IProviderOrchestrator
{
    private readonly ProviderRegistry _registry;
    private readonly IProviderHealthMonitor _healthMonitor;
    private readonly IProviderSelector _selector;
    private readonly ICostTracker _costTracker;
    private readonly BudgetManager _budgetManager;
    private readonly FailoverHandler _failoverHandler;
    private readonly RetryPolicy _retryPolicy;

    private SelectionStrategy _defaultStrategy = SelectionStrategy.Priority;
    private SelectionContext? _defaultContext;

    public ProviderOrchestrator()
    {
        _registry = new ProviderRegistry();
        _healthMonitor = new ProviderHealthMonitor(_registry);
        _selector = new ProviderSelector(_registry, _healthMonitor);
        _costTracker = new CostTracker();
        _budgetManager = new BudgetManager(_costTracker);
        _failoverHandler = new FailoverHandler(_selector);
        _retryPolicy = new RetryPolicy();

        // Subscribe to budget alerts
        _budgetManager.BudgetAlertTriggered += OnBudgetAlertTriggered;
    }

    #region Provider Management

    /// <summary>
    /// Register a provider
    /// </summary>
    public void RegisterProvider(string name, ILLMClient client, ProviderMetadata metadata)
    {
        _registry.Register(name, client, metadata);
    }

    /// <summary>
    /// Unregister a provider
    /// </summary>
    public void UnregisterProvider(string name)
    {
        _registry.Unregister(name);
    }

    /// <summary>
    /// Get a provider by name
    /// </summary>
    public ILLMClient? GetProvider(string name)
    {
        return _registry.GetProvider(name);
    }

    /// <summary>
    /// Get provider metadata
    /// </summary>
    public ProviderMetadata? GetProviderMetadata(string name)
    {
        return _registry.GetMetadata(name);
    }

    /// <summary>
    /// Enable/disable a provider
    /// </summary>
    public void SetProviderEnabled(string name, bool enabled)
    {
        _registry.SetProviderEnabled(name, enabled);
    }

    /// <summary>
    /// Set provider priority
    /// </summary>
    public void SetProviderPriority(string name, int priority)
    {
        _registry.SetProviderPriority(name, priority);
    }

    #endregion

    #region Selection Strategy

    /// <summary>
    /// Set default selection strategy
    /// </summary>
    public void SetDefaultStrategy(SelectionStrategy strategy)
    {
        _defaultStrategy = strategy;
    }

    /// <summary>
    /// Set default selection context
    /// </summary>
    public void SetDefaultContext(SelectionContext context)
    {
        _defaultContext = context;
    }

    #endregion

    #region Cost Tracking

    /// <summary>
    /// Get total cost
    /// </summary>
    public decimal GetTotalCost()
    {
        return _costTracker.GetTotalCost();
    }

    /// <summary>
    /// Get cost by provider
    /// </summary>
    public Dictionary<string, decimal> GetCostByProvider()
    {
        return _costTracker.GetCostByProvider();
    }

    /// <summary>
    /// Set budget
    /// </summary>
    public void SetBudget(string providerName, decimal limit, BudgetPeriod period)
    {
        _budgetManager.SetBudget(providerName, limit, period);
    }

    /// <summary>
    /// Add budget alert
    /// </summary>
    public void AddBudgetAlert(string providerName, double thresholdPercentage, string? message = null)
    {
        _budgetManager.AddAlert(providerName, thresholdPercentage, message);
    }

    private void OnBudgetAlertTriggered(object? sender, BudgetAlertEventArgs e)
    {
        // Log or notify about budget alert
        Console.WriteLine($"[Budget Alert] {e.Alert.Message} - Current: ${e.CurrentCost:F2} ({e.CurrentUtilization:F1}%)");
    }

    #endregion

    #region Health Monitoring

    /// <summary>
    /// Get health status
    /// </summary>
    public ProviderHealthStatus GetHealthStatus(string name)
    {
        return _healthMonitor.GetHealthStatus(name);
    }

    /// <summary>
    /// Get all health statuses
    /// </summary>
    public IEnumerable<ProviderHealthStatus> GetAllHealthStatuses()
    {
        return _healthMonitor.GetAllHealthStatuses();
    }

    /// <summary>
    /// Start health monitoring
    /// </summary>
    public Task StartHealthMonitoringAsync(TimeSpan interval, CancellationToken cancellationToken = default)
    {
        return _healthMonitor.StartMonitoringAsync(interval, cancellationToken);
    }

    /// <summary>
    /// Stop health monitoring
    /// </summary>
    public void StopHealthMonitoring()
    {
        _healthMonitor.StopMonitoring();
    }

    #endregion

    #region Resilience

    /// <summary>
    /// Reset circuit breaker
    /// </summary>
    public void ResetCircuitBreaker(string providerName)
    {
        _failoverHandler.ResetCircuitBreaker(providerName);
    }

    #endregion

    #region ILLMClient Implementation - Transparent Multi-Provider Support

    public async Task<Embedding> GenerateEmbedding(string data)
    {
        var result = await _failoverHandler.ExecuteWithFailoverAsync(async provider =>
        {
            var embedding = await provider.GenerateEmbedding(data);
            // Embeddings don't return TokenUsageInfo, so we can't track cost easily here
            return new LLMResponse<Embedding>(embedding, new TokenUsageInfo());
        }, _defaultStrategy, _defaultContext);

        return result.Result;
    }

    public async Task<LLMResponse<HazinaGeneratedImage>> GetImage(
        string prompt, HazinaChatResponseFormat responseFormat, IToolsContext? toolsContext,
        List<ImageData>? images, CancellationToken cancel)
    {
        var result = await _failoverHandler.ExecuteWithFailoverAsync(async provider =>
        {
            var response = await provider.GetImage(prompt, responseFormat, toolsContext, images, cancel);
            RecordUsage(response.TokenUsage);
            return response;
        }, _defaultStrategy, _defaultContext);

        return result;
    }

    public async Task<LLMResponse<string>> GetResponse(
        List<HazinaChatMessage> messages, HazinaChatResponseFormat responseFormat,
        IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel)
    {
        var result = await _failoverHandler.ExecuteWithFailoverAsync(async provider =>
        {
            var response = await provider.GetResponse(messages, responseFormat, toolsContext, images, cancel);
            RecordUsage(response.TokenUsage);
            return response;
        }, _defaultStrategy, _defaultContext);

        return result;
    }

    public async Task<LLMResponse<ResponseType?>> GetResponse<ResponseType>(
        List<HazinaChatMessage> messages, IToolsContext? toolsContext, List<ImageData>? images,
        CancellationToken cancel) where ResponseType : ChatResponse<ResponseType>, new()
    {
        var result = await _failoverHandler.ExecuteWithFailoverAsync(async provider =>
        {
            var response = await provider.GetResponse<ResponseType>(messages, toolsContext, images, cancel);
            RecordUsage(response.TokenUsage);
            return response;
        }, _defaultStrategy, _defaultContext);

        return result;
    }

    public async Task<LLMResponse<string>> GetResponseStream(
        List<HazinaChatMessage> messages, Action<string> onChunkReceived,
        HazinaChatResponseFormat responseFormat, IToolsContext? toolsContext,
        List<ImageData>? images, CancellationToken cancel)
    {
        var result = await _failoverHandler.ExecuteWithFailoverAsync(async provider =>
        {
            var response = await provider.GetResponseStream(messages, onChunkReceived, responseFormat, toolsContext, images, cancel);
            RecordUsage(response.TokenUsage);
            return response;
        }, _defaultStrategy, _defaultContext);

        return result;
    }

    public async Task<LLMResponse<ResponseType?>> GetResponseStream<ResponseType>(
        List<HazinaChatMessage> messages, Action<string> onChunkReceived,
        IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel)
        where ResponseType : ChatResponse<ResponseType>, new()
    {
        var result = await _failoverHandler.ExecuteWithFailoverAsync(async provider =>
        {
            var response = await provider.GetResponseStream<ResponseType>(messages, onChunkReceived, toolsContext, images, cancel);
            RecordUsage(response.TokenUsage);
            return response;
        }, _defaultStrategy, _defaultContext);

        return result;
    }

    public async Task SpeakStream(string text, string voice, Action<byte[]> onAudioChunk,
        string mimeType, CancellationToken cancel)
    {
        await _failoverHandler.ExecuteWithFailoverAsync(async provider =>
        {
            await provider.SpeakStream(text, voice, onAudioChunk, mimeType, cancel);
            return new LLMResponse<object?>(null, new TokenUsageInfo());
        }, _defaultStrategy, _defaultContext);
    }

    #endregion

    #region Helper Methods

    private void RecordUsage(TokenUsageInfo usage)
    {
        if (!string.IsNullOrEmpty(usage.ModelName))
        {
            // Try to find provider name from model name
            var providerName = InferProviderFromModel(usage.ModelName);
            if (!string.IsNullOrEmpty(providerName))
            {
                _costTracker.RecordUsage(providerName, usage);
                _budgetManager.CheckAlerts();
            }
        }
    }

    private string? InferProviderFromModel(string modelName)
    {
        if (modelName.Contains("gpt", StringComparison.OrdinalIgnoreCase))
            return "openai";
        if (modelName.Contains("claude", StringComparison.OrdinalIgnoreCase))
            return "anthropic";
        if (modelName.Contains("gemini", StringComparison.OrdinalIgnoreCase))
            return "google";
        if (modelName.Contains("mistral", StringComparison.OrdinalIgnoreCase))
            return "mistral";

        return null;
    }

    #endregion
}
