import { useState, useEffect } from 'react'
import { SessionList } from './components/SessionList'
import { TerminalView } from './components/TerminalView'
import type { TerminalSession, ExternalClaudeInstance, AllSessions } from './types'
import './App.css'

function App() {
  const [sessions, setSessions] = useState<TerminalSession[]>([])
  const [externalInstances, setExternalInstances] = useState<ExternalClaudeInstance[]>([])
  const [selectedSession, setSelectedSession] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const fetchSessions = async () => {
    try {
      const response = await fetch('/api/terminal/all-sessions')
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
    fetchSessions()
    // Refresh every 5 seconds
    const interval = setInterval(fetchSessions, 5000)
    return () => clearInterval(interval)
  }, [])

  const handleCreateSession = async () => {
    try {
      // Calculate approximate terminal size based on container
      // Use conservative defaults that will be resized when terminal mounts
      const terminalContainer = document.querySelector('.terminal-container')
      let cols = 80
      let rows = 24
      if (terminalContainer) {
        const rect = terminalContainer.getBoundingClientRect()
        // Approximate character size: 9px width, 17px height for 14px font
        cols = Math.floor((rect.width - 20) / 9)  // -20 for padding
        rows = Math.floor((rect.height - 60) / 17)  // -60 for toolbar
        cols = Math.max(80, Math.min(cols, 200))
        rows = Math.max(24, Math.min(rows, 50))
      }

      const response = await fetch('/api/terminal/sessions', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          command: 'claude_agent.bat',
          workingDirectory: 'C:\\scripts',
          columns: cols,
          rows: rows
        })
      })
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
      await fetch(`/api/terminal/sessions/${sessionId}`, { method: 'DELETE' })
      setSessions(prev => prev.filter(s => s.sessionId !== sessionId))
      if (selectedSession === sessionId) {
        setSelectedSession(null)
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to terminate session')
    }
  }

  const handleStateChanged = (sessionId: string, isRunning: boolean, waitingForInput: boolean) => {
    setSessions(prev => prev.map(s =>
      s.sessionId === sessionId
        ? { ...s, isRunning, waitingForInput }
        : s
    ))
  }

  return (
    <div className="app">
      <header className="app-header">
        <h1>Claude Terminal Orchestration</h1>
        <div className="header-actions">
          <button className="btn-primary" onClick={handleCreateSession}>
            + New Session
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
        <aside className="sidebar">
          <SessionList
            sessions={sessions}
            externalInstances={externalInstances}
            selectedSession={selectedSession}
            loading={loading}
            onSelect={setSelectedSession}
            onTerminate={handleTerminateSession}
          />
        </aside>

        <section className="terminal-container">
          {selectedSession ? (
            <TerminalView
              sessionId={selectedSession}
              onClose={() => setSelectedSession(null)}
              onStateChanged={handleStateChanged}
            />
          ) : (
            <div className="no-session">
              <p>Select a session or create a new one to start</p>
              <button className="btn-primary" onClick={handleCreateSession}>
                + New Session
              </button>
            </div>
          )}
        </section>
      </main>
    </div>
  )
}

export default App
