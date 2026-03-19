# Hazina Local Agent Platform - Frontend (Milestone 1.1)

React frontend for the Local Agent Platform MVP. Schema-driven UI with real-time event streaming.

## Features

- **Intent Input**: Natural language interface for submitting user intents
- **Event Timeline**: Real-time event visualization from the event store
- **Schema-Driven UI**: Dynamic component rendering based on YAML schema definitions
- **SignalR Integration**: Live event streaming from backend
- **File Explorer**: Structural indexer visualization
- **Task Management**: View active tasks and approve/deny actions

## Tech Stack

- **React 19** with TypeScript
- **Vite** for build tooling
- **Zustand** for state management
- **SignalR** for real-time communication
- **Tailwind CSS** for styling
- **Axios** for HTTP requests

## Project Structure

```
src/
├── components/          # React components
│   ├── IntentInput.tsx
│   ├── EventTimeline.tsx
│   ├── TaskCard.tsx
│   └── FileTree.tsx
├── services/           # API and SignalR services
│   ├── agentApi.ts
│   ├── signalRService.ts
│   └── schemaService.ts
├── store/              # Zustand state management
│   └── agentStore.ts
├── types/              # TypeScript definitions
│   ├── events.ts
│   ├── schema.ts
│   └── api.ts
├── hooks/              # Custom React hooks
│   └── useSignalR.ts
└── utils/              # Utility functions
```

## Getting Started

### Prerequisites

- Node.js 18+ and npm
- Backend running on http://localhost:5000 (Hazina.Demo.AgenticOrchestration)

### Installation

```bash
npm install
```

### Development

```bash
npm run dev
```

Opens on http://localhost:5173

### Build

```bash
npm run build
```

## Configuration

The frontend connects to the backend at `http://localhost:5000` by default. To change this:

1. Update `src/services/agentApi.ts` - change `baseURL`
2. Update `src/services/signalRService.ts` - change `baseURL` in connect()

## Schema Components

The UI renders components based on YAML schema definitions located in `public/schemas/`:

- `ui.task-card.component.yaml` - Task breakdown visualization
- `ui.file-tree.component.yaml` - File explorer
- `ui.approval-panel.component.yaml` - Action approval UI
- `ui.progress-tracker.component.yaml` - Task progress visualization
- `ui.command-preview.component.yaml` - Command execution preview

These schemas are loaded dynamically from the backend's schema registry.

## API Endpoints Used

- `POST /api/agent/intent` - Submit user intent
- `GET /api/agent/events` - Fetch events from event store
- `GET /api/agent/tasks` - Get active tasks
- `POST /api/agent/approve` - Approve/deny actions
- `GET /api/agent/files` - Get file tree from structural indexer
- `WS /hubs/terminal` - SignalR hub for real-time events

## State Management

The app uses Zustand for centralized state management:

- **Events**: All events from the event store
- **Tasks**: Active tasks mapped by ID
- **Pending Approvals**: Actions awaiting user approval
- **Connection Status**: SignalR connection state
- **UI State**: Selected events, active views

## Development Notes

### Adding New Components

1. Create component in `src/components/`
2. Define TypeScript types in `src/types/schema.ts`
3. Add YAML schema definition in `public/schemas/`
4. Register in schema service

### Event Handling

Events flow: Backend → SignalR → useSignalR hook → Zustand store → UI components

### Testing Intent Submission

Try these example intents:
- "list files in current directory"
- "show me Python files modified today"
- "help"

The StubIntentParser on the backend recognizes basic patterns.

## Next Steps (Future Milestones)

- [ ] LLamaSharp integration for local LLM (replace StubIntentParser)
- [ ] Full task execution engine
- [ ] Approval flow implementation
- [ ] File operation capabilities
- [ ] Progressive Web App (PWA) support
- [ ] Desktop app wrapper (Electron/Tauri)

## License

Part of the Hazina Framework - see main project LICENSE
