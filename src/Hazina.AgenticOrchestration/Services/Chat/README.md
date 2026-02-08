# LLM-Powered Chat for Hazina Orchestration

## Overview

Orchestration Chat provides an AI-powered conversational interface to the Hazina Terminal Orchestration system. Users can ask questions about terminal sessions, request system status, and search for sessions using natural language.

## Architecture

### Components

1. **OrchestrationChatService** - Core chat orchestrator
   - Manages conversation history (in-memory)
   - Integrates with OpenAI via `OpenAIClientWrapper`
   - Implements rate limiting (5 messages/minute per session)
   - Token management (sliding window of 20 messages)
   - Streams responses via callback

2. **SessionManagementTools** - 5 chat tools for terminal operations
   - `list_sessions` - List all active terminal sessions
   - `get_session_details` - Get detailed session information
   - `list_archived_sessions` - List completed sessions (placeholder)
   - `get_system_status` - System health and session counts
   - `search_sessions` - Search sessions by name or command

3. **OrchestrationToolsContext** - IToolsContext implementation
   - Provides session management tools to LLM
   - Integrates with `ITerminalSessionManager`

4. **ChatController** - REST API endpoints
   - `POST /api/chat/{sessionId}/message` - Send message, stream response
   - `GET /api/chat/{sessionId}/history` - Get conversation history
   - `DELETE /api/chat/{sessionId}` - Clear conversation
   - `POST /api/chat/{sessionId}/subscribe` - SignalR subscription info

### SignalR Integration

Real-time streaming via `ClaudeOrchestrationHub`:
- **ChatChunk** event - Streaming response chunks
- **ChatComplete** event - Completion signal with final message

Client connects to:
- Hub: `/hubs/agentic`
- Group: `chat-{sessionId}`

## API Endpoints

### Send Message

```http
POST /api/chat/{sessionId}/message
Authorization: Basic {credentials}
Content-Type: application/json

{
  "message": "How many sessions are running?"
}
```

**Response:**
```json
{
  "success": true,
  "message": "There are currently 3 active sessions running...",
  "tokensUsed": 450
}
```

**SignalR Events:**
```javascript
// During streaming
{
  "sessionId": "chat-session-1",
  "chunk": "There are ",
  "timestamp": "2026-02-08T12:00:00Z"
}

// On completion
{
  "sessionId": "chat-session-1",
  "message": "Complete response text",
  "tokensUsed": 450,
  "timestamp": "2026-02-08T12:00:01Z"
}
```

### Get History

```http
GET /api/chat/{sessionId}/history
Authorization: Basic {credentials}
```

**Response:**
```json
{
  "sessionId": "chat-session-1",
  "messages": [
    {
      "role": "User",
      "text": "How many sessions are running?",
      "timestamp": "..."
    },
    {
      "role": "Assistant",
      "text": "There are currently 3 active sessions...",
      "timestamp": "..."
    }
  ]
}
```

### Clear History

```http
DELETE /api/chat/{sessionId}
Authorization: Basic {credentials}
```

**Response:**
```json
{
  "success": true,
  "message": "Conversation history cleared"
}
```

## Testing

### Prerequisites

1. **OpenAI API Key** configured in `appsettings.Secrets.json`:
```json
{
  "OpenAI": {
    "ApiKey": "sk-..."
  }
}
```

2. **Demo App Running**:
```bash
cd apps/Demos/Hazina.Demo.AgenticOrchestration
dotnet run --configuration Release
```

3. **Authentication Credentials** (from appsettings.json):
   - Username: `bosi`
   - Password: `Th1s1sSp4rt4!`

### Example Conversation 1: Session Count

**Request:**
```bash
curl -X POST https://localhost:5123/api/chat/test-session-1/message \
  -u bosi:Th1s1sSp4rt4! \
  -H "Content-Type: application/json" \
  -d '{"message": "How many sessions are running?"}'
```

**Expected Tool Call:** `get_system_status`

**Expected Response:**
```
There are currently X active sessions running, Y inactive sessions, and Z sessions waiting for input.
```

### Example Conversation 2: Session Details

**Request:**
```bash
curl -X POST https://localhost:5123/api/chat/test-session-1/message \
  -u bosi:Th1s1sSp4rt4! \
  -H "Content-Type: application/json" \
  -d '{"message": "Show me details of session 20260208-153045-abc12345"}'
```

**Expected Tool Call:** `get_session_details` with `sessionId` parameter

**Expected Response:**
```
Session 20260208-153045-abc12345 is running 'claude' and started at 15:30:45.
Duration: 00:15:23. Status: Running.
```

### Example Conversation 3: Search Sessions

**Request:**
```bash
curl -X POST https://localhost:5123/api/chat/test-session-1/message \
  -u bosi:Th1s1sSp4rt4! \
  -H "Content-Type: application/json" \
  -d '{"message": "Find all sessions related to build"}'
```

**Expected Tool Call:** `search_sessions` with `query="build"`

**Expected Response:**
```
I found 2 sessions matching 'build':
- Session build-backend (Running) - Started: 14:30:00
- Session build-frontend (Stopped) - Started: 13:15:00
```

### Example Conversation 4: List All Sessions

**Request:**
```bash
curl -X POST https://localhost:5123/api/chat/test-session-1/message \
  -u bosi:Th1s1sSp4rt4! \
  -H "Content-Type: application/json" \
  -d '{"message": "List all sessions"}'
```

**Expected Tool Call:** `list_sessions`

**Expected Response:**
```
Here are all active terminal sessions:

1. Session 20260208-153045-abc12345
   - Command: claude
   - Status: Running
   - Duration: 00:15:23
   - Waiting for input: No

2. Session 20260208-143000-def67890
   - Command: npm run dev
   - Status: Running
   - Duration: 01:25:15
   - Waiting for input: Yes
```

## Rate Limiting

- **Limit:** 5 messages per minute per session
- **Error Response (429-like):**
```json
{
  "success": false,
  "errorMessage": "Rate limit exceeded. Maximum 5 messages per minute.",
  "totalTokensUsed": 0
}
```

## Token Management

- **Max Conversation Messages:** 20 (sliding window)
- **Pruning:** Automatically removes old messages when limit exceeded
- **System Prompt:** Always included, not counted in limit

## Error Handling

### LLM Unavailable
```json
{
  "success": false,
  "errorMessage": "OpenAI API error: ...",
  "totalTokensUsed": 0
}
```

### Invalid Session ID (get_session_details)
Tool returns:
```json
{
  "error": "Session 'invalid-id' not found"
}
```

LLM response:
```
I couldn't find a session with ID 'invalid-id'. You can list all sessions by asking "show all sessions".
```

### Tool Execution Error
Errors are returned to LLM, which explains to user:
```
I encountered an error while checking the system status: [error details]. Please try again.
```

## Configuration

### OpenAI Model
Default: `gpt-4o-mini` (configured in `appsettings.json`)

### System Prompt
Defined in `OrchestrationChatService.cs`:
```
You are an AI assistant for the Hazina Terminal Orchestration system.

Your role:
- Help users manage their terminal sessions
- Answer questions about session status and system health
- Provide information about terminal operations
- Execute commands via available tools when needed

Available tools: [list of 5 tools]

Guidelines:
- Be concise and helpful
- Always use tools when user asks about sessions
- Format output clearly (use markdown for better readability)
- If a session ID is mentioned, validate it exists before showing details
- Provide actionable information
```

## Troubleshooting

### Chat not responding
1. Check OpenAI API key in `appsettings.Secrets.json`
2. Verify demo app is running on HTTPS port 5123
3. Check authentication credentials
4. Verify SignalR connection (client-side)

### Tool not being called
1. Check LLM logs for tool selection reasoning
2. Verify tool definitions in `SessionManagementTools.cs`
3. Try more explicit user message (e.g., "list all sessions" vs "sessions?")

### Rate limit issues
1. Wait 60 seconds between bursts of messages
2. Check rate limit logic in `OrchestrationChatService.CheckRateLimit()`
3. Adjust `MAX_MESSAGES_PER_MINUTE` constant if needed

## Future Enhancements

### Phase 2 Features (Not Yet Implemented)
1. **Conversation Persistence** - Save to file system instead of in-memory
2. **More Tools:**
   - `create_session` - Start new session via chat
   - `send_command` - Send command to session via chat
   - `stop_session` - Stop session via chat
3. **Multi-user Support** - Separate conversations per user
4. **Voice Input** - Speech-to-text for chat
5. **Proactive Notifications** - Agent alerts user when session fails

## Implementation Notes

### Tool Calling Pattern
Uses `HazinaChatTool` delegate pattern:
```csharp
new HazinaChatTool(
    name: "list_sessions",
    description: "List all active terminal sessions with their status",
    parameters: new List<ChatToolParameter>(),
    execute: async (messages, toolCall, ct) =>
    {
        var sessions = sessionManager.GetAllSessions();
        var result = sessions.Select(s => new { ... }).ToList();
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }
)
```

### Session Properties
`ITerminalSession` properties used by tools:
- `SessionId` - Unique identifier
- `Command` - Executable/command being run
- `StartedAt` - Session start timestamp
- `IsRunning` - Whether process is active
- `WaitingForInput` - Whether session expects user input
- `Title` - Dynamic title (or null, fallback to Command)
- `ExitCode` - Process exit code (or null if running)

### SignalR Group Naming
Chat groups follow pattern: `chat-{sessionId}`

Example: `chat-test-session-1`

Client must call `JoinChatSession(sessionId)` on hub to receive events.

---

**Last Updated:** 2026-02-08
**Version:** 1.0.0 (MVP)
**Status:** ✅ Backend Complete, Frontend Optional
