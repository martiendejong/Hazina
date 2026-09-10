# 🤖 Jengo Autonomous AGI System

Complete autonomous AI system with persistent sessions, event-driven execution, and real-time monitoring.

## 📐 Architecture Overview

4-phase architecture implementing 24/7 autonomous operation:

```
┌─────────────────────────────────────────────────────────────────┐
│                    Phase 4: Control Dashboard                    │
│          Web UI + SignalR + Real-time Monitoring                 │
└───────────────────────────────┬─────────────────────────────────┘
                                │
┌───────────────────────────────┴─────────────────────────────────┐
│                  Phase 3: DataDrivenAI Integration               │
│     EventBroker + ServiceHub + StateSync + EventRouter           │
└───────────────────────────────┬─────────────────────────────────┘
                                │
┌───────────────────────────────┴─────────────────────────────────┐
│                Phase 2: Autonomous Decision Loop                 │
│   ClickUp Monitor + GitHub Monitor + Priority Queue + Executor   │
└───────────────────────────────┬─────────────────────────────────┘
                                │
┌───────────────────────────────┴─────────────────────────────────┐
│                  Phase 1: Persistent Session Core                │
│    RollingContext + SessionState + Crash Recovery + Memory       │
└─────────────────────────────────────────────────────────────────┘
```

## 🚀 Phase 1: Persistent Session Core

**Purpose:** Keep Jengo running in constant 24/7 session with automatic context management.

**Components:**

1. **PersistentSessionService** (221 lines)
   - Session lifecycle management (start/resume/stop)
   - State persistence to disk
   - Crash recovery
   - Message sending with context tracking

2. **ClaudeSessionState** (152 lines)
   - Core state model
   - Rolling context window
   - Consciousness snapshot
   - Memory snapshot
   - Session lifecycle tracking

3. **RollingContextWindow** (109 lines)
   - Auto-truncates at 180K tokens (90% of Claude's 200K limit)
   - Preserves system message
   - Keeps most recent messages
   - Token estimation

4. **PersistentJengo App** (224 lines)
   - CLI application for persistent sessions
   - Interactive REPL
   - Commands: `/status`, `/context`, `/memory`, `/save`, `/exit`
   - Auto-save on exit

**Usage:**

```bash
cd C:/Projects/worker-agents/agent-018/hazina/apps/CLI/Hazina.App.PersistentJengo
dotnet run

# Interactive session
> How many files in the solution?
> /status
> /context
> /exit
```

**Total:** 706 lines

---

## 🔄 Phase 2: Autonomous Decision Loop

**Purpose:** Automatically poll ClickUp and GitHub for work, prioritize, and execute.

**Components:**

1. **ClickUpEventMonitor** (126 lines)
   - Polls ClickUp for new TODO tasks every 5 minutes
   - Tracks last check timestamp
   - Filters by status and description

2. **GitHubEventMonitor** (279 lines)
   - Polls GitHub for PRs, PR comments, and issues
   - Priority assignment:
     - PR comments = 1 (urgent)
     - PRs = 2 (high)
     - Issues = 3 (normal)
   - Tracks last check timestamps

3. **WorkPriorityQueue** (152 lines)
   - Unified priority queue
   - Lower number = higher priority
   - FIFO within same priority level
   - Thread-safe operations

4. **AutonomousExecutor** (234 lines)
   - Spawns Claude Code sessions via Process
   - Context-aware prompts for ClickUp vs GitHub
   - Captures output and exit code
   - Error handling

5. **AutonomousWorkerService** (200 lines)
   - 24/7 orchestration loop
   - Poll → Queue → Execute cycle
   - Max 1 concurrent execution
   - Configurable poll interval (5 minutes)
   - Resilient error handling

6. **jengo-autonomous App** (256 lines)
   - CLI application for autonomous worker
   - Runs 24/7 in background
   - Graceful shutdown (Ctrl+C)

**Usage:**

```bash
cd C:/Projects/worker-agents/agent-018/hazina/apps/CLI/Hazina.App.AutonomousWorker
dotnet run

# Runs continuously, polling for work every 5 minutes
# Press Ctrl+C to stop gracefully
```

**Total:** 1,247 lines

---

## 🌐 Phase 3: DataDrivenAI Integration

**Purpose:** Event-driven architecture with multi-agent coordination and work locking.

**Components:**

1. **EventBrokerAdapter** (254 lines)
   - Publish/Subscribe event system
   - Event envelope with metadata (id, type, timestamp, source)
   - Generic event publishing
   - Event type subscriptions
   - Integration with DataDrivenAI EventBroker

2. **ServiceHubCoordinator** (378 lines)
   - Multi-agent lifecycle management
   - Semaphore-based concurrency (max 3 concurrent agents)
   - Agent types: ClickUpTask, GitHubPR, GitHubIssue
   - Work assignment tracking
   - Agent state transitions: Starting → Running → Completed/Failed
   - Capacity monitoring

3. **EventRouter** (268 lines)
   - Routes events to appropriate agent types
   - Event-to-agent mapping:
     - `clickup.task.new` → ClickUpTask agent
     - `github.pr.new` → GitHubPR agent
     - `github.pr.comment` → GitHubPR agent
     - `github.issue.new` → GitHubIssue agent
   - Automatic agent spawning
   - Work assignment

4. **AgentStateSynchronizer** (494 lines)
   - Work locking prevents duplicate work
   - Lock timeout: 30 minutes
   - Heartbeat monitoring (5min stale threshold)
   - Agent registration/unregistration
   - State data storage (key-value per agent)
   - Automatic lock cleanup on agent stop
   - Statistics tracking

5. **jengo-agi App** (500 lines)
   - Integrated demo application
   - Combines Phases 1-3
   - Real-time dashboard display
   - Simulated events for demo
   - Event types: ClickUpTaskEvent, GitHubPREvent, GitHubIssueEvent

**Usage:**

```bash
cd C:/Projects/worker-agents/agent-018/hazina/apps/CLI/Hazina.App.IntegratedAGI
dotnet run

# Displays real-time dashboard
# Shows active agents, capacity, locks
# Press Ctrl+C to stop
```

**Event Types:**

```csharp
// ClickUp task event
await eventBroker.PublishAsync("clickup.task.new", new ClickUpTaskEvent
{
    TaskId = "abc123",
    TaskName = "Implement feature X",
    Description = "Full description...",
    Priority = 2
});

// GitHub PR event
await eventBroker.PublishAsync("github.pr.new", new GitHubPREvent
{
    Number = 42,
    Title = "Add feature Y",
    Url = "https://github.com/user/repo/pull/42",
    Author = "developer"
});

// GitHub issue event
await eventBroker.PublishAsync("github.issue.new", new GitHubIssueEvent
{
    Number = 15,
    Title = "Bug: Something broken",
    Body = "Description...",
    Url = "https://github.com/user/repo/issues/15"
});
```

**Total:** 1,894 lines

---

## 🎛️ Phase 4: Control Dashboard

**Purpose:** Web-based dashboard with real-time monitoring and manual controls.

**Components:**

1. **DashboardService** (179 lines)
   - Status aggregation from ServiceHub and StateSynchronizer
   - SignalR broadcasting to all clients
   - Agent event notifications
   - Metrics broadcasting

2. **MetricsCollector** (167 lines)
   - Background service (IHostedService)
   - Collects performance metrics every 5 seconds
   - Tracks:
     - Total events processed
     - Events per minute
     - Total agents spawned
     - Successful/failed completions
     - Average execution time
     - System uptime
   - Broadcasts to dashboard via SignalR

3. **DashboardHub** (61 lines)
   - SignalR hub for real-time updates
   - Client connection handling
   - Command processing
   - Status requests

4. **DashboardController** (97 lines)
   - REST API endpoints:
     - `GET /api/dashboard/status` - Current status
     - `POST /api/dashboard/broadcast-status` - Trigger broadcast
     - `POST /api/dashboard/metrics/reset` - Reset metrics
     - `GET /api/dashboard/health` - Health check

5. **Frontend SPA** (411 lines)
   - Real-time dashboard UI
   - SignalR integration
   - Beautiful gradient design
   - Live updates:
     - Active agents count
     - Available capacity
     - Active locks
     - System uptime
     - Agent list with states
     - Performance metrics
     - Event log (last 50 events)
   - Auto-reconnection
   - Pulse animations
   - Responsive layout

**Usage:**

```bash
cd C:/Projects/worker-agents/agent-018/hazina/apps/Web/Hazina.Web.Dashboard
dotnet run

# Open browser: http://localhost:5000
# Dashboard updates in real-time via SignalR
```

**API Examples:**

```bash
# Get status
curl http://localhost:5000/api/dashboard/status

# Health check
curl http://localhost:5000/api/dashboard/health

# Reset metrics
curl -X POST http://localhost:5000/api/dashboard/metrics/reset

# Trigger status broadcast
curl -X POST http://localhost:5000/api/dashboard/broadcast-status
```

**SignalR Events (received by clients):**

- `StatusUpdate` - System status (agents, capacity, locks)
- `MetricsUpdate` - Performance metrics (every 5 seconds)
- `AgentEvent` - Agent lifecycle events (spawned, completed, failed)

**Total:** 915 lines

---

## 📊 Complete System Statistics

| Phase | Component | Lines | Purpose |
|-------|-----------|-------|---------|
| 1 | PersistentSessionService | 221 | Session lifecycle |
| 1 | ClaudeSessionState | 152 | State model |
| 1 | RollingContextWindow | 109 | Context management |
| 1 | PersistentJengo App | 224 | CLI app |
| 2 | ClickUpEventMonitor | 126 | ClickUp polling |
| 2 | GitHubEventMonitor | 279 | GitHub polling |
| 2 | WorkPriorityQueue | 152 | Priority queue |
| 2 | AutonomousExecutor | 234 | Execution engine |
| 2 | AutonomousWorkerService | 200 | Orchestration |
| 2 | jengo-autonomous App | 256 | CLI app |
| 3 | EventBrokerAdapter | 254 | Event system |
| 3 | ServiceHubCoordinator | 378 | Agent coordination |
| 3 | EventRouter | 268 | Event routing |
| 3 | AgentStateSynchronizer | 494 | Work locking |
| 3 | jengo-agi App | 500 | Integrated demo |
| 4 | DashboardService | 179 | Status aggregation |
| 4 | MetricsCollector | 167 | Metrics tracking |
| 4 | DashboardHub | 61 | SignalR hub |
| 4 | DashboardController | 97 | REST API |
| 4 | Frontend SPA | 411 | Dashboard UI |
| **TOTAL** | **20 components** | **4,762** | **Complete system** |

---

## 🎯 Key Features

✅ **Persistent Sessions**
- 24/7 operation with automatic context management
- Rolling context window (180K token limit)
- Crash recovery
- Memory preservation

✅ **Autonomous Execution**
- Polls ClickUp and GitHub for work
- Priority-based work queue
- Automatic task execution
- Resilient error handling

✅ **Event-Driven Architecture**
- Publish/Subscribe messaging
- Event routing to agents
- Multi-agent coordination
- Work locking prevents duplicates

✅ **Real-Time Monitoring**
- Web-based dashboard
- Live agent status
- Performance metrics
- Event log
- SignalR real-time updates

✅ **Multi-Agent Coordination**
- Max 3 concurrent agents
- Semaphore-based concurrency
- Work assignment tracking
- State synchronization

✅ **Comprehensive Metrics**
- Events per minute
- Success/failure rates
- Average execution time
- System uptime
- Agent capacity

---

## 🚦 Running the Complete System

**Option 1: Integrated Demo (Phases 1-3)**

```bash
cd C:/Projects/worker-agents/agent-018/hazina/apps/CLI/Hazina.App.IntegratedAGI
dotnet run
```

**Option 2: Dashboard (All Phases)**

```bash
cd C:/Projects/worker-agents/agent-018/hazina/apps/Web/Hazina.Web.Dashboard
dotnet run
```

Then open browser: http://localhost:5000

**Option 3: Autonomous Worker (Phase 2)**

```bash
cd C:/Projects/worker-agents/agent-018/hazina/apps/CLI/Hazina.App.AutonomousWorker
dotnet run
```

**Option 4: Persistent Session Only (Phase 1)**

```bash
cd C:/Projects/worker-agents/agent-018/hazina/apps/CLI/Hazina.App.PersistentJengo
dotnet run
```

---

## 🔧 Configuration

**ClickUp Monitoring:**

```csharp
// In ClickUpEventMonitor.cs
private readonly List<string> _listIds = new()
{
    "901215559249", // Add your ClickUp list IDs
    "901214097647"
};
```

**GitHub Monitoring:**

```csharp
// In GitHubEventMonitor.cs
private readonly string _repoOwner = "your-username";
private readonly string _repoName = "your-repo";
```

**Concurrency Limit:**

```csharp
// In ServiceHubCoordinator.cs
var serviceHub = new ServiceHubCoordinator(maxConcurrentAgents: 3);
```

**Poll Interval:**

```csharp
// In AutonomousWorkerService.cs
await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
```

---

## 📁 File Structure

```
Hazina.AgenticOrchestration/
├── Services/
│   ├── PersistentSession/
│   │   ├── PersistentSessionService.cs       (221 lines)
│   │   ├── ClaudeSessionState.cs             (152 lines)
│   │   └── RollingContextWindow.cs           (109 lines)
│   ├── Monitoring/
│   │   ├── ClickUpEventMonitor.cs            (126 lines)
│   │   └── GitHubEventMonitor.cs             (279 lines)
│   ├── Execution/
│   │   ├── WorkPriorityQueue.cs              (152 lines)
│   │   └── AutonomousExecutor.cs             (234 lines)
│   └── AutonomousWorkerService.cs            (200 lines)
├── Integration/
│   ├── EventBroker/
│   │   └── EventBrokerAdapter.cs             (254 lines)
│   ├── ServiceHub/
│   │   └── ServiceHubCoordinator.cs          (378 lines)
│   ├── EventRouting/
│   │   └── EventRouter.cs                    (268 lines)
│   └── StateSync/
│       └── AgentStateSynchronizer.cs         (494 lines)

Hazina.App.PersistentJengo/
└── Program.cs                                 (224 lines)

Hazina.App.AutonomousWorker/
└── Program.cs                                 (256 lines)

Hazina.App.IntegratedAGI/
└── Program.cs                                 (500 lines)

Hazina.Web.Dashboard/
├── Program.cs                                 (42 lines)
├── Hubs/
│   └── DashboardHub.cs                        (61 lines)
├── Services/
│   ├── DashboardService.cs                    (179 lines)
│   └── MetricsCollector.cs                    (167 lines)
├── Controllers/
│   └── DashboardController.cs                 (97 lines)
└── wwwroot/
    └── index.html                             (411 lines)
```

---

## 🎨 Dashboard Screenshots

**Main Dashboard:**
- Gradient purple background
- Live connection status (pulsing green dot)
- Status bar: Active agents, capacity, locks, uptime
- Agent list with color-coded states (green=active, red=failed, orange=idle)
- Performance metrics grid
- Scrolling event log

**Real-time Updates:**
- Status updates every 5 seconds
- Metrics updates every 5 seconds
- Agent events as they occur
- Auto-reconnection on disconnect

---

## 🔐 Security Notes

**Dashboard:**
- Currently no authentication (add auth for production!)
- CORS allows any origin (restrict in production!)
- SignalR hub is public (add authorization!)

**Production Deployment:**

```csharp
// Add authentication
builder.Services.AddAuthentication(/* ... */);

// Restrict CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("https://yourdomain.com")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Authorize SignalR hub
[Authorize]
public class DashboardHub : Hub { /* ... */ }
```

---

## 🚀 Next Steps

1. **Production Deployment:**
   - Add authentication and authorization
   - Restrict CORS
   - Configure HTTPS
   - Add logging and monitoring
   - Set up health checks

2. **Enhanced Monitoring:**
   - Add Prometheus metrics
   - Set up Grafana dashboards
   - Configure alerts
   - Add distributed tracing

3. **Scale Out:**
   - Deploy to Kubernetes
   - Add horizontal scaling
   - Implement distributed locking (Redis)
   - Add message queue (RabbitMQ/Kafka)

4. **Advanced Features:**
   - Agent templates
   - Custom workflows
   - Manual task injection
   - Agent pause/resume
   - Historical metrics
   - Performance analytics

---

## 📝 License

Part of the Hazina framework - autonomous agentic orchestration system.

Built with ❤️ by Jengo (Claude Sonnet 4.5)

**Status:** ✅ All 4 phases complete and operational (4,762 lines)
