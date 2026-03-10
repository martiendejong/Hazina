# Overstory Automation Implementation Plan

**Goal:** Port Overstory's battle-tested multi-agent orchestration to Hazina Orchestration

**Source:** Overstory (tmux-based) → Hazina (ConPTY-based)
**Timeline:** Complete implementation in one session

---

## Phase 1: Core Automation (Beacon & Injection)

### 1.1 Beacon Prompt System
**File:** `src/Hazina.AgenticOrchestration/Services/BeaconService.cs`
- `BuildBeacon()` - Structured startup prompt
- Format: `[HAZINA] {agentName} ({capability}) {timestamp} task:{taskId}`
- Includes startup protocol (read CLAUDE.md, prime, check mail, begin task)

### 1.2 ConPTY Stdin Injection
**File:** `src/Hazina.AgenticOrchestration/Terminal/ConPty/ConPtyTerminalSession.cs`
- `SendBeacon()` - Inject prompt with double-Enter workaround
- `SendNudge()` - Inject message into running session
- Fixed 3-second delay for Claude initialization
- 500ms delay between text and Enter

### 1.3 Terminal Session Manager Enhancement
**File:** `src/Hazina.AgenticOrchestration/Terminal/TerminalSessionManager.cs`
- `CreateAgentSession()` - Spawn with automatic beacon
- Agent session lifecycle tracking

---

## Phase 2: Mail System (SQLite-based)

### 2.1 Database Schema
**File:** `src/Hazina.AgenticOrchestration/Data/MailStore.cs`
- Messages table (id, from, to, subject, body, type, priority, payload, read, created_at, thread_id)
- Support for message types: status, question, result, error, worker_done, merge_ready, escalation
- Support for priorities: low, normal, high, urgent

### 2.2 Mail Service
**File:** `src/Hazina.AgenticOrchestration/Services/MailService.cs`
- `Send()` - Send message with auto-nudge for urgent/high
- `Check()` - Get unread messages for agent
- `CheckInject()` - Format messages for hook injection
- `MarkRead()` - Mark message as read
- `Reply()` - Reply to message in thread

### 2.3 Pending Nudge Markers
**Directory:** `data/pending-nudges/{agentName}.json`
- Written on urgent/high priority messages
- Cleared by mail check hook
- Prevents I/O corruption during tool execution

---

## Phase 3: Hook System

### 3.1 Hook Configuration Generator
**File:** `src/Hazina.AgenticOrchestration/Services/HookConfigService.cs`
- `GenerateHooksConfig()` - Create .claude/settings.local.json
- SessionStart hook: Load agent identity + prime
- UserPromptSubmit hook: Check mail with --inject flag
- Debounce support (5000ms default)

### 3.2 Hook Installer
**File:** `src/Hazina.AgenticOrchestration/Services/HookInstaller.cs`
- `InstallHooks()` - Write hooks to worktree
- `UninstallHooks()` - Remove hooks
- Merge with existing hooks (don't overwrite)

---

## Phase 4: Agent Identity System

### 4.1 Agent CV (Curriculum Vitae)
**File:** `src/Hazina.AgenticOrchestration/Models/AgentIdentity.cs`
- Name, capability, created timestamp
- Sessions completed count
- Expertise domains
- Recent tasks
- Learning history

### 4.2 Identity Manager
**File:** `src/Hazina.AgenticOrchestration/Services/AgentIdentityService.cs`
- `CreateIdentity()` - Initialize new agent
- `LoadIdentity()` - Load existing agent CV
- `UpdateIdentity()` - Record session completion
- Storage: `data/agents/{agentName}.json`

---

## Phase 5: Controllers & API

### 5.1 Agent Spawning Controller
**File:** `src/Hazina.AgenticOrchestration/Controllers/AgentController.cs`
- POST `/api/agents/spawn` - Spawn agent with beacon
- GET `/api/agents` - List active agents
- DELETE `/api/agents/{name}` - Terminate agent
- GET `/api/agents/{name}` - Get agent details

### 5.2 Mail Controller
**File:** `src/Hazina.AgenticOrchestration/Controllers/MailController.cs`
- POST `/api/mail/send` - Send message
- GET `/api/mail/check` - Check inbox
- GET `/api/mail/list` - List messages with filters
- POST `/api/mail/{id}/read` - Mark as read
- POST `/api/mail/{id}/reply` - Reply to message

### 5.3 Hook Management Controller
**File:** `src/Hazina.AgenticOrchestration/Controllers/HooksController.cs`
- POST `/api/hooks/install` - Install hooks to worktree
- DELETE `/api/hooks/uninstall` - Remove hooks
- GET `/api/hooks/status` - Check installation status

---

## Phase 6: Frontend Integration

### 6.1 Agent Spawn UI
**File:** `apps/Demos/Hazina.Demo.AgenticOrchestration/ClientApp/src/components/AgentSpawn.tsx`
- Agent name input
- Capability selector (scout, builder, reviewer, lead, merger)
- Task ID input
- Parent agent selector
- Depth indicator
- File scope input

### 6.2 Mail UI
**File:** `apps/Demos/Hazina.Demo.AgenticOrchestration/ClientApp/src/components/MailView.tsx`
- Inbox list with unread indicators
- Message composer
- Priority selector
- Type selector
- Threaded conversations

### 6.3 Agent Dashboard
**File:** `apps/Demos/Hazina.Demo.AgenticOrchestration/ClientApp/src/components/AgentDashboard.tsx`
- Active agents list
- Status indicators (booting, working, waiting, completed)
- Mail notification badges
- Session metrics

---

## Phase 7: Configuration & Options

### 7.1 Configuration Model
**File:** `src/Hazina.AgenticOrchestration/Models/AgentOrchestrationOptions.cs`
- Mail database path
- Agent identities path
- Pending nudges path
- Max concurrent agents
- Default beacon delay (3000ms)
- Default hook debounce (5000ms)

### 7.2 Service Registration Extension
**File:** `src/Hazina.AgenticOrchestration/Extensions/ServiceCollectionExtensions.cs`
- Update `AddHazinaAgenticOrchestration()` with new services
- Register mail service
- Register beacon service
- Register hook services
- Register identity service

---

## Implementation Order

1. ✅ **Models first** (AgentIdentity, MailMessage, BeaconOptions)
2. ✅ **Database** (MailStore schema + migrations)
3. ✅ **Core services** (BeaconService, MailService, AgentIdentityService)
4. ✅ **ConPTY enhancement** (SendBeacon, SendNudge with delays)
5. ✅ **Hook system** (HookConfigService, HookInstaller)
6. ✅ **Controllers** (AgentController, MailController, HooksController)
7. ✅ **Frontend** (UI components for spawning + mail)
8. ✅ **Testing** (Integration tests for beacon + mail flow)

---

## Success Criteria

✅ Agent can be spawned with automatic beacon injection
✅ Beacon appears in Claude session within 3.5 seconds
✅ Mail messages can be sent between agents
✅ Urgent messages trigger pending nudge markers
✅ UserPromptSubmit hook delivers messages non-intrusively
✅ Agent identity persists across sessions
✅ Full UI for spawning agents + managing mail
✅ All endpoints documented in Swagger

---

## Testing Plan

### Unit Tests
- BeaconService: Format validation
- MailService: Send/check/reply logic
- HookConfigService: JSON generation
- AgentIdentityService: CRUD operations

### Integration Tests
- Spawn agent → beacon injected → Claude responds
- Send urgent mail → pending nudge created → hook delivers
- SessionStart hook → identity loaded
- UserPromptSubmit hook → mail checked

### Manual Testing
- Spawn 2 agents in parallel
- Send mail from agent A to agent B
- Verify agent B receives message on next prompt
- Check agent identity persists after restart

---

## File Structure

```
src/Hazina.AgenticOrchestration/
├── Controllers/
│   ├── AgentController.cs          (NEW)
│   ├── MailController.cs           (NEW)
│   └── HooksController.cs          (NEW)
├── Data/
│   └── MailStore.cs                (NEW)
├── Models/
│   ├── AgentIdentity.cs            (NEW)
│   ├── MailMessage.cs              (NEW)
│   ├── BeaconOptions.cs            (NEW)
│   └── AgentOrchestrationOptions.cs (UPDATE)
├── Services/
│   ├── BeaconService.cs            (NEW)
│   ├── MailService.cs              (NEW)
│   ├── AgentIdentityService.cs     (NEW)
│   ├── HookConfigService.cs        (NEW)
│   └── HookInstaller.cs            (NEW)
├── Terminal/
│   └── ConPty/
│       └── ConPtyTerminalSession.cs (UPDATE - add SendBeacon)
└── Extensions/
    └── ServiceCollectionExtensions.cs (UPDATE)

apps/Demos/Hazina.Demo.AgenticOrchestration/
└── ClientApp/
    └── src/
        └── components/
            ├── AgentSpawn.tsx      (NEW)
            ├── MailView.tsx        (NEW)
            └── AgentDashboard.tsx  (NEW)
```

---

**Ready to implement!** Starting with Phase 1 (Beacon System).
