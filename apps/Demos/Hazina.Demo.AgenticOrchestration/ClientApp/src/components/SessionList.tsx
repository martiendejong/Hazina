import type { TerminalSession, ExternalClaudeInstance } from '../types'

interface SessionListProps {
  sessions: TerminalSession[]
  externalInstances: ExternalClaudeInstance[]
  selectedSession: string | null
  loading: boolean
  onSelect: (sessionId: string) => void
  onTerminate: (sessionId: string) => void
}

export function SessionList({
  sessions,
  externalInstances,
  selectedSession,
  loading,
  onSelect,
  onTerminate
}: SessionListProps) {
  if (loading) {
    return (
      <div className="session-list">
        <h2>Sessions</h2>
        <div className="loading">Loading sessions...</div>
      </div>
    )
  }

  const formatTime = (dateStr: string) => {
    const date = new Date(dateStr)
    return date.toLocaleTimeString()
  }

  const formatRelativeTime = (dateStr: string) => {
    const date = new Date(dateStr)
    const now = new Date()
    const diffMs = now.getTime() - date.getTime()
    const diffSec = Math.floor(diffMs / 1000)
    if (diffSec < 60) return `${diffSec}s ago`
    const diffMin = Math.floor(diffSec / 60)
    if (diffMin < 60) return `${diffMin}m ago`
    const diffHour = Math.floor(diffMin / 60)
    return `${diffHour}h ago`
  }

  const totalCount = sessions.length + externalInstances.length

  if (totalCount === 0) {
    return (
      <div className="session-list">
        <h2>Sessions</h2>
        <div className="empty">No active sessions</div>
      </div>
    )
  }

  return (
    <div className="session-list">
      <h2>Sessions ({totalCount})</h2>

      {/* Terminal Sessions (managed by this tool) */}
      {sessions.length > 0 && (
        <>
          <h3 className="section-header">
            <span className="icon">🖥️</span> Terminal Sessions
          </h3>
          <ul>
            {sessions.map(session => (
              <li
                key={session.sessionId}
                className={`session-item ${selectedSession === session.sessionId ? 'selected' : ''} ${session.isRunning ? (session.waitingForInput ? 'waiting' : 'running') : 'stopped'}`}
                onClick={() => onSelect(session.sessionId)}
              >
                <div className="session-info">
                  <span className="session-command" title={session.title ? `Command: ${session.command}` : undefined}>
                    {session.title || session.command}
                  </span>
                  <span className="session-id" title={session.sessionId}>{session.sessionId}</span>
                </div>
                <div className="session-meta">
                  <span className={`status ${session.isRunning ? (session.waitingForInput ? 'waiting' : 'running') : 'stopped'}`}>
                    {session.isRunning
                      ? (session.waitingForInput ? 'Question' : 'Running')
                      : `Exited (${session.exitCode})`}
                  </span>
                  <span className="start-time">{formatTime(session.startedAt)}</span>
                </div>
                <button
                  className="btn-terminate"
                  onClick={(e) => {
                    e.stopPropagation()
                    onTerminate(session.sessionId)
                  }}
                  title="Terminate session"
                >
                  ×
                </button>
              </li>
            ))}
          </ul>
        </>
      )}

      {/* External Claude Instances (detected from database) */}
      {externalInstances.length > 0 && (
        <>
          <h3 className="section-header">
            <span className="icon">🤖</span> External Claude Instances
          </h3>
          <ul>
            {externalInstances.map(instance => (
              <li
                key={instance.agentId}
                className="session-item external running"
                title={`Agent: ${instance.agentId}\nTask: ${instance.currentTask || 'Unknown'}\nWorktree: ${instance.worktreeSeat || 'None'}`}
              >
                <div className="session-info">
                  <span className="session-command external-badge">
                    {instance.worktreeSeat || instance.agentId.slice(0, 16)}
                  </span>
                  <span className="session-id" title={instance.agentId}>{instance.agentId}</span>
                </div>
                <div className="session-meta">
                  <span className="status external">External</span>
                  <span className="start-time" title={`Last heartbeat: ${formatTime(instance.lastHeartbeat)}`}>
                    ❤️ {formatRelativeTime(instance.lastHeartbeat)}
                  </span>
                </div>
                {instance.currentTask && (
                  <div className="external-task" title={instance.currentTask}>
                    📋 {instance.currentTask.length > 30
                      ? instance.currentTask.slice(0, 30) + '...'
                      : instance.currentTask}
                  </div>
                )}
              </li>
            ))}
          </ul>
        </>
      )}
    </div>
  )
}
