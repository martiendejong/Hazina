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
            services.AddSingleton<IAgentSessionLogger>(
                new AgentSessionLogger(options.AgentSessionLogsPath));
        }
        else
        {
            // Register null implementation to avoid null checks everywhere
            services.TryAddSingleton<IAgentSessionLogger>(
                new NullAgentSessionLogger());
        }

        // Register Terminal Session Manager for real-time process streaming
        if (options.EnableTerminalStreaming)
        {
            services.AddSingleton<ITerminalSessionManager>(sp =>
                new TerminalSessionManager(
                    sp.GetRequiredService<IHubContext<TerminalHub>>(),
                    sp.GetRequiredService<ILogger<TerminalSessionManager>>(),
                    sp.GetRequiredService<ILoggerFactory>(),
                    sp.GetRequiredService<IAgentSessionLogger>()));
        }

        // Register Prompt Template Service for predefined prompts
        services.AddSingleton<IPromptTemplateService, PromptTemplateService>();

        // Register OrchestrationChatService for LLM-powered chat
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
                sp.GetRequiredService<ILogger<MailService>>()));

        // Register AgentIdentityService for persistent agent CVs
        services.AddSingleton<IAgentIdentityService>(sp =>
            new AgentIdentityService(
                options.AgentIdentitiesPath,
                sp.GetRequiredService<ILogger<AgentIdentityService>>()));

        // Register HookConfigService for Claude Code hooks
        services.AddSingleton<IHookConfigService, HookConfigService>();

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
/// Configuration options for Agentic Orchestration
/// </summary>
public class AgenticOrchestrationOptions
{
    /// <summary>
    /// Path to the SQLite database file
    /// Default: C:\scripts\_machine\agent-activity.db
    /// </summary>
    public string DatabasePath { get; set; } = @"C:\scripts\_machine\agent-activity.db";

    /// <summary>
    /// Path to the logs directory for output streaming
    /// Default: C:\scripts\logs
    /// </summary>
    public string LogsPath { get; set; } = @"C:\scripts\logs";

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
    public string[] DefaultArguments { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Enable logging of all agent session input/output to files.
    /// Default: true
    /// </summary>
    public bool EnableSessionLogging { get; set; } = true;

    /// <summary>
    /// Base path for agent session log files.
    /// Files are organized as: {BasePath}/{yyyy-MM-dd}/{HH}/session-{sessionId}.log
    /// Default: C:\scripts\logs\agent-sessions
    /// </summary>
    public string AgentSessionLogsPath { get; set; } = @"C:\scripts\logs\agent-sessions";

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
    /// Path to the mail database (SQLite)
    /// Default: C:\scripts\_machine\mail.db
    /// </summary>
    public string MailDatabasePath { get; set; } = @"C:\scripts\_machine\mail.db";

    /// <summary>
    /// Path to agent identities directory
    /// Default: C:\scripts\_machine\agents
    /// </summary>
    public string AgentIdentitiesPath { get; set; } = @"C:\scripts\_machine\agents";

    /// <summary>
    /// Path to pending nudges directory
    /// Default: C:\scripts\_machine\pending-nudges
    /// </summary>
    public string PendingNudgesPath { get; set; } = @"C:\scripts\_machine\pending-nudges";

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
}
