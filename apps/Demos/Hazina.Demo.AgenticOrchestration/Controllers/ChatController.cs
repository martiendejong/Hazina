using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Hazina.AgenticOrchestration.Terminal;
using Hazina.AgenticOrchestration.Extensions;
using Hazina.LLMs;

namespace Hazina.Demo.AgenticOrchestration.Controllers;

/// <summary>
/// Chat API controller for AI-powered terminal orchestration assistant.
/// Provides a conversational interface with tools for managing terminal sessions.
/// </summary>
[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly ITerminalSessionManager _sessionManager;
    private readonly ILLMClient? _llmClient;
    private readonly ILogger<ChatController> _logger;
    private readonly AgenticOrchestrationOptions _options;
    private static readonly Dictionary<string, List<ChatMessageDto>> _conversations = new();

    public ChatController(
        ITerminalSessionManager sessionManager,
        ILogger<ChatController> logger,
        Microsoft.Extensions.Options.IOptions<AgenticOrchestrationOptions> options,
        ILLMClient? llmClient = null)
    {
        _sessionManager = sessionManager;
        _logger = logger;
        _options = options.Value;
        _llmClient = llmClient;
    }

    /// <summary>
    /// Send a chat message and receive a streaming response.
    /// The AI assistant can use tools to manage terminal sessions.
    /// </summary>
    [HttpPost("messages")]
    public async Task SendMessage([FromBody] ChatRequest request, CancellationToken ct)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        var conversationId = request.ConversationId ?? Guid.NewGuid().ToString();

        // Get or create conversation history
        if (!_conversations.TryGetValue(conversationId, out var history))
        {
            history = new List<ChatMessageDto>();
            _conversations[conversationId] = history;
        }

        // Add user message to history
        var userMessage = new ChatMessageDto
        {
            Id = Guid.NewGuid().ToString(),
            Role = "user",
            Content = request.Message,
            Timestamp = DateTime.UtcNow
        };
        history.Add(userMessage);

        try
        {
            // Send conversation ID
            await WriteSSE(new { conversationId });

            // Process with LLM or fallback to simple response
            if (_llmClient != null)
            {
                await ProcessWithLLM(history, conversationId, ct);
            }
            else
            {
                await ProcessWithoutLLM(request.Message, history, conversationId, ct);
            }

            await WriteSSE(new { type = "done" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat processing failed");
            await WriteSSE(new { type = "error", message = ex.Message });
        }

        await Response.Body.FlushAsync(ct);
    }

    /// <summary>
    /// Get conversation history
    /// </summary>
    [HttpGet("conversations/{conversationId}")]
    public ActionResult<List<ChatMessageDto>> GetConversation(string conversationId)
    {
        if (_conversations.TryGetValue(conversationId, out var history))
        {
            return Ok(history);
        }
        return NotFound();
    }

    /// <summary>
    /// Delete a conversation
    /// </summary>
    [HttpDelete("conversations/{conversationId}")]
    public IActionResult DeleteConversation(string conversationId)
    {
        _conversations.Remove(conversationId);
        return Ok();
    }

    private async Task ProcessWithLLM(List<ChatMessageDto> history, string conversationId, CancellationToken ct)
    {
        // Build messages for LLM
        var messages = new List<HazinaChatMessage>
        {
            new()
            {
                Role = HazinaMessageRole.System,
                Text = GetSystemPrompt()
            }
        };

        foreach (var msg in history)
        {
            messages.Add(new HazinaChatMessage
            {
                Role = msg.Role == "user" ? HazinaMessageRole.User : HazinaMessageRole.Assistant,
                Text = msg.Content
            });
        }

        // Get LLM response with streaming
        var responseContent = new StringBuilder();

        await _llmClient!.GetResponseStream(
            messages,
            chunk =>
            {
                responseContent.Append(chunk);
                WriteSSE(new { type = "content", content = chunk }).GetAwaiter().GetResult();
            },
            HazinaChatResponseFormat.Text,
            toolsContext: null,
            images: null,
            ct
        );

        // Add assistant response to history
        history.Add(new ChatMessageDto
        {
            Id = Guid.NewGuid().ToString(),
            Role = "assistant",
            Content = responseContent.ToString(),
            Timestamp = DateTime.UtcNow
        });
    }

    private async Task ProcessWithoutLLM(string message, List<ChatMessageDto> history, string conversationId, CancellationToken ct)
    {
        // Simple keyword-based processing when no LLM is available
        var response = new StringBuilder();
        var lowerMessage = message.ToLowerInvariant();

        if (lowerMessage.Contains("session") && (lowerMessage.Contains("list") || lowerMessage.Contains("what") || lowerMessage.Contains("running") || lowerMessage.Contains("status")))
        {
            var result = await ExecuteTool("list_sessions", new Dictionary<string, object>(), ct);
            response.AppendLine("Here are the current sessions:\n");
            response.AppendLine(result);
        }
        else if (lowerMessage.Contains("start") || lowerMessage.Contains("create") || lowerMessage.Contains("new"))
        {
            // Extract instruction if present
            var instruction = message;
            if (lowerMessage.Contains("clickup"))
            {
                instruction = "Work on ClickUp tasks";
            }

            var args = new Dictionary<string, object> { ["instruction"] = instruction };
            var result = await ExecuteTool("create_session", args, ct);

            await WriteSSE(new
            {
                type = "tool_call",
                toolCall = new
                {
                    id = Guid.NewGuid().ToString(),
                    name = "create_session",
                    arguments = args
                }
            });

            response.AppendLine("I've started a new session:\n");
            response.AppendLine(result);

            await WriteSSE(new
            {
                type = "tool_result",
                toolCallId = "create_session",
                result
            });
        }
        else if (lowerMessage.Contains("terminate") || lowerMessage.Contains("stop") || lowerMessage.Contains("kill"))
        {
            if (lowerMessage.Contains("all"))
            {
                var result = await ExecuteTool("terminate_all_stopped", new Dictionary<string, object>(), ct);
                response.AppendLine(result);
            }
            else
            {
                response.AppendLine("Please specify which session to terminate. You can say 'terminate session [id]' or 'terminate all stopped sessions'.");
            }
        }
        else if (lowerMessage.Contains("ctrl+c") || lowerMessage.Contains("interrupt"))
        {
            var sessions = _sessionManager.GetAllSessions().Where(s => s.IsRunning).ToList();
            if (sessions.Any())
            {
                var session = sessions.First();
                var result = await ExecuteTool("send_signal", new Dictionary<string, object>
                {
                    ["sessionId"] = session.SessionId,
                    ["signal"] = "interrupt"
                }, ct);
                response.AppendLine($"Sent Ctrl+C to session {session.SessionId.Substring(0, 8)}...\n");
                response.AppendLine(result);
            }
            else
            {
                response.AppendLine("No running sessions found.");
            }
        }
        else if (lowerMessage.Contains("help"))
        {
            response.AppendLine("I can help you manage terminal sessions. Try asking:");
            response.AppendLine("- \"What sessions are running?\"");
            response.AppendLine("- \"Start a new Claude agent\"");
            response.AppendLine("- \"Start a session to work on ClickUp tasks\"");
            response.AppendLine("- \"Send Ctrl+C to the first session\"");
            response.AppendLine("- \"Terminate all stopped sessions\"");
            response.AppendLine("- \"What are the agents working on?\"");
        }
        else
        {
            response.AppendLine("I'm your terminal orchestration assistant. I can help you:");
            response.AppendLine("- List and monitor terminal sessions");
            response.AppendLine("- Start new Claude agents with specific instructions");
            response.AppendLine("- Send signals (Ctrl+C) to sessions");
            response.AppendLine("- Terminate sessions");
            response.AppendLine("\nTry asking: \"What sessions are running?\" or \"Start a new agent\"");
        }

        // Stream the response
        var content = response.ToString();
        await WriteSSE(new { type = "content", content });

        // Add to history
        history.Add(new ChatMessageDto
        {
            Id = Guid.NewGuid().ToString(),
            Role = "assistant",
            Content = content,
            Timestamp = DateTime.UtcNow
        });
    }

    private async Task<string> ExecuteTool(string toolName, Dictionary<string, object> args, CancellationToken ct)
    {
        _logger.LogInformation("Executing tool: {Tool} with args: {Args}", toolName, JsonSerializer.Serialize(args));

        return toolName switch
        {
            "list_sessions" => await ListSessions(),
            "create_session" => await CreateSession(args, ct),
            "terminate_session" => await TerminateSession(args),
            "terminate_all_stopped" => await TerminateAllStopped(),
            "send_signal" => await SendSignal(args),
            "get_session_output" => await GetSessionOutput(args),
            _ => $"Unknown tool: {toolName}"
        };
    }

    private Task<string> ListSessions()
    {
        var sessions = _sessionManager.GetAllSessions().ToList();

        if (!sessions.Any())
        {
            return Task.FromResult("No active sessions.");
        }

        var sb = new StringBuilder();
        foreach (var session in sessions)
        {
            var status = session.IsRunning
                ? (session.WaitingForInput ? "⏳ Waiting for input" : "🟢 Running")
                : $"⚫ Stopped (exit: {session.ExitCode})";

            var title = session.Title ?? session.Command;
            var duration = DateTime.UtcNow - session.StartedAt;

            sb.AppendLine($"**{title}**");
            sb.AppendLine($"  ID: {session.SessionId.Substring(0, 12)}...");
            sb.AppendLine($"  Status: {status}");
            sb.AppendLine($"  Runtime: {duration:hh\\:mm\\:ss}");
            sb.AppendLine();
        }

        return Task.FromResult(sb.ToString());
    }

    private async Task<string> CreateSession(Dictionary<string, object> args, CancellationToken ct)
    {
        var instruction = args.TryGetValue("instruction", out var inst) ? inst.ToString() : null;

        var config = new TerminalSessionConfig
        {
            Command = _options.DefaultCommand,
            Arguments = _options.DefaultArguments,
            WorkingDirectory = _options.DefaultWorkingDirectory,
            Columns = _options.DefaultTerminalColumns,
            Rows = _options.DefaultTerminalRows
        };

        // If instruction provided, pass it as an argument
        if (!string.IsNullOrEmpty(instruction))
        {
            var argsList = config.Arguments?.ToList() ?? new List<string>();
            argsList.Add("-p");
            argsList.Add(instruction);
            config.Arguments = argsList.ToArray();
        }

        try
        {
            var session = await _sessionManager.CreateSessionAsync(config, ct);
            return $"Created session {session.SessionId.Substring(0, 12)}... running '{config.Command}'";
        }
        catch (Exception ex)
        {
            return $"Failed to create session: {ex.Message}";
        }
    }

    private async Task<string> TerminateSession(Dictionary<string, object> args)
    {
        if (!args.TryGetValue("sessionId", out var sessionIdObj))
        {
            return "No session ID provided";
        }

        var sessionId = sessionIdObj.ToString()!;
        var session = _sessionManager.GetSession(sessionId);

        if (session == null)
        {
            // Try partial match
            session = _sessionManager.GetAllSessions()
                .FirstOrDefault(s => s.SessionId.StartsWith(sessionId) || s.SessionId.Contains(sessionId));
        }

        if (session == null)
        {
            return $"Session not found: {sessionId}";
        }

        await _sessionManager.RemoveSessionAsync(session.SessionId);
        return $"Terminated session {session.SessionId.Substring(0, 12)}...";
    }

    private async Task<string> TerminateAllStopped()
    {
        var stoppedSessions = _sessionManager.GetAllSessions()
            .Where(s => !s.IsRunning)
            .ToList();

        if (!stoppedSessions.Any())
        {
            return "No stopped sessions to terminate.";
        }

        foreach (var session in stoppedSessions)
        {
            await _sessionManager.RemoveSessionAsync(session.SessionId);
        }

        return $"Terminated {stoppedSessions.Count} stopped session(s).";
    }

    private async Task<string> SendSignal(Dictionary<string, object> args)
    {
        if (!args.TryGetValue("sessionId", out var sessionIdObj))
        {
            return "No session ID provided";
        }

        var sessionId = sessionIdObj.ToString()!;
        var signal = args.TryGetValue("signal", out var sig) ? sig.ToString() : "interrupt";

        var session = _sessionManager.GetSession(sessionId);
        if (session == null)
        {
            session = _sessionManager.GetAllSessions()
                .FirstOrDefault(s => s.SessionId.StartsWith(sessionId) || s.SessionId.Contains(sessionId));
        }

        if (session == null)
        {
            return $"Session not found: {sessionId}";
        }

        var terminalSignal = signal?.ToLowerInvariant() switch
        {
            "interrupt" or "sigint" or "ctrl+c" => TerminalSignal.Interrupt,
            "quit" or "sigquit" => TerminalSignal.Quit,
            "terminate" or "sigterm" => TerminalSignal.Terminate,
            "kill" or "sigkill" => TerminalSignal.Kill,
            _ => TerminalSignal.Interrupt
        };

        await session.SendSignalAsync(terminalSignal);
        return $"Sent {terminalSignal} to session {session.SessionId.Substring(0, 12)}...";
    }

    private Task<string> GetSessionOutput(Dictionary<string, object> args)
    {
        if (!args.TryGetValue("sessionId", out var sessionIdObj))
        {
            return Task.FromResult("No session ID provided");
        }

        var sessionId = sessionIdObj.ToString()!;
        var session = _sessionManager.GetSession(sessionId);

        if (session == null)
        {
            session = _sessionManager.GetAllSessions()
                .FirstOrDefault(s => s.SessionId.StartsWith(sessionId) || s.SessionId.Contains(sessionId));
        }

        if (session == null)
        {
            return Task.FromResult($"Session not found: {sessionId}");
        }

        var history = session.GetOutputHistory();
        var text = Encoding.UTF8.GetString(history);

        // Return last 2000 chars
        if (text.Length > 2000)
        {
            text = "..." + text.Substring(text.Length - 2000);
        }

        return Task.FromResult(text);
    }

    private string GetSystemPrompt()
    {
        return @"You are Hazina, an AI assistant for terminal orchestration. You help users manage Claude Code CLI terminal sessions.

You can help users with:
- Listing and monitoring terminal sessions
- Starting new Claude agents with specific instructions
- Sending signals (Ctrl+C) to sessions
- Terminating sessions

Be helpful, concise, and informative about what's happening with their terminal sessions.";
    }

    private async Task WriteSSE(object data)
    {
        var json = JsonSerializer.Serialize(data);
        await Response.WriteAsync($"data: {json}\n\n");
        await Response.Body.FlushAsync();
    }
}

public class ChatRequest
{
    public string Message { get; set; } = "";
    public string? ConversationId { get; set; }
}

public class ChatMessageDto
{
    public string Id { get; set; } = "";
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public List<ToolCallDto>? ToolCalls { get; set; }
}

public class ToolCallDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public Dictionary<string, object> Arguments { get; set; } = new();
    public string? Result { get; set; }
}
