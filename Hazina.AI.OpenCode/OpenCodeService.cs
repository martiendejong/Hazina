using Hazina.AI.OpenCode.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Hazina.AI.OpenCode;

/// <summary>
/// Service for interacting with OpenCode multi-agent orchestration system
/// </summary>
public class OpenCodeService : IOpenCodeService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OpenCodeService> _logger;

    private static readonly Dictionary<string, AgentInfo> _agents = new()
    {
        ["saas-dev"] = new AgentInfo
        {
            Name = "saas-dev",
            Description = "SaaS development (client-manager, hazina, brand2boost)",
            Port = 3051,
            BaseUrl = "http://localhost:3051",
            AllowedProjects = new() { "client-manager", "hazina", "brand2boost" }
        },
        ["wordpress-dev"] = new AgentInfo
        {
            Name = "wordpress-dev",
            Description = "WordPress development (artrevisionist)",
            Port = 3052,
            BaseUrl = "http://localhost:3052",
            AllowedProjects = new() { "artrevisionist", "wordpress" }
        },
        ["research-agent"] = new AgentInfo
        {
            Name = "research-agent",
            Description = "Read-only research and analysis",
            Port = 3053,
            BaseUrl = "http://localhost:3053",
            AllowedProjects = new()
        }
    };

    public OpenCodeService(IHttpClientFactory httpClientFactory, ILogger<OpenCodeService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public Task<string> ExecuteInSaasContextAsync(string task, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("saas-dev", new OpenCodeRequest { Message = task }, cancellationToken);
    }

    public Task<string> ExecuteInWordPressContextAsync(string task, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("wordpress-dev", new OpenCodeRequest { Message = task }, cancellationToken);
    }

    public Task<string> ResearchAsync(string query, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("research-agent", new OpenCodeRequest { Message = query }, cancellationToken);
    }

    public async Task<string> ExecuteAsync(string agentName, OpenCodeRequest request, CancellationToken cancellationToken = default)
    {
        if (!_agents.TryGetValue(agentName, out var agent))
        {
            throw new ArgumentException($"Unknown agent: {agentName}", nameof(agentName));
        }

        _logger.LogInformation("Executing task on agent {AgentName}: {Task}", agentName, request.Message);

        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(agent.BaseUrl);
        client.Timeout = TimeSpan.FromMinutes(5);

        try
        {
            var response = await client.PostAsJsonAsync("/api/chat", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OpenCodeResponse>(cancellationToken);

            if (result?.Error != null)
            {
                _logger.LogError("OpenCode error from {AgentName}: {Error}", agentName, result.Error);
                throw new Exception($"OpenCode error: {result.Error}");
            }

            return result?.Content ?? string.Empty;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to connect to agent {AgentName} at {BaseUrl}", agentName, agent.BaseUrl);
            throw new Exception($"Failed to connect to agent {agentName}. Ensure OpenCode orchestration is running.", ex);
        }
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string agentName,
        OpenCodeRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!_agents.TryGetValue(agentName, out var agent))
        {
            throw new ArgumentException($"Unknown agent: {agentName}", nameof(agentName));
        }

        _logger.LogInformation("Streaming from agent {AgentName}: {Task}", agentName, request.Message);

        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(agent.BaseUrl);
        client.Timeout = Timeout.InfiniteTimeSpan;

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));

        var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (line.StartsWith("data: "))
            {
                var data = line.Substring(6);
                if (data == "[DONE]") break;

                try
                {
                    var chunk = JsonSerializer.Deserialize<OpenCodeResponse>(data);
                    if (chunk?.Content != null)
                    {
                        yield return chunk.Content;
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse SSE chunk: {Data}", data);
                }
            }
        }
    }

    public async Task<List<AgentInfo>> GetAgentStatusAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<AgentInfo>();

        foreach (var agent in _agents.Values)
        {
            var agentInfo = new AgentInfo
            {
                Name = agent.Name,
                Description = agent.Description,
                Port = agent.Port,
                BaseUrl = agent.BaseUrl,
                AllowedProjects = agent.AllowedProjects
            };

            agentInfo.IsHealthy = await IsAgentHealthyAsync(agent.Name, cancellationToken);
            agentInfo.LastHealthCheck = DateTime.UtcNow;

            results.Add(agentInfo);
        }

        return results;
    }

    public async Task<bool> IsAgentHealthyAsync(string agentName, CancellationToken cancellationToken = default)
    {
        if (!_agents.TryGetValue(agentName, out var agent))
        {
            return false;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            var response = await client.GetAsync($"{agent.BaseUrl}/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            // If health endpoint doesn't exist, try root endpoint
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(5);

                var response = await client.GetAsync(agent.BaseUrl, cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }

    public async Task<string> SmartRouteAsync(string task, string? projectHint = null, CancellationToken cancellationToken = default)
    {
        // Smart routing logic based on project hint or task content
        var agentName = DetermineAgent(task, projectHint);

        _logger.LogInformation("Smart routing task to {AgentName} based on hint: {ProjectHint}", agentName, projectHint);

        return await ExecuteAsync(agentName, new OpenCodeRequest { Message = task }, cancellationToken);
    }

    private static string DetermineAgent(string task, string? projectHint)
    {
        // Project hint routing
        if (!string.IsNullOrEmpty(projectHint))
        {
            var lowerHint = projectHint.ToLowerInvariant();

            foreach (var agent in _agents.Values)
            {
                if (agent.AllowedProjects.Any(p => lowerHint.Contains(p.ToLowerInvariant())))
                {
                    return agent.Name;
                }
            }
        }

        // Content-based routing
        var lowerTask = task.ToLowerInvariant();

        // SaaS keywords
        if (lowerTask.Contains("client-manager") ||
            lowerTask.Contains("hazina") ||
            lowerTask.Contains("brand2boost") ||
            lowerTask.Contains(".net") ||
            lowerTask.Contains("c#") ||
            lowerTask.Contains("entity framework"))
        {
            return "saas-dev";
        }

        // WordPress keywords
        if (lowerTask.Contains("wordpress") ||
            lowerTask.Contains("artrevisionist") ||
            lowerTask.Contains("php") ||
            lowerTask.Contains("wp-cli"))
        {
            return "wordpress-dev";
        }

        // Read-only keywords
        if (lowerTask.Contains("analyze") ||
            lowerTask.Contains("search") ||
            lowerTask.Contains("find") ||
            lowerTask.Contains("research"))
        {
            return "research-agent";
        }

        // Default to research agent for safety (read-only)
        return "research-agent";
    }
}
