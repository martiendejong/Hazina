using Hazina.LLMs.GoogleADK.A2A.Models;
using Hazina.LLMs.GoogleADK.A2A.Registry;
using Xunit;

namespace Hazina.LLMs.GoogleADK.Tests.A2A;

public class AgentDirectoryTests
{
    [Fact]
    public async Task RegisterAgentAsync_ShouldAddAgentToDirectory()
    {
        // Arrange
        var directory = new InMemoryAgentDirectory();
        var agent = new AgentDescriptor
        {
            AgentId = "agent-1",
            Name = "TestAgent",
            Description = "Test agent"
        };

        // Act
        var registered = await directory.RegisterAgentAsync(agent);

        // Assert
        Assert.NotNull(registered);
        Assert.Equal("agent-1", registered.AgentId);
        Assert.Equal(A2AAgentStatus.Available, registered.Status);

        await directory.DisposeAsync();
    }

    [Fact]
    public async Task GetAgentAsync_ShouldReturnRegisteredAgent()
    {
        // Arrange
        var directory = new InMemoryAgentDirectory();
        var agent = new AgentDescriptor { AgentId = "agent-1", Name = "TestAgent" };
        await directory.RegisterAgentAsync(agent);

        // Act
        var retrieved = await directory.GetAgentAsync("agent-1");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("agent-1", retrieved.AgentId);

        await directory.DisposeAsync();
    }

    [Fact]
    public async Task FindAgentsByCapabilityAsync_ShouldFindMatchingAgents()
    {
        // Arrange
        var directory = new InMemoryAgentDirectory();

        var agent1 = new AgentDescriptor
        {
            AgentId = "agent-1",
            Name = "Agent1",
            Capabilities = new List<AgentCapability>
            {
                new() { Name = "summarize" }
            }
        };

        var agent2 = new AgentDescriptor
        {
            AgentId = "agent-2",
            Name = "Agent2",
            Capabilities = new List<AgentCapability>
            {
                new() { Name = "translate" }
            }
        };

        await directory.RegisterAgentAsync(agent1);
        await directory.RegisterAgentAsync(agent2);

        // Act
        var result = await directory.FindAgentsByCapabilityAsync("summarize");

        // Assert
        Assert.Single(result.Agents);
        Assert.Equal("agent-1", result.Agents[0].AgentId);

        await directory.DisposeAsync();
    }

    [Fact]
    public async Task FindAgentsByTagsAsync_ShouldFindMatchingAgents()
    {
        // Arrange
        var directory = new InMemoryAgentDirectory();

        var agent1 = new AgentDescriptor
        {
            AgentId = "agent-1",
            Name = "Agent1",
            Capabilities = new List<AgentCapability>
            {
                new() { Tags = new List<string> { "nlp", "text" } }
            }
        };

        await directory.RegisterAgentAsync(agent1);

        // Act
        var result = await directory.FindAgentsByTagsAsync(new List<string> { "nlp" });

        // Assert
        Assert.Single(result.Agents);

        await directory.DisposeAsync();
    }

    [Fact]
    public async Task UpdateAgentStatusAsync_ShouldUpdateStatus()
    {
        // Arrange
        var directory = new InMemoryAgentDirectory();
        var agent = new AgentDescriptor { AgentId = "agent-1", Name = "TestAgent" };
        await directory.RegisterAgentAsync(agent);

        // Act
        var updated = await directory.UpdateAgentStatusAsync("agent-1", A2AAgentStatus.Busy);
        var retrieved = await directory.GetAgentAsync("agent-1");

        // Assert
        Assert.True(updated);
        Assert.NotNull(retrieved);
        Assert.Equal(A2AAgentStatus.Busy, retrieved.Status);

        await directory.DisposeAsync();
    }

    [Fact]
    public async Task HeartbeatAsync_ShouldUpdateLastHeartbeat()
    {
        // Arrange
        var directory = new InMemoryAgentDirectory();
        var agent = new AgentDescriptor { AgentId = "agent-1", Name = "TestAgent" };
        await directory.RegisterAgentAsync(agent);

        var initialHeartbeat = (await directory.GetAgentAsync("agent-1"))!.LastHeartbeat;
        await Task.Delay(100);

        // Act
        await directory.HeartbeatAsync("agent-1");
        var updatedAgent = await directory.GetAgentAsync("agent-1");

        // Assert
        Assert.NotNull(updatedAgent);
        Assert.True(updatedAgent.LastHeartbeat > initialHeartbeat);

        await directory.DisposeAsync();
    }

    [Fact]
    public async Task UnregisterAgentAsync_ShouldRemoveAgent()
    {
        // Arrange
        var directory = new InMemoryAgentDirectory();
        var agent = new AgentDescriptor { AgentId = "agent-1", Name = "TestAgent" };
        await directory.RegisterAgentAsync(agent);

        // Act
        var removed = await directory.UnregisterAgentAsync("agent-1");
        var retrieved = await directory.GetAgentAsync("agent-1");

        // Assert
        Assert.True(removed);
        Assert.Null(retrieved);

        await directory.DisposeAsync();
    }
}
