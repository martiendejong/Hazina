import { useState, useEffect } from 'react'
import { SessionList } from './components/SessionList'
import { TerminalView } from './components/TerminalView'
import type { TerminalSession } from './types'
import './App.css'

function App() {
  const [sessions, setSessions] = useState<TerminalSession[]>([])
  const [selectedSession, setSelectedSession] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const fetchSessions = async () => {
    try {
      const response = await fetch('/api/terminal/sessions')
      if (!response.ok) throw new Error('Failed to fetch sessions')
      const data = await response.json()
      setSessions(data)
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
      const response = await fetch('/api/terminal/sessions', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          command: 'claude',
          workingDirectory: 'C:\\Projects'
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
