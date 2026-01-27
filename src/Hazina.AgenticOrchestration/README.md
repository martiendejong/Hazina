# Hazina.AgenticOrchestration

Complete orchestration module for managing Claude Code CLI instances via web interface.

## Features

### 1. **Show All Active Instances** ✅
View all currently running Claude Code CLI instances with:
- Session ID
- Agent name
- Start time and runtime
- Last heartbeat (with active/stale detection)
- Current task
- Tasks completed/failed
- Worktree seat allocation

**API Endpoint:**
```
GET /api/agentic/instances
```

**Response:**
```json
[
  {
    "sessionId": "20260127-023500-abc123",
    "agentName": "agent-002",
    "startTime": "2026-01-27T02:35:00Z",
    "lastHeartbeat": "2026-01-27T02:40:00Z",
    "status": "active",
    "currentTask": "Building Agentic Orchestration module",
    "worktreeSeat": "agent-002",
    "tasksCompleted": 5,
    "tasksFailed": 0,
    "runtime": "00:05:00",
    "isActive": true
  }
]
```

---

### 2. **Capture and Stream Output** ✅
Real-time streaming of Claude instance output with:
- Historical output retrieval
- Live output streaming via SignalR
- Timestamp extraction from logs
- Line-by-line delivery

**REST API:**
```
GET /api/agentic/instances/{sessionId}/output?lastN=100
```

**SignalR:**
```javascript
connection.on('ReceiveOutput', (data) => {
  console.log(data.output); // Real-time output
});
```

---

### 3. **User Input Notifications** ✅
Notify users when Claude instances need input:
- Browser notifications
- Real-time alerts via SignalR
- Pending interactions dashboard
- Multiple notification channels (instance-specific + global)

**From Claude CLI:**
```powershell
# Request user input
.\tools\Request-UserInput.ps1 `
    -Prompt "Should I create PR now or wait?" `
    -Type "choice" `
    -Options @("Create PR", "Wait", "Cancel")
```

**API Endpoint (called by Claude CLI):**
```
POST /api/agentic/instances/{sessionId}/await-input
{
  "type": "choice",
  "prompt": "Should I create PR now?",
  "options": ["Create PR", "Wait", "Cancel"]
}
```

**SignalR Events:**
```javascript
// Instance-specific notification
connection.on('InputRequired', (data) => {
  showNotification(data.prompt, data.options);
});

// Global notification (all orchestrators)
connection.on('GlobalInputRequired', (data) => {
  showNotification(`${data.agentName} needs input: ${data.prompt}`);
});
```

---

### 4. **Send User Responses** ✅
Users respond to Claude instances via web interface:
- REST API for submitting responses
- Real-time delivery to Claude CLI via polling
- Response tracking (who responded, when)
- Automatic status updates

**Web UI submits response:**
```
POST /api/agentic/instances/{sessionId}/interactions/{interactionId}/respond
{
  "response": "Create PR"
}
```

**Claude CLI polls for response:**
```
GET /api/agentic/instances/{sessionId}/interactions/{interactionId}/response
```

**Returns:**
```json
{
  "status": "responded",
  "response": "Create PR",
  "respondedAt": "2026-01-27T02:45:00Z",
  "respondedBy": "admin@example.com"
}
```

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      Web Frontend (React)                   │
│  - Instance Dashboard                                       │
│  - Output Viewer (terminal-like)                            │
│  - Pending Interactions Panel                               │
│  - Response Form                                             │
└─────────────────────────────────────────────────────────────┘
                              │
                              │ SignalR + REST API
                              ▼
┌─────────────────────────────────────────────────────────────┐
│               Hazina.AgenticOrchestration                   │
│                                                              │
│  Controllers:                                                │
│  - InstancesController (CRUD for instances)                 │
│  - InteractionsController (pending interactions)            │
│                                                              │
│  Services:                                                   │
│  - ClaudeInstanceManager (query active instances)           │
│  - OutputCaptureService (stream/read logs)                  │
│  - InteractionService (manage user input requests)          │
│                                                              │
│  Hubs:                                                       │
│  - ClaudeOrchestrationHub (real-time events)                │
│                                                              │
│  Data:                                                       │
│  - DatabaseInitializer (SQLite schema)                      │
└─────────────────────────────────────────────────────────────┘
                              │
                              │ SQLite + File System
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                  Data Storage Layer                         │
│                                                              │
│  SQLite Database (C:\scripts\_machine\agent-activity.db):  │
│  - agent_sessions (active instances)                        │
│  - interaction_requests (user input requests)               │
│                                                              │
│  File System:                                                │
│  - C:\scripts\logs\agent-{sessionId}.log (output logs)     │
└─────────────────────────────────────────────────────────────┘
                              │
                              │ REST API + Polling
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                  Claude Code CLI Instances                  │
│                                                              │
│  - agent-session.ps1 (start/heartbeat/end)                  │
│  - Request-UserInput.ps1 (request user input)               │
│  - Automatic polling for responses                          │
└─────────────────────────────────────────────────────────────┘
```

---

## Database Schema

### `interaction_requests` Table

```sql
CREATE TABLE interaction_requests (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    session_id TEXT NOT NULL,
    request_type TEXT NOT NULL,  -- 'question', 'confirmation', 'choice'
    prompt TEXT NOT NULL,
    options TEXT,  -- JSON array
    created_at DATETIME NOT NULL,
    status TEXT NOT NULL DEFAULT 'pending',  -- 'pending', 'responded', 'cancelled'
    response TEXT,
    responded_at DATETIME,
    responded_by TEXT,
    FOREIGN KEY (session_id) REFERENCES agent_sessions(session_id)
);

CREATE INDEX idx_interaction_requests_status
    ON interaction_requests(status, created_at);

CREATE INDEX idx_interaction_requests_session
    ON interaction_requests(session_id);
```

---

## Usage Examples

### Example 1: Monitor All Active Instances

```typescript
// Fetch active instances
const response = await fetch('/api/agentic/instances');
const instances = await response.json();

instances.forEach(instance => {
  console.log(`${instance.agentName}: ${instance.currentTask}`);
});
```

---

### Example 2: Watch Real-Time Output

```typescript
import { HubConnectionBuilder } from '@microsoft/signalr';

const connection = new HubConnectionBuilder()
  .withUrl('/hubs/claude-orchestration')
  .build();

// Subscribe to instance output
await connection.invoke('SubscribeToInstance', sessionId);

// Receive real-time output
connection.on('ReceiveOutput', (data) => {
  console.log(`[${data.timestamp}] ${data.output}`);
});

await connection.start();
```

---

### Example 3: Respond to User Input Request

```typescript
// Get pending interactions
const pending = await fetch('/api/agentic/interactions/pending')
  .then(r => r.json());

// User selects response
const interaction = pending[0];
const userResponse = "Create PR";

// Submit response
await fetch(`/api/agentic/instances/${interaction.sessionId}/interactions/${interaction.id}/respond`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ response: userResponse })
});
```

---

### Example 4: Claude CLI Requests User Input

```powershell
# In a Claude workflow script
$userChoice = . "C:\Projects\hazina\tools\Request-UserInput.ps1" `
    -Prompt "Should I create PR now or wait for more changes?" `
    -Type "choice" `
    -Options @("Create PR now", "Wait for more changes", "Cancel")

if ($userChoice -eq "Create PR now") {
    # Create PR
    gh pr create --title "..." --body "..."
}
elseif ($userChoice -eq "Wait for more changes") {
    Write-Host "Waiting for more changes..."
    return
}
else {
    Write-Host "Cancelled by user"
    return
}
```

---

## Integration with Existing Systems

### 1. **agent-activity.db Integration**
- Uses existing `agent_sessions` table
- Adds `interaction_requests` table
- Joins on `session_id` for full context

### 2. **Log File Integration**
- Reads from `C:\scripts\logs\agent-{sessionId}.log`
- Streams new lines in real-time
- Extracts timestamps from log format

### 3. **agent-session.ps1 Integration**
- No changes required to existing script
- New `Request-UserInput.ps1` utility
- Uses existing session ID mechanism

---

## Installation & Setup

### 1. **Add to Hazina Solution**

```bash
# Already created in src/Hazina.AgenticOrchestration/
dotnet sln add src/Hazina.AgenticOrchestration/Hazina.AgenticOrchestration.csproj
```

### 2. **Initialize Database**

```csharp
// In Startup.cs or Program.cs
var dbPath = @"C:\scripts\_machine\agent-activity.db";
var dbInitializer = new DatabaseInitializer(dbPath);
dbInitializer.Initialize();
```

### 3. **Configure Services**

```csharp
// Register services
builder.Services.AddSingleton<IClaudeInstanceManager>(
    new ClaudeInstanceManager(@"C:\scripts\_machine\agent-activity.db"));

builder.Services.AddSingleton<IOutputCaptureService>(sp =>
    new OutputCaptureService(
        sp.GetRequiredService<IHubContext<ClaudeOrchestrationHub>>(),
        @"C:\scripts\logs"));

builder.Services.AddSingleton<IInteractionService>(sp =>
    new InteractionService(
        sp.GetRequiredService<IHubContext<ClaudeOrchestrationHub>>(),
        @"C:\scripts\_machine\agent-activity.db"));

// Add SignalR
builder.Services.AddSignalR();
```

### 4. **Map Hub Endpoint**

```csharp
// In Program.cs
app.MapHub<ClaudeOrchestrationHub>("/hubs/claude-orchestration");
```

---

## Frontend Components

See example React components in the design doc for:
- Instance Dashboard
- Output Viewer
- Pending Interactions Panel

---

## Security Considerations

1. **Authentication**: Add authentication to controllers/hub
2. **Authorization**: Limit who can respond to interactions
3. **Rate Limiting**: Prevent abuse of polling endpoints
4. **Input Validation**: Sanitize all user responses

---

## Future Enhancements

- [ ] WebSocket-based output streaming (vs file polling)
- [ ] Multi-user collaboration (multiple orchestrators)
- [ ] Task queue integration
- [ ] Historical session playback
- [ ] Performance metrics dashboard
- [ ] Automated testing framework

---

## License

Part of Hazina framework - see main repository LICENSE
