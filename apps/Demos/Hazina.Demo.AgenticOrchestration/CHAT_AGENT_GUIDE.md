# Chat Agent with OpenAI - Setup and Usage Guide

## Overview

The Chat Agent in the Hazina Agentic Orchestration app has been enhanced to use OpenAI's GPT models with proper tool calling capabilities. The agent can now intelligently manage terminal sessions through natural conversation.

## Features

### 🤖 AI-Powered Conversation
- **LLM Provider**: OpenAI (GPT-4o-mini by default, configurable)
- **Streaming Responses**: Real-time text generation
- **Conversation History**: Maintains context across messages
- **Tool Calling**: AI can autonomously use tools to perform actions

### 🛠️ Available Tools (5 Total)

The Chat Agent has access to five powerful tools for complete session lifecycle management:
1. **list_sessions** - Monitor active sessions
2. **create_session** - Start new Claude agents
3. **send_command** - Execute commands in running sessions
4. **list_archived_sessions** - Browse completed sessions 🆕
5. **restore_session** - Resume previous sessions with full context 🆕

The Chat Agent has access to three powerful tools:

#### 1. **list_sessions**
- **Description**: Lists all terminal sessions with their status, ID, title, and runtime
- **Parameters**: None
- **Use Cases**:
  - Check what sessions are running
  - Monitor session status
  - Get session IDs for sending commands

**Example User Request**: "What sessions are currently running?"

#### 2. **create_session**
- **Description**: Creates a new terminal session with an optional instruction
- **Parameters**:
  - `instruction` (optional string): The instruction to pass to the session
- **Use Cases**:
  - Start new Claude agents with specific tasks
  - Launch terminal processes
  - Initialize new workflows

**Example User Requests**:
- "Start a new Claude agent"
- "Create a session to work on ClickUp tasks"
- "Start a new agent to review pull requests"

#### 3. **send_command** ⭐ NEW
- **Description**: Sends a command to a specific terminal session and automatically presses Enter to execute it
- **Parameters**:
  - `sessionId` (required string): The ID of the target session
  - `command` (required string): The command to execute
- **Use Cases**:
  - Execute commands in running sessions
  - Interact with Claude agents programmatically
  - Automate terminal workflows

**Example User Requests**:
- "Send 'git status' to session abc123"
- "Execute 'npm test' in the first session"
- "Run 'dotnet build' in session def456"

#### 4. **list_archived_sessions** 🗄️ NEW
- **Description**: Lists archived (completed) sessions that can be restored
- **Parameters**:
  - `limit` (optional integer): Maximum number of sessions to return (default: 10)
- **Use Cases**:
  - Find past sessions to restore
  - Review session history
  - Check when sessions completed and their exit codes

**Example User Requests**:
- "Show me recent archived sessions"
- "List the last 20 completed sessions"
- "What archived sessions are available?"

#### 5. **restore_session** 🔄 NEW
- **Description**: Restores an archived session by creating a new session with the full conversation history as context
- **Parameters**:
  - `sessionId` (required string): The ID of the archived session to restore
  - `includeTimestamps` (optional boolean): Whether to include timestamps (default: false)
- **How It Works**:
  1. Reads the archived session log file
  2. Creates a new Claude Code CLI session
  3. **Waits for the CLI to fully start** (critical!)
  4. Sends the entire archived conversation as input to the new session
  5. The new session has complete context from the previous session
- **Use Cases**:
  - Continue a previous conversation
  - Review what was done in a past session
  - Resume work after a crash or disconnect
  - Learn from previous agent actions

**Example User Requests**:
- "Restore session abc123"
- "Continue the session from yesterday"
- "Bring back the archived session def456"

## Configuration

### 1. OpenAI API Key Setup

Edit `appsettings.json` and add your OpenAI API key:

```json
{
  "OpenAI": {
    "ApiKey": "sk-proj-...",  // ⚠️ REPLACE WITH YOUR ACTUAL API KEY
    "Model": "gpt-4o-mini",
    "EmbeddingModel": "text-embedding-3-small",
    "ImageModel": "dall-e-3",
    "TtsModel": "gpt-4o-mini-tts"
  }
}
```

**⚠️ Security Note**: For production, use environment variables or Azure Key Vault instead of hardcoding the API key:

```bash
# Windows (PowerShell)
$env:OpenAI__ApiKey = "sk-proj-..."

# Linux/Mac
export OpenAI__ApiKey="sk-proj-..."
```

### 2. Model Selection

You can change the model in `appsettings.json`:

| Model | Use Case | Cost |
|-------|----------|------|
| `gpt-4o-mini` | Default - Fast and cheap | $0.15/1M tokens |
| `gpt-4o` | More capable, slower | $2.50/1M tokens |
| `gpt-4-turbo` | High performance | $10/1M tokens |

### 3. Verify Registration

When you start the app, you should see:

```
✅ Hazina Agentic Orchestration services registered (declarative)
✅ OpenAI LLM Client registered (model: gpt-4o-mini)
✅ Basic Authentication configured (enabled: True)
```

If the OpenAI line is missing, check:
1. API key is configured
2. `Hazina.LLMs.OpenAI` project reference is added
3. Using statements are present in `Program.cs`

## Usage Examples

### Example 1: List All Sessions

**User**: "What sessions are running?"

**AI Response**:
```
I'll check the current sessions for you.

**Claude Agent - ClickUp Tasks**
  ID: a1b2c3d4-e5f6-7890-abcd-ef1234567890
  Status: 🟢 Running
  Runtime: 01:23:45

**Claude Agent - Code Review**
  ID: f9e8d7c6-b5a4-3210-9876-543210fedcba
  Status: ⏳ Waiting for input
  Runtime: 00:15:30
```

### Example 2: Create a New Session

**User**: "Start a new Claude agent to work on database migrations"

**AI Response**:
```
I'll create a new session for database migration work.

Created session d7c6b5a4-3210-9876-5432-10fedcba9876 running 'C:\scripts\claude_agent.bat' with instruction: Work on database migrations
```

### Example 3: Send a Command to a Session

**User**: "Send 'git status' to session a1b2c3d4"

**AI Response**:
```
I'll execute that command for you.

Command sent to session a1b2c3d4-e5f6-7890-abcd-ef1234567890: git status
```

The command is automatically executed (Enter is pressed), and you can monitor the output through the terminal session.

### Example 4: Complex Workflow

**User**: "Create a new session to fix the authentication bug, then send it the command to navigate to the API folder"

**AI Response**:
```
I'll help you set that up.

1. Created session c9b8a7b6-4321-0987-6543-210fedcba987 running 'C:\scripts\claude_agent.bat' with instruction: Fix the authentication bug

2. Command sent to session c9b8a7b6-4321-0987-6543-210fedcba987: cd C:\Projects\client-manager\ClientManagerAPI

Your session is ready and in the correct directory!
```

### Example 5: List Archived Sessions

**User**: "Show me the recent archived sessions"

**AI Response**:
```
Here are the recent archived sessions:

**C:\scripts\claude_agent.bat**
  Session ID: 20260130-142536-abc12345
  Started: 2026-01-30 14:25:36
  Ended: 2026-01-30 15:42:18
  Duration: 01:16:42
  Exit Code: 0
  Log Size: 245.73 KB

**C:\scripts\claude_agent.bat**
  Session ID: 20260130-103421-def67890
  Started: 2026-01-30 10:34:21
  Ended: 2026-01-30 11:05:09
  Duration: 00:30:48
  Exit Code: 0
  Log Size: 128.45 KB

**C:\scripts\claude_agent.bat**
  Session ID: 20260129-163045-ghi11213
  Started: 2026-01-29 16:30:45
  Ended: 2026-01-29 17:12:33
  Duration: 00:41:48
  Exit Code: 1
  Log Size: 98.21 KB
```

### Example 6: Restore an Archived Session

**User**: "Restore session 20260130-142536-abc12345"

**AI Response**:
```
I'll restore that session for you.

✅ Session restored successfully!

New Session ID: 20260130-154823-xyz98765
Original Session ID: 20260130-142536-abc12345
Status: Running

The archived conversation has been sent to the new session as context. The new Claude agent now has access to all the work from the previous session.
```

**What happens behind the scenes:**
1. System reads the log file for session `20260130-142536-abc12345`
2. Extracts the full conversation (removes timestamps and metadata)
3. Creates a new Claude Code CLI session
4. **Waits** for the CLI to fully start (10 second timeout)
5. **Waits** an additional 2 seconds for Claude to initialize
6. Sends the entire archived conversation as input to the new session
7. The new session can now continue where the previous one left off

## API Endpoint

### POST `/api/chat/messages`

**Request Body**:
```json
{
  "message": "What sessions are running?",
  "conversationId": "optional-conversation-id"
}
```

**Response**: Server-Sent Events (SSE) stream

```
data: {"conversationId":"abc-123"}
data: {"type":"content","content":"I'll "}
data: {"type":"content","content":"check "}
data: {"type":"content","content":"the "}
...
data: {"type":"done"}
```

**Response Events**:
- `conversationId`: Session identifier for follow-up messages
- `type: "content"`: Text chunk from AI response
- `type: "done"`: Stream complete

### GET `/api/chat/conversations/{conversationId}`

Retrieve conversation history.

**Response**:
```json
[
  {
    "id": "msg-1",
    "role": "user",
    "content": "What sessions are running?",
    "timestamp": "2026-01-30T12:34:56Z"
  },
  {
    "id": "msg-2",
    "role": "assistant",
    "content": "I'll check the current sessions...",
    "timestamp": "2026-01-30T12:34:57Z"
  }
]
```

### DELETE `/api/chat/conversations/{conversationId}`

Delete a conversation.

## Terminal API Endpoints

### GET `/api/terminal/archive`

List archived sessions with pagination.

**Query Parameters**:
- `page` (optional, default: 0): Page number
- `pageSize` (optional, default: 50): Items per page

**Response**:
```json
{
  "sessions": [
    {
      "sessionId": "20260130-142536-abc12345",
      "command": "C:\\scripts\\claude_agent.bat",
      "workingDirectory": "C:\\scripts",
      "startedAt": "2026-01-30T14:25:36Z",
      "endedAt": "2026-01-30T15:42:18Z",
      "exitCode": 0,
      "logFilePath": "C:\\scripts\\logs\\agent-sessions\\2026-01-30\\14\\session-20260130-142536-abc12345.log",
      "logSizeBytes": 251648
    }
  ],
  "totalCount": 45,
  "page": 0,
  "pageSize": 50,
  "hasMore": false
}
```

### GET `/api/terminal/archive/{sessionId}/log`

Get the full log content for an archived session.

**Response**:
```json
{
  "sessionId": "20260130-142536-abc12345",
  "content": "═══════════════════════...\n[14:25:36.123] [OUT] ...",
  "totalBytes": 251648
}
```

### POST `/api/terminal/archive/{sessionId}/restore` ⭐ NEW

Restore an archived session - creates a new session with the archived conversation as context.

**Query Parameters**:
- `includeTimestamps` (optional, default: false): Whether to include timestamps from the original log

**How It Works**:
1. Reads the archived session log file
2. Creates a new terminal session
3. **Waits for the session to start** (10 second timeout)
4. **Waits an additional 2 seconds** for initialization
5. Sends the archived conversation as input (with `\r` to execute)

**Response**:
```json
{
  "sessionId": "20260130-154823-xyz98765",
  "command": "C:\\scripts\\claude_agent.bat",
  "title": "Restored: 20260130-142536-abc12345",
  "startedAt": "2026-01-30T15:48:23Z",
  "isRunning": true,
  "waitingForInput": false,
  "signalRHubUrl": "/hubs/terminal"
}
```

**Error Responses**:
- `404`: Session log not found
- `500`: Session failed to start or timed out

## Architecture

### Tool Calling Flow

```
User Message
    ↓
Chat Controller
    ↓
OpenAI LLM Client (with ToolsContext)
    ↓
[AI decides to use tool]
    ↓
Tool Execute Function
    ↓
ITerminalSessionManager
    ↓
Terminal Session (PTY)
    ↓
Tool Result returned to AI
    ↓
AI formulates response
    ↓
Stream response to user
```

### Key Components

1. **ChatController**: Handles HTTP requests, manages conversation history
2. **ILLMClient**: Hazina's abstraction over OpenAI SDK
3. **ToolsContext**: Defines available tools for AI
4. **HazinaChatTool**: Tool definition (name, description, parameters, execute function)
5. **ITerminalSessionManager**: Manages terminal sessions (PTY instances)

## Troubleshooting

### Issue: "No LLM client registered"

**Symptoms**: Chat falls back to keyword-based processing

**Solution**:
1. Check OpenAI API key in `appsettings.json`
2. Verify `Hazina.LLMs.OpenAI` project reference
3. Check Program.cs has `builder.Services.AddSingleton<ILLMClient>(...)`

### Issue: "AI doesn't use tools"

**Symptoms**: AI responds conversationally but doesn't call tools

**Solution**:
1. Verify `CreateToolsContext()` is called in `ProcessWithLLM`
2. Check `toolsContext` parameter is passed to `GetResponseStream`
3. Review system prompt includes tool descriptions
4. Try more explicit requests: "Use the list_sessions tool"

### Issue: "send_command not working"

**Symptoms**: Command is called but nothing happens in terminal

**Solution**:
1. Verify session ID is correct (use `list_sessions` first)
2. Check session is running (`session.IsRunning`)
3. Ensure `\r` (carriage return) is appended to command
4. Check terminal session is in waiting state (`WaitingForInput`)

### Issue: "restore_session fails with timeout"

**Symptoms**: "Session failed to start" error after 10 seconds

**Solution**:
1. Check that Claude Code CLI is installed and accessible
2. Verify `claude_agent.bat` path in `appsettings.json`
3. Check system resources (CPU/memory) - CLI may be slow to start
4. Try increasing timeout in `RestoreArchivedSession` method (currently 10 seconds)
5. Manually test: `C:\scripts\claude_agent.bat` should start normally

### Issue: "Archived session not found"

**Symptoms**: 404 error when trying to restore a session

**Solution**:
1. Use `list_archived_sessions` tool first to verify session ID
2. Check session logs path: `C:\scripts\logs\agent-sessions\`
3. Verify session log files exist in subdirectories (organized by date/hour)
4. Try partial session ID match (e.g., first 8 characters)
5. Check file permissions on logs directory

### Issue: "Restored session doesn't have full context"

**Symptoms**: New session seems to be missing parts of conversation

**Solution**:
1. Check if original log file is complete (not truncated)
2. Try using `includeTimestamps=true` parameter
3. Verify log file size matches expected conversation length
4. Check for any errors in the `ExtractConversationContent` method
5. Manually inspect the log file to verify content is present

### Issue: "API key invalid"

**Symptoms**: 401 Unauthorized errors from OpenAI

**Solution**:
1. Verify API key starts with `sk-proj-` (project key) or `sk-` (legacy)
2. Check key is active at https://platform.openai.com/api-keys
3. Ensure no extra spaces/quotes in appsettings.json
4. Test key with curl:
   ```bash
   curl https://api.openai.com/v1/chat/completions \
     -H "Authorization: Bearer sk-proj-..." \
     -H "Content-Type: application/json" \
     -d '{"model":"gpt-4o-mini","messages":[{"role":"user","content":"test"}]}'
   ```

## Advanced Customization

### Add Custom Tools

Edit `ChatController.cs` → `CreateToolsContext()`:

```csharp
// Tool 4: Get session output
toolsContext.Add(new HazinaChatTool(
    name: "get_session_output",
    description: "Retrieves the last 2000 characters of output from a session",
    parameters: new List<ChatToolParameter>
    {
        new ChatToolParameter
        {
            Name = "sessionId",
            Description = "The ID of the session",
            Type = "string",
            Required = true
        }
    },
    execute: async (messages, call, cancel) =>
    {
        var param = new ChatToolParameter { Name = "sessionId" };
        if (!param.TryGetValue(call, out var sessionId))
        {
            return "Error: sessionId parameter is required";
        }

        var session = _sessionManager.GetSession(sessionId);
        if (session == null)
        {
            return $"Session not found: {sessionId}";
        }

        var history = session.GetOutputHistory();
        var text = Encoding.UTF8.GetString(history);

        // Return last 2000 chars
        if (text.Length > 2000)
        {
            text = "..." + text.Substring(text.Length - 2000);
        }

        return text;
    }
));
```

### Change System Prompt

Edit `GetSystemPrompt()` in `ChatController.cs` to customize AI behavior.

### Switch to GPT-4

For more capable (but slower/expensive) responses:

```json
{
  "OpenAI": {
    "Model": "gpt-4o"  // or "gpt-4-turbo"
  }
}
```

## Cost Estimation

### Token Usage Per Request

| Operation | Input Tokens | Output Tokens | Cost (gpt-4o-mini) |
|-----------|-------------|---------------|---------------------|
| List sessions | ~300 | ~150 | $0.000068 |
| Create session | ~300 | ~100 | $0.000060 |
| Send command | ~300 | ~80 | $0.000057 |

**Note**: Costs are approximate. Tool definitions, conversation history, and system prompt add to input tokens.

### Monthly Cost Example

**Scenario**: 1000 chat messages/month, average 2 tools per conversation

- Input tokens: 300 tokens/msg × 1000 = 300K tokens
- Output tokens: 150 tokens/msg × 1000 = 150K tokens
- Tool calls: 200 tokens/call × 2000 = 400K tokens
- **Total**: 850K tokens = **$0.13/month**

Even with heavy usage (10K messages/month), cost is ~$1.30/month.

## Security Best Practices

1. **API Key Management**:
   - Use environment variables in production
   - Never commit API keys to Git
   - Rotate keys regularly

2. **Authentication**:
   - Keep Basic Auth enabled (already configured)
   - Use HTTPS in production
   - Consider OAuth2 for multi-user scenarios

3. **Tool Safety**:
   - Validate all tool parameters
   - Sanitize command inputs (prevent injection)
   - Log all tool executions for audit

4. **Rate Limiting**:
   - Implement per-user rate limits
   - Monitor API usage
   - Set up billing alerts on OpenAI dashboard

## Future Enhancements

Potential improvements:
- **Tool: Terminate Session** - Stop running sessions via AI command
- **Tool: Get Session Logs** - Retrieve full output history for analysis
- **Tool: Send Signal** - Send Ctrl+C, Ctrl+Z, etc. programmatically
- **Smart Session Restoration** - AI automatically detects incomplete work and suggests restoration
- **Session Tagging** - Tag sessions by project/task for easier organization
- **Session Search** - Full-text search across archived session logs
- **Session Diff** - Compare sessions to see what changed
- **Automatic Checkpointing** - Periodic snapshots during long sessions
- **Conversation Persistence** - Save to database instead of memory
- **Multi-User Support** - Per-user conversation histories and session isolation
- **Voice Input** - Speech-to-text for hands-free control
- **Proactive Monitoring** - AI suggests actions based on session state (e.g., "Session abc123 has been idle for 10 minutes, should I terminate it?")
- **Session Replay** - Step-by-step playback of archived sessions
- **Contextual Restoration** - Restore with modified context (e.g., "Restore session abc123 but change the task to X")

## Support

For issues or questions:
- Check logs at `C:\scripts\logs\`
- Review session logs at `C:\scripts\logs\agent-sessions\`
- Open GitHub issues on the Hazina repository

---

**Last Updated**: 2026-01-30
**Hazina Version**: 1.0.0
**OpenAI SDK**: Latest compatible version
