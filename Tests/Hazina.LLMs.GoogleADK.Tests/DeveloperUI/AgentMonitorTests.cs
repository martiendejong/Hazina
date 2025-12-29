using Hazina.LLMs.GoogleADK.Agents;
using Hazina.LLMs.GoogleADK.Core;
using Hazina.LLMs.GoogleADK.DeveloperUI;
using Hazina.LLMs.GoogleADK.Events;
using Xunit;

namespace Hazina.LLMs.GoogleADK.Tests.DeveloperUI;

public class AgentMonitorTests
{
    private class MockLLMClient : ILLMClient
    {
        public Task<Embedding> GenerateEmbedding(string data)
        {
            return Task.FromResult(new Embedding(new[] { 0.1, 0.2, 0.3 }));
        }

        public Task<LLMResponse<HazinaGeneratedImage>> GetImage(string prompt, HazinaChatResponseFormat responseFormat,
            IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel)
        {
            throw new NotImplementedException();
        }

        public Task<LLMResponse<string>> GetResponse(List<HazinaChatMessage> messages, HazinaChatResponseFormat responseFormat,
            IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel)
        {
            return Task.FromResult(new LLMResponse<string>("Test response", new TokenUsageInfo()));
        }

        public Task<LLMResponse<ResponseType?>> GetResponse<ResponseType>(List<HazinaChatMessage> messages,
            IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel)
            where ResponseType : ChatResponse<ResponseType>, new()
        {
            throw new NotImplementedException();
        }

        public Task<LLMResponse<string>> GetResponseStream(List<HazinaChatMessage> messages, Action<string> onChunkReceived,
            HazinaChatResponseFormat responseFormat, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel)
        {
            onChunkReceived("Test ");
            onChunkReceived("response");
            return Task.FromResult(new LLMResponse<string>("Test response", new TokenUsageInfo()));
        }

        public Task<LLMResponse<ResponseType?>> GetResponseStream<ResponseType>(List<HazinaChatMessage> messages,
            Action<string> onChunkReceived, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel)
            where ResponseType : ChatResponse<ResponseType>, new()
        {
            throw new NotImplementedException();
        }

        public Task SpeakStream(string text, string voice, Action<byte[]> onAudioChunk, string mimeType, CancellationToken cancel)
        {
            throw new NotImplementedException();
        }
    }

    [Fact]
    public void RegisterAgent_ShouldAddAgentToMonitor()
    {
        // Arrange
        var monitor = new AgentMonitor();
        var llmClient = new MockLLMClient();
        var agent = new LlmAgent("TestAgent", llmClient);

        // Act
        monitor.RegisterAgent(agent);
        var agents = monitor.GetAgents();

        // Assert
        Assert.Single(agents);
        Assert.Equal(agent.AgentId, agents[0].AgentId);
        Assert.Equal("TestAgent", agents[0].Name);
    }

    [Fact]
    public void GetAgent_ShouldReturnAgentInfo()
    {
        // Arrange
        var monitor = new AgentMonitor();
        var llmClient = new MockLLMClient();
        var agent = new LlmAgent("TestAgent", llmClient);

        monitor.RegisterAgent(agent);

        // Act
        var agentInfo = monitor.GetAgent(agent.AgentId);

        // Assert
        Assert.NotNull(agentInfo);
        Assert.Equal(agent.AgentId, agentInfo.AgentId);
    }

    [Fact]
    public void UnregisterAgent_ShouldRemoveAgent()
    {
        // Arrange
        var monitor = new AgentMonitor();
        var llmClient = new MockLLMClient();
        var agent = new LlmAgent("TestAgent", llmClient);

        monitor.RegisterAgent(agent);

        // Act
        monitor.UnregisterAgent(agent.AgentId);
        var agentInfo = monitor.GetAgent(agent.AgentId);

        // Assert
        Assert.Null(agentInfo);
    }

    [Fact]
    public void GetStatistics_ShouldReturnCorrectStats()
    {
        // Arrange
        var monitor = new AgentMonitor();
        var llmClient = new MockLLMClient();
        var agent1 = new LlmAgent("Agent1", llmClient);
        var agent2 = new LlmAgent("Agent2", llmClient);

        monitor.RegisterAgent(agent1);
        monitor.RegisterAgent(agent2);

        // Act
        var stats = monitor.GetStatistics();

        // Assert
        Assert.Equal(2, stats.TotalAgents);
    }
}
