using FluentAssertions;

using Hazina.AgentFactory.Services.BigQuery;
using Hazina.AgentFactory.Services.Email;
using Hazina.AgentFactory.Services.Tools;
using Hazina.AgentFactory.Services.Tools.Parameters;
using Hazina.AgentFactory.Tests.Fixtures;
using Hazina.LLMs;

using NSubstitute;

namespace Hazina.AgentFactory.Tests.Services;

/// <summary>
/// Unit tests for ToolRegistrationService.
/// </summary>
public class ToolRegistrationServiceTests : TestBase
{
    private readonly IToolsContext _toolsContext;
    private readonly List<StoreConfig> _storesConfig;
    private readonly List<AgentConfig> _agentsConfig;
    private readonly List<FlowConfig> _flowsConfig;
    private readonly IEmailService _emailService;
    private readonly IBigQueryService? _bigQueryService;

    public ToolRegistrationServiceTests()
    {
        _toolsContext = Substitute.For<IToolsContext>();
        _storesConfig = CreateTestStoreConfigs();
        _agentsConfig = CreateTestAgentConfigs();
        _flowsConfig = CreateTestFlowConfigs();
        _emailService = Substitute.For<IEmailService>();
        _bigQueryService = Substitute.For<IBigQueryService>();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange & Act
        var service = CreateToolRegistrationService();

        // Assert
        service.Should().NotBeNull();
    }

    #endregion

    #region Interface Implementation Tests

    [Fact]
    public void ToolRegistrationService_ImplementsIToolRegistrationService()
    {
        // Arrange
        var service = CreateToolRegistrationService();

        // Assert
        service.Should().BeAssignableTo<IToolRegistrationService>();
    }

    [Fact]
    public void IToolRegistrationService_HasAddStoreToolsMethod()
    {
        // Arrange
        var interfaceType = typeof(IToolRegistrationService);

        // Assert
        interfaceType.GetMethod("AddStoreTools").Should().NotBeNull();
    }

    #endregion

    #region AddStoreTools Tests

    [Fact]
    public void AddStoreTools_WithEmptyStores_DoesNotThrow()
    {
        // Arrange
        var service = CreateToolRegistrationService();
        var stores = Enumerable.Empty<(IDocumentStore Store, bool Write)>();

        // Act
        var act = () => service.AddStoreTools(stores, _toolsContext, [], [], [], "test-caller");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void AddStoreTools_WithEmailFunction_AddsEmailTools()
    {
        // Arrange
        var service = CreateToolRegistrationService();
        var mockStore = Substitute.For<IDocumentStore>();
        mockStore.Name.Returns("test-store");
        var stores = new List<(IDocumentStore Store, bool Write)> { (mockStore, false) };

        // Act
        service.AddStoreTools(stores, _toolsContext, ["email"], [], [], "test-caller");

        // Assert
        _toolsContext.Received().Add(Arg.Is<HazinaChatTool>(t => t.FunctionName == "email_send"));
        _toolsContext.Received().Add(Arg.Is<HazinaChatTool>(t => t.FunctionName == "email_list_inbox"));
        _toolsContext.Received().Add(Arg.Is<HazinaChatTool>(t => t.FunctionName == "email_read"));
    }

    [Fact]
    public void AddStoreTools_WithWriteEnabled_AddsWriteTools()
    {
        // Arrange
        var service = CreateToolRegistrationService();
        var mockStore = Substitute.For<IDocumentStore>();
        mockStore.Name.Returns("test-store");
        var stores = new List<(IDocumentStore Store, bool Write)> { (mockStore, true) };

        // Act
        service.AddStoreTools(stores, _toolsContext, [], [], [], "test-caller");

        // Assert
        _toolsContext.Received().Add(Arg.Is<HazinaChatTool>(t => t.FunctionName == "test-store_write"));
        _toolsContext.Received().Add(Arg.Is<HazinaChatTool>(t => t.FunctionName == "test-store_delete"));
    }

    [Fact]
    public void AddStoreTools_AlwaysAddsReadTools()
    {
        // Arrange
        var service = CreateToolRegistrationService();
        var mockStore = Substitute.For<IDocumentStore>();
        mockStore.Name.Returns("test-store");
        var stores = new List<(IDocumentStore Store, bool Write)> { (mockStore, false) };

        // Act
        service.AddStoreTools(stores, _toolsContext, [], [], [], "test-caller");

        // Assert
        _toolsContext.Received().Add(Arg.Is<HazinaChatTool>(t => t.FunctionName == "test-store_list"));
        _toolsContext.Received().Add(Arg.Is<HazinaChatTool>(t => t.FunctionName == "test-store_relevancy"));
        _toolsContext.Received().Add(Arg.Is<HazinaChatTool>(t => t.FunctionName == "test-store_read"));
    }

    #endregion

    #region CommonToolParameters Tests

    [Fact]
    public void CommonToolParameters_Key_HasCorrectProperties()
    {
        // Assert
        CommonToolParameters.Key.Name.Should().Be("key");
        CommonToolParameters.Key.Type.Should().Be("string");
        CommonToolParameters.Key.Required.Should().BeTrue();
    }

    [Fact]
    public void CommonToolParameters_Content_HasCorrectProperties()
    {
        // Assert
        CommonToolParameters.Content.Name.Should().Be("content");
        CommonToolParameters.Content.Type.Should().Be("string");
        CommonToolParameters.Content.Required.Should().BeTrue();
    }

    [Fact]
    public void CommonToolParameters_Instruction_HasCorrectProperties()
    {
        // Assert
        CommonToolParameters.Instruction.Name.Should().Be("instruction");
        CommonToolParameters.Instruction.Type.Should().Be("string");
        CommonToolParameters.Instruction.Required.Should().BeTrue();
    }

    [Fact]
    public void CommonToolParameters_TimeoutSeconds_HasCorrectProperties()
    {
        // Assert
        CommonToolParameters.TimeoutSeconds.Name.Should().Be("timeout");
        CommonToolParameters.TimeoutSeconds.Type.Should().Be("number");
        CommonToolParameters.TimeoutSeconds.Required.Should().BeTrue();
    }

    [Fact]
    public void CommonToolParameters_EmailParameters_Exist()
    {
        // Assert
        CommonToolParameters.Recipient.Should().NotBeNull();
        CommonToolParameters.Subject.Should().NotBeNull();
        CommonToolParameters.Body.Should().NotBeNull();
        CommonToolParameters.EmailId.Should().NotBeNull();
        CommonToolParameters.EmailAmount.Should().NotBeNull();
    }

    [Fact]
    public void CommonToolParameters_BigQueryParameters_Exist()
    {
        // Assert
        CommonToolParameters.BigQueryArguments.Should().NotBeNull();
        CommonToolParameters.BigQueryDataSet.Should().NotBeNull();
        CommonToolParameters.BigQueryTableName.Should().NotBeNull();
    }

    #endregion

    #region Agent Tools Tests

    [Fact]
    public void AddStoreTools_WithAgents_AddsAgentTools()
    {
        // Arrange
        var service = CreateToolRegistrationService();
        var mockStore = Substitute.For<IDocumentStore>();
        mockStore.Name.Returns("test-store");
        var stores = new List<(IDocumentStore Store, bool Write)> { (mockStore, false) };

        // Act
        service.AddStoreTools(stores, _toolsContext, [], ["test-agent"], [], "test-caller");

        // Assert
        _toolsContext.Received().Add(Arg.Is<HazinaChatTool>(t => t.FunctionName == "test-agent"));
    }

    [Fact]
    public void AddStoreTools_WithUnknownAgent_ThrowsException()
    {
        // Arrange
        var service = CreateToolRegistrationService();
        var mockStore = Substitute.For<IDocumentStore>();
        mockStore.Name.Returns("test-store");
        var stores = new List<(IDocumentStore Store, bool Write)> { (mockStore, false) };

        // Act
        var act = () => service.AddStoreTools(stores, _toolsContext, [], ["unknown-agent"], [], "test-caller");

        // Assert
        act.Should().Throw<Exception>()
            .WithMessage("Agent unknown-agent is not defined. Request by test-caller.");
    }

    #endregion

    #region Flow Tools Tests

    [Fact]
    public void AddStoreTools_WithFlows_AddsFlowTools()
    {
        // Arrange
        var service = CreateToolRegistrationService();
        var mockStore = Substitute.For<IDocumentStore>();
        mockStore.Name.Returns("test-store");
        var stores = new List<(IDocumentStore Store, bool Write)> { (mockStore, false) };

        // Act
        service.AddStoreTools(stores, _toolsContext, [], [], ["test-flow"], "test-caller");

        // Assert
        _toolsContext.Received().Add(Arg.Is<HazinaChatTool>(t => t.FunctionName == "test-flow"));
    }

    [Fact]
    public void AddStoreTools_WithUnknownFlow_ThrowsException()
    {
        // Arrange
        var service = CreateToolRegistrationService();
        var mockStore = Substitute.For<IDocumentStore>();
        mockStore.Name.Returns("test-store");
        var stores = new List<(IDocumentStore Store, bool Write)> { (mockStore, false) };

        // Act
        var act = () => service.AddStoreTools(stores, _toolsContext, [], [], ["unknown-flow"], "test-caller");

        // Assert
        act.Should().Throw<Exception>()
            .WithMessage("Flow unknown-flow not found");
    }

    #endregion

    private ToolRegistrationService CreateToolRegistrationService()
    {
        return new ToolRegistrationService(
            _storesConfig,
            _agentsConfig,
            _flowsConfig,
            _emailService,
            _bigQueryService,
            (name, query, caller, cancel) => Task.FromResult("agent-response"),
            (name, query, caller, cancel) => Task.FromResult("flow-response"),
            (name, query, caller, cancel) => Task.FromResult("coder-response"),
            (systemPrompt, store, instruction, isCoder, cancel) => Task.FromResult("custom-response"),
            () => false);
    }
}
