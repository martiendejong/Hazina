# HazinaCoder Architecture

## High-Level Design

```
┌──────────────────────────────────────────┐
│          CLI Layer                       │
│  (Program.cs + Command Handlers)         │
└──────────────────┬───────────────────────┘
                   │
┌──────────────────▼───────────────────────┐
│     Orchestration Layer                  │
│  • ProviderRouter                        │
│  • StreamingOrchestrator                 │
│  • EventBus                              │
└──────────────────┬───────────────────────┘
                   │
         ┌─────────┼─────────┐
         │         │         │
┌────────▼───┐ ┌──▼──────┐ ┌▼────────┐
│  Providers │ │Learning │ │Multi    │
│  • OpenAI  │ │ System  │ │Agent    │
│  • Claude  │ │         │ │Coord    │
│  • Ollama  │ │         │ │         │
└────────────┘ └─────────┘ └─────────┘
```

## Core Principles

### 1. Event-Driven Architecture
- All components communicate via `AgentEventBus`
- Loose coupling enables extensibility
- Real-time streaming via event channels

### 2. Feature Flags
- Every feature can be toggled
- Gradual rollout support
- A/B testing capability

### 3. Multi-Provider Strategy
- Provider abstraction via `ILLMProvider`
- Automatic failover
- Cost-aware routing

### 4. Dependency Injection
- Constructor injection throughout
- Testable components
- Configuration-driven wiring

## Key Components

### Provider Router
Selects best provider based on:
- Availability
- Cost
- Capability requirements
- Historical performance

### Streaming Orchestrator
Manages real-time output:
- Token-by-token streaming
- Progress reporting
- Interrupt handling

### Learning System
Continuous improvement:
- Pattern recognition
- Mistake prevention
- Success amplification

### Multi-Agent Coordination
- CAS-based resource allocation
- Heartbeat monitoring
- Conflict detection

## Data Flow

```
User Input
   ↓
Command Parser
   ↓
Provider Router → Select Provider
   ↓
LLM Client → API Call
   ↓
Streaming Response
   ↓
Event Bus → Publish tokens
   ↓
CLI Output → Display to user
```

## Extension Points

1. **Custom Providers:** Implement `ILLMProvider`
2. **Tools:** Register via `DynamicToolRegistry`
3. **Skills:** YAML-based auto-discovery
4. **Event Handlers:** Subscribe to `AgentEventBus`

## Performance Considerations

- Connection pooling for providers
- Smart context caching
- Incremental embedding
- Parallel tool execution

## Security

- Secret scanning (pre-commit)
- API key encryption (DPAPI/Keychain)
- Sandboxed file operations
- Audit logging

## Monitoring

- Health checks (providers, memory, disk)
- Metrics export (Prometheus)
- Cost tracking with budgets
- Performance profiling
