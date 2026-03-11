import { useEffect } from 'react'
import type { TerminalSession } from '../types'
import './SessionTabs.css'

interface SessionTabsProps {
  sessions: TerminalSession[]
  activeSessionId: string | null
  onSelect: (sessionId: string) => void
  onClose: (sessionId: string) => void
}

export function SessionTabs({ sessions, activeSessionId, onSelect, onClose }: SessionTabsProps) {
  // Keyboard shortcuts
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      // Ctrl+Tab / Cmd+Tab: Next tab
      if ((e.ctrlKey || e.metaKey) && e.key === 'Tab' && !e.shiftKey) {
        e.preventDefault()
        if (sessions.length === 0) return

        const currentIndex = sessions.findIndex(s => s.sessionId === activeSessionId)
        const nextIndex = (currentIndex + 1) % sessions.length
        onSelect(sessions[nextIndex].sessionId)
      }

      // Ctrl+Shift+Tab / Cmd+Shift+Tab: Previous tab
      if ((e.ctrlKey || e.metaKey) && e.key === 'Tab' && e.shiftKey) {
        e.preventDefault()
        if (sessions.length === 0) return

        const currentIndex = sessions.findIndex(s => s.sessionId === activeSessionId)
        const prevIndex = currentIndex <= 0 ? sessions.length - 1 : currentIndex - 1
        onSelect(sessions[prevIndex].sessionId)
      }

      // Ctrl+W / Cmd+W: Close active tab
      if ((e.ctrlKey || e.metaKey) && e.key === 'w') {
        e.preventDefault()
        if (activeSessionId) {
          onClose(activeSessionId)
        }
      }

      // Ctrl+1-9: Switch to tab by number
      if ((e.ctrlKey || e.metaKey) && e.key >= '1' && e.key <= '9') {
        e.preventDefault()
        const index = parseInt(e.key, 10) - 1
        if (index < sessions.length) {
          onSelect(sessions[index].sessionId)
        }
      }
    }

    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [sessions, activeSessionId, onSelect, onClose])

  if (sessions.length === 0) {
    return (
      <div className="session-tabs-empty">
        <span>No sessions open. Create a new session to get started.</span>
      </div>
    )
  }

  return (
    <div className="session-tabs">
      <div className="tab-list">
        {sessions.map((session, index) => {
          const isActive = session.sessionId === activeSessionId
          const title = session.title || session.command || 'Terminal'
          const shortTitle = title.length > 20 ? title.slice(0, 20) + '…' : title

          return (
            <div
              key={session.sessionId}
              className={`tab-item ${isActive ? 'active' : ''}`}
              onClick={() => onSelect(session.sessionId)}
              title={`${title} (${session.sessionId.slice(0, 8)}…)\n\nShortcut: Ctrl+${index + 1}`}
            >
              <div className="tab-status">
                {session.isRunning ? (
                  session.waitingForInput ? '⏳' : '●'
                ) : '○'}
              </div>

              <span className="tab-title">{shortTitle}</span>

              <button
                className="tab-close"
                onClick={(e) => {
                  e.stopPropagation()
                  onClose(session.sessionId)
                }}
                title="Close tab (Ctrl+W)"
              >
                ×
              </button>
            </div>
          )
        })}
      </div>

      <div className="tab-shortcuts-hint">
        Ctrl+Tab: Next | Ctrl+W: Close | Ctrl+1-9: Jump to tab
      </div>
    </div>
  )
}
