import { useState, useEffect } from 'react'
import { authFetch } from '../auth'
import '../styles/ChatAdminDashboard.css'

interface ChatMetrics {
  sessionId: string
  messageCount: number
  totalTokens: number
  totalToolCalls: number
  averageDurationMs: number
  lastMessageAt: string
}

interface ChatServiceHealth {
  isHealthy: boolean
  activeConversations: number
  totalStorageMB: number
  circuitBreakerState: string
  totalMessages: number
  totalTokens: number
  totalToolCalls: number
}

interface ChatStatistics {
  overview: {
    activeConversations: number
    savedConversations: number
    totalMessages: number
    totalTokens: number
    totalToolCalls: number
    totalStorageMB: number
    circuitBreakerState: string
  }
  topSessions: Array<{
    sessionId: string
    messages: number
    tokens: number
    tools: number
    avgDuration: number
    lastMessage: string
  }>
  recentActivity: Array<{
    sessionId: string
    lastMessage: string
    messages: number
  }>
  tokenUsage: {
    total: number
    average: number
    max: number
  }
  toolUsage: {
    total: number
    average: number
    sessionsWithTools: number
  }
  performance: {
    averageDurationMs: number
    maxDurationMs: number
    minDurationMs: number
  }
}

export function ChatAdminDashboard() {
  const [health, setHealth] = useState<ChatServiceHealth | null>(null)
  const [stats, setStats] = useState<ChatStatistics | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [refreshInterval, setRefreshInterval] = useState(5000)

  const fetchData = async () => {
    try {
      setError(null)

      const [healthRes, statsRes] = await Promise.all([
        authFetch('/api/chat/admin/health'),
        authFetch('/api/chat/admin/stats')
      ])

      if (!healthRes.ok || !statsRes.ok) {
        throw new Error('Failed to fetch dashboard data')
      }

      const healthData = await healthRes.json()
      const statsData = await statsRes.json()

      setHealth(healthData)
      setStats(statsData)
      setLoading(false)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load dashboard')
      setLoading(false)
    }
  }

  useEffect(() => {
    fetchData()

    // Auto-refresh
    const interval = setInterval(fetchData, refreshInterval)
    return () => clearInterval(interval)
  }, [refreshInterval])

  if (loading) {
    return (
      <div className="chat-admin-dashboard">
        <div className="loading">Loading dashboard...</div>
      </div>
    )
  }

  if (error) {
    return (
      <div className="chat-admin-dashboard">
        <div className="error">{error}</div>
        <button onClick={fetchData}>Retry</button>
      </div>
    )
  }

  if (!health || !stats) {
    return <div className="chat-admin-dashboard">No data available</div>
  }

  return (
    <div className="chat-admin-dashboard">
      <div className="dashboard-header">
        <h1>Chat Service Dashboard</h1>
        <div className="header-actions">
          <select
            value={refreshInterval}
            onChange={(e) => setRefreshInterval(Number(e.target.value))}
          >
            <option value={5000}>Refresh every 5s</option>
            <option value={10000}>Refresh every 10s</option>
            <option value={30000}>Refresh every 30s</option>
            <option value={60000}>Refresh every 60s</option>
          </select>
          <button onClick={fetchData}>Refresh Now</button>
        </div>
      </div>

      {/* Health Status */}
      <div className="dashboard-section">
        <h2>Service Health</h2>
        <div className="health-cards">
          <div className={`health-card ${health.isHealthy ? 'healthy' : 'unhealthy'}`}>
            <div className="health-icon">
              {health.isHealthy ? '✅' : '❌'}
            </div>
            <div className="health-label">Status</div>
            <div className="health-value">{health.isHealthy ? 'Healthy' : 'Unhealthy'}</div>
          </div>
          <div className="health-card">
            <div className="health-icon">🔌</div>
            <div className="health-label">Circuit Breaker</div>
            <div className="health-value">{stats.overview.circuitBreakerState}</div>
          </div>
          <div className="health-card">
            <div className="health-icon">💬</div>
            <div className="health-label">Active Conversations</div>
            <div className="health-value">{stats.overview.activeConversations}</div>
          </div>
          <div className="health-card">
            <div className="health-icon">💾</div>
            <div className="health-label">Storage</div>
            <div className="health-value">{stats.overview.totalStorageMB.toFixed(2)} MB</div>
          </div>
        </div>
      </div>

      {/* Overview Stats */}
      <div className="dashboard-section">
        <h2>Overview</h2>
        <div className="stats-grid">
          <div className="stat-card">
            <div className="stat-label">Total Messages</div>
            <div className="stat-value">{stats.overview.totalMessages.toLocaleString()}</div>
          </div>
          <div className="stat-card">
            <div className="stat-label">Total Tokens</div>
            <div className="stat-value">{stats.overview.totalTokens.toLocaleString()}</div>
          </div>
          <div className="stat-card">
            <div className="stat-label">Total Tool Calls</div>
            <div className="stat-value">{stats.overview.totalToolCalls.toLocaleString()}</div>
          </div>
          <div className="stat-card">
            <div className="stat-label">Saved Conversations</div>
            <div className="stat-value">{stats.overview.savedConversations}</div>
          </div>
        </div>
      </div>

      {/* Token Usage */}
      <div className="dashboard-section">
        <h2>Token Usage</h2>
        <div className="stats-grid">
          <div className="stat-card">
            <div className="stat-label">Total</div>
            <div className="stat-value">{stats.tokenUsage.total.toLocaleString()}</div>
          </div>
          <div className="stat-card">
            <div className="stat-label">Average per Session</div>
            <div className="stat-value">{Math.round(stats.tokenUsage.average).toLocaleString()}</div>
          </div>
          <div className="stat-card">
            <div className="stat-label">Max per Session</div>
            <div className="stat-value">{stats.tokenUsage.max.toLocaleString()}</div>
          </div>
        </div>
      </div>

      {/* Tool Usage */}
      <div className="dashboard-section">
        <h2>Tool Usage</h2>
        <div className="stats-grid">
          <div className="stat-card">
            <div className="stat-label">Total Tool Calls</div>
            <div className="stat-value">{stats.toolUsage.total.toLocaleString()}</div>
          </div>
          <div className="stat-card">
            <div className="stat-label">Average per Session</div>
            <div className="stat-value">{Math.round(stats.toolUsage.average)}</div>
          </div>
          <div className="stat-card">
            <div className="stat-label">Sessions with Tools</div>
            <div className="stat-value">{stats.toolUsage.sessionsWithTools}</div>
          </div>
        </div>
      </div>

      {/* Performance */}
      <div className="dashboard-section">
        <h2>Performance</h2>
        <div className="stats-grid">
          <div className="stat-card">
            <div className="stat-label">Average Duration</div>
            <div className="stat-value">{Math.round(stats.performance.averageDurationMs)} ms</div>
          </div>
          <div className="stat-card">
            <div className="stat-label">Max Duration</div>
            <div className="stat-value">{Math.round(stats.performance.maxDurationMs)} ms</div>
          </div>
          <div className="stat-card">
            <div className="stat-label">Min Duration</div>
            <div className="stat-value">{Math.round(stats.performance.minDurationMs)} ms</div>
          </div>
        </div>
      </div>

      {/* Top Sessions */}
      <div className="dashboard-section">
        <h2>Top Sessions by Message Count</h2>
        <div className="table-container">
          <table className="sessions-table">
            <thead>
              <tr>
                <th>Session ID</th>
                <th>Messages</th>
                <th>Tokens</th>
                <th>Tools</th>
                <th>Avg Duration</th>
                <th>Last Message</th>
              </tr>
            </thead>
            <tbody>
              {stats.topSessions.map((session) => (
                <tr key={session.sessionId}>
                  <td className="session-id">{session.sessionId}</td>
                  <td>{session.messages}</td>
                  <td>{session.tokens.toLocaleString()}</td>
                  <td>{session.tools}</td>
                  <td>{Math.round(session.avgDuration)} ms</td>
                  <td>{new Date(session.lastMessage).toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Recent Activity */}
      <div className="dashboard-section">
        <h2>Recent Activity</h2>
        <div className="table-container">
          <table className="sessions-table">
            <thead>
              <tr>
                <th>Session ID</th>
                <th>Messages</th>
                <th>Last Message</th>
              </tr>
            </thead>
            <tbody>
              {stats.recentActivity.map((session) => (
                <tr key={session.sessionId}>
                  <td className="session-id">{session.sessionId}</td>
                  <td>{session.messages}</td>
                  <td>{new Date(session.lastMessage).toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  )
}
