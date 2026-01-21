using FluentAssertions;

using Hazina.AgentFactory.Services.Execution;
using Hazina.AgentFactory.Tests.Fixtures;
using Hazina.LLMs;

using NSubstitute;

namespace Hazina.AgentFactory.Tests.Services;

/// <summary>
/// Unit tests for AgentExecutionService.
/// </summary>
public class AgentExecutionServiceTests : TestBase
{
    private readonly Dictionary<string, HazinaAgent> _agents;
    private readonly Dictionary<string, HazinaFlow> _flows;
    private readonly List<HazinaChatMessage> _messages;
    private bool _writeMode;

    public AgentExecutionServiceTests()
    {
        _agents = new Dictionary<string, HazinaAgent>();
        _flows = new Dictionary<string, HazinaFlow>();
        _messages = new List<HazinaChatMessage>();
        _writeMode = false;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullAgents_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AgentExecutionService(
            null!,
            _flows,
            _messages,
            () => _writeMode,
            v => _writeMode = v);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("agents");
    }

    [Fact]
    public void Constructor_WithNullFlows_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AgentExecutionService(
            _agents,
            null!,
            _messages,
            () => _writeMode,
            v => _writeMode = v);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("flows");
    }

    [Fact]
    public void Constructor_WithNullMessages_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AgentExecutionService(
            _agents,
            _flows,
            null!,
            () => _writeMode,
            v => _writeMode = v);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("messages");
    }

    [Fact]
    public void Constructor_WithNullGetWriteMode_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AgentExecutionService(
            _agents,
            _flows,
            _messages,
            null!,
            v => _writeMode = v);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("getWriteMode");
    }

    [Fact]
    public void Constructor_WithNullSetWriteMode_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AgentExecutionService(
            _agents,
            _flows,
            _messages,
            () => _writeMode,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("setWriteMode");
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        var service = CreateAgentExecutionService();

        // Assert
        service.Should().NotBeNull();
    }

    #endregion

    #region Interface Implementation Tests

    [Fact]
    public void AgentExecutionService_ImplementsIAgentExecutionService()
    {
        // Arrange
        var service = CreateAgentExecutionService();

        // Assert
        service.Should().BeAssignableTo<IAgentExecutionService>();
    }

    [Fact]
    public void IAgentExecutionService_HasAllRequiredMethods()
    {
        // Arrange
        var interfaceType = typeof(IAgentExecutionService);

        // Assert
        interfaceType.GetMethod("CallAgentAsync").Should().NotBeNull();
        interfaceType.GetMethod("CallFlowAsync").Should().NotBeNull();
        interfaceType.GetMethod("CallCoderAgentAsync").Should().NotBeNull();
        interfaceType.GetMethod("CallAgentWithMetaAsync").Should().NotBeNull();
    }

    #endregion

    #region CallAgentAsync Tests

    [Fact]
    public async Task CallAgentAsync_WithNonExistentAgent_ThrowsKeyNotFoundException()
    {
        // Arrange
        var service = CreateAgentExecutionService();

        // Act
        Func<Task> act = async () => await service.CallAgentAsync("non-existent", "query", "caller", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    #endregion

    #region CallFlowAsync Tests

    [Fact]
    public async Task CallFlowAsync_WithNonExistentFlow_ThrowsKeyNotFoundException()
    {
        // Arrange
        var service = CreateAgentExecutionService();

        // Act
        Func<Task> act = async () => await service.CallFlowAsync("non-existent", "query", "caller", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    #endregion

    #region Message Tracking Tests

    [Fact]
    public void Messages_StartsEmpty()
    {
        // Assert
        _messages.Should().BeEmpty();
    }

    [Fact]
    public void HazinaChatMessage_CanBeCreated()
    {
        // Arrange & Act
        var message = FakeDataGenerator.GenerateChatMessage();

        // Assert
        message.Should().NotBeNull();
        message.MessageId.Should().NotBeEmpty();
        message.Text.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region WriteMode Tests

    [Fact]
    public void WriteMode_DefaultsFalse()
    {
        // Assert
        _writeMode.Should().BeFalse();
    }

    [Fact]
    public void WriteMode_CanBeToggled()
    {
        // Act
        _writeMode = true;

        // Assert
        _writeMode.Should().BeTrue();
    }

    #endregion

    private AgentExecutionService CreateAgentExecutionService()
    {
        return new AgentExecutionService(
            _agents,
            _flows,
            _messages,
            () => _writeMode,
            v => _writeMode = v);
    }
}
