import { useState, useEffect } from 'react'
import {
  DndContext,
  closestCenter,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
  type DragEndEvent,
} from '@dnd-kit/core'
import {
  arrayMove,
  SortableContext,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import type { TerminalSession, ExternalClaudeInstance, SessionProperties } from '../types'
import { authFetch } from '../auth'

interface SessionListProps {
  sessions: TerminalSession[]
  externalInstances: ExternalClaudeInstance[]
  selectedSession: string | null
  loading: boolean
  sessionProperties: Record<string, SessionProperties>
  onSelect: (sessionId: string) => void
  onTerminate: (sessionId: string) => void
  onEditProperties: (sessionId: string) => void
}

interface SortableSessionItemProps {
  session: TerminalSession
  selectedSession: string | null
  sessionProperties: Record<string, SessionProperties>
  onSelect: (sessionId: string) => void
  onTerminate: (sessionId: string) => void
  onEditProperties: (sessionId: string) => void
  getDisplayName: (session: TerminalSession) => string
  formatTime: (dateStr: string) => string
}

function SortableSessionItem({
  session,
  selectedSession,
  sessionProperties,
  onSelect,
  onTerminate,
  onEditProperties,
  getDisplayName,
  formatTime
}: SortableSessionItemProps) {
  const {
    attributes,
    listeners,
    setNodeRef,
    transform,
    transition,
    isDragging,
  } = useSortable({ id: session.sessionId })

  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.5 : 1,
  }

  const props = sessionProperties[session.sessionId]
  const cardColor = props?.cardColor

  return (
    <li
      ref={setNodeRef}
      style={{
        ...style,
        ...(cardColor ? {
          background: `color-mix(in srgb, ${cardColor} 50%, #21262d)`,
        } : {})
      }}
      className={`session-item ${selectedSession === session.sessionId ? 'selected' : ''} ${session.isRunning ? (session.waitingForInput ? 'waiting' : 'running') : 'stopped'}`}
      onClick={() => onSelect(session.sessionId)}
    >
      <div className="drag-handle" {...attributes} {...listeners} title="Drag to reorder">
        <svg viewBox="0 0 16 16" fill="currentColor" width="14" height="14">
          <circle cx="5" cy="4" r="1.5" />
          <circle cx="11" cy="4" r="1.5" />
          <circle cx="5" cy="8" r="1.5" />
          <circle cx="11" cy="8" r="1.5" />
          <circle cx="5" cy="12" r="1.5" />
          <circle cx="11" cy="12" r="1.5" />
        </svg>
      </div>
      <div className="session-info">
        <span className="session-command" title={session.title ? `Command: ${session.command}` : undefined}>
          {getDisplayName(session)}
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
      <div className="session-item-actions">
        <button
          className="btn-session-menu"
          onClick={(e) => {
            e.stopPropagation()
            onEditProperties(session.sessionId)
          }}
          title="Session properties"
        >
          <svg viewBox="0 0 16 16" fill="currentColor" width="14" height="14">
            <circle cx="8" cy="2.5" r="1.5" />
            <circle cx="8" cy="8" r="1.5" />
            <circle cx="8" cy="13.5" r="1.5" />
          </svg>
        </button>
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
      </div>
    </li>
  )
}

export function SessionList({
  sessions,
  externalInstances,
  selectedSession,
  loading,
  sessionProperties,
  onSelect,
  onTerminate,
  onEditProperties
}: SessionListProps) {
  const [orderedSessions, setOrderedSessions] = useState<TerminalSession[]>([])
  const [isSaving, setIsSaving] = useState(false)

  const sensors = useSensors(
    useSensor(PointerSensor),
    useSensor(KeyboardSensor, {
      coordinateGetter: sortableKeyboardCoordinates,
    })
  )

  // Load initial ordering from backend
  useEffect(() => {
    async function loadOrdering() {
      try {
        const response = await authFetch('/api/chat/admin/sessions/ordering')
        if (response.ok) {
          const data = await response.json()
          const ordering: Record<string, number> = data.ordering || {}

          // Sort sessions by display order
          const sorted = [...sessions].sort((a, b) => {
            const orderA = ordering[a.sessionId] ?? 999999
            const orderB = ordering[b.sessionId] ?? 999999
            return orderA - orderB
          })

          setOrderedSessions(sorted)
        } else {
          // No ordering saved, use original order
          setOrderedSessions(sessions)
        }
      } catch (err) {
        console.error('Failed to load session ordering:', err)
        setOrderedSessions(sessions)
      }
    }

    if (sessions.length > 0) {
      loadOrdering()
    } else {
      setOrderedSessions([])
    }
  }, [sessions])

  const handleDragEnd = async (event: DragEndEvent) => {
    const { active, over } = event

    if (!over || active.id === over.id) {
      return
    }

    const oldIndex = orderedSessions.findIndex(s => s.sessionId === active.id)
    const newIndex = orderedSessions.findIndex(s => s.sessionId === over.id)

    const newOrder = arrayMove(orderedSessions, oldIndex, newIndex)
    setOrderedSessions(newOrder)

    // Persist new ordering to backend
    setIsSaving(true)
    try {
      const orderPayload = newOrder.map((session, index) => ({
        sessionId: session.sessionId,
        order: index
      }))

      const response = await authFetch('/api/chat/admin/sessions/reorder', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ sessions: orderPayload })
      })

      if (!response.ok) {
        console.error('Failed to save session ordering')
      }
    } catch (err) {
      console.error('Error saving session ordering:', err)
    } finally {
      setIsSaving(false)
    }
  }

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

  const getDisplayName = (session: TerminalSession) => {
    const props = sessionProperties[session.sessionId]
    return props?.customName || session.title || session.command
  }

  const totalCount = orderedSessions.length + externalInstances.length

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
      <h2>
        Sessions ({totalCount})
        {isSaving && <span className="saving-indicator"> Saving...</span>}
      </h2>

      {/* Terminal Sessions (managed by this tool) */}
      {orderedSessions.length > 0 && (
        <>
          <h3 className="section-header">
            <span className="icon">🖥️</span> Terminal Sessions
          </h3>
          <DndContext
            sensors={sensors}
            collisionDetection={closestCenter}
            onDragEnd={handleDragEnd}
          >
            <SortableContext
              items={orderedSessions.map(s => s.sessionId)}
              strategy={verticalListSortingStrategy}
            >
              <ul>
                {orderedSessions.map(session => (
                  <SortableSessionItem
                    key={session.sessionId}
                    session={session}
                    selectedSession={selectedSession}
                    sessionProperties={sessionProperties}
                    onSelect={onSelect}
                    onTerminate={onTerminate}
                    onEditProperties={onEditProperties}
                    getDisplayName={getDisplayName}
                    formatTime={formatTime}
                  />
                ))}
              </ul>
            </SortableContext>
          </DndContext>
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
