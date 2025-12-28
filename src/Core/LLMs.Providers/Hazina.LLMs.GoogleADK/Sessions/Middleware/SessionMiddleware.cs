using Hazina.LLMs.GoogleADK.Sessions.Models;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Sessions.Middleware;

/// <summary>
/// Middleware for session lifecycle hooks
/// </summary>
public class SessionMiddleware
{
    private readonly List<ISessionHook> _hooks = new();
    private readonly ILogger? _logger;

    public SessionMiddleware(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Register a session hook
    /// </summary>
    public void RegisterHook(ISessionHook hook)
    {
        _hooks.Add(hook);
        _logger?.LogDebug("Registered session hook: {HookType}", hook.GetType().Name);
    }

    /// <summary>
    /// Execute hooks for session creation
    /// </summary>
    public async Task OnSessionCreatedAsync(Session session, CancellationToken cancellationToken = default)
    {
        foreach (var hook in _hooks)
        {
            try
            {
                await hook.OnSessionCreatedAsync(session, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in OnSessionCreated hook: {HookType}", hook.GetType().Name);
            }
        }
    }

    /// <summary>
    /// Execute hooks for session resumption
    /// </summary>
    public async Task OnSessionResumedAsync(Session session, CancellationToken cancellationToken = default)
    {
        foreach (var hook in _hooks)
        {
            try
            {
                await hook.OnSessionResumedAsync(session, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in OnSessionResumed hook: {HookType}", hook.GetType().Name);
            }
        }
    }

    /// <summary>
    /// Execute hooks before message is added
    /// </summary>
    public async Task<bool> OnBeforeMessageAsync(
        Session session,
        SessionMessage message,
        CancellationToken cancellationToken = default)
    {
        foreach (var hook in _hooks)
        {
            try
            {
                var shouldContinue = await hook.OnBeforeMessageAsync(session, message, cancellationToken);
                if (!shouldContinue)
                {
                    _logger?.LogDebug("Hook {HookType} prevented message addition", hook.GetType().Name);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in OnBeforeMessage hook: {HookType}", hook.GetType().Name);
            }
        }
        return true;
    }

    /// <summary>
    /// Execute hooks after message is added
    /// </summary>
    public async Task OnAfterMessageAsync(
        Session session,
        SessionMessage message,
        CancellationToken cancellationToken = default)
    {
        foreach (var hook in _hooks)
        {
            try
            {
                await hook.OnAfterMessageAsync(session, message, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in OnAfterMessage hook: {HookType}", hook.GetType().Name);
            }
        }
    }

    /// <summary>
    /// Execute hooks for session completion
    /// </summary>
    public async Task OnSessionCompletedAsync(Session session, CancellationToken cancellationToken = default)
    {
        foreach (var hook in _hooks)
        {
            try
            {
                await hook.OnSessionCompletedAsync(session, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in OnSessionCompleted hook: {HookType}", hook.GetType().Name);
            }
        }
    }

    /// <summary>
    /// Execute hooks for session termination
    /// </summary>
    public async Task OnSessionTerminatedAsync(Session session, CancellationToken cancellationToken = default)
    {
        foreach (var hook in _hooks)
        {
            try
            {
                await hook.OnSessionTerminatedAsync(session, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in OnSessionTerminated hook: {HookType}", hook.GetType().Name);
            }
        }
    }
}

/// <summary>
/// Interface for session lifecycle hooks
/// </summary>
public interface ISessionHook
{
    Task OnSessionCreatedAsync(Session session, CancellationToken cancellationToken = default);
    Task OnSessionResumedAsync(Session session, CancellationToken cancellationToken = default);
    Task<bool> OnBeforeMessageAsync(Session session, SessionMessage message, CancellationToken cancellationToken = default);
    Task OnAfterMessageAsync(Session session, SessionMessage message, CancellationToken cancellationToken = default);
    Task OnSessionCompletedAsync(Session session, CancellationToken cancellationToken = default);
    Task OnSessionTerminatedAsync(Session session, CancellationToken cancellationToken = default);
}

/// <summary>
/// Base class for session hooks with default implementations
/// </summary>
public abstract class SessionHookBase : ISessionHook
{
    public virtual Task OnSessionCreatedAsync(Session session, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public virtual Task OnSessionResumedAsync(Session session, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public virtual Task<bool> OnBeforeMessageAsync(Session session, SessionMessage message, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public virtual Task OnAfterMessageAsync(Session session, SessionMessage message, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public virtual Task OnSessionCompletedAsync(Session session, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public virtual Task OnSessionTerminatedAsync(Session session, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// Logging hook for debugging
/// </summary>
public class LoggingSessionHook : SessionHookBase
{
    private readonly ILogger _logger;

    public LoggingSessionHook(ILogger logger)
    {
        _logger = logger;
    }

    public override Task OnSessionCreatedAsync(Session session, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Session created: {SessionId} for agent {AgentName}", session.SessionId, session.AgentName);
        return Task.CompletedTask;
    }

    public override Task OnAfterMessageAsync(Session session, SessionMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Message added to session {SessionId}: {Role}", session.SessionId, message.Role);
        return Task.CompletedTask;
    }

    public override Task OnSessionCompletedAsync(Session session, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Session completed: {SessionId} with {MessageCount} messages",
            session.SessionId, session.Messages.Count);
        return Task.CompletedTask;
    }
}
