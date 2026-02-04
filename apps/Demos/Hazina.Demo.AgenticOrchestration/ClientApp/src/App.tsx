import { useState, useEffect } from 'react'
import { SessionList } from './components/SessionList'
import { TerminalView } from './components/TerminalView'
import { ChatView } from './components/ChatView'
import { ArchiveView } from './components/ArchiveView'
import { Login } from './components/Login'
import { authFetch, hasCredentials, clearCredentials } from './auth'
import type { TerminalSession, ExternalClaudeInstance, AllSessions, TerminalConfig, PendingRestore } from './types'
import './App.css'

type ViewMode = 'sessions' | 'chat' | 'archive'

function App() {
  const [isAuthenticated, setIsAuthenticated] = useState(hasCredentials())
  const [sessions, setSessions] = useState<TerminalSession[]>([])
  const [externalInstances, setExternalInstances] = useState<ExternalClaudeInstance[]>([])
  const [selectedSession, setSelectedSession] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [config, setConfig] = useState<TerminalConfig | null>(null)
  const [viewMode, setViewMode] = useState<ViewMode>('sessions')
  const [pendingRestore, setPendingRestore] = useState<PendingRestore | null>(null)

  const handleUnauthorized = () => {
    clearCredentials()
    setIsAuthenticated(false)
  }

  const fetchConfig = async () => {
    try {
      const response = await authFetch('/api/terminal/config')
      if (response.status === 401) {
        handleUnauthorized()
        return
      }
      if (!response.ok) throw new Error('Failed to fetch config')
      const data: TerminalConfig = await response.json()
      setConfig(data)
    } catch (err) {
      console.error('Failed to load terminal config:', err)
      // Use defaults if config fails to load
      setConfig({
        defaultCommand: 'claude',
        defaultWorkingDirectory: null,
        defaultArguments: [],
        defaultColumns: 120,
        defaultRows: 30,
        maxConcurrentSessions: 10,
        sessionTimeoutMinutes: 60,
        signalRHubUrl: '/hubs/terminal'
      })
    }
  }

  const fetchSessions = async () => {
    try {
      const response = await authFetch('/api/terminal/all-sessions')
      if (response.status === 401) {
        handleUnauthorized()
        return
      }
      if (!response.ok) throw new Error('Failed to fetch sessions')
      const data: AllSessions = await response.json()
      setSessions(data.terminalSessions)
      setExternalInstances(data.externalInstances)
      setError(null)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load sessions')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    if (!isAuthenticated) return

    // Fetch config once on mount
    fetchConfig()
    // Fetch sessions initially and refresh every 5 seconds
    fetchSessions()
    const interval = setInterval(fetchSessions, 5000)
    return () => clearInterval(interval)
  }, [isAuthenticated])

  const handleCreateSession = async () => {
    try {
      // Calculate approximate terminal size based on container
      // Use conservative defaults that will be resized when terminal mounts
      const terminalContainer = document.querySelector('.terminal-section')
      let cols = config?.defaultColumns ?? 80
      let rows = config?.defaultRows ?? 24
      if (terminalContainer) {
        const rect = terminalContainer.getBoundingClientRect()
        // Approximate character size: 9px width, 17px height for 14px font
        cols = Math.floor((rect.width - 20) / 9)  // -20 for padding
        rows = Math.floor((rect.height - 60) / 17)  // -60 for toolbar
        cols = Math.max(80, Math.min(cols, 200))
        rows = Math.max(24, Math.min(rows, 50))
      }

      // Use configured defaults - command and workingDirectory come from backend config
      // Only pass columns/rows from our calculation; backend will use its defaults for command/workingDirectory
      const response = await authFetch('/api/terminal/sessions', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          columns: cols,
          rows: rows
          // command and workingDirectory omitted - backend uses appsettings defaults
        })
      })
      if (response.status === 401) {
        handleUnauthorized()
        return
      }
      if (!response.ok) throw new Error('Failed to create session')
      const newSession = await response.json()
      setSessions(prev => [...prev, newSession])
      setSelectedSession(newSession.sessionId)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create session')
    }
  }

  const handleTerminateSession = async (sessionId: string) => {
    try {
      const response = await authFetch(`/api/terminal/sessions/${sessionId}`, { method: 'DELETE' })
      if (response.status === 401) {
        handleUnauthorized()
        return
      }
      setSessions(prev => prev.filter(s => s.sessionId !== sessionId))
      if (selectedSession === sessionId) {
        setSelectedSession(null)
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to terminate session')
    }
  }

  const handleLogout = () => {
    clearCredentials()
    setIsAuthenticated(false)
    setSessions([])
    setExternalInstances([])
    setSelectedSession(null)
  }

  const handleStateChanged = (sessionId: string, isRunning: boolean, waitingForInput: boolean) => {
    setSessions(prev => prev.map(s =>
      s.sessionId === sessionId
        ? { ...s, isRunning, waitingForInput }
        : s
    ))
  }

  const handleTitleChanged = (sessionId: string, title: string) => {
    setSessions(prev => prev.map(s =>
      s.sessionId === sessionId
        ? { ...s, title }
        : s
    ))
  }

  // Handle restoring a session from archive
  const handleRestoreSession = async (archivedSessionId: string, content: string) => {
    try {
      // Calculate terminal size
      const terminalContainer = document.querySelector('.terminal-section')
      let cols = config?.defaultColumns ?? 80
      let rows = config?.defaultRows ?? 24
      if (terminalContainer) {
        const rect = terminalContainer.getBoundingClientRect()
        cols = Math.floor((rect.width - 20) / 9)
        rows = Math.floor((rect.height - 60) / 17)
        cols = Math.max(80, Math.min(cols, 200))
        rows = Math.max(24, Math.min(rows, 50))
      }

      // Create new session
      const response = await authFetch('/api/terminal/sessions', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ columns: cols, rows: rows })
      })
      if (response.status === 401) {
        handleUnauthorized()
        return
      }
      if (!response.ok) throw new Error('Failed to create session')

      const newSession = await response.json()
      setSessions(prev => [...prev, newSession])

      // Store pending restore content - will be pasted after Claude loads
      setPendingRestore({ sessionId: newSession.sessionId, content })

      // Switch to sessions view and select the new session
      setViewMode('sessions')
      setSelectedSession(newSession.sessionId)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to restore session')
    }
  }

  // Clear pending restore after it's been handled
  const handleRestoreComplete = () => {
    setPendingRestore(null)
  }

  // Show login screen if not authenticated
  if (!isAuthenticated) {
    return <Login onLogin={() => setIsAuthenticated(true)} />
  }

  return (
    <div className="app">
      <header className="app-header">
        <h1>Claude Terminal Orchestration</h1>
        <div className="header-actions">
          <button
            className={`btn-nav ${viewMode === 'sessions' ? 'active' : ''}`}
            onClick={() => { setViewMode('sessions'); setSelectedSession(null) }}
          >
            Sessions
          </button>
          <button
            className={`btn-nav ${viewMode === 'chat' ? 'active' : ''}`}
            onClick={() => setViewMode('chat')}
          >
            Chat
          </button>
          <button
            className={`btn-nav ${viewMode === 'archive' ? 'active' : ''}`}
            onClick={() => setViewMode('archive')}
          >
            Archive
          </button>
          {viewMode === 'sessions' && (
            <button className="btn-primary" onClick={handleCreateSession}>
              + New Session
            </button>
          )}
          <button className="btn-logout" onClick={handleLogout}>
            Logout
          </button>
        </div>
      </header>

      {error && (
        <div className="error-banner">
          {error}
          <button onClick={() => setError(null)}>Dismiss</button>
        </div>
      )}

      <main className="app-main">
        {viewMode === 'sessions' && (
          <>
            <aside className={`sidebar ${selectedSession ? 'session-active' : ''}`}>
              <SessionList
                sessions={sessions}
                externalInstances={externalInstances}
                selectedSession={selectedSession}
                loading={loading}
                onSelect={setSelectedSession}
                onTerminate={handleTerminateSession}
              />
            </aside>

            <section className={`terminal-section ${selectedSession ? 'has-session' : ''}`}>
              {selectedSession ? (
                <TerminalView
                  sessionId={selectedSession}
                  onClose={() => setSelectedSession(null)}
                  onStateChanged={handleStateChanged}
                  onTitleChanged={handleTitleChanged}
                  pendingRestore={pendingRestore?.sessionId === selectedSession ? pendingRestore : undefined}
                  onRestoreComplete={handleRestoreComplete}
                />
              ) : (
                <div className="no-session">
                  <p>Select a session or create a new one</p>
                </div>
              )}
            </section>
          </>
        )}

        {viewMode === 'chat' && (
          <ChatView onClose={() => setViewMode('sessions')} />
        )}

        {viewMode === 'archive' && (
          <ArchiveView
            onClose={() => setViewMode('sessions')}
            onRestoreSession={handleRestoreSession}
          />
        )}
      </main>
    </div>
  )
}

export default App
