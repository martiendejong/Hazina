using System.Text;
using System.Text.Json;

namespace Hazina.App.HazinaCoder.Core.API;

/// <summary>
/// Client SDK for HazinaCoder HTTP API
/// Makes it easy to interact with remote HazinaCoder server
/// </summary>
public class HazinaCoderClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public HazinaCoderClient(string baseUrl = "http://localhost:27185")
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl)
        };
    }

    public HazinaCoderClient(HttpClient httpClient, string baseUrl = "http://localhost:27185")
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = httpClient;
    }

    /// <summary>
    /// Create a new session
    /// </summary>
    public async Task<string> CreateSessionAsync()
    {
        var response = await _httpClient.PostAsync("/api/sessions", null);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse>(json);

        if (result?.Success == true && result.Data != null)
        {
            var data = JsonSerializer.Deserialize<JsonElement>(
                JsonSerializer.Serialize(result.Data));

            return data.GetProperty("SessionId").GetString() ?? string.Empty;
        }

        throw new Exception("Failed to create session");
    }

    /// <summary>
    /// List all sessions
    /// </summary>
    public async Task<List<string>> ListSessionsAsync()
    {
        var response = await _httpClient.GetAsync("/api/sessions");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse>(json);

        if (result?.Success == true && result.Data != null)
        {
            var data = JsonSerializer.Deserialize<JsonElement>(
                JsonSerializer.Serialize(result.Data));

            var sessions = new List<string>();
            foreach (var session in data.GetProperty("Sessions").EnumerateArray())
            {
                sessions.Add(session.GetString() ?? string.Empty);
            }

            return sessions;
        }

        return new List<string>();
    }

    /// <summary>
    /// Get session information
    /// </summary>
    public async Task<SessionInfo?> GetSessionAsync(string sessionId)
    {
        var response = await _httpClient.GetAsync($"/api/sessions/{sessionId}");

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse>(json);

        if (result?.Success == true && result.Data != null)
        {
            var data = JsonSerializer.Deserialize<JsonElement>(
                JsonSerializer.Serialize(result.Data));

            var state = data.GetProperty("State");

            return new SessionInfo
            {
                SessionId = data.GetProperty("SessionId").GetString() ?? string.Empty,
                IterationCount = state.GetProperty("IterationCount").GetInt32(),
                LastActivity = state.GetProperty("LastActivity").GetDateTime(),
                MessageCount = state.GetProperty("MessageCount").GetInt32(),
                WorkingDirectory = state.GetProperty("WorkingDirectory").GetString() ?? string.Empty
            };
        }

        return null;
    }

    /// <summary>
    /// Send message to session
    /// </summary>
    public async Task<string> SendMessageAsync(string sessionId, string message)
    {
        var request = new MessageRequest
        {
            Message = message
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(
            $"/api/sessions/{sessionId}/messages",
            content);

        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse>(responseJson);

        if (result?.Success == true && result.Data != null)
        {
            var data = JsonSerializer.Deserialize<JsonElement>(
                JsonSerializer.Serialize(result.Data));

            return data.GetProperty("Response").GetString() ?? string.Empty;
        }

        throw new Exception(result?.Error ?? "Unknown error");
    }

    /// <summary>
    /// Analyze image
    /// </summary>
    public async Task<string> AnalyzeImageAsync(
        string sessionId,
        string imagePath,
        string? query = null)
    {
        var request = new ImageRequest
        {
            ImagePath = imagePath,
            Query = query ?? "Analyze this image"
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(
            $"/api/sessions/{sessionId}/images",
            content);

        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse>(responseJson);

        if (result?.Success == true && result.Data != null)
        {
            var data = JsonSerializer.Deserialize<JsonElement>(
                JsonSerializer.Serialize(result.Data));

            return data.GetProperty("Analysis").GetString() ?? string.Empty;
        }

        throw new Exception(result?.Error ?? "Unknown error");
    }

    /// <summary>
    /// End session
    /// </summary>
    public async Task EndSessionAsync(string sessionId)
    {
        var response = await _httpClient.DeleteAsync($"/api/sessions/{sessionId}");
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Check provider health
    /// </summary>
    public async Task<Dictionary<string, object>> CheckHealthAsync()
    {
        var response = await _httpClient.GetAsync("/api/health");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse>(json);

        if (result?.Success == true && result.Data != null)
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(
                JsonSerializer.Serialize(result.Data)) ?? new Dictionary<string, object>();
        }

        return new Dictionary<string, object>();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

/// <summary>
/// Session information
/// </summary>
public class SessionInfo
{
    public string SessionId { get; set; } = string.Empty;
    public int IterationCount { get; set; }
    public DateTime LastActivity { get; set; }
    public int MessageCount { get; set; }
    public string WorkingDirectory { get; set; } = string.Empty;
}
