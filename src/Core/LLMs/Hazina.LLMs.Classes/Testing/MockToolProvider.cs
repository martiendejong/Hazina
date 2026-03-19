using System.Collections.Concurrent;
using Hazina.LLMs.Classes.Providers;

namespace Hazina.LLMs.Classes.Testing;

/// <summary>
/// Mock tool provider for testing purposes
/// </summary>
public class MockToolProvider : IToolProvider
{
    private readonly ConcurrentDictionary<string, MockToolDefinition> _mockTools = new();

    public string ProviderId { get; set; } = "mock";
    public string DisplayName { get; set; } = "Mock Tools";
    public string Description { get; set; } = "Mock tools for testing";

    public MockToolProvider()
    {
    }

    public MockToolProvider(string providerId)
    {
        ProviderId = providerId;
    }

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

    /// <summary>
    /// Register a mock tool with a fixed response
    /// </summary>
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
                mock.LastArguments = toolCall.FunctionArguments;

                if (mock.Delay > TimeSpan.Zero)
                {
                    await Task.Delay(mock.Delay, cancel);
                }

                if (mock.ShouldThrow)
                {
                    throw new InvalidOperationException(mock.ExceptionMessage ?? "Mock tool exception");
                }

                // If there's a response generator, use it
                if (mock.ResponseGenerator != null)
                {
                    return mock.ResponseGenerator(messages, toolCall);
                }

                return mock.Response;
            });

        _mockTools[name] = new MockToolDefinition
        {
            Tool = tool,
            Response = mockResponse
        };
    }

    /// <summary>
    /// Register a mock tool with a dynamic response generator
    /// </summary>
    public void RegisterMockTool(
        string name,
        string description,
        Func<List<HazinaChatMessage>, HazinaChatToolCall, string> responseGenerator,
        List<ChatToolParameter>? parameters = null)
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
                mock.LastArguments = toolCall.FunctionArguments;

                if (mock.Delay > TimeSpan.Zero)
                {
                    await Task.Delay(mock.Delay, cancel);
                }

                if (mock.ShouldThrow)
                {
                    throw new InvalidOperationException(mock.ExceptionMessage ?? "Mock tool exception");
                }

                return mock.ResponseGenerator!(messages, toolCall);
            });

        _mockTools[name] = new MockToolDefinition
        {
            Tool = tool,
            Response = string.Empty,
            ResponseGenerator = responseGenerator
        };
    }

    /// <summary>
    /// Configure mock behavior
    /// </summary>
    public void ConfigureMock(string toolName, Action<MockToolConfiguration> configure)
    {
        if (_mockTools.TryGetValue(toolName, out var mock))
        {
            var config = new MockToolConfiguration(mock);
            configure(config);
        }
    }

    /// <summary>
    /// Get call statistics for a mock tool
    /// </summary>
    public MockToolStats? GetStats(string toolName)
    {
        if (!_mockTools.TryGetValue(toolName, out var mock))
            return null;

        return new MockToolStats
        {
            ToolName = toolName,
            CallCount = mock.CallCount,
            LastCall = mock.LastCall,
            LastArguments = mock.LastArguments
        };
    }

    /// <summary>
    /// Reset all mock statistics
    /// </summary>
    public void Reset()
    {
        foreach (var mock in _mockTools.Values)
        {
            mock.CallCount = 0;
            mock.LastCall = null;
            mock.LastArguments = null;
        }
    }

    /// <summary>
    /// Clear all registered mocks
    /// </summary>
    public void Clear()
    {
        _mockTools.Clear();
    }
}

/// <summary>
/// Internal mock tool definition
/// </summary>
internal class MockToolDefinition
{
    public HazinaChatTool Tool { get; set; } = null!;
    public string Response { get; set; } = string.Empty;
    public Func<List<HazinaChatMessage>, HazinaChatToolCall, string>? ResponseGenerator { get; set; }
    public int CallCount { get; set; }
    public DateTime? LastCall { get; set; }
    public string? LastArguments { get; set; }
    public TimeSpan Delay { get; set; }
    public bool ShouldThrow { get; set; }
    public string? ExceptionMessage { get; set; }
}

/// <summary>
/// Configuration for mock tool behavior
/// </summary>
public class MockToolConfiguration
{
    private readonly MockToolDefinition _mock;

    internal MockToolConfiguration(MockToolDefinition mock)
    {
        _mock = mock;
    }

    /// <summary>
    /// Set a delay before the mock responds
    /// </summary>
    public MockToolConfiguration WithDelay(TimeSpan delay)
    {
        _mock.Delay = delay;
        return this;
    }

    /// <summary>
    /// Make the mock throw an exception
    /// </summary>
    public MockToolConfiguration ThrowException(string? message = null)
    {
        _mock.ShouldThrow = true;
        _mock.ExceptionMessage = message;
        return this;
    }

    /// <summary>
    /// Update the mock response
    /// </summary>
    public MockToolConfiguration WithResponse(string response)
    {
        _mock.Response = response;
        _mock.ResponseGenerator = null;
        return this;
    }
}

/// <summary>
/// Statistics about mock tool calls
/// </summary>
public class MockToolStats
{
    public string ToolName { get; set; } = string.Empty;
    public int CallCount { get; set; }
    public DateTime? LastCall { get; set; }
    public string? LastArguments { get; set; }
}
