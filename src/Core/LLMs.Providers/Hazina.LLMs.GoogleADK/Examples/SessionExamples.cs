using Hazina.LLMs.GoogleADK.Agents;
using Hazina.LLMs.GoogleADK.Core;
using Hazina.LLMs.GoogleADK.Events;
using Hazina.LLMs.GoogleADK.Sessions;
using Hazina.LLMs.GoogleADK.Sessions.Middleware;
using Hazina.LLMs.GoogleADK.Sessions.Models;
using Hazina.LLMs.GoogleADK.Sessions.Storage;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Examples;

/// <summary>
/// Examples demonstrating session management features
/// </summary>
public class SessionExamples
{
    /// <summary>
    /// Example 1: Basic session usage with auto-create
    /// </summary>
    public static async Task BasicSessionExample(ILLMClient llmClient, ILogger logger)
    {
        // Create session manager with in-memory storage
        var sessionStorage = new InMemorySessionStorage(logger);
        var sessionManager = new SessionManager(sessionStorage, logger);

        // Create session-enabled agent
        var agent = new SessionEnabledAgent(
            name: "ChatBot",
            llmClient: llmClient,
            sessionManager: sessionManager,
            context: new AgentContext(new AgentState(), new EventBus(), logger)
        );

        await agent.InitializeAsync();

        // Execute with auto-created session
        var result1 = await agent.ExecuteWithSessionAsync("Hello, what's your name?");
        Console.WriteLine($"Response: {result1.Output}");

        // Continue conversation in same session
        var result2 = await agent.ExecuteWithSessionAsync("Can you remember what I just asked?");
        Console.WriteLine($"Response: {result2.Output}");

        // Save and complete session
        await agent.CompleteSessionAsync();

        await agent.DisposeAsync();
        await sessionManager.DisposeAsync();
    }

    /// <summary>
    /// Example 2: Session with custom configuration
    /// </summary>
    public static async Task CustomConfigurationExample(ILLMClient llmClient, ILogger logger)
    {
        var sessionStorage = new InMemorySessionStorage(logger);
        var sessionManager = new SessionManager(sessionStorage, logger);

        var agent = new SessionEnabledAgent(
            name: "Assistant",
            llmClient: llmClient,
            sessionManager: sessionManager,
            context: new AgentContext(new AgentState(), new EventBus(), logger)
        );

        await agent.InitializeAsync();

        // Create session with custom configuration
        var config = new SessionConfiguration
        {
            MaxMessages = 20,              // Keep only last 20 messages
            TimeoutMinutes = 60,           // 1-hour timeout
            AutoSaveIntervalSeconds = 30,  // Auto-save every 30 seconds
            PersistToStorage = true,       // Persist to storage
            EnableRecovery = true          // Enable recovery
        };

        var metadata = new Dictionary<string, object>
        {
            ["user_language"] = "en",
            ["user_location"] = "US",
            ["session_purpose"] = "customer_support"
        };

        var session = await agent.StartSessionAsync(
            userId: "user-12345",
            configuration: config,
            metadata: metadata
        );

        session.Tags.Add("support");
        session.Tags.Add("priority");

        // Use the session
        await agent.ExecuteWithSessionAsync("I need help with my account");

        // Manually save session
        await agent.SaveSessionAsync();

        await agent.DisposeAsync();
        await sessionManager.DisposeAsync();
    }

    /// <summary>
    /// Example 3: Resume existing session
    /// </summary>
    public static async Task ResumeSessionExample(ILLMClient llmClient, ILogger logger)
    {
        var storageDir = Path.Combine(Path.GetTempPath(), "hazina-sessions");
        var sessionStorage = new FileSessionStorage(storageDir, logger);
        var sessionManager = new SessionManager(sessionStorage, logger);

        var agent = new SessionEnabledAgent(
            name: "Assistant",
            llmClient: llmClient,
            sessionManager: sessionManager,
            context: new AgentContext(new AgentState(), new EventBus(), logger)
        );

        await agent.InitializeAsync();

        // Start new session
        var session = await agent.StartSessionAsync(userId: "user-12345");
        await agent.ExecuteWithSessionAsync("What's the weather like today?");

        var sessionId = session.SessionId;

        // Pause and dispose
        await agent.PauseSessionAsync();
        await agent.DisposeAsync();

        // Later... create new agent and resume
        var newAgent = new SessionEnabledAgent(
            name: "Assistant",
            llmClient: llmClient,
            sessionManager: sessionManager,
            context: new AgentContext(new AgentState(), new EventBus(), logger)
        );

        await newAgent.InitializeAsync();

        // Resume the session
        var resumedSession = await newAgent.ResumeSessionAsync(sessionId);

        if (resumedSession != null)
        {
            Console.WriteLine($"Resumed session with {resumedSession.Messages.Count} messages");

            // Continue conversation
            await newAgent.ExecuteWithSessionAsync("And what about tomorrow?");

            await newAgent.CompleteSessionAsync();
        }

        await newAgent.DisposeAsync();
        await sessionManager.DisposeAsync();
    }

    /// <summary>
    /// Example 4: Session recovery after failure
    /// </summary>
    public static async Task SessionRecoveryExample(ILLMClient llmClient, ILogger logger)
    {
        var storageDir = Path.Combine(Path.GetTempPath(), "hazina-sessions");
        var sessionStorage = new FileSessionStorage(storageDir, logger);
        var sessionManager = new SessionManager(sessionStorage, logger);
        var recoveryService = new SessionRecoveryService(sessionStorage, logger);

        var agent = new SessionEnabledAgent(
            name: "SupportBot",
            llmClient: llmClient,
            sessionManager: sessionManager,
            context: new AgentContext(new AgentState(), new EventBus(), logger)
        );

        await agent.InitializeAsync();

        // Simulate crash scenario - create sessions
        var session1 = await agent.StartSessionAsync(userId: "user1");
        await agent.ExecuteWithSessionAsync("Help me with my order");
        await agent.SaveSessionAsync();

        // Get the session ID before "crash"
        var sessionId = session1.SessionId;

        // Simulate crash - dispose without completing
        await agent.DisposeAsync();

        Console.WriteLine("Simulated crash... recovering...");

        // Recovery: List all active sessions for the agent
        var activeSessions = await recoveryService.RecoverAgentSessionsAsync("SupportBot");
        Console.WriteLine($"Found {activeSessions.Count} active sessions");

        // Recover specific session
        var recoveryResult = await recoveryService.RecoverSessionAsync(sessionId);

        if (recoveryResult.Success)
        {
            Console.WriteLine("Session recovered successfully!");

            // Create new agent and resume
            var newAgent = new SessionEnabledAgent(
                name: "SupportBot",
                llmClient: llmClient,
                sessionManager: sessionManager,
                context: new AgentContext(new AgentState(), new EventBus(), logger)
            );

            await newAgent.InitializeAsync();
            await newAgent.ResumeSessionAsync(sessionId);
            await newAgent.ExecuteWithSessionAsync("I was helping with an order...");
            await newAgent.CompleteSessionAsync();

            await newAgent.DisposeAsync();
        }

        await sessionManager.DisposeAsync();
    }

    /// <summary>
    /// Example 5: Session hooks and middleware
    /// </summary>
    public static async Task SessionHooksExample(ILLMClient llmClient, ILogger logger)
    {
        var sessionStorage = new InMemorySessionStorage(logger);
        var sessionManager = new SessionManager(sessionStorage, logger);

        // Create middleware with hooks
        var middleware = new SessionMiddleware(logger);

        // Register logging hook
        middleware.RegisterHook(new LoggingSessionHook(logger));

        // Register custom hook
        middleware.RegisterHook(new CustomAnalyticsHook());

        var agent = new SessionEnabledAgent(
            name: "HookedAgent",
            llmClient: llmClient,
            sessionManager: sessionManager,
            context: new AgentContext(new AgentState(), new EventBus(), logger)
        );

        await agent.InitializeAsync();

        // Start session and trigger hooks
        var session = await agent.StartSessionAsync();
        await middleware.OnSessionCreatedAsync(session);

        await agent.ExecuteWithSessionAsync("Test message");

        await agent.CompleteSessionAsync();
        await middleware.OnSessionCompletedAsync(session);

        await agent.DisposeAsync();
        await sessionManager.DisposeAsync();
    }

    /// <summary>
    /// Example 6: List and filter sessions
    /// </summary>
    public static async Task ListSessionsExample(ILLMClient llmClient, ILogger logger)
    {
        var sessionStorage = new InMemorySessionStorage(logger);
        var sessionManager = new SessionManager(sessionStorage, logger);

        var agent = new SessionEnabledAgent(
            name: "MultiAgent",
            llmClient: llmClient,
            sessionManager: sessionManager,
            context: new AgentContext(new AgentState(), new EventBus(), logger)
        );

        await agent.InitializeAsync();

        // Create multiple sessions for different users
        for (int i = 0; i < 5; i++)
        {
            await agent.StartSessionAsync(userId: $"user-{i}");
            await agent.ExecuteWithSessionAsync($"Message {i}");

            if (i % 2 == 0)
            {
                await agent.CompleteSessionAsync();
            }
            else
            {
                await agent.PauseSessionAsync();
            }
        }

        // List all sessions for this agent
        var allSessions = await agent.GetAgentSessionsAsync();
        Console.WriteLine($"Total sessions: {allSessions.Count}");

        // Filter by status
        var activeSessions = await agent.GetAgentSessionsAsync(status: SessionStatus.Active);
        Console.WriteLine($"Active sessions: {activeSessions.Count}");

        var completedSessions = await agent.GetAgentSessionsAsync(status: SessionStatus.Completed);
        Console.WriteLine($"Completed sessions: {completedSessions.Count}");

        // Filter by user
        var user0Sessions = await agent.GetAgentSessionsAsync(userId: "user-0");
        Console.WriteLine($"User 0 sessions: {user0Sessions.Count}");

        // Get statistics
        var stats = await sessionManager.GetStatisticsAsync();
        Console.WriteLine($"Active: {stats.ActiveSessions}, Total: {stats.TotalSessions}");

        await agent.DisposeAsync();
        await sessionManager.DisposeAsync();
    }
}

/// <summary>
/// Custom hook for analytics
/// </summary>
public class CustomAnalyticsHook : SessionHookBase
{
    public override Task OnSessionCreatedAsync(Session session, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[Analytics] Session created: {session.SessionId}");
        // Send to analytics service
        return Task.CompletedTask;
    }

    public override Task OnAfterMessageAsync(Session session, SessionMessage message, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[Analytics] Message count: {session.Messages.Count}");
        // Track message count
        return Task.CompletedTask;
    }

    public override Task OnSessionCompletedAsync(Session session, CancellationToken cancellationToken = default)
    {
        var duration = session.LastActiveAt - session.CreatedAt;
        Console.WriteLine($"[Analytics] Session duration: {duration.TotalMinutes} minutes");
        // Send session metrics
        return Task.CompletedTask;
    }
}
