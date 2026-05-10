using System.Linq;
using Hazina.AgenticOrchestration.Abstractions;
using Hazina.AgenticOrchestration.Data;
using Hazina.AgenticOrchestration.Hubs;
using Hazina.AgenticOrchestration.Services;
using Hazina.AgenticOrchestration.Services.Chat;
using Hazina.AgenticOrchestration.Terminal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hazina.AgenticOrchestration.Extensions;

/// <summary>
/// Extension methods for registering Agentic Orchestration services
/// using Hazina's declarative pattern
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add Hazina Agentic Orchestration with default configuration
    /// </summary>
    /// <example>
    /// // One-liner in Program.cs:
    /// builder.Services.AddHazinaAgenticOrchestration();
    /// </example>
    public static IServiceCollection AddHazinaAgenticOrchestration(
        this IServiceCollection services)
    {
        return services.AddHazinaAgenticOrchestration(new AgenticOrchestrationOptions());
    }

    /// <summary>
    /// Add Hazina Agentic Orchestration with custom configuration
    /// </summary>
    /// <example>
    /// builder.Services.AddHazinaAgenticOrchestration(options =>
    /// {
    ///     options.DatabasePath = @"C:\my\custom\path.db";
    ///     options.LogsPath = @"C:\my\logs";
    ///     options.EnableSignalR = true;
    /// });
    /// </example>
    public static IServiceCollection AddHazinaAgenticOrchestration(
        this IServiceCollection services,
        Action<AgenticOrchestrationOptions> configureOptions)
    {
        var options = new AgenticOrchestrationOptions();
        configureOptions(options);
        return services.AddHazinaAgenticOrchestration(options);
    }

    /// <summary>
    /// Add Hazina Agentic Orchestration with options object
    /// </summary>
    public static IServiceCollection AddHazinaAgenticOrchestration(
        this IServiceCollection services,
        AgenticOrchestrationOptions options)
    {
        // Initialize database
        var dbInitializer = new DatabaseInitializer(options.DatabasePath);
        dbInitializer.Initialize();

        // Register infrastructure abstractions (IFileSystem + ISystemClock)
        services.TryAddSingleton<IFileSystem, PhysicalFileSystem>();
        services.TryAddSingleton<ISystemClock, SystemClock>();

        // Register options - both as singleton and IOptions<T> for flexibility
        services.AddSingleton(options);
        services.AddOptions<AgenticOrchestrationOptions>()
            .Configure(opt =>
            {
                opt.DatabasePath = options.DatabasePath;
                opt.LogsPath = options.LogsPath;
                opt.EnableSignalR = options.EnableSignalR;
                opt.SignalRHubPath = options.SignalRHubPath;
                opt.HeartbeatTimeoutSeconds = options.HeartbeatTimeoutSeconds;
                opt.InteractionExpiryMinutes = options.InteractionExpiryMinutes;
                opt.EnableTerminalStreaming = options.EnableTerminalStreaming;
                opt.TerminalHubPath = options.TerminalHubPath;
                opt.DefaultTerminalColumns = options.DefaultTerminalColumns;
                opt.DefaultTerminalRows = options.DefaultTerminalRows;
                opt.MaxConcurrentSessions = options.MaxConcurrentSessions;
                opt.SessionTimeoutMinutes = options.SessionTimeoutMinutes;
                opt.DefaultCommand = options.DefaultCommand;
                opt.DefaultWorkingDirectory = options.DefaultWorkingDirectory;
                opt.DefaultArguments = options.DefaultArguments;
                opt.EnableSessionLogging = options.EnableSessionLogging;
                opt.AgentSessionLogsPath = options.AgentSessionLogsPath;
                opt.UploadsPath = options.UploadsPath;
                opt.MaxUploadFileSizeMB = options.MaxUploadFileSizeMB;
                // Overstory automation options
                opt.MailDatabasePath = options.MailDatabasePath;
                opt.AgentIdentitiesPath = options.AgentIdentitiesPath;
                opt.PendingNudgesPath = options.PendingNudgesPath;
                opt.MaxConcurrentAgents = options.MaxConcurrentAgents;
                opt.BeaconDelayMs = options.BeaconDelayMs;
                opt.HookDebounceMs = options.HookDebounceMs;
            });

        // Register core services
        services.AddSingleton<IClaudeInstanceManager>(
            new ClaudeInstanceManager(options.DatabasePath));

        // SignalR-dependent services need to be registered as factory
        services.AddSingleton<IOutputCaptureService>(sp =>
            new OutputCaptureService(
                sp.GetRequiredService<IHubContext<ClaudeOrchestrationHub>>(),
                options.LogsPath));

        services.AddSingleton<IInteractionService>(sp =>
            new InteractionService(
                sp.GetRequiredService<IHubContext<ClaudeOrchestrationHub>>(),
                options.DatabasePath));

        // Add SignalR if enabled
        if (options.EnableSignalR)
        {
            services.AddSignalR();
        }

        // Register Agent Session Logger for file-based I/O logging
        if (options.EnableSessionLogging)
        {
            services.AddSingleton<IAgentSessionLogger>(sp =>
                new AgentSessionLogger(
                    options.AgentSessionLogsPath,
                    sp.GetRequiredService<IFileSystem>(),
                    sp.GetRequiredService<ISystemClock>()));
        }
        else
        {
            // Register null implementation to avoid null checks everywhere
            services.TryAddSingleton<IAgentSessionLogger>(
                new NullAgentSessionLogger());
        }

        // Register Session Persistence for crash recovery
        services.AddSingleton<ISessionPersistence, SessionPersistence>();

        // Register Terminal Session Manager for real-time process streaming
        if (options.EnableTerminalStreaming)
        {
            services.AddSingleton<ITerminalSessionManager>(sp =>
                new TerminalSessionManager(
                    sp.GetRequiredService<IHubContext<TerminalHub>>(),
                    sp.GetRequiredService<ILogger<TerminalSessionManager>>(),
                    sp.GetRequiredService<ILoggerFactory>(),
                    sp.GetRequiredService<IAgentSessionLogger>(),
                    sp.GetRequiredService<ISessionPersistence>()));

            // Register startup recovery hosted service
            services.AddHostedService<SessionRecoveryStartupService>();
        }

        // Register Prompt Template Service for predefined prompts
        services.AddSingleton<IPromptTemplateService, PromptTemplateService>();

        // Register Chat Services for LLM-powered chat (Enterprise Edition)
        services.AddSingleton<ConversationRepository>(sp =>
            new ConversationRepository(
                options.ChatConversationsPath,
                sp.GetRequiredService<ILogger<ConversationRepository>>()));

        services.AddSingleton<EnterpriseOrchestrationChatService>();

        // Legacy service for backward compatibility
        services.AddSingleton<OrchestrationChatService>();

        // ═══════════════════════════════════════════════════════════════
        // OVERSTORY AUTOMATION SERVICES (NEW)
        // ═══════════════════════════════════════════════════════════════

        // Register BeaconService for structured startup prompts
        services.AddSingleton<IBeaconService, BeaconService>();

        // Register MailService for inter-agent messaging
        services.AddSingleton<IMailService>(sp =>
            new MailService(
                options.MailDatabasePath,
                options.PendingNudgesPath,
                sp.GetRequiredService<ILogger<MailService>>(),
                sp.GetRequiredService<IFileSystem>()));

        // Register AgentIdentityService for persistent agent CVs
        services.AddSingleton<IAgentIdentityService>(sp =>
            new AgentIdentityService(
                options.AgentIdentitiesPath,
                sp.GetRequiredService<ILogger<AgentIdentityService>>(),
                sp.GetRequiredService<IFileSystem>(),
                sp.GetRequiredService<ISystemClock>()));

        // Register HookConfigService for Claude Code hooks
        services.AddSingleton<IHookConfigService, HookConfigService>();

        // Register SessionOrderingService for session display ordering
        services.AddSingleton<ISessionOrderingService, SessionOrderingService>();

        return services;
    }

    /// <summary>
    /// Map SignalR hubs for Agentic Orchestration.
    /// Call this in Program.cs after app.Build()
    /// </summary>
    /// <example>
    /// app.MapHazinaAgenticHubs();
    /// </example>
    public static IEndpointRouteBuilder MapHazinaAgenticHubs(
        this IEndpointRouteBuilder endpoints,
        AgenticOrchestrationOptions? options = null)
    {
        options ??= new AgenticOrchestrationOptions();

        // Map orchestration hub (for instance management, interactions)
        endpoints.MapHub<ClaudeOrchestrationHub>(options.SignalRHubPath);

        // Map terminal hub (for real-time process I/O)
        if (options.EnableTerminalStreaming)
        {
            endpoints.MapHub<TerminalHub>(options.TerminalHubPath);
        }

        return endpoints;
    }
}

/// <summary>
/// Background service that scans for recoverable sessions on application startup.
/// Runs once during startup, then stops.
/// </summary>
public class SessionRecoveryStartupService : IHostedService
{
    private readonly ITerminalSessionManager _sessionManager;
    private readonly ILogger<SessionRecoveryStartupService> _logger;

    public SessionRecoveryStartupService(
        ITerminalSessionManager sessionManager,
        ILogger<SessionRecoveryStartupService> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Session recovery startup service: scanning for recoverable sessions...");

        try
        {
            var recoverable = await _sessionManager.GetRecoverableSessionsAsync();
            _logger.LogInformation("Found {Count} recoverable sessions", recoverable.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Session recovery startup service failed");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Configuration options for Agentic Orchestration.
/// All path properties default to relative paths from the app's base directory.
/// Override with absolute paths via appsettings.json or code configuration.
/// </summary>
public class AgenticOrchestrationOptions
{
    /// <summary>
    /// Path to the SQLite database file.
    /// Default: agent-activity.db (relative to app base directory)
    /// </summary>
    public string DatabasePath { get; set; } = "agent-activity.db";

    /// <summary>
    /// Path to the logs directory for output streaming.
    /// Default: logs (relative to app base directory)
    /// </summary>
    public string LogsPath { get; set; } = "logs";

    /// <summary>
    /// Enable SignalR for real-time updates
    /// Default: true
    /// </summary>
    public bool EnableSignalR { get; set; } = true;

    /// <summary>
    /// SignalR hub path
    /// Default: /hubs/agentic
    /// </summary>
    public string SignalRHubPath { get; set; } = "/hubs/agentic";

    /// <summary>
    /// Heartbeat timeout in seconds for considering an instance inactive
    /// Default: 60
    /// </summary>
    public int HeartbeatTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Interaction request expiry in minutes
    /// Default: 60
    /// </summary>
    public int InteractionExpiryMinutes { get; set; } = 60;

    /// <summary>
    /// Enable real-time terminal streaming (process-based I/O)
    /// Default: true
    /// </summary>
    public bool EnableTerminalStreaming { get; set; } = true;

    /// <summary>
    /// Terminal hub path for real-time process I/O
    /// Default: /hubs/terminal
    /// </summary>
    public string TerminalHubPath { get; set; } = "/hubs/terminal";

    /// <summary>
    /// Default terminal columns
    /// Default: 120
    /// </summary>
    public int DefaultTerminalColumns { get; set; } = 120;

    /// <summary>
    /// Default terminal rows
    /// Default: 30
    /// </summary>
    public int DefaultTerminalRows { get; set; } = 30;

    /// <summary>
    /// Maximum concurrent terminal sessions
    /// Default: 10
    /// </summary>
    public int MaxConcurrentSessions { get; set; } = 10;

    /// <summary>
    /// Session timeout in minutes (inactive sessions are terminated)
    /// Default: 60
    /// </summary>
    public int SessionTimeoutMinutes { get; set; } = 60;

    /// <summary>
    /// Default command/executable to run when creating a new terminal session
    /// Default: "claude" (assumes Claude CLI is in PATH)
    /// </summary>
    public string DefaultCommand { get; set; } = "claude";

    /// <summary>
    /// Default working directory for new terminal sessions
    /// Default: null (uses current directory)
    /// </summary>
    public string? DefaultWorkingDirectory { get; set; }

    /// <summary>
    /// Default arguments to pass to the command
    /// Default: empty array
    /// </summary>
    public string[] DefaultArguments { get; set; } = new[] { "--model", "opus" };

    /// <summary>
    /// Enable logging of all agent session input/output to files.
    /// Default: true
    /// </summary>
    public bool EnableSessionLogging { get; set; } = true;

    /// <summary>
    /// Base path for agent session log files.
    /// Files are organized as: {BasePath}/{yyyy-MM-dd}/{HH}/session-{sessionId}.log
    /// Default: logs/agent-sessions (relative to app base directory)
    /// </summary>
    public string AgentSessionLogsPath { get; set; } = Path.Combine("logs", "agent-sessions");

    /// <summary>
    /// Path for uploaded files.
    /// Default: uploads (relative to app directory)
    /// </summary>
    public string? UploadsPath { get; set; }

    /// <summary>
    /// Maximum upload file size in megabytes.
    /// Default: 50
    /// </summary>
    public int MaxUploadFileSizeMB { get; set; } = 50;

    // ═══════════════════════════════════════════════════════════════
    // OVERSTORY AUTOMATION OPTIONS (NEW)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Path to the mail database (SQLite).
    /// Default: mail.db (relative to app base directory)
    /// </summary>
    public string MailDatabasePath { get; set; } = "mail.db";

    /// <summary>
    /// Path to agent identities directory.
    /// Default: agents (relative to app base directory)
    /// </summary>
    public string AgentIdentitiesPath { get; set; } = "agents";

    /// <summary>
    /// Path to pending nudges directory.
    /// Default: pending-nudges (relative to app base directory)
    /// </summary>
    public string PendingNudgesPath { get; set; } = "pending-nudges";

    /// <summary>
    /// Maximum concurrent agents (parallel agent limit)
    /// Default: 10
    /// </summary>
    public int MaxConcurrentAgents { get; set; } = 10;

    /// <summary>
    /// Beacon initialization delay in milliseconds (Claude TUI startup time)
    /// Default: 3000 (Overstory pattern)
    /// </summary>
    public int BeaconDelayMs { get; set; } = 3000;

    /// <summary>
    /// Hook debounce interval in milliseconds (mail check throttling)
    /// Default: 5000 (Overstory pattern)
    /// </summary>
    public int HookDebounceMs { get; set; } = 5000;

    // ═══════════════════════════════════════════════════════════════
    // CHAT SERVICE OPTIONS (ENTERPRISE)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Path to the directory for storing chat conversations.
    /// Default: orchestration-chats (relative to app base directory)
    /// </summary>
    public string ChatConversationsPath { get; set; } = "orchestration-chats";

    // ═══════════════════════════════════════════════════════════════
    // PATH SECURITY OPTIONS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Root directory that worktree paths must be within (for path safety validation).
    /// Override this to point to your worktrees root directory.
    /// Default: worker-agents (relative to app base directory)
    /// </summary>
    public string WorktreeRoot { get; set; } = "worker-agents";
}
