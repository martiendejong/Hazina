import type { TerminalSession } from '../types'

interface SessionListProps {
  sessions: TerminalSession[]
  selectedSession: string | null
  loading: boolean
  onSelect: (sessionId: string) => void
  onTerminate: (sessionId: string) => void
}

export function SessionList({
  sessions,
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

  if (sessions.length === 0) {
    return (
      <div className="session-list">
        <h2>Sessions</h2>
        <div className="empty">No active sessions</div>
      </div>
    )
  }

  const formatTime = (dateStr: string) => {
    const date = new Date(dateStr)
    return date.toLocaleTimeString()
  }

  return (
    <div className="session-list">
      <h2>Sessions ({sessions.length})</h2>
      <ul>
        {sessions.map(session => (
          <li
            key={session.sessionId}
            className={`session-item ${selectedSession === session.sessionId ? 'selected' : ''} ${session.isRunning ? 'running' : 'stopped'}`}
            onClick={() => onSelect(session.sessionId)}
          >
            <div className="session-info">
              <span className="session-command">{session.command}</span>
              <span className="session-id">{session.sessionId.slice(0, 8)}...</span>
            </div>
            <div className="session-meta">
              <span className={`status ${session.isRunning ? 'running' : 'stopped'}`}>
                {session.isRunning ? 'Running' : `Exited (${session.exitCode})`}
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
    </div>
  )
}
