using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Hazina.LLMs.Configuration;
using Hazina.LLMs.Resilience;

namespace Hazina.LLMs.Providers;

/// <summary>
/// Abstract base class for LLM provider client implementations.
/// Provides common HTTP client functionality, retry logic, and error handling.
/// Reduces boilerplate code across 8 provider implementations (~300 LOC reduction).
/// </summary>
/// <typeparam name="TConfig">The configuration type for this provider.</typeparam>
public abstract class LLMProviderBase<TConfig> where TConfig : IProviderConfig
{
    /// <summary>
    /// The provider configuration.
    /// </summary>
    protected TConfig Config { get; }

    /// <summary>
    /// Shared HTTP client for API calls.
    /// </summary>
    protected HttpClient Http { get; }

    /// <summary>
    /// Resilience executor for retry and rate limiting.
    /// </summary>
    protected ResilienceExecutor Resilience { get; }

    /// <summary>
    /// JSON serializer options for request/response handling.
    /// </summary>
    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    /// <summary>
    /// Creates a new provider instance with the specified configuration.
    /// </summary>
    /// <param name="config">Provider configuration.</param>
    /// <param name="retryStrategy">Optional retry strategy (defaults to exponential backoff).</param>
    /// <param name="rateLimiter">Optional rate limiter.</param>
    protected LLMProviderBase(
        TConfig config,
        IRetryStrategy? retryStrategy = null,
        IRateLimiter? rateLimiter = null)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
        // Initialize HttpClient directly to avoid virtual method call in constructor
        Http = CreateHttpClientInternal();

        // Initialize resilience executor with default or provided strategies
        Resilience = new ResilienceExecutor(
            retryStrategy ?? new ExponentialBackoffStrategy(),
            rateLimiter);
    }

    /// <summary>
    /// Internal method to create HttpClient without virtual call in constructor.
    /// </summary>
    private HttpClient CreateHttpClientInternal()
    {
        var client = new HttpClient();

        if (!string.IsNullOrEmpty(Config.Endpoint))
            client.BaseAddress = new Uri(Config.Endpoint);

        ConfigureHttpClient(client);
        return client;
    }

    /// <summary>
    /// Creates and configures the HTTP client for this provider.
    /// Note: Not used in constructor to avoid virtual method call.
    /// Use CreateHttpClientInternal for initialization.
    /// </summary>
    [Obsolete("Use CreateHttpClientInternal to avoid virtual call in constructor")]
    protected virtual HttpClient CreateHttpClient()
    {
        var client = new HttpClient();

        if (!string.IsNullOrEmpty(Config.Endpoint))
            client.BaseAddress = new Uri(Config.Endpoint);

        ConfigureHttpClient(client);
        return client;
    }

    /// <summary>
    /// Configures the HTTP client with authentication headers.
    /// Override to add provider-specific headers.
    /// </summary>
    protected virtual void ConfigureHttpClient(HttpClient client)
    {
        if (!string.IsNullOrEmpty(Config.ApiKey))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Config.ApiKey);
    }

    /// <summary>
    /// Sends a POST request with JSON content and deserializes the response.
    /// Automatically applies retry and rate limiting.
    /// </summary>
    protected async Task<TResponse> PostJsonAsync<TRequest, TResponse>(
        string endpoint,
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        return await Resilience.ExecuteAsync(async () =>
        {
            var json = JsonSerializer.Serialize(request, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await Http.PostAsync(endpoint, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<TResponse>(responseJson, JsonOptions)
                ?? throw new InvalidOperationException("Failed to deserialize response");
        }, cancellationToken);
    }

    /// <summary>
    /// Sends a POST request and returns the raw response stream for SSE handling.
    /// Automatically applies retry and rate limiting.
    /// </summary>
    protected async Task<Stream> PostStreamAsync<TRequest>(
        string endpoint,
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        return await Resilience.ExecuteAsync(async () =>
        {
            var json = JsonSerializer.Serialize(request, JsonOptions);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            var response = await Http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStreamAsync(cancellationToken);
        }, cancellationToken);
    }

    /// <summary>
    /// Executes an operation with retry logic for transient failures.
    /// </summary>
    protected async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        int maxRetries = 3,
        int baseDelayMs = 1000,
        CancellationToken cancellationToken = default)
    {
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (HttpRequestException ex) when (IsTransientError(ex) && attempt < maxRetries)
            {
                lastException = ex;
                var delay = baseDelayMs * attempt;
                await Task.Delay(delay, cancellationToken);
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
        }

        throw lastException ?? new InvalidOperationException("Operation failed");
    }

    /// <summary>
    /// Determines if an HTTP error is transient and should be retried.
    /// </summary>
    protected virtual bool IsTransientError(HttpRequestException ex)
    {
        // Retry on rate limits (429) and server errors (5xx)
        return ex.StatusCode is
            System.Net.HttpStatusCode.TooManyRequests or
            System.Net.HttpStatusCode.InternalServerError or
            System.Net.HttpStatusCode.BadGateway or
            System.Net.HttpStatusCode.ServiceUnavailable or
            System.Net.HttpStatusCode.GatewayTimeout;
    }

    /// <summary>
    /// Logs API interactions if a log path is configured.
    /// </summary>
    protected void Log(string? message)
    {
        if (string.IsNullOrEmpty(Config.LogPath) || string.IsNullOrEmpty(message))
            return;

        try
        {
            File.AppendAllText(Config.LogPath, $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} | {message}\n");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ignore logging errors (file access issues)
        }
    }

    /// <summary>
    /// Builds the JSON format instruction for structured output.
    /// </summary>
    protected static string BuildJsonFormatInstruction<TResponse>() where TResponse : ChatResponse<TResponse>, new()
    {
        return $"YOUR OUTPUT WILL ALWAYS BE ONLY A JSON RESPONSE IN THIS FORMAT AND NOTHING ELSE: {ChatResponse<TResponse>.Signature} EXAMPLE: {JsonSerializer.Serialize(ChatResponse<TResponse>.Example)}";
    }

    /// <summary>
    /// Creates a token usage info object from input/output counts.
    /// </summary>
    protected static TokenUsageInfo CreateTokenUsage(int inputTokens, int outputTokens, string? modelName = null)
    {
        return new TokenUsageInfo(
            inputTokens,
            outputTokens,
            0m, // Input cost - can be calculated based on model
            0m, // Output cost
            modelName ?? string.Empty
        );
    }
}
