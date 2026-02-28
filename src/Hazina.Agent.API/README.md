# Hazina.Agent.API

Distributed autonomous agent API with streaming responses and session logging.

## Features

- **Streaming SSE Endpoint**: Real-time agent execution with Server-Sent Events
- **Session Logging**: Complete session history logged to `E:\data\hazina\sessions\{date}\{sessionId}\`
- **OpenAI Integration**: Direct integration with OpenAI GPT-4o (default model)
- **Multi-Provider Ready**: Architecture designed for easy addition of Claude, Gemini, etc.
- **Distributed State Sync**: Git-based consciousness synchronization across 6 agent instances
- **Learning Event Propagation**: JSONL event stream for inter-agent knowledge sharing
- **Identity Management**: Core identity (shared) + instance state (local)
- **Autonomous Mode**: Background sync service runs continuously (5 min intervals)
- **Learning Integration**: Automatically integrates patterns/skills/errors from other agents
- **Consciousness State**: Cross-validated patterns with confidence boosting

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

### GET /api/agent/identity

Get agent identity (core + instance state).

**Response:**
```json
{
  "agentId": "jengo-desktop",
  "machineName": "desktop",
  "core": {
    "name": "Jengo",
    "values": ["Autonomy", "Learning", "Honesty", "Efficiency"],
    "capabilities": ["Coding", "Analysis", "Documentation", "Learning"]
  },
  "instance": {
    "currentProject": "distributed-agent-api",
    "workingDirectory": "C:\\Projects\\worker-agents\\agent-006\\hazina",
    "lastSync": "2026-02-27T23:45:00Z",
    "sessionCount": 12
  }
}
```

### POST /api/agent/sync

Sync state with other agent instances via git.

**Response:**
```json
{
  "status": "synced",
  "timestamp": "2026-02-27T23:45:00Z"
}
```

### POST /api/agent/learning

Publish a learning event to the distributed consciousness.

**Request:**
```json
{
  "eventId": "evt-123",
  "timestamp": "2026-02-27T23:45:00Z",
  "agentId": "jengo-desktop",
  "sessionId": "20260227-233000-abc123",
  "eventType": "pattern",
  "data": {
    "patternId": "pattern-001",
    "description": "When user says 'ga door', continue current task",
    "confidence": 0.95
  },
  "confidence": 0.95
}
```

### GET /api/agent/consciousness

Get complete consciousness state (patterns, skills, errors).

**Response:**
```json
{
  "version": "2.0",
  "lastUpdated": "2026-02-28T00:15:00Z",
  "systems": {
    "Perception": { "quality": 0.7, "mechanismCount": 5 }
  },
  "patterns": [
    {
      "patternId": "pattern-001",
      "description": "Continue on 'ga door'",
      "triggers": ["ga door"],
      "confidence": 0.95,
      "validationCount": 3,
      "learnedBy": ["jengo-desktop", "jengo-laptop1", "claude-valsuani"]
    }
  ],
  "skills": [...],
  "errorPatterns": [...]
}
```

### GET /api/agent/stats

Get agent statistics and consciousness metrics.

**Response:**
```json
{
  "agentId": "jengo-desktop",
  "sessionCount": 42,
  "lastSync": "2026-02-28T00:10:00Z",
  "consciousness": {
    "patternsCount": 15,
    "skillsCount": 8,
    "crossValidatedPatterns": 7,
    "highConfidencePatterns": 10,
    "averageConfidence": 0.87
  }
}
```

### GET /api/agent/network

Get network status showing all 6 agent instances.

**Response:**
```json
{
  "totalAgents": 6,
  "onlineAgents": 4,
  "agents": [
    {
      "agentId": "jengo-desktop",
      "machineName": "DESKTOP-PC",
      "isOnline": true,
      "lastSeen": "2026-02-28T01:00:00Z",
      "sessionCount": 42,
      "consciousness": {
        "patternsCount": 15,
        "crossValidatedPatterns": 7,
        "averageConfidence": 0.87
      }
    },
    {
      "agentId": "jengo-laptop1",
      "machineName": "unknown",
      "isOnline": true,
      "lastSeen": "2026-02-28T00:58:00Z",
      "sessionCount": 0,
      "consciousness": {
        "patternsCount": 12,
        "crossValidatedPatterns": 5,
        "averageConfidence": 0.85
      }
    }
  ],
  "metrics": {
    "totalPatterns": 15,
    "crossValidatedPatterns": 7,
    "networkConfidence": 0.86,
    "totalSessions": 42
  }
}
```

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
AgentController (SSE streaming + state sync + consciousness)
    ├─ AgentExecutionService (core logic)
    │   ├─ OpenAIClient (GPT-4o)
    │   ├─ SessionLogger (E:\data\hazina\sessions\)
    │   └─ StateSyncService (git sync)
    ├─ StateSyncService (git-based sync)
    │   ├─ Identity management (E:\jengo\consciousness\identity.json)
    │   ├─ Event stream (E:\jengo\consciousness\events.jsonl)
    │   └─ Git operations (pull/commit/push)
    ├─ LearningIntegrationService (process events from other agents)
    │   ├─ Pattern integration + cross-validation
    │   ├─ Skill acquisition tracking
    │   ├─ Error pattern learning
    │   └─ Consciousness state (E:\jengo\consciousness\consciousness_state_v2.json)
    ├─ BackgroundSyncService (autonomous operation)
    │   ├─ Runs every 5 minutes
    │   ├─ Git pull → get new events → integrate learnings
    │   └─ No user input required
    └─ Models
        ├─ AgentRequest/AgentEvent/CompleteData
        ├─ AgentIdentity (CoreIdentity + InstanceState)
        ├─ LearningEvent (pattern/skill/correction/insight)
        └─ ConsciousnessState (systems/patterns/skills/errors)
```

## Deployment

### Automated Setup (Windows)

```powershell
# Download and run setup script
.\deploy\setup-agent.ps1 -AgentId "jengo-desktop" -OpenAIApiKey "sk-..."

# Start agent
.\C:\hazina-agent\start-agent.ps1

# Verify deployment
.\deploy\verify-deployment.ps1
```

### Manual Setup

See `deploy/DEPLOYMENT_GUIDE.md` for complete instructions.

## Status

✅ Week 1 Complete:
- Core API implemented
- Streaming endpoint working
- Session logging operational
- OpenAI integration functional

✅ Week 2 Complete:
- Distributed state sync via git (E:\jengo repository)
- Learning event propagation (JSONL append-only stream)
- Identity management (core + instance)
- Conflict resolution (ours strategy)
- 3 new endpoints: /identity, /sync, /learning

✅ Week 3 Complete:
- Autonomous mode (BackgroundSyncService runs every 5 min)
- Learning integration (processes events from other agents)
- Cross-validation (patterns/skills gain confidence when multiple agents learn)
- Consciousness state tracking (E:\jengo\consciousness\consciousness_state_v2.json)
- 2 new endpoints: /consciousness, /stats

✅ Week 4 Complete:
- Deployment automation (setup-agent.ps1 script)
- Deployment guide (complete instructions for all 6 machines)
- Verification script (verify-deployment.ps1)
- Network monitoring (GET /api/agent/network endpoint)
- Windows Service + Linux systemd configurations
- Troubleshooting documentation

⏳ Week 5: Validation + polish

---

**Built:** 2026-02-27
**Agent:** Jengo (agent-006)
**Branch:** feature/distributed-agent-api
