using System.Collections.Concurrent;

/// <summary>
/// Mock tool provider for testing
/// </summary>
public class MockToolProvider : IToolProvider
{
    private readonly ConcurrentDictionary<string, MockToolDefinition> _mockTools = new();

    public string ProviderId { get; set; } = "mock";
    public string DisplayName { get; set; } = "Mock Tools";
    public string Description { get; set; } = "Mock tools for testing";

    public Task<IReadOnlyList<HazinaChatTool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        var tools = _mockTools.Values.Select(m => m.Tool).ToList();
        IReadOnlyList<HazinaChatTool> result = tools;
        return Task.FromResult(result);
    }

    public Task<HazinaChatTool?> GetToolAsync(string toolName, CancellationToken cancellationToken = default)
    {
        _mockTools.TryGetValue(toolName, out var mock);
        return Task.FromResult(mock?.Tool);
    }

    public Task<bool> HasToolAsync(string toolName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_mockTools.ContainsKey(toolName));
    }

    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public ToolProviderCapabilities GetCapabilities()
    {
        return new ToolProviderCapabilities
        {
            SupportsDynamicRegistration = true,
            SupportsUnregistration = true,
            SupportsHotReload = false,
            RequiresConfiguration = false,
            MaxTools = null,
            Tags = new List<string> { "mock", "testing" }
        };
    }

    public void RegisterMockTool(string name, string description, string mockResponse, List<ChatToolParameter>? parameters = null)
    {
        var tool = new HazinaChatTool(
            name,
            description,
            parameters ?? new List<ChatToolParameter>(),
            async (messages, toolCall, cancel) =>
            {
                var mock = _mockTools[name];
                mock.CallCount++;
                mock.LastCall = DateTime.UtcNow;
                return mock.Response;
            });

        _mockTools[name] = new MockToolDefinition { Tool = tool, Response = mockResponse };
    }

    public void Reset()
    {
        foreach (var mock in _mockTools.Values) mock.CallCount = 0;
    }
}

internal class MockToolDefinition
{
    public HazinaChatTool Tool { get; set; } = null!;
    public string Response { get; set; } = string.Empty;
    public int CallCount { get; set; }
    public DateTime? LastCall { get; set; }
}
