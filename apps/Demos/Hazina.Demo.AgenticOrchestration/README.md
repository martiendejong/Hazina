# Hazina.Demo.AgenticOrchestration

**Complete example application** demonstrating the Hazina Agentic Orchestration module using Hazina's declarative language patterns.

## Quick Start

```bash
cd apps/Demos/Hazina.Demo.AgenticOrchestration
dotnet run
```

Then visit:
- **Swagger UI**: http://localhost:5000/swagger
- **API Root**: http://localhost:5000
- **Health Check**: http://localhost:5000/health

## Declarative Configuration

### entities.yaml - Zero-Code API Definition

This demo uses Hazina's YAML-based declarative language to define the entire API:

```yaml
entities:
  - name: ClaudeInstance
    fields:
      - name: sessionId
        type: String
        required: true
        indexed: true
      - name: status
        type: Enum
        enumValues: ["active", "idle", "waiting", "completed"]
    features:
      crud: true
      realtime: true      # SignalR updates
      search: true
```

### One-Liner Service Registration

```csharp
// Program.cs - Just one line to register everything!
builder.Services.AddHazinaAgenticOrchestration(options =>
{
    options.DatabasePath = @"C:\scripts\_machine\agent-activity.db";
    options.LogsPath = @"C:\scripts\logs";
    options.EnableSignalR = true;
});
```

## API Endpoints

### Instances

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/agentic/instances` | List all active Claude instances |
| GET | `/api/agentic/instances/{sessionId}` | Get instance details |
| GET | `/api/agentic/instances/{sessionId}/output` | Get output history |

### Interactions

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/agentic/instances/{sessionId}/await-input` | Claude requests user input |
| GET | `/api/agentic/instances/{sessionId}/interactions/{id}/response` | Poll for user response |
| POST | `/api/agentic/instances/{sessionId}/interactions/{id}/respond` | User submits response |
| GET | `/api/agentic/interactions/pending` | All pending interactions |

### Utility

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/health` | Health check |
| GET | `/api/stats` | Quick stats (active count, instances) |
| GET | `/api/interactions/count` | Pending interaction count |

## SignalR Real-Time Events

Connect to: `ws://localhost:5000/hubs/agentic`

### Events

| Event | Direction | Description |
|-------|-----------|-------------|
| `ReceiveOutput` | Server → Client | New output line from instance |
| `InputRequired` | Server → Client | Claude needs user input |
| `ResponseReceived` | Server → Client | User responded to interaction |
| `GlobalInputRequired` | Server → All | Broadcast to all orchestrators |

### JavaScript Example

```javascript
import { HubConnectionBuilder } from '@microsoft/signalr';

const connection = new HubConnectionBuilder()
    .withUrl('http://localhost:5000/hubs/agentic')
    .build();

// Subscribe to instance
await connection.invoke('SubscribeToInstance', sessionId);

// Real-time output
connection.on('ReceiveOutput', (data) => {
    console.log(`[${data.timestamp}] ${data.output}`);
});

// Input required notification
connection.on('InputRequired', (data) => {
    showNotification(`Claude needs input: ${data.prompt}`);
});

await connection.start();
```

## Hazina Declarative Patterns Used

### 1. YAML Entity DSL (`entities.yaml`)

Defines 4 entities with zero C# code:
- **ClaudeInstance** - Active CLI sessions
- **InteractionRequest** - User input requests
- **OutputLine** - Captured output
- **AgentTask** - Submitted tasks

### 2. Service Extension Pattern

```csharp
// One method registers everything
builder.Services.AddHazinaAgenticOrchestration();
```

### 3. Feature Flags (per entity)

```yaml
features:
  crud: true              # GET, POST, PUT, DELETE
  search: true            # Full-text search
  realtime: true          # SignalR events
  pagination: true        # List pagination
  filtering: true         # Query parameters
  bulkOperations: true    # Batch create/update/delete
```

### 4. Custom Endpoints

```yaml
customEndpoints:
  - path: /api/claudeinstances/active
    method: GET
    query: "status = 'active' AND lastHeartbeat > NOW() - INTERVAL '1 minute'"
```

### 5. Real-Time Events

```yaml
realtimeEvents:
  - name: InputRequired
    entity: InteractionRequest
    trigger: create
    condition: "status = 'pending'"
```

## Configuration

### appsettings.json

```json
{
  "AgenticOrchestration": {
    "DatabasePath": "C:\\scripts\\_machine\\agent-activity.db",
    "LogsPath": "C:\\scripts\\logs",
    "SignalR": {
      "Enabled": true,
      "HubPath": "/hubs/agentic"
    }
  }
}
```

### Environment Variables

```bash
AgenticOrchestration__DatabasePath=C:\scripts\_machine\agent-activity.db
AgenticOrchestration__LogsPath=C:\scripts\logs
```

## Integration with Claude CLI

### Request User Input

```powershell
# From Claude workflow
$response = . "C:\scripts\tools\Request-UserInput.ps1" `
    -Prompt "Should I create PR?" `
    -Type "choice" `
    -Options @("Yes", "No", "Cancel")
```

### Session Management

```powershell
# Start session (registers with API)
.\agent-session.ps1 -Action start

# Heartbeat (keeps instance active)
.\agent-session.ps1 -Action heartbeat

# End session
.\agent-session.ps1 -Action end -ExitReason "normal"
```

## File Structure

```
Hazina.Demo.AgenticOrchestration/
├── entities.yaml                    # Declarative entity definitions
├── appsettings.json                 # Configuration
├── Program.cs                       # Full example with all features
├── Program.Declarative.cs.example   # Minimal 20-line version
├── Hazina.Demo.AgenticOrchestration.csproj
└── README.md
```

## Dependencies

- **Hazina.AgenticOrchestration** - Core orchestration module
- **Hazina.API.Generic** - Hazina's generic API infrastructure
- **ASP.NET Core 9.0** - Web framework
- **SignalR** - Real-time communication
- **SQLite** - Database storage

## Running in Production

```bash
# Build
dotnet publish -c Release -o ./publish

# Run
cd publish
./Hazina.Demo.AgenticOrchestration
```

### Docker

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY publish/ .
EXPOSE 5000
ENTRYPOINT ["dotnet", "Hazina.Demo.AgenticOrchestration.dll"]
```

## Next Steps

1. **Add Authentication** - Secure endpoints with JWT
2. **Build Frontend** - React dashboard for instance management
3. **Add Task Queue** - Submit tasks via web interface
4. **Integrate with ClickUp** - Pull tasks from project management

---

**Part of the Hazina Framework**
https://github.com/martiendejong/Hazina
