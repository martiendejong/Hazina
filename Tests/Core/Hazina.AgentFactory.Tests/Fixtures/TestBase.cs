using Hazina.AgentFactory.Services.Email;
using Hazina.AgentFactory.Services.BigQuery;
using Hazina.LLMs;

using NSubstitute;

namespace Hazina.AgentFactory.Tests.Fixtures;

/// <summary>
/// Base class for all AgentFactory tests providing common setup and utilities.
/// </summary>
public abstract class TestBase : IDisposable
{
    protected readonly IEmailService MockEmailService;
    protected readonly IBigQueryService MockBigQueryService;
    protected readonly ILLMClient MockLLMClient;
    protected readonly IToolsContext MockToolsContext;

    protected TestBase()
    {
        MockEmailService = Substitute.For<IEmailService>();
        MockBigQueryService = Substitute.For<IBigQueryService>();
        MockLLMClient = Substitute.For<ILLMClient>();
        MockToolsContext = Substitute.For<IToolsContext>();
    }

    /// <summary>
    /// Creates a default EmailSettings for testing.
    /// </summary>
    protected EmailSettings CreateTestEmailSettings()
    {
        return new EmailSettings
        {
            SmtpHost = "test.smtp.local",
            SmtpPort = 587,
            SmtpUser = "test@example.com",
            SmtpPassword = "test-password",
            ImapHost = "test.imap.local",
            ImapPort = 993,
            ImapUser = "test@example.com",
            ImapPassword = "test-password",
            UseSsl = false
        };
    }

    /// <summary>
    /// Creates a list of test store configs.
    /// </summary>
    protected List<StoreConfig> CreateTestStoreConfigs()
    {
        return new List<StoreConfig>
        {
            new StoreConfig
            {
                Name = "test-store",
                Description = "Test store for unit tests",
                Path = "/test/path",
                FileFilters = new[] { "*.cs", "*.json" },
                SubDirectory = "",
                ExcludePattern = new[] { "bin", "obj" }
            }
        };
    }

    /// <summary>
    /// Creates a list of test agent configs.
    /// </summary>
    protected List<AgentConfig> CreateTestAgentConfigs()
    {
        return new List<AgentConfig>
        {
            new AgentConfig
            {
                Name = "test-agent",
                Description = "Test agent for unit tests",
                ExplicitModify = false
            }
        };
    }

    /// <summary>
    /// Creates a list of test flow configs.
    /// </summary>
    protected List<FlowConfig> CreateTestFlowConfigs()
    {
        return new List<FlowConfig>
        {
            new FlowConfig
            {
                Name = "test-flow",
                Description = "Test flow for unit tests"
            }
        };
    }

    public virtual void Dispose()
    {
        // Cleanup if needed
    }
}
