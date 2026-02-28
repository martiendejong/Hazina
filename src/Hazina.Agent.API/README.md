# Hazina.Agent.API

Distributed autonomous agent API with streaming responses and session logging.

## Features

- **Streaming SSE Endpoint**: Real-time agent execution with Server-Sent Events
- **Session Logging**: Complete session history logged to `E:\data\hazina\sessions\{date}\{sessionId}\`
- **OpenAI Integration**: Direct integration with OpenAI GPT-4o (default model)
- **Multi-Provider Ready**: Architecture designed for easy addition of Claude, Gemini, etc.

## API Endpoints

### POST /api/agent/execute

Execute an agent instruction with streaming response.

**Request:**
```json
{
  "instruction": "Implement user authentication",
  "provider": "openai",
  "model": "gpt-4o",
  "context": {
    "project": "client-manager",
    "workingDirectory": "C:\\Projects\\client-manager",
    "sessionId": null
  },
  "options": {
    "autonomous": false,
    "streamResponse": true,
    "maxTokens": 4000,
    "temperature": 0.7
  }
}
```

**Response (Server-Sent Events):**
```
event: output
data: {"type":"output","data":{"content":"I'll help you implement..."},"timestamp":"2026-02-27T23:30:00Z"}

event: complete
data: {"type":"complete","data":{"sessionId":"20260227-233000-abc123","finalResult":"...","duration":12.5},"timestamp":"2026-02-27T23:30:12Z"}
```

### GET /api/agent/health

Health check endpoint.

## Configuration

### appsettings.json

```json
{
  "OpenAI": {
    "ApiKey": "sk-..."
  }
}
```

## Session Logging

Sessions are logged to: `E:\data\hazina\sessions\{date}\{sessionId}\`

**Files created per session:**
- `request.json` - Original request
- `stream.log` - All streaming events (JSONL)
- `result.md` - Final result as markdown
- `complete.json` - Completion metadata

## Running

```bash
# Development
dotnet run --project src/Hazina.Agent.API

# Production
dotnet run --project src/Hazina.Agent.API --configuration Release
```

API will be available at:
- HTTPS: https://localhost:5001
- HTTP: http://localhost:5000
- Swagger: https://localhost:5001/swagger

## Next Steps (Week 2-5)

- **Week 2**: Distributed consciousness sync (git-based state replication)
- **Week 3**: Autonomous mode (continuous operation without user input)
- **Week 4**: Deploy to 6 machines (desktop, 2 laptops, VPS, Frank's laptop, Diko's machine)
- **Week 5**: Validation + polish

## Architecture

```
AgentController (SSE streaming)
    ├─ AgentExecutionService (core logic)
    │   ├─ OpenAIClient (GPT-4o)
    │   └─ SessionLogger (E:\data\hazina\sessions\)
    └─ Models
        ├─ AgentRequest
        ├─ AgentEvent
        └─ CompleteData
```

## Status

✅ Week 1 Complete:
- Core API implemented
- Streaming endpoint working
- Session logging operational
- OpenAI integration functional

⏳ Week 2-5: Planned

---

**Built:** 2026-02-27
**Agent:** Jengo (agent-006)
**Branch:** feature/distributed-agent-api
