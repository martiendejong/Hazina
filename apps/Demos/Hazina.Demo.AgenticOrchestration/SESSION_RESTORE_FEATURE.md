# Session Restore Feature - Implementation Summary

## Overview

Added complete session restoration capability to the Hazina Agentic Orchestration Chat Agent. Users can now restore archived sessions with full conversation history, allowing them to continue previous work seamlessly.

## What Was Implemented

### 1. Backend API Endpoint

**Location**: `TerminalController.cs`

**New Endpoint**: `POST /api/terminal/archive/{sessionId}/restore`

**Features**:
- Reads archived session log files from disk
- Extracts conversation content (removes timestamps and metadata)
- Creates a new terminal session
- **Waits for Claude CLI to fully start** (10 second timeout)
- **Waits additional 2 seconds** for initialization
- Sends archived conversation as input to new session
- Returns new session details

**Query Parameters**:
- `includeTimestamps` (optional, default: false): Include or strip timestamps from archived content

### 2. Chat Agent Tools

**Location**: `ChatController.cs`

**New Tools** (2):

#### Tool 4: `list_archived_sessions`
- Lists completed sessions with metadata
- Parameters: `limit` (optional integer, default: 10)
- Returns: Session ID, command, start/end times, duration, exit code, log size
- Use: "Show me archived sessions"

#### Tool 5: `restore_session`
- Restores an archived session with full context
- Parameters:
  - `sessionId` (required string)
  - `includeTimestamps` (optional boolean, default: false)
- Returns: New session ID and status
- Use: "Restore session abc123"

### 3. Enhanced System Prompt

Updated AI system prompt to include:
- Descriptions of new tools
- Usage guidelines for session restoration
- Workflow recommendations (list archived sessions first, then restore)

### 4. Helper Methods

**Added to TerminalController**:

#### `ExtractConversationContent(string logContent)`
- Parses log files to extract clean conversation text
- Removes:
  - Header metadata (Session ID, command, working directory)
  - Timestamps (e.g., `[12:34:56.789]`)
  - Log type markers (`[OUT]`, `[INPUT]`)
  - Footer (SESSION ENDED block)
- Formats:
  - Input lines as "User: {content}"
  - Output lines as plain text
- Cleans control characters (`[CR]`, `[LF]`, `>>>`)

## How It Works

### Restoration Flow

```
User Request: "Restore session abc123"
         ↓
Chat Agent calls restore_session tool
         ↓
Tool makes HTTP POST to /api/terminal/archive/{sessionId}/restore
         ↓
Backend reads log file from disk
         ↓
Extracts conversation content (removes metadata)
         ↓
Creates new terminal session
         ↓
WAITS for session.IsRunning == true (max 10 seconds)
         ↓
WAITS additional 2 seconds for Claude initialization
         ↓
Sends archived content to new session: "{content}\r"
         ↓
Returns new session details to tool
         ↓
AI responds to user with success message
```

### Key Implementation Details

1. **Timeout Protection**: 10 second timeout for session startup prevents indefinite waiting
2. **Initialization Delay**: 2 second delay after startup ensures Claude is ready to receive input
3. **Carriage Return**: Appends `\r` to input to simulate Enter key press
4. **Partial ID Matching**: Supports abbreviated session IDs for easier restoration
5. **Error Handling**: Comprehensive error messages for debugging

## File Locations

| Component | Path |
|-----------|------|
| Terminal Controller | `src/Hazina.AgenticOrchestration/Controllers/TerminalController.cs` |
| Chat Controller | `apps/Demos/Hazina.Demo.AgenticOrchestration/Controllers/ChatController.cs` |
| Configuration | `apps/Demos/Hazina.Demo.AgenticOrchestration/appsettings.json` |
| Documentation | `apps/Demos/Hazina.Demo.AgenticOrchestration/CHAT_AGENT_GUIDE.md` |
| Session Logs | `C:\scripts\logs\agent-sessions\{date}\{hour}\session-{id}.log` |

## Usage Examples

### Example 1: List Archived Sessions

**User**: "Show me recent archived sessions"

**AI**: Uses `list_archived_sessions` tool to display:
- Session IDs
- Commands executed
- Start/end times
- Duration
- Exit codes
- Log file sizes

### Example 2: Restore a Session

**User**: "Restore session 20260130-142536-abc12345"

**AI**:
1. Calls `restore_session` tool with session ID
2. Backend creates new session
3. Backend waits for CLI to start
4. Backend sends archived conversation as input
5. AI confirms: "✅ Session restored successfully! New Session ID: xyz98765"

### Example 3: Restore with Timestamps

**User**: "Restore session abc123 and include the timestamps"

**AI**: Calls `restore_session` with `includeTimestamps=true`, preserving exact timing from original session

## Testing

### Manual Test 1: List Archives
```bash
curl http://localhost:5000/api/terminal/archive
```

### Manual Test 2: Restore Session
```bash
curl -X POST http://localhost:5000/api/terminal/archive/20260130-142536-abc12345/restore
```

### Manual Test 3: Chat Agent
```bash
POST /api/chat/messages
{
  "message": "List archived sessions and restore the most recent one"
}
```

## Configuration

### Required Settings (`appsettings.json`)

```json
{
  "AgenticOrchestration": {
    "SessionLogging": {
      "Enabled": true,
      "BasePath": "C:\\scripts\\logs\\agent-sessions"
    },
    "Terminal": {
      "DefaultCommand": "C:\\scripts\\claude_agent.bat"
    }
  }
}
```

### Adjustable Timeouts

**Location**: `TerminalController.RestoreArchivedSession()`

```csharp
// Wait for session to start (adjust if needed)
var timeout = DateTime.UtcNow.AddSeconds(10);  // Currently 10 seconds

// Initialization delay (adjust if needed)
await Task.Delay(2000, ct);  // Currently 2 seconds
```

## Error Handling

| Error | HTTP Code | Cause | Solution |
|-------|-----------|-------|----------|
| Session not found | 404 | Session ID doesn't exist in logs | Use `list_archived_sessions` to find correct ID |
| Session failed to start | 500 | CLI didn't start within 10 seconds | Check system resources, verify claude_agent.bat path |
| Session logs not configured | 404 | Logs path not set or doesn't exist | Configure `SessionLogging.BasePath` in appsettings |
| Failed to restore | 500 | Generic error during restoration | Check logs for detailed error message |

## Performance Considerations

### Memory
- Log files are read into memory during restoration
- Large sessions (>10MB logs) may cause delays
- Consider streaming for very large files in future enhancement

### Startup Time
- Claude CLI startup: ~3-5 seconds
- Initialization delay: 2 seconds
- Total restoration time: ~5-7 seconds typical

### Concurrency
- Multiple simultaneous restorations are supported
- Each creates an independent new session
- No locking required on log files (read-only access with `FileShare.ReadWrite`)

## Known Limitations

1. **No Streaming**: Archived content sent as single input (not streamed incrementally)
2. **Fixed Delays**: Hardcoded 10s timeout and 2s initialization delay
3. **Text-Only**: Images/attachments from original session not preserved
4. **No Validation**: Doesn't verify if archived content is valid for current context
5. **In-Memory**: Conversation history stored in memory (not persisted to database)

## Future Enhancements

### Priority 1: High Impact
- [ ] Smart timeout adjustment based on session size
- [ ] Progress indicators during restoration
- [ ] Partial restoration (restore last N messages only)
- [ ] Session metadata editing before restoration

### Priority 2: Quality of Life
- [ ] Automatic session tagging for easier discovery
- [ ] Full-text search across archived sessions
- [ ] Session diff (compare archived vs current state)
- [ ] Bulk restoration (restore multiple sessions)

### Priority 3: Advanced
- [ ] Machine learning-based timeout prediction
- [ ] Context modification during restoration ("Change task to X")
- [ ] Session replay (step-by-step playback)
- [ ] Automatic checkpointing during long sessions

## Security Considerations

1. **File Access**: Only reads from configured logs directory
2. **No Code Execution**: Archived content passed as text input only
3. **User Authentication**: Inherits authentication from Terminal API
4. **Path Traversal**: Uses `SearchOption.AllDirectories` but limited to base path
5. **Input Sanitization**: No special handling (relies on terminal to sanitize)

## Maintenance

### Log Rotation
- Session logs organized by date/hour: `{date}/{hour}/session-{id}.log`
- Old logs can be archived/compressed externally
- No automatic cleanup (consider implementing log retention policy)

### Monitoring
- Track restoration success rate
- Monitor average restoration time
- Alert on repeated failures

### Debugging
- Enable detailed logging: `"Logging.LogLevel.Default": "Debug"`
- Check session log files for completeness
- Verify Claude CLI starts normally outside orchestration

## Documentation

- **User Guide**: `CHAT_AGENT_GUIDE.md` - Complete usage documentation
- **API Reference**: Swagger UI at `/swagger` when running
- **Code Comments**: Inline documentation in source files

## Testing Checklist

- [x] Backend endpoint returns 404 for non-existent session
- [x] Backend endpoint creates new session
- [x] Backend waits for session to start
- [x] Backend sends archived content to new session
- [x] Chat agent tool calls backend endpoint
- [x] AI responds with success message
- [x] Documentation updated
- [ ] Integration tests added
- [ ] E2E tests with real Claude CLI
- [ ] Load testing with large sessions

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-01-30 | Initial implementation - restore functionality added |

---

**Implemented by**: Claude Code Agent
**Date**: 2026-01-30
**Status**: ✅ Complete and ready for testing
