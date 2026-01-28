import { useState, useEffect, useRef } from 'react'
import { authFetch } from '../auth'
import type { ArchivedSession, ArchivedSessionsResponse, SessionLogResponse } from '../types'

interface ArchiveViewProps {
  onClose: () => void
}

export function ArchiveView({ onClose }: ArchiveViewProps) {
  const [sessions, setSessions] = useState<ArchivedSession[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [page, setPage] = useState(0)
  const [hasMore, setHasMore] = useState(false)
  const [totalCount, setTotalCount] = useState(0)
  const [selectedSession, setSelectedSession] = useState<string | null>(null)
  const [logContent, setLogContent] = useState<string | null>(null)
  const [logLoading, setLogLoading] = useState(false)
  const logContainerRef = useRef<HTMLDivElement>(null)

  const pageSize = 50

  const fetchSessions = async (pageNum: number) => {
    setLoading(true)
    setError(null)
    try {
      const response = await authFetch(`/api/terminal/archive?page=${pageNum}&pageSize=${pageSize}`)
      if (!response.ok) {
        throw new Error('Failed to fetch archived sessions')
      }
      const data: ArchivedSessionsResponse = await response.json()
      setSessions(data.sessions)
      setHasMore(data.hasMore)
      setTotalCount(data.totalCount)
      setPage(pageNum)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load sessions')
    } finally {
      setLoading(false)
    }
  }

  const fetchSessionLog = async (sessionId: string) => {
    setLogLoading(true)
    setLogContent(null)
    try {
      const response = await authFetch(`/api/terminal/archive/${sessionId}/log`)
      if (!response.ok) {
        throw new Error('Failed to fetch session log')
      }
      const data: SessionLogResponse = await response.json()
      setLogContent(data.content)
      // Scroll to top of log
      setTimeout(() => {
        logContainerRef.current?.scrollTo(0, 0)
      }, 0)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load log')
    } finally {
      setLogLoading(false)
    }
  }

  useEffect(() => {
    fetchSessions(0)
  }, [])

  const handleSessionClick = (sessionId: string) => {
    setSelectedSession(sessionId)
    fetchSessionLog(sessionId)
  }

  const handleBack = () => {
    setSelectedSession(null)
    setLogContent(null)
  }

  const formatDate = (dateString: string) => {
    const date = new Date(dateString)
    return date.toLocaleString()
  }

  const formatDuration = (startedAt: string, endedAt?: string) => {
    const start = new Date(startedAt).getTime()
    const end = endedAt ? new Date(endedAt).getTime() : Date.now()
    const durationMs = end - start

    const seconds = Math.floor(durationMs / 1000)
    const minutes = Math.floor(seconds / 60)
    const hours = Math.floor(minutes / 60)

    if (hours > 0) {
      return `${hours}h ${minutes % 60}m`
    } else if (minutes > 0) {
      return `${minutes}m ${seconds % 60}s`
    } else {
      return `${seconds}s`
    }
  }

  const formatBytes = (bytes: number) => {
    if (bytes < 1024) return `${bytes} B`
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
  }

  // Render session log view
  if (selectedSession && logContent !== null) {
    const session = sessions.find(s => s.sessionId === selectedSession)
    return (
      <div className="archive-view">
        <div className="archive-header">
          <button className="btn-back" onClick={handleBack}>
            ← Back to list
          </button>
          <div className="archive-title">
            <h2>Session Log</h2>
            {session && (
              <span className="archive-subtitle">
                {session.command} - {formatDate(session.startedAt)}
              </span>
            )}
          </div>
          <button className="btn-close" onClick={onClose}>Close</button>
        </div>

        <div className="log-container" ref={logContainerRef}>
          <pre className="session-log">{logContent}</pre>
        </div>
      </div>
    )
  }

  // Render session list
  return (
    <div className="archive-view">
      <div className="archive-header">
        <div className="archive-title">
          <h2>Session Archive</h2>
          <span className="archive-subtitle">
            {totalCount} total sessions
          </span>
        </div>
        <button className="btn-close" onClick={onClose}>Close</button>
      </div>

      {error && (
        <div className="archive-error">
          {error}
          <button onClick={() => setError(null)}>Dismiss</button>
        </div>
      )}

      <div className="archive-list">
        {loading ? (
          <div className="archive-loading">Loading sessions...</div>
        ) : sessions.length === 0 ? (
          <div className="archive-empty">No archived sessions found</div>
        ) : (
          <>
            <div className="session-table">
              <div className="table-header">
                <span className="col-command">Command</span>
                <span className="col-started">Started</span>
                <span className="col-duration">Duration</span>
                <span className="col-exit">Exit</span>
                <span className="col-size">Size</span>
              </div>
              {sessions.map(session => (
                <div
                  key={session.sessionId}
                  className={`table-row ${logLoading && selectedSession === session.sessionId ? 'loading' : ''}`}
                  onClick={() => handleSessionClick(session.sessionId)}
                >
                  <span className="col-command" title={session.command}>
                    {session.command}
                  </span>
                  <span className="col-started">
                    {formatDate(session.startedAt)}
                  </span>
                  <span className="col-duration">
                    {formatDuration(session.startedAt, session.endedAt)}
                  </span>
                  <span className={`col-exit ${session.exitCode === 0 ? 'success' : session.exitCode ? 'error' : ''}`}>
                    {session.exitCode ?? '-'}
                  </span>
                  <span className="col-size">
                    {formatBytes(session.logSizeBytes)}
                  </span>
                </div>
              ))}
            </div>

            <div className="pagination">
              <button
                className="btn-page"
                onClick={() => fetchSessions(page - 1)}
                disabled={page === 0 || loading}
              >
                ← Previous
              </button>
              <span className="page-info">
                Page {page + 1} of {Math.ceil(totalCount / pageSize)}
              </span>
              <button
                className="btn-page"
                onClick={() => fetchSessions(page + 1)}
                disabled={!hasMore || loading}
              >
                Next →
              </button>
            </div>
          </>
        )}
      </div>
    </div>
  )
}
