import { useState, useEffect } from 'react'
import { SessionList } from './components/SessionList'
import { TerminalView } from './components/TerminalView'
import { ChatView } from './components/ChatView'
import { ArchiveView } from './components/ArchiveView'
import { Login } from './components/Login'
import { SplitPane } from './components/SplitPane'
import { CommandPalette, useCommandPalette } from './components/CommandPalette'
import { authFetch, hasCredentials, clearCredentials } from './auth'
import type { TerminalSession, ExternalClaudeInstance, AllSessions, TerminalConfig, PendingRestore } from './types'
import './App.css'

type ViewMode = 'sessions' | 'chat' | 'archive'

function App() {
  const isMobile = /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent)
  const [isAuthenticated, setIsAuthenticated] = useState(hasCredentials())
  const [sessions, setSessions] = useState<TerminalSession[]>([])
  const [externalInstances, setExternalInstances] = useState<ExternalClaudeInstance[]>([])
  const [openSessions, setOpenSessions] = useState<string[]>([])
  const [activeSessionId, setActiveSessionId] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [config, setConfig] = useState<TerminalConfig | null>(null)
  const [viewMode, setViewMode] = useState<ViewMode>('sessions')
  const [pendingRestore, setPendingRestore] = useState<PendingRestore | null>(null)
  const [commandPaletteOpen, setCommandPaletteOpen] = useState(false)
  const [version, setVersion] = useState<string | null>(null)

  // Command palette keyboard shortcut (Cmd+K / Ctrl+K)
  useCommandPalette(() => setCommandPaletteOpen(prev => !prev))

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

    // Fetch config and version once on mount
    fetchConfig()
    authFetch('/api/terminal/version')
      .then(r => r.ok ? r.json() : null)
      .then(data => { if (data?.version) setVersion(data.version) })
      .catch(() => {})
    // Fetch sessions initially - no auto-refresh
    fetchSessions()

    // Optional: refresh every 30 seconds only if needed
    // const interval = setInterval(fetchSessions, 30000)
    // return () => clearInterval(interval)
  }, [isAuthenticated])

  const handleSelectSession = (sessionId: string) => {
    // Open session in a new tab if not already open
    if (!openSessions.includes(sessionId)) {
      setOpenSessions(prev => [...prev, sessionId])
    }
    setActiveSessionId(sessionId)
  }

  const handleCloseTab = (sessionId: string) => {
    // Close tab (remove from open sessions) but don't terminate the session
    setOpenSessions(prev => prev.filter(id => id !== sessionId))
    if (activeSessionId === sessionId) {
      // Switch to another open session or null
      const remaining = openSessions.filter(id => id !== sessionId)
      setActiveSessionId(remaining.length > 0 ? remaining[0] : null)
    }
  }

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
      // Open new session in tab and make it active
      setOpenSessions(prev => [...prev, newSession.sessionId])
      setActiveSessionId(newSession.sessionId)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create session')
    }
  }

  const handleTerminateSession = async (sessionId: string) => {
    try {
      // Optimistically remove from UI immediately
      setSessions(prev => prev.filter(s => s.sessionId !== sessionId))
      setOpenSessions(prev => prev.filter(id => id !== sessionId))
      if (activeSessionId === sessionId) {
        const remaining = openSessions.filter(id => id !== sessionId)
        setActiveSessionId(remaining.length > 0 ? remaining[0] : null)
      }

      // Then call the API
      const response = await authFetch(`/api/terminal/sessions/${sessionId}`, { method: 'DELETE' })
      if (response.status === 401) {
        handleUnauthorized()
        return
      }
      if (!response.ok) {
        // If DELETE fails, re-fetch to restore correct state
        fetchSessions()
        throw new Error('Failed to terminate session')
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to terminate session')
      // Re-fetch sessions to restore correct state after error
      fetchSessions()
    }
  }

  const handleLogout = () => {
    clearCredentials()
    setIsAuthenticated(false)
    setSessions([])
    setExternalInstances([])
    setOpenSessions([])
    setActiveSessionId(null)
  }

  const handleStateChanged = (sessionId: string, isRunning: boolean, waitingForInput: boolean) => {
    setSessions(prev => prev.map(s =>
      s.sessionId === sessionId
        ? { ...s, isRunning, waitingForInput }
        : s
    ))
  }

  const handleTitleChanged = (sessionId: string, title: string) => {
    // Strip ANSI escape sequences (ESC[...X patterns) from title
    const cleanTitle = title
      .replace(/\x1b\[[0-9;]*[a-zA-Z]/g, '')  // CSI sequences like ESC[111C, ESC[K, ESC[0m
      .replace(/\x1b\][^\x07\x1b]*(?:\x07|\x1b\\)/g, '')  // OSC sequences
      .replace(/\x1b[()][0-2]/g, '')  // Character set sequences
      .trim()
    setSessions(prev => prev.map(s =>
      s.sessionId === sessionId
        ? { ...s, title: cleanTitle }
        : s
    ))
  }

  // Handle restoring a session from archive
  const handleRestoreSession = async (archivedSessionId: string) => {
    try {
      // Use backend restore endpoint - it creates the session and returns archived content
      const response = await authFetch(`/api/terminal/archive/${archivedSessionId}/restore`, {
        method: 'POST'
      })

      if (response.status === 401) {
        handleUnauthorized()
        return
      }
      if (!response.ok) {
        const error = await response.json()
        throw new Error(error.error || 'Failed to restore session')
      }

      const result = await response.json()
      console.log('[DEBUG] Restore response received:', {
        hasArchivedContent: !!result.archivedContent,
        contentLength: result.archivedContent?.length,
        sessionId: result.sessionId
      })

      // Add to sessions list
      setSessions(prev => [...prev, {
        sessionId: result.sessionId,
        command: result.command,
        title: result.title,
        startedAt: result.startedAt,
        isRunning: result.isRunning,
        waitingForInput: result.waitingForInput
      }])

      // Store the archived content to be displayed in terminal
      if (result.archivedContent) {
        console.log('[DEBUG] Setting pendingRestore with content length:', result.archivedContent.length)
        setPendingRestore({
          sessionId: result.sessionId,
          content: result.archivedContent
        })
      } else {
        console.log('[DEBUG] No archivedContent in response!')
      }

      // Switch to sessions view and open session in tab
      setViewMode('sessions')
      setOpenSessions(prev => [...prev, result.sessionId])
      setActiveSessionId(result.sessionId)
    } catch (err) {
      console.error('[DEBUG] Restore failed:', err)
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
        <h1>Claude Terminal Orchestration{version && <span className="version-badge">v{version}</span>}</h1>
        <div className="header-actions">
          <button
            className={`btn-nav ${viewMode === 'sessions' ? 'active' : ''}`}
            onClick={() => setViewMode('sessions')}
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
            {isMobile ? (
              // Mobile: full-screen session list OR full-screen terminal
              activeSessionId ? (
                <TerminalView
                  sessionId={activeSessionId}
                  onClose={() => handleCloseTab(activeSessionId)}
                  onStateChanged={handleStateChanged}
                  onTitleChanged={handleTitleChanged}
                  pendingRestore={pendingRestore?.sessionId === activeSessionId ? pendingRestore : undefined}
                  onRestoreComplete={handleRestoreComplete}
                />
              ) : (
                <SessionList
                  sessions={sessions}
                  externalInstances={externalInstances}
                  selectedSession={activeSessionId}
                  loading={loading}
                  onSelect={handleSelectSession}
                  onTerminate={handleTerminateSession}
                />
              )
            ) : (
              // Desktop: split pane layout
              <SplitPane
                left={
                  <SessionList
                    sessions={sessions}
                    externalInstances={externalInstances}
                    selectedSession={activeSessionId}
                    loading={loading}
                    onSelect={handleSelectSession}
                    onTerminate={handleTerminateSession}
                  />
                }
                right={
                  activeSessionId ? (
                    <TerminalView
                      sessionId={activeSessionId}
                      onClose={() => handleCloseTab(activeSessionId)}
                      onStateChanged={handleStateChanged}
                      onTitleChanged={handleTitleChanged}
                      pendingRestore={pendingRestore?.sessionId === activeSessionId ? pendingRestore : undefined}
                      onRestoreComplete={handleRestoreComplete}
                    />
                  ) : (
                    <div className="no-session">
                      <p>Select a session or create a new one</p>
                      <p style={{ fontSize: '0.875rem', color: '#8b949e', marginTop: '0.5rem' }}>
                        Press Cmd+K or Ctrl+K to open command palette
                      </p>
                    </div>
                  )
                }
                defaultSize={320}
                minSize={250}
                maxSize={500}
                storageKey="terminal-orchestrator-split"
              />
            )}

            <CommandPalette
              sessions={sessions}
              onSelectSession={handleSelectSession}
              onCreateSession={handleCreateSession}
              onNavigate={setViewMode}
              isOpen={commandPaletteOpen}
              onClose={() => setCommandPaletteOpen(false)}
            />
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
