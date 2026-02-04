import { useState, useEffect, useRef } from 'react'
import type { TerminalSession } from '../types'
import './CommandPalette.css'

interface Command {
  id: string
  label: string
  description?: string
  category: 'session' | 'action' | 'navigation'
  action: () => void
}

interface CommandPaletteProps {
  sessions: TerminalSession[]
  onSelectSession: (sessionId: string) => void
  onCreateSession: () => void
  onNavigate: (view: 'sessions' | 'chat' | 'archive') => void
  isOpen: boolean
  onClose: () => void
}

export function CommandPalette({
  sessions,
  onSelectSession,
  onCreateSession,
  onNavigate,
  isOpen,
  onClose
}: CommandPaletteProps) {
  const [query, setQuery] = useState('')
  const [selectedIndex, setSelectedIndex] = useState(0)
  const inputRef = useRef<HTMLInputElement>(null)

  // Build command list
  const commands: Command[] = [
    // Actions
    {
      id: 'new-session',
      label: 'New Session',
      description: 'Create a new terminal session',
      category: 'action',
      action: () => { onCreateSession(); onClose() }
    },
    // Navigation
    {
      id: 'nav-sessions',
      label: 'Sessions',
      description: 'View all terminal sessions',
      category: 'navigation',
      action: () => { onNavigate('sessions'); onClose() }
    },
    {
      id: 'nav-chat',
      label: 'Chat',
      description: 'Open AI chat assistant',
      category: 'navigation',
      action: () => { onNavigate('chat'); onClose() }
    },
    {
      id: 'nav-archive',
      label: 'Archive',
      description: 'Browse archived sessions',
      category: 'navigation',
      action: () => { onNavigate('archive'); onClose() }
    },
    // Sessions
    ...sessions.map(session => ({
      id: `session-${session.sessionId}`,
      label: session.title || session.command || 'Terminal',
      description: `${session.sessionId.slice(0, 12)}… • ${session.isRunning ? 'Running' : 'Stopped'}`,
      category: 'session' as const,
      action: () => { onSelectSession(session.sessionId); onClose() }
    }))
  ]

  // Fuzzy search
  const filteredCommands = commands.filter(cmd => {
    if (!query) return true
    const searchText = `${cmd.label} ${cmd.description || ''}`.toLowerCase()
    const queryLower = query.toLowerCase()
    return searchText.includes(queryLower)
  })

  // Reset selection when filtered list changes
  useEffect(() => {
    setSelectedIndex(0)
  }, [query])

  // Focus input when opened
  useEffect(() => {
    if (isOpen) {
      inputRef.current?.focus()
      setQuery('')
      setSelectedIndex(0)
    }
  }, [isOpen])

  // Keyboard navigation
  useEffect(() => {
    if (!isOpen) return

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        e.preventDefault()
        onClose()
      } else if (e.key === 'ArrowDown') {
        e.preventDefault()
        setSelectedIndex(i => Math.min(i + 1, filteredCommands.length - 1))
      } else if (e.key === 'ArrowUp') {
        e.preventDefault()
        setSelectedIndex(i => Math.max(i - 1, 0))
      } else if (e.key === 'Enter') {
        e.preventDefault()
        if (filteredCommands[selectedIndex]) {
          filteredCommands[selectedIndex].action()
        }
      }
    }

    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [isOpen, selectedIndex, filteredCommands, onClose])

  if (!isOpen) return null

  return (
    <div className="command-palette-overlay" onClick={onClose}>
      <div className="command-palette" onClick={e => e.stopPropagation()}>
        <div className="command-palette-input">
          <svg className="search-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor">
            <circle cx="11" cy="11" r="8" />
            <path d="m21 21-4.35-4.35" />
          </svg>
          <input
            ref={inputRef}
            type="text"
            placeholder="Search commands, sessions, actions..."
            value={query}
            onChange={e => setQuery(e.target.value)}
          />
          <kbd className="shortcut-hint">Esc</kbd>
        </div>

        <div className="command-palette-results">
          {filteredCommands.length === 0 ? (
            <div className="no-results">No commands found</div>
          ) : (
            filteredCommands.map((cmd, index) => (
              <div
                key={cmd.id}
                className={`command-item ${index === selectedIndex ? 'selected' : ''}`}
                onClick={() => cmd.action()}
                onMouseEnter={() => setSelectedIndex(index)}
              >
                <div className="command-icon">
                  {cmd.category === 'session' ? '🖥️' : cmd.category === 'action' ? '⚡' : '📍'}
                </div>
                <div className="command-content">
                  <div className="command-label">{cmd.label}</div>
                  {cmd.description && (
                    <div className="command-description">{cmd.description}</div>
                  )}
                </div>
                {index === selectedIndex && (
                  <kbd className="enter-hint">Enter</kbd>
                )}
              </div>
            ))
          )}
        </div>

        <div className="command-palette-footer">
          <span>↑↓ Navigate</span>
          <span>Enter Select</span>
          <span>Esc Close</span>
        </div>
      </div>
    </div>
  )
}

// Hook for global keyboard shortcut
export function useCommandPalette(onToggle: () => void) {
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      // Cmd+K / Ctrl+K
      if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
        e.preventDefault()
        onToggle()
      }
    }

    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [onToggle])
}
