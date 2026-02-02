import { useEffect, useRef, useState, useCallback } from 'react'
import { Terminal } from '@xterm/xterm'
import { FitAddon } from '@xterm/addon-fit'
import { WebLinksAddon } from '@xterm/addon-web-links'
import { Unicode11Addon } from '@xterm/addon-unicode11'
import * as signalR from '@microsoft/signalr'
import { authFetch, getAuthHeader } from '../auth'
import { useVoiceControl } from '../hooks/useVoiceControl'
import type { PendingRestore } from '../types'
import '@xterm/xterm/css/xterm.css'

interface TerminalViewProps {
  sessionId: string
  onClose: () => void
  onStateChanged?: (sessionId: string, isRunning: boolean, waitingForInput: boolean) => void
  onTitleChanged?: (sessionId: string, title: string) => void
  pendingRestore?: PendingRestore
  onRestoreComplete?: () => void
}

export function TerminalView({ sessionId, onClose, onStateChanged, onTitleChanged, pendingRestore, onRestoreComplete }: TerminalViewProps) {
  const terminalRef = useRef<HTMLDivElement>(null)
  const terminalInstance = useRef<Terminal | null>(null)
  const fitAddon = useRef<FitAddon | null>(null)
  const connection = useRef<signalR.HubConnection | null>(null)
  const resizeTimeoutRef = useRef<number | null>(null)
  const lastDimensionsRef = useRef<{ cols: number; rows: number } | null>(null)
  const [isConnected, setIsConnected] = useState(false)
  const [displayConnected, setDisplayConnected] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [isWaitingForInput, setIsWaitingForInput] = useState(false)
  const restoreHandledRef = useRef(false)
  const disconnectTimeoutRef = useRef<number | null>(null)

  // Voice control - send transcribed speech to terminal
  const handleVoiceTranscript = useCallback((text: string) => {
    if (connection.current?.state === signalR.HubConnectionState.Connected) {
      // Sanitize: trim whitespace, limit length, remove control characters
      const sanitized = text
        .trim()
        .slice(0, 1000) // Reasonable max length
        .replace(/[\x00-\x1F\x7F]/g, '') // Remove control chars except those we add

      if (!sanitized) return

      // Send the text as input with a newline to execute
      const encoder = new TextEncoder()
      const bytes = Array.from(encoder.encode(sanitized + '\n'))
      connection.current.invoke('SendInput', sessionId, bytes)
        .catch(err => console.error('SendInput (voice) failed:', err))

      // Also show indicator in terminal (escape for display)
      const displayText = sanitized.length > 50 ? sanitized.slice(0, 50) + '...' : sanitized
      terminalInstance.current?.writeln(`\r\n\x1b[36m[Voice: "${displayText}"]\x1b[0m`)
    }
  }, [sessionId])

  const [voiceState, voiceActions] = useVoiceControl(handleVoiceTranscript)

  // Keyboard shortcut: Ctrl+M to toggle voice
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.ctrlKey && e.key === 'm') {
        e.preventDefault()
        voiceActions.toggle()
      }
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [voiceActions])

  // Debounce connection status display to prevent flickering
  useEffect(() => {
    if (isConnected) {
      // Immediately show connected
      if (disconnectTimeoutRef.current) {
        clearTimeout(disconnectTimeoutRef.current)
        disconnectTimeoutRef.current = null
      }
      setDisplayConnected(true)
    } else {
      // Wait 1000ms before showing disconnected
      disconnectTimeoutRef.current = window.setTimeout(() => {
        setDisplayConnected(false)
      }, 1000)
    }

    return () => {
      if (disconnectTimeoutRef.current) {
        clearTimeout(disconnectTimeoutRef.current)
      }
    }
  }, [isConnected])

  // Detect if we're on a mobile device
  const isMobile = /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent)

  useEffect(() => {
    if (!terminalRef.current) return

    // Create terminal with proper settings for ConPTY
    const terminal = new Terminal({
      cursorBlink: true,
      fontSize: isMobile ? 12 : 14,  // Smaller font on mobile
      fontFamily: 'Cascadia Code, Consolas, Monaco, monospace',
      theme: {
        background: '#1e1e1e',
        foreground: '#d4d4d4',
        cursor: '#ffffff',
        cursorAccent: '#1e1e1e',
        selectionBackground: '#264f78',
        selectionForeground: '#ffffff',
        selectionInactiveBackground: '#264f78',  // Same as active to prevent grey
        black: '#000000',
        red: '#cd3131',
        green: '#0dbc79',
        yellow: '#e5e510',
        blue: '#2472c8',
        magenta: '#bc3fbc',
        cyan: '#11a8cd',
        white: '#e5e5e5',
        brightBlack: '#666666',
        brightRed: '#f14c4c',
        brightGreen: '#23d18b',
        brightYellow: '#f5f543',
        brightBlue: '#3b8eea',
        brightMagenta: '#d670d6',
        brightCyan: '#29b8db',
        brightWhite: '#ffffff',
      },
      scrollback: isMobile ? 1000 : 10000,  // Less scrollback on mobile for performance
      allowProposedApi: true,
      // Don't convert EOL - ConPTY sends proper \r\n sequences
      convertEol: false,
      // Mobile optimizations
      scrollOnUserInput: true,
      fastScrollSensitivity: isMobile ? 1 : 5,
      smoothScrollDuration: isMobile ? 0 : 125,  // Disable smooth scroll on mobile
      // Clipboard support
      rightClickSelectsWord: true,  // Right-click selects word for easy copy
    })

    // Add addons
    const fit = new FitAddon()
    terminal.loadAddon(fit)
    terminal.loadAddon(new WebLinksAddon())

    // Add Unicode 11 support for proper emoji and symbol rendering
    const unicode11 = new Unicode11Addon()
    terminal.loadAddon(unicode11)
    terminal.unicode.activeVersion = '11'

    // Open terminal
    terminal.open(terminalRef.current)

    // Initial fit after DOM is ready
    requestAnimationFrame(() => {
      fit.fit()
    })

    terminalInstance.current = terminal
    fitAddon.current = fit

    // Debounced resize handler that prevents rapid resizing on mobile
    const doResize = () => {
      if (!fitAddon.current || !terminalInstance.current) return

      try {
        fitAddon.current.fit()
        const newCols = terminalInstance.current.cols
        const newRows = terminalInstance.current.rows

        // Only send resize to server if dimensions actually changed
        const last = lastDimensionsRef.current
        if (!last || last.cols !== newCols || last.rows !== newRows) {
          lastDimensionsRef.current = { cols: newCols, rows: newRows }

          if (connection.current?.state === signalR.HubConnectionState.Connected) {
            connection.current.invoke('Resize', sessionId, newCols, newRows)
              .catch(err => console.error('Resize failed:', err))
          }
        }
      } catch (err) {
        console.error('Fit error:', err)
      }
    }

    // Heavily debounced resize for mobile (keyboard open/close causes many events)
    const debouncedResize = () => {
      if (resizeTimeoutRef.current) {
        clearTimeout(resizeTimeoutRef.current)
      }
      // Use longer debounce on mobile to avoid jumping
      const debounceMs = isMobile ? 300 : 100
      resizeTimeoutRef.current = window.setTimeout(doResize, debounceMs)
    }

    // Handle window resize
    const handleResize = () => {
      debouncedResize()
    }
    window.addEventListener('resize', handleResize)

    // Use ResizeObserver for container size changes (but not on mobile to avoid keyboard issues)
    let resizeObserver: ResizeObserver | null = null
    if (!isMobile) {
      resizeObserver = new ResizeObserver(() => {
        debouncedResize()
      })
      resizeObserver.observe(terminalRef.current)
    }

    // Create SignalR connection with auth headers
    const authHeader = getAuthHeader()
    const hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/terminal', {
        headers: authHeader ? { 'Authorization': authHeader } : undefined
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Information)
      .build()

    // Handle output
    hubConnection.on('OnOutput', (sid: string, data: number[]) => {
      if (sid === sessionId) {
        const bytes = new Uint8Array(data)
        terminal.write(bytes)
      }
    })

    // Handle exit
    hubConnection.on('OnExit', (sid: string, exitCode: number) => {
      if (sid === sessionId) {
        terminal.writeln(`\r\n\x1b[90m[Process exited with code ${exitCode}]\x1b[0m`)
      }
    })

    // Handle error
    hubConnection.on('OnError', (sid: string, errorMsg: string) => {
      if (sid === sessionId) {
        terminal.writeln(`\r\n\x1b[31m[Error: ${errorMsg}]\x1b[0m`)
      }
    })

    // Handle state changes (Running/Question)
    hubConnection.on('OnStateChanged', (sid: string, isRunning: boolean, waitingForInput: boolean) => {
      if (sid === sessionId) {
        setIsWaitingForInput(waitingForInput)
        if (onStateChanged) {
          onStateChanged(sid, isRunning, waitingForInput)
        }
      }
    })

    // Handle title changes (detected from "STATUS: Title" in output)
    hubConnection.on('OnTitleChanged', (sid: string, title: string) => {
      if (sid === sessionId && onTitleChanged) {
        onTitleChanged(sid, title)
      }
    })

    // Handle user input
    terminal.onData(data => {
      if (hubConnection.state === signalR.HubConnectionState.Connected) {
        const encoder = new TextEncoder()
        const bytes = Array.from(encoder.encode(data))
        hubConnection.invoke('SendInput', sessionId, bytes)
          .catch(err => console.error('SendInput failed:', err))
      }
    })

    // Custom keyboard handler for copy/paste
    // Ctrl+C: Copy if text selected, otherwise send interrupt
    // Ctrl+V: Paste from clipboard
    terminal.attachCustomKeyEventHandler((event) => {
      // Handle Ctrl+C - copy if selection exists
      if (event.ctrlKey && event.key === 'c' && event.type === 'keydown') {
        const selection = terminal.getSelection()
        if (selection && selection.length > 0) {
          navigator.clipboard.writeText(selection).catch(err => {
            console.error('Copy failed:', err)
          })
          return false // Prevent default (don't send to terminal)
        }
        // No selection - let it pass through as interrupt (Ctrl+C)
        return true
      }

      // Handle Ctrl+V - paste
      if (event.ctrlKey && event.key === 'v' && event.type === 'keydown') {
        navigator.clipboard.readText().then(text => {
          if (text && hubConnection.state === signalR.HubConnectionState.Connected) {
            const encoder = new TextEncoder()
            const bytes = Array.from(encoder.encode(text))
            hubConnection.invoke('SendInput', sessionId, bytes)
              .catch(err => console.error('Paste failed:', err))
          }
        }).catch(err => {
          console.error('Paste failed:', err)
        })
        return false // Prevent default
      }

      // Handle Ctrl+Shift+C - always copy (alternative shortcut)
      if (event.ctrlKey && event.shiftKey && event.key === 'C' && event.type === 'keydown') {
        const selection = terminal.getSelection()
        if (selection && selection.length > 0) {
          navigator.clipboard.writeText(selection).catch(err => {
            console.error('Copy failed:', err)
          })
        }
        return false
      }

      // Handle Ctrl+Shift+V - always paste (alternative shortcut)
      if (event.ctrlKey && event.shiftKey && event.key === 'V' && event.type === 'keydown') {
        navigator.clipboard.readText().then(text => {
          if (text && hubConnection.state === signalR.HubConnectionState.Connected) {
            const encoder = new TextEncoder()
            const bytes = Array.from(encoder.encode(text))
            hubConnection.invoke('SendInput', sessionId, bytes)
              .catch(err => console.error('Paste failed:', err))
          }
        }).catch(err => {
          console.error('Paste failed:', err)
        })
        return false
      }

      return true // Allow all other keys
    })

    // Right-click context menu for copy/paste
    const handleContextMenu = (e: MouseEvent) => {
      e.preventDefault()
      const selection = terminal.getSelection()

      // Create context menu
      const existingMenu = document.querySelector('.terminal-context-menu')
      if (existingMenu) existingMenu.remove()

      const menu = document.createElement('div')
      menu.className = 'terminal-context-menu'
      menu.style.cssText = `
        position: fixed;
        left: ${e.clientX}px;
        top: ${e.clientY}px;
        background: #21262d;
        border: 1px solid #30363d;
        border-radius: 6px;
        padding: 4px 0;
        z-index: 1000;
        box-shadow: 0 8px 24px rgba(0,0,0,0.4);
        min-width: 120px;
      `

      const createMenuItem = (text: string, action: () => void, disabled = false) => {
        const item = document.createElement('div')
        item.textContent = text
        item.style.cssText = `
          padding: 8px 16px;
          cursor: ${disabled ? 'default' : 'pointer'};
          color: ${disabled ? '#6e7681' : '#c9d1d9'};
          font-size: 14px;
        `
        if (!disabled) {
          item.onmouseenter = () => item.style.background = '#30363d'
          item.onmouseleave = () => item.style.background = 'transparent'
          item.onclick = () => {
            action()
            menu.remove()
          }
        }
        return item
      }

      // Copy option
      menu.appendChild(createMenuItem(
        'Copy',
        () => {
          if (selection) {
            navigator.clipboard.writeText(selection)
          }
        },
        !selection || selection.length === 0
      ))

      // Paste option
      menu.appendChild(createMenuItem(
        'Paste',
        () => {
          navigator.clipboard.readText().then(text => {
            if (text && hubConnection.state === signalR.HubConnectionState.Connected) {
              const encoder = new TextEncoder()
              const bytes = Array.from(encoder.encode(text))
              hubConnection.invoke('SendInput', sessionId, bytes)
            }
          })
        }
      ))

      // Select All option
      menu.appendChild(createMenuItem(
        'Select All',
        () => terminal.selectAll()
      ))

      document.body.appendChild(menu)

      // Close menu on click outside
      const closeMenu = (ev: MouseEvent) => {
        if (!menu.contains(ev.target as Node)) {
          menu.remove()
          document.removeEventListener('click', closeMenu)
        }
      }
      setTimeout(() => document.addEventListener('click', closeMenu), 0)
    }

    terminalRef.current.addEventListener('contextmenu', handleContextMenu)

    // Connection state handlers
    hubConnection.onreconnecting(() => {
      terminal.writeln('\r\n\x1b[33m[Reconnecting...]\x1b[0m')
      setIsConnected(false)
    })

    hubConnection.onreconnected(() => {
      terminal.writeln('\r\n\x1b[32m[Reconnected]\x1b[0m')
      setIsConnected(true)
      hubConnection.invoke('JoinSession', sessionId)
    })

    hubConnection.onclose(() => {
      setIsConnected(false)
    })

    // Connect, resize, then load history
    const connectAndLoadHistory = async () => {
      // First connect to SignalR
      try {
        await hubConnection.start()
        setIsConnected(true)
        await hubConnection.invoke('JoinSession', sessionId)

        // IMPORTANT: Sync terminal size BEFORE loading history
        // This ensures ConPTY uses the correct dimensions
        fit.fit()
        await hubConnection.invoke('Resize', sessionId, terminal.cols, terminal.rows)
        console.log(`Synced terminal size: ${terminal.cols}x${terminal.rows}`)

        // Small delay to let ConPTY process the resize
        await new Promise(resolve => setTimeout(resolve, 100))

        // Now fetch and display historical output
        try {
          const historyResponse = await authFetch(`/api/terminal/sessions/${sessionId}/history`)
          if (historyResponse.ok) {
            const historyData = await historyResponse.json()
            if (historyData.data && historyData.data.length > 0) {
              const bytes = new Uint8Array(historyData.data)
              terminal.write(bytes)
            }
          }
        } catch (err) {
          console.warn('Failed to load history:', err)
        }
      } catch (err) {
        console.error('Connection failed:', err)
        setError('Failed to connect to terminal hub')
        terminal.writeln('\x1b[31m✗ Failed to connect to terminal hub\x1b[0m')
      }
    }

    connectAndLoadHistory()

    connection.current = hubConnection

    // Cleanup
    return () => {
      window.removeEventListener('resize', handleResize)
      if (resizeObserver) {
        resizeObserver.disconnect()
      }
      if (resizeTimeoutRef.current) {
        clearTimeout(resizeTimeoutRef.current)
      }
      // Remove context menu handler
      if (terminalRef.current) {
        terminalRef.current.removeEventListener('contextmenu', handleContextMenu)
      }
      // Remove any lingering context menu
      const existingMenu = document.querySelector('.terminal-context-menu')
      if (existingMenu) existingMenu.remove()

      if (hubConnection.state === signalR.HubConnectionState.Connected) {
        hubConnection.invoke('LeaveSession', sessionId).catch(() => {})
        hubConnection.stop()
      }
      terminal.dispose()
    }
  }, [sessionId, isMobile])

  const handleInterrupt = async () => {
    if (connection.current?.state === signalR.HubConnectionState.Connected) {
      try {
        await connection.current.invoke('SendSignal', sessionId, 'interrupt')
        terminalInstance.current?.writeln('\r\n\x1b[33m[Sent Ctrl+C]\x1b[0m')
      } catch (err) {
        console.error('SendSignal failed:', err)
      }
    }
  }

  const handleTerminate = async () => {
    if (connection.current?.state === signalR.HubConnectionState.Connected) {
      try {
        await connection.current.invoke('Terminate', sessionId)
        terminalInstance.current?.writeln('\r\n\x1b[31m[Session terminated]\x1b[0m')
      } catch (err) {
        console.error('Terminate failed:', err)
      }
    }
  }

  // Handle pending restore - paste content when Claude is ready (waiting for input)
  useEffect(() => {
    // Only proceed if:
    // 1. We have pending restore content for this session
    // 2. We're connected
    // 3. We haven't already handled this restore
    // 4. Either Claude is waiting for input OR 5 seconds have passed
    if (
      pendingRestore &&
      pendingRestore.sessionId === sessionId &&
      isConnected &&
      !restoreHandledRef.current &&
      connection.current?.state === signalR.HubConnectionState.Connected
    ) {
      const performRestore = async (retryCount = 0) => {
        const maxRetries = 3

        // Check if connection is actually connected (not reconnecting)
        if (connection.current?.state !== signalR.HubConnectionState.Connected) {
          if (retryCount < maxRetries) {
            console.log(`Connection not ready, retrying in 2s (attempt ${retryCount + 1}/${maxRetries})`)
            setTimeout(() => performRestore(retryCount + 1), 2000)
            return
          } else {
            restoreHandledRef.current = true
            terminalInstance.current?.writeln('\r\n\x1b[31m[Failed to restore: connection not stable]\x1b[0m')
            return
          }
        }

        restoreHandledRef.current = true

        // Show restore message in terminal
        terminalInstance.current?.writeln('\r\n\x1b[36m[Restoring session content...]\x1b[0m\r\n')

        // Send the content as input
        const encoder = new TextEncoder()
        const bytes = Array.from(encoder.encode(pendingRestore.content))

        try {
          await connection.current.invoke('SendInput', sessionId, bytes)
          console.log('Session content restored successfully')
          terminalInstance.current?.writeln('\r\n\x1b[32m[Session restored successfully]\x1b[0m')
          if (onRestoreComplete) {
            onRestoreComplete()
          }
        } catch (err) {
          console.error('Failed to restore session content:', err)

          // Retry if connection was lost during send
          if (retryCount < maxRetries && err instanceof Error && err.message.includes('connection')) {
            console.log(`Retrying restore (attempt ${retryCount + 1}/${maxRetries})`)
            restoreHandledRef.current = false // Allow retry
            setTimeout(() => performRestore(retryCount + 1), 2000)
          } else {
            terminalInstance.current?.writeln('\r\n\x1b[31m[Failed to restore session content]\x1b[0m')
          }
        }
      }

      // If Claude is already waiting for input, restore immediately
      if (isWaitingForInput) {
        performRestore()
      } else {
        // Otherwise wait up to 10 seconds for waitingForInput signal
        // This gives the connection time to stabilize
        const timeout = window.setTimeout(() => {
          if (!restoreHandledRef.current) {
            console.log('Restoring content after timeout (waitingForInput not received)')
            performRestore()
          }
        }, 10000)

        return () => clearTimeout(timeout)
      }
    }
  }, [pendingRestore, sessionId, isConnected, isWaitingForInput, onRestoreComplete])

  // Reset restore handled flag when pendingRestore changes
  useEffect(() => {
    if (!pendingRestore) {
      restoreHandledRef.current = false
    }
  }, [pendingRestore])

  return (
    <div className="terminal-view">
      <div className="terminal-toolbar">
        <div className="terminal-info">
          <span className="session-label">Session: {sessionId.slice(0, 12)}...</span>
          <span className={`connection-status ${displayConnected ? 'connected' : 'disconnected'}`}>
            {displayConnected ? 'Connected' : 'Disconnected'}
          </span>
        </div>
        <div className="terminal-actions">
          {voiceState.isSupported && (
            <button
              className={`btn-voice ${voiceState.isListening ? 'recording' : ''}`}
              onClick={voiceActions.toggle}
              title={voiceState.isListening ? 'Stop voice input (Ctrl+M)' : 'Start voice input (Ctrl+M)'}
              aria-pressed={voiceState.isListening}
              aria-label={voiceState.isListening ? 'Stop voice input' : 'Start voice input'}
            >
              <svg className="mic-icon" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
                <path d="M12 14c1.66 0 3-1.34 3-3V5c0-1.66-1.34-3-3-3S9 3.34 9 5v6c0 1.66 1.34 3 3 3zm-1-9c0-.55.45-1 1-1s1 .45 1 1v6c0 .55-.45 1-1 1s-1-.45-1-1V5zm6 6c0 2.76-2.24 5-5 5s-5-2.24-5-5H5c0 3.53 2.61 6.43 6 6.92V21h2v-3.08c3.39-.49 6-3.39 6-6.92h-2z"/>
              </svg>
              {voiceState.isListening ? 'Listening...' : 'Voice'}
            </button>
          )}
          {/* Accessible live region for voice status announcements */}
          <span
            className="voice-status-live"
            role="status"
            aria-live="polite"
            aria-atomic="true"
          >
            {voiceState.isListening && voiceState.interimTranscript
              ? `Hearing: ${voiceState.interimTranscript}`
              : voiceState.error || ''}
          </span>
          {voiceState.interimTranscript && (
            <span className="voice-interim" aria-hidden="true">{voiceState.interimTranscript}</span>
          )}
          {voiceState.error && (
            <span className="voice-error" aria-hidden="true">{voiceState.error}</span>
          )}
          <button className="btn-interrupt" onClick={handleInterrupt} title="Send Ctrl+C">
            Ctrl+C
          </button>
          <button className="btn-terminate" onClick={handleTerminate} title="Terminate process">
            Terminate
          </button>
          <button className="btn-close" onClick={onClose} title="Close view">
            Close
          </button>
        </div>
      </div>
      {error && <div className="terminal-error">{error}</div>}
      <div className="terminal-container" ref={terminalRef} />
    </div>
  )
}
